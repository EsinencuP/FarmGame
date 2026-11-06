using System;
using System.Collections;
using System.IO;
using System.Linq;
using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Gameplay.World;
using MyLittleFarm.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Optional capture console for an isolated Windows player. Copy into the build snapshot only.
public sealed class Rc03Preview : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FARMGAME_RC03_PREVIEW"))) return;
        var go = new GameObject("RC-03 preview");
        DontDestroyOnLoad(go);
        go.AddComponent<Rc03Preview>();
    }

    private IEnumerator Start()
    {
        var folder = Environment.GetEnvironmentVariable("FARMGAME_RC03_PREVIEW");
        Directory.CreateDirectory(folder);
        var request = Path.Combine(folder, "request.txt");
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var mouse = InputSystem.AddDevice<Mouse>();
        var keys = Array.Empty<Key>();
        GameEvents.ActionVisualFeedback += (action, _, item, amount) => Log($"feedback {action} {item} {amount} frame={Time.frameCount}");
        GameEvents.StatusChanged += status => Log("status " + status);
        Log("profile " + Application.persistentDataPath);
        while (true)
        {
            yield return null;
            if (!File.Exists(request)) continue;
            var parts = File.ReadAllText(request).Trim().Split(' ');
            File.Delete(request);
            switch (parts[0])
            {
                case "settings":
                    var ui = FindAnyObjectByType<FarmSettingsUI>(FindObjectsInactive.Include);
                    var title = FindAnyObjectByType<MainMenuUI>();
                    if (title != null)
                        ((Button)typeof(MainMenuUI).GetField("options", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                            .GetValue(title)).onClick.Invoke();
                    else ui.Open();
                    ButtonNamed(ui.transform, "Управление").onClick.Invoke();
                    break;
                case "bind":
                    var command = Enum.Parse<InputReader.Command>(parts[1]);
                    var controls = FindAnyObjectByType<FarmSettingsUI>();
                    var row = controls.GetComponentsInChildren<Button>().First(b => b.name == "Bind " + command);
                    var scroll = row.GetComponentInParent<ScrollRect>();
                    var position = ((RectTransform)row.transform).anchoredPosition.y;
                    var range = scroll.content.rect.height - scroll.viewport.rect.height;
                    scroll.verticalNormalizedPosition = Mathf.Clamp01(1 + (position + 60) / Mathf.Max(1, range));
                    row.onClick.Invoke();
                    break;
                case "reset":
                    ButtonNamed(FindAnyObjectByType<FarmSettingsUI>().transform, "Вернуть стандартные клавиши").onClick.Invoke();
                    break;
                case "keys":
                    keys = parts.Skip(1).Select(s => Enum.Parse<Key>(s)).ToArray();
                    Log($"keys {string.Join(" ", keys)} queued frame={Time.frameCount}");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
                    if (keys.Length > 0) yield return null;
                    break;
                case "farm":
                    FarmSceneFlow.LoadGame();
                    while (FarmSceneFlow.IsLoading || SceneManager.GetActiveScene().name != "Prototype") yield return null;
                    FindAnyObjectByType<SaveSlotsUI>().GetComponentsInChildren<SaveSlotCard>(true)[int.Parse(parts[1])].Primary.onClick.Invoke();
                    break;
                case "button":
                    var market = FindAnyObjectByType<FarmMarketUI>();
                    ButtonNamed(market.transform, string.Join(" ", parts.Skip(1))).onClick.Invoke();
                    break;
                case "load":
                    Log("load manual " + FindAnyObjectByType<SaveSystem>().LoadSlot(int.Parse(parts[1]), true));
                    break;
                case "zoom":
                    InputSystem.QueueStateEvent(mouse, new MouseState { scroll = new Vector2(0, float.Parse(parts[1])) });
                    break;
                case "pose":
                    var point = GridSystem.Instance.GridToWorld(int.Parse(parts[1]), int.Parse(parts[2]));
                    FindAnyObjectByType<PlayerController>().Teleport(point + Vector3.up * .744f);
                    Camera.main.GetComponent<IsometricCameraController>().RefreshPosition();
                    break;
                case "cell":
                    var world = GridSystem.Instance.GridToWorld(int.Parse(parts[1]), int.Parse(parts[2]));
                    var pixel = (Vector2)Camera.main.WorldToScreenPoint(world);
                    Log($"pointer {parts[1]} {parts[2]} {parts[3]} frame={Time.frameCount}");
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = pixel }.WithButton(MouseButton.Left, parts[3] == "down"));
                    break;
                case "shot":
                    yield return new WaitForEndOfFrame();
                    ScreenCapture.CaptureScreenshot(Path.Combine(folder, parts[1] + ".png"));
                    break;
                case "state":
                    var selector = FindAnyObjectByType<CellSelector>();
                    var camera = Camera.main;
                    var player = FindAnyObjectByType<PlayerController>();
                    Log($"{SceneManager.GetActiveScene().name} interact={InputReader.KeyLabel(InputReader.Command.Interact)} action={InputReader.ActionHint} rotate={InputReader.BuildRotateHint} player={player?.transform.position.ToString("F3")} camera={camera.transform.position:F3} yaw={camera.transform.eulerAngles.y:F1} silhouetteCommands={camera.GetCommandBuffers(CameraEvent.AfterForwardOpaque).Sum(buffer => buffer.sizeInBytes)} hover={selector?.HoveredCell} type={selector?.HoveredCellType} stable={selector?.IsSelectionStable}");
                    break;
                case "quit":
                    Application.Quit();
                    yield break;
            }
        }

        void Log(string line) => File.AppendAllText(Path.Combine(folder, "notes.txt"), line + Environment.NewLine);
    }

    private static Button ButtonNamed(Transform parent, string text) => parent.GetComponentsInChildren<Button>(true)
        .First(b => b.GetComponentInChildren<Text>(true)?.text == text);
}
