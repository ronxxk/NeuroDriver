using UnityEngine;
using UnityEngine.InputSystem;

public class GenerationManager : MonoBehaviour
{
    public static int Gen { get; private set; } = 1;

    public float maxGenTime = 60f;   // seconds before a generation is cut off
    public int settleFrames = 5;     // frames to wait so Python sees the final fitness values

    float genStart;
    int settle;
    bool paused;
    bool showHud = true;
    float savedTimeScale = 1f;
    GUIStyle label, button;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Gen = 1; }

    void Start()
    {
        Application.runInBackground = true;
        genStart = Time.time;
    }

    void Update()
    {
        var k = Keyboard.current;
        if (k == null) return;
        if (k.pKey.wasPressedThisFrame) TogglePause();
        if (k.hKey.wasPressedThisFrame) showHud = !showHud;
    }

    void TogglePause()
    {
        paused = !paused;
        if (paused) { savedTimeScale = Time.timeScale; Time.timeScale = 0f; }
        else Time.timeScale = savedTimeScale;   // restores your speed-up if you set one
    }

    void FixedUpdate()
    {
        if (CarSensors.All.Count == 0) return;

        bool allDead = true;
        foreach (var c in CarSensors.All)
            if (!c.crashed) { allDead = false; break; }

        bool timeUp = Time.time - genStart > maxGenTime;

        if (!allDead && !timeUp) { settle = 0; return; }
        if (++settle < settleFrames) return;

        foreach (var c in CarSensors.All) c.ResetState();

        Gen++;
        settle = 0;
        genStart = Time.time;
        Debug.Log("Generation " + Gen);
    }

    void OnGUI()
    {
        if (!showHud) return;

        if (label == null)
        {
            label = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            button = new GUIStyle(GUI.skin.button);
        }

        int size = Mathf.RoundToInt(Screen.height * 0.04f);
        label.fontSize = size;
        button.fontSize = Mathf.RoundToInt(size * 0.7f);

        int alive = 0;
        float best = 0f;
        foreach (var c in CarSensors.All)
        {
            if (!c.crashed) alive++;
            best = Mathf.Max(best, c.Fitness);
        }

        string text = "Generation " + Gen
                    + "\nAlive " + alive + "/" + CarSensors.All.Count + "   Best " + best.ToString("F1")
                    + (paused ? "\nPAUSED" : "");

        var r = new Rect(20, 20, Screen.width * 0.6f, size * 4f);
        label.normal.textColor = Color.black;
        GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, label);   // shadow
        label.normal.textColor = Color.white;
        GUI.Label(r, text, label);

        float w = size * 5f, h = size * 1.6f;
        if (GUI.Button(new Rect(Screen.width - w - 20, 20, w, h), paused ? "Resume (P)" : "Pause (P)", button))
            TogglePause();
    }
}