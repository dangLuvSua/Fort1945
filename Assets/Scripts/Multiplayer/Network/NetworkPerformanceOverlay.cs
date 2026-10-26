using System.Globalization;
using Fusion;
using UnityEngine;

/// <summary>
/// Small local diagnostics overlay. Toggle it with F3.
/// </summary>
public sealed class NetworkPerformanceOverlay : MonoBehaviour
{
    private const float RefreshInterval = 0.5f;

    private bool visible = true;
    private float fps;
    private float fpsTimer;
    private int fpsFrames;
    private GUIStyle panelStyle;
    private GUIStyle textStyle;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F3))
            visible = !visible;

        fpsFrames++;
        fpsTimer += Time.unscaledDeltaTime;

        if (fpsTimer >= RefreshInterval)
        {
            fps = fpsFrames / fpsTimer;
            fpsFrames = 0;
            fpsTimer = 0f;
        }
    }

    private void OnGUI()
    {
        if (!visible)
            return;

        EnsureStyles();

        NetworkRunner runner = NetworkRunnerHandler.Instance != null
            ? NetworkRunnerHandler.Instance.GameRunner
            : null;

        string connection = runner != null && runner.IsRunning ? "Connected" : "Offline";
        string ping = "--";

        if (runner != null && runner.IsRunning && runner.LocalPlayer.IsValid)
        {
            float rttMilliseconds = (float)(runner.GetPlayerRtt(runner.LocalPlayer) * 1000.0);
            if (!float.IsNaN(rttMilliseconds) && !float.IsInfinity(rttMilliseconds) && rttMilliseconds >= 0f)
                ping = Mathf.RoundToInt(rttMilliseconds).ToString(CultureInfo.InvariantCulture) + " ms";
        }

        string label = $"NET  {connection}\nFPS  {Mathf.RoundToInt(fps)}\nRTT  {ping}\nF3  Toggle";
        const float width = 170f;
        const float height = 86f;
        Rect rect = new Rect(Screen.width - width - 12f, 12f, width, height);

        Color previousGuiColor = GUI.color;
        GUI.color = new Color(0.035f, 0.045f, 0.06f, 0.88f);
        GUI.Box(rect, GUIContent.none, panelStyle);
        GUI.color = previousGuiColor;
        GUI.Label(new Rect(rect.x + 10f, rect.y + 7f, rect.width - 20f, rect.height - 14f), label, textStyle);
    }

    private void EnsureStyles()
    {
        if (panelStyle != null)
            return;

        panelStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.UpperLeft
        };
        textStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.UpperLeft,
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
    }
}
