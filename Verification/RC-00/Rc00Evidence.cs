using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using MyLittleFarm.Gameplay.World;

public sealed class Rc00Evidence : MonoBehaviour
{
    private string output;
    private int index;
    private readonly List<float> frameTimes = new List<float>(8192);
    private double deadline;
    private double nextRequestPoll;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        var path = Environment.GetEnvironmentVariable("FARMGAME_RC00_EVIDENCE");
        if (string.IsNullOrEmpty(path)) return;
        var root = new GameObject("RC-00 manual evidence");
        DontDestroyOnLoad(root);
        root.AddComponent<Rc00Evidence>().output = path;
        Debug.Log("[RC00] Profile: " + Application.persistentDataPath);
        Debug.Log("[RC00] " + SystemInfo.processorType + " | " + SystemInfo.graphicsDeviceName + " | " + SystemInfo.graphicsDeviceType + " | RAM MiB " + SystemInfo.systemMemorySize);
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        var request = ReadRequest();
        if ((request == "frame" || keyboard?.f8Key.wasPressedThisFrame == true) && deadline == 0)
        {
            var name = $"frame-{DateTime.UtcNow:HHmmss}-{++index:00}-{SceneManager.GetActiveScene().name}";
            ScreenCapture.CaptureScreenshot(Path.Combine(output, name + ".png"));
            File.AppendAllText(Path.Combine(output, "camera.txt"), name + " | " + State() + Environment.NewLine);
        }

        if ((request == "timing" || keyboard?.f7Key.wasPressedThisFrame == true) && deadline == 0)
        {
            frameTimes.Clear();
            deadline = Time.realtimeSinceStartupAsDouble + 15;
            Debug.Log("[RC00] Timing started");
            return;
        }
        if (deadline == 0) return;
        frameTimes.Add(Time.unscaledDeltaTime * 1000f);
        if (Time.realtimeSinceStartupAsDouble < deadline) return;
        deadline = 0;
        frameTimes.Sort();
        var n = frameTimes.Count;
        double sum = 0;
        foreach (var frame in frameTimes) sum += frame;
        string line = FormattableString.Invariant($"frames={n} meanFPS={1000*n/sum:F1} p50ms={frameTimes[(n-1)/2]:F2} p95ms={frameTimes[(int)(.95*(n-1))]:F2} maxms={frameTimes[n-1]:F2} | ") + State();
        File.AppendAllText(Path.Combine(output, "frame-times.txt"), line + Environment.NewLine);
        Debug.Log("[RC00] " + line);
    }

    private string ReadRequest()
    {
        if (Time.realtimeSinceStartupAsDouble < nextRequestPoll) return null;
        nextRequestPoll = Time.realtimeSinceStartupAsDouble + .25;
        var path = Path.Combine(output, "capture-request.txt");
        if (!File.Exists(path)) return null;
        var request = File.ReadAllText(path).Trim();
        File.Delete(path);
        return request;
    }

    private static string State()
    {
        var camera = Camera.main;
        var player = FindAnyObjectByType<PlayerController>();
        var cycle = FindAnyObjectByType<DayNightCycle>();
        var weather = FindAnyObjectByType<WeatherController>();
        var onboarding = FindAnyObjectByType<OnboardingSystem>();
        return FormattableString.Invariant($"{Screen.width}x{Screen.height} api={SystemInfo.graphicsDeviceType} focused={Application.isFocused} quality={QualitySettings.names[QualitySettings.GetQualityLevel()]} vsync={QualitySettings.vSyncCount} scene={SceneManager.GetActiveScene().name} player={player?.transform.position.ToString("F3")} camera={camera?.transform.position.ToString("F3")} euler={camera?.transform.eulerAngles.ToString("F3")} projection={(camera == null ? null : camera.orthographic ? "ortho" : "perspective")} ortho={camera?.orthographicSize:F3} fov={camera?.fieldOfView:F3} hour={cycle?.Hour:F3} weather={weather?.TargetState} clouds={weather?.CloudCover:F2} rain={weather?.RainIntensity:F2} onboarding={onboarding?.Step}");
    }
}
