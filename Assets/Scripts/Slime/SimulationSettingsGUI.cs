using UnityEngine;

public class SimulationSettingsGUI : MonoBehaviour
{
    public Simulation simulation;

    void Awake()
    {
        if (simulation == null)
        {
            simulation = FindObjectOfType<Simulation>();
        }
    }

    GUIStyle titleStyle;
    GUIStyle labelStyle;

    void OnGUI()
    {
        if (simulation == null || simulation.settings == null)
            return;

        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            labelStyle = new GUIStyle(GUI.skin.label) { margin = new RectOffset(0, 0, 2, 2) };
        }

        var settings = simulation.settings;
        Rect rect = new Rect(10, 10, 240, 220);
        GUI.color = new Color(0, 0, 0, 0.6f);
        GUI.Box(rect, GUIContent.none);
        GUI.color = Color.white;

        GUILayout.BeginArea(rect);
        GUILayout.Space(4);
        GUILayout.Label("Simulation Settings", titleStyle);
        GUILayout.Space(8);

        DrawIntSlider(ref settings.stepsPerFrame, 1, 10, "Steps Per Frame");
        DrawFloatSlider(ref settings.trailWeight, 0f, 10f, "Trail Weight");
        DrawFloatSlider(ref settings.decayRate, 0f, 5f, "Decay Rate");
        DrawFloatSlider(ref settings.diffuseRate, 0f, 5f, "Diffuse Rate");

        GUILayout.Space(5);
        if (GUILayout.Button("Apply", GUILayout.Height(24)))
        {
            simulation.ResetSimulation();
        }
        GUILayout.EndArea();
    }

    void DrawIntSlider(ref int value, int min, int max, string label)
    {
        value = Mathf.RoundToInt(GUILayout.HorizontalSlider(value, min, max));
        GUILayout.Label($"{label}: {value}", labelStyle);
    }

    void DrawFloatSlider(ref float value, float min, float max, string label)
    {
        value = GUILayout.HorizontalSlider(value, min, max);
        GUILayout.Label($"{label}: {value:F2}", labelStyle);
    }
}
