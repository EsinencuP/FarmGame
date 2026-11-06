using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Gameplay.World;
using MyLittleFarm.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Profiling;
using UnityEngine.UI;

// Editor snapshot only. Frames use the authored camera and a separate Player profile.
public sealed class CaptureLandmarks : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FARMGAME_RC02_PREVIEW"))) return;
        var go = new GameObject("RC-02 landmark preview");
        DontDestroyOnLoad(go);
        go.AddComponent<CaptureLandmarks>();
    }

    private IEnumerator Start()
    {
        var folder = Environment.GetEnvironmentVariable("FARMGAME_RC02_PREVIEW");
        Directory.CreateDirectory(folder);
        FarmSceneFlow.LoadGame();
        while (FarmSceneFlow.IsLoading || SceneManager.GetActiveScene().name != "Prototype") yield return null;
        var saves = FindAnyObjectByType<SaveSystem>();
        var slot = Array.FindIndex(saves.ReadSlots(), s => s.IsEmpty);
        if (saves.HasActiveFarm || slot < 0)
        {
            Debug.LogError("RC-02 needs an empty slot in the isolated preview profile.");
            Application.Quit(1);
            yield break;
        }
        var slots = FindAnyObjectByType<SaveSlotsUI>();
        slots.GetComponentsInChildren<SaveSlotCard>(true)[slot].Primary.onClick.Invoke();
        var deadline = Time.realtimeSinceStartup + 15;
        while ((!saves.HasActiveFarm || slots.IsOpen) && Time.realtimeSinceStartup < deadline) yield return null;
        if (!saves.HasActiveFarm || slots.IsOpen)
        {
            Debug.LogError("Could not enter the isolated farm for the landmark preview.");
            Application.Quit(1);
            yield break;
        }
        var journal = FindAnyObjectByType<FarmJournalUI>();
        var uiCanvas = journal.GetComponentInParent<Canvas>();
        var allCanvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
        foreach (var canvas in allCanvases) canvas.enabled = false;
        var grid = GridSystem.Instance;
        var cycle = FindAnyObjectByType<DayNightCycle>();
        var player = FindAnyObjectByType<PlayerController>();
        var camera = Camera.main;
        camera.GetComponent<IsometricCameraController>().enabled = false;
        Time.timeScale = 0;
        yield return null;
        yield return null;
        var landmarks = GameObject.Find("RC-02 Landmarks");
        File.WriteAllText(Path.Combine(folder, "views.txt"), FormattableString.Invariant(
            $"profile={Application.persistentDataPath}; api={SystemInfo.graphicsDeviceType}; quality={QualitySettings.names[QualitySettings.GetQualityLevel()]}; baseHash={grid.ComputeBaseMapHash()}; decor={landmarks.GetComponentsInChildren<Renderer>().Length}; colliders={landmarks.GetComponentsInChildren<Collider>().Length}\n"));
        foreach (var cache in FindObjectsByType<RareSeedCache>())
            File.AppendAllText(Path.Combine(folder, "views.txt"), FormattableString.Invariant($"cache {cache.Id} {cache.transform.position:F3}\n"));

        Frame("pond-shore", -12.5f, 9.5f, -90, 13, 11.5f);
        Frame("pond-reeds-close", -13.2f, 8.5f, -90, 7, 11.5f);
        Frame("pond-cache", -13.4f, 17.3f, -23, 10, 11.5f);
        Frame("forest-approach", -1.5f, 25.5f, 5, 13, 11.5f);
        Frame("forest-clearing", -1.5f, 29.7f, 8, 10, 11.5f);
        Frame("hill-cairn", 27.6f, 19.4f, 53, 13, 11.5f);
        Frame("hill-farm-view", 28.2f, 20.1f, -131, 20, 11.5f);
        Frame("trail-south", -4f, -16.5f, -116, 13, 11.5f);
        Frame("trail-east", 24.5f, 10.5f, 68, 13, 11.5f);
        Frame("pond-breeze-a", -13.2f, 8.5f, -90, 7, 11.5f);
        Time.timeScale = 1;
        yield return new WaitForSecondsRealtime(1.4f);
        Time.timeScale = 0;
        Frame("pond-breeze-b", -13.2f, 8.5f, -90, 7, 11.5f);
        Frame("pond-evening", -12.5f, 9.5f, -90, 13, 18);

        journal.Open();
        var tab = journal.GetComponentsInChildren<Button>(true)
            .First(b => b.GetComponentInChildren<Text>()?.text == "Открытия");
        tab.onClick.Invoke();
        uiCanvas.enabled = true;
        uiCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        uiCanvas.worldCamera = camera;
        uiCanvas.planeDistance = camera.nearClipPlane + .35f;
        Canvas.ForceUpdateCanvases();
        Shot("journal-discoveries");
        if (Environment.GetEnvironmentVariable("FARMGAME_RC02_PERF") == "1")
        {
            journal.Close();
            uiCanvas.enabled = false;
            var vegetation = landmarks.GetComponentsInChildren<MeshRenderer>()
                .Where(r => !r.GetComponent<MeshFilter>().sharedMesh.name.StartsWith("Stone", StringComparison.Ordinal))
                .ToArray();
            yield return Benchmark(folder, vegetation, cycle, player, grid, camera);
        }
        Application.Quit();

        void Frame(string name, float x, float z, float yaw, float zoom, float hour)
        {
            var anchor = new Vector3(x, grid.GetSurfaceHeight(x, z) + .744f, z);
            player.Teleport(anchor);
            cycle.ConfigureLanternShadowFocus(player.transform);
            cycle.SetHour(hour);
            var rotation = Quaternion.Euler(38, yaw, 0);
            camera.transform.SetPositionAndRotation(anchor + Vector3.up * .8f + rotation * (Vector3.back * zoom), rotation);
            Shot(name);
            File.AppendAllText(Path.Combine(folder, "views.txt"), FormattableString.Invariant(
                $"{name} | anchor={anchor:F3} yaw={yaw} pitch=38 distance={zoom} fov={camera.fieldOfView} hour={cycle.Hour}\n"));
        }

        void Shot(string name)
        {
            const int width = 1600, height = 900;
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
            Destroy(image);
            camera.targetTexture = null;
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
        }
    }

    private static IEnumerator Benchmark(string folder, MeshRenderer[] vegetation, DayNightCycle cycle,
        PlayerController player, GridSystem grid, Camera camera)
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
        var originalShadows = QualitySettings.shadows;
        var path = Path.Combine(folder, "render-cost.csv");
        File.WriteAllText(path, "view,mode,frames,p50_ms,p95_ms,cpu_p50_ms,gpu_p50_ms,batches,setpass,triangles,shadow_casters\n");
        using var batches = Counter("Batches Count");
        using var setPass = Counter("SetPass Calls Count");
        using var triangles = Counter("Triangles Count");
        using var casters = Counter("Shadow Casters Count");
        var modes = new[] { "full", "shadows_off", "new_vegetation_off", "full_repeat" };
        var frameTimes = new List<double>(8192);
        var cpu = new List<double>(8192);
        var gpu = new List<double>(8192);
        var timing = new FrameTiming[1];
        var timingEnabled = FrameTimingManager.IsFeatureEnabled();
        foreach (var view in new[] { "forest_day", "pond_night" })
        {
            var forest = view == "forest_day";
            var x = forest ? -1.5f : -12.5f;
            var z = forest ? 27f : 9.5f;
            var anchor = new Vector3(x, grid.GetSurfaceHeight(x, z) + .744f, z);
            player.Teleport(anchor);
            cycle.ConfigureLanternShadowFocus(player.transform);
            cycle.SetHour(forest ? 11.5f : 23);
            var rotation = Quaternion.Euler(38, forest ? 5 : -90, 0);
            camera.transform.SetPositionAndRotation(anchor + Vector3.up * .8f + rotation * (Vector3.back * 13), rotation);
            foreach (var mode in modes)
            {
                QualitySettings.shadows = mode == "shadows_off" ? ShadowQuality.Disable : originalShadows;
                foreach (var renderer in vegetation) renderer.enabled = mode != "new_vegetation_off";
                yield return new WaitForSecondsRealtime(2);
                frameTimes.Clear(); cpu.Clear(); gpu.Clear();
                var deadline = Time.realtimeSinceStartupAsDouble + 4;
                ulong lastTimestamp = 0;
                while (Time.realtimeSinceStartupAsDouble < deadline)
                {
                    frameTimes.Add(Time.unscaledDeltaTime * 1000.0);
                    if (timingEnabled)
                    {
                        FrameTimingManager.CaptureFrameTimings();
                        if (FrameTimingManager.GetLatestTimings(1, timing) > 0 && timing[0].frameStartTimestamp != lastTimestamp)
                        {
                            lastTimestamp = timing[0].frameStartTimestamp;
                            if (timing[0].cpuMainThreadFrameTime > 0) cpu.Add(timing[0].cpuMainThreadFrameTime);
                            if (timing[0].gpuFrameTime > 0) gpu.Add(timing[0].gpuFrameTime);
                        }
                    }
                    yield return null;
                }
                frameTimes.Sort(); cpu.Sort(); gpu.Sort();
                File.AppendAllText(path, FormattableString.Invariant(
                    $"{view},{mode},{frameTimes.Count},{Quantile(frameTimes, .5)},{Quantile(frameTimes, .95)},{Quantile(cpu, .5)},{Quantile(gpu, .5)},{Value(batches)},{Value(setPass)},{Value(triangles)},{Value(casters)}\n"));
            }
        }
    }

    private static ProfilerRecorder Counter(string name)
    {
        try { return ProfilerRecorder.StartNew(ProfilerCategory.Render, name, 1); }
        catch (ArgumentException) { return default; }
    }

    private static string Value(ProfilerRecorder counter) => counter.Valid ? counter.LastValue.ToString() : "n/a";
    private static string Quantile(List<double> values, double p) => values.Count == 0 ? "n/a"
        : FormattableString.Invariant($"{values[(int)((values.Count - 1) * p)]:F3}");
}
