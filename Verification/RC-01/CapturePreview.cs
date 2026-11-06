using System;
using System.Collections;
using System.IO;
using System.Linq;
using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Gameplay.Farming;
using MyLittleFarm.Gameplay.World;
using MyLittleFarm.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

// Copy into Assets only in an isolated build snapshot. This is a camera preview with no assertions.
public sealed class CapturePreview : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FARMGAME_RC01_PREVIEW"))) return;
        var go = new GameObject("RC-01 camera preview");
        DontDestroyOnLoad(go);
        go.AddComponent<CapturePreview>();
    }

    private IEnumerator Start()
    {
        var folder = Environment.GetEnvironmentVariable("FARMGAME_RC01_PREVIEW");
        Directory.CreateDirectory(folder);
        FarmSceneFlow.LoadGame();
        while (FarmSceneFlow.IsLoading || SceneManager.GetActiveScene().name != "Prototype") yield return null;
        var saves = FindAnyObjectByType<SaveSystem>();
        var slot = Array.FindIndex(saves.ReadSlots(), s => s.IsEmpty);
        if (saves.HasActiveFarm || slot < 0 || !saves.NewFarm(slot))
        {
            Debug.LogError("RC-01 preview requires an empty slot in an isolated Player profile.");
            Application.Quit(1);
            yield break;
        }
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include)) canvas.enabled = false;
        var grid = GridSystem.Instance;
        var crops = FindAnyObjectByType<CropSystem>();
        var cycle = FindAnyObjectByType<DayNightCycle>();
        var player = FindAnyObjectByType<PlayerController>();
        var camera = Camera.main;
        camera.GetComponent<IsometricCameraController>().enabled = false;
        FindAnyObjectByType<CellSelector>().enabled = false;
        Time.timeScale = 0;

        var bed = new[] { new Vector2Int(5, 0), new Vector2Int(5, -1), new Vector2Int(6, 0), new Vector2Int(6, -1) };
        foreach (var p in bed)
            if (grid.CanTill(p)) grid.SetFarmingCell(p, new FarmingCell { soilType = CellType.Tilled });
        crops.Plant(bed[1], "carrot", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 1400);
        crops.Plant(bed[3], "wheat", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 8200);
        grid.Select(bed[0]);
        yield return null;
        yield return null;
        var lights = FindObjectsByType<Light>().Where(l => l.type == LightType.Point).ToArray();
        var renderers = FindObjectsByType<ChunkRenderer>(FindObjectsInactive.Include);
        File.WriteAllText(Path.Combine(folder, "views.txt"), FormattableString.Invariant(
            $"profile={Application.persistentDataPath}; api={SystemInfo.graphicsDeviceType}; quality={QualitySettings.names[QualitySettings.GetQualityLevel()]}; baseHash={grid.ComputeBaseMapHash()}; material={renderers[0].GetComponent<MeshRenderer>().sharedMaterial.name}\n"));
        File.AppendAllText(Path.Combine(folder, "views.txt"), string.Join(", ", bed.Select(p => p + "=" + grid.GetCellType(p))) + Environment.NewLine);

        cycle.ConfigureLanternShadowFocus(player.transform);
        cycle.SetHour(11.5f);
        var baseline = Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(m => m.name == "LandscapeGround");
        if (baseline != null)
        {
            var surfaces = renderers.Select(r => r.GetComponent<MeshRenderer>()).ToArray();
            var originals = surfaces.Select(r => r.sharedMaterial).ToArray();
            foreach (var surface in surfaces) surface.sharedMaterial = baseline;
            Shot("farm-day-normal-before", new Vector3(3.5f, 0.744f, 0.5f), 45, 13);
            for (var i = 0; i < surfaces.Length; i++) surfaces[i].sharedMaterial = originals[i];
        }
        Shot("farm-day-normal", new Vector3(3.5f, 0.744f, 0.5f), 45, 13);
        Shot("farm-day-close", new Vector3(5.5f, 0.744f, 0), 45, 7);
        Shot("farm-day-far", new Vector3(3.5f, 0.744f, 0.5f), 45, 20);
        Shot("market-day-normal", new Vector3(8.09f, 0.764f, 0.54f), 24.3f, 13);
        Shot("dirt-trail-and-stone-day", new Vector3(24.5f, 0.92f, -16.8f), 45, 13);
        Shot("pond-day-normal", new Vector3(-12.5f, 0.7f, 9.5f), -90, 13);
        Shot("pond-day-close", new Vector3(-13.2f, 0.56f, 8.5f), -90, 7);

        player.Teleport(new Vector3(4.5f, 0.744f, 0.4f));
        cycle.ConfigureLanternShadowFocus(player.transform);
        cycle.SetHour(23);
        Shot("farm-night-normal", new Vector3(3.5f, 0.744f, 0.5f), 45, 13);
        Shot("farm-night-close", new Vector3(5.5f, 0.744f, 0), 45, 7);
        foreach (var light in lights) light.shadows = LightShadows.None;
        Shot("farm-night-close-no-point-shadows", new Vector3(5.5f, 0.744f, 0), 45, 7);
        player.Teleport(new Vector3(-12.5f, 0.7f, 9.5f));
        cycle.ConfigureLanternShadowFocus(player.transform);
        cycle.SetHour(23);
        Shot("pond-night-normal", new Vector3(-12.5f, 0.7f, 9.5f), -90, 13);
        Shot("pond-night-close", new Vector3(-13.2f, 0.56f, 8.5f), -90, 7);
        Application.Quit();

        void Shot(string name, Vector3 anchor, float yaw, float zoom)
        {
            const int width = 1600, height = 900;
            var rotation = Quaternion.Euler(38, yaw, 0);
            var focus = anchor + Vector3.up * 0.8f;
            camera.transform.SetPositionAndRotation(focus + rotation * (Vector3.back * zoom), rotation);
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
            File.AppendAllText(Path.Combine(folder, "views.txt"), FormattableString.Invariant(
                $"{name} | camera={camera.transform.position:F3} yaw={yaw} pitch=38 distance={zoom} fov={camera.fieldOfView} hour={cycle.Hour} pointShadows={lights.Count(l => l.enabled && l.shadows != LightShadows.None)}\n"));
            Destroy(image);
            camera.targetTexture = null;
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
        }
    }
}
