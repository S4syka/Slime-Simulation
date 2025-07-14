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
        Rect rect = new Rect(10, 10, 240, 520);
        GUI.color = new Color(0, 0, 0, 0.6f);
        GUI.Box(rect, GUIContent.none);
        GUI.color = Color.white;

        GUILayout.BeginArea(rect);
        GUILayout.Space(4);
        GUILayout.Label("Simulation Settings", titleStyle);
        GUILayout.Space(8);

        DrawFloatSlider(ref settings.trailWeight, 0f, 100f, "Trail Weight");
        DrawFloatSlider(ref settings.decayRate, 0f, 3f, "Decay Rate");
        DrawFloatSlider(ref settings.diffuseRate, 0f, 3f, "Diffuse Rate");
        DrawIntSlider(ref settings.numAgents, 1, 1000000, "Number of Agents");

        DrawFloatSlider(ref settings.speciesSettings[0].moveSpeed, 0.1f, 100f, "Species 1 Move Speed");
        DrawFloatSlider(ref settings.speciesSettings[0].turnSpeed, -10f, 100f, "Species 1 Turn Speed");
        DrawFloatSlider(ref settings.speciesSettings[0].sensorAngleSpacing, 0f, 180f, "Species 1 Sensor Angle Spacing");
        DrawFloatSlider(ref settings.speciesSettings[0].sensorOffsetDst, 0f, 100f, "Species 1 Sensor Offset Distance");
        DrawIntSlider(ref settings.speciesSettings[0].sensorSize, 1, 5, "Species 1 Sensor Size");

        GUILayout.Space(5);
        if (GUILayout.Button("Apply", GUILayout.Height(24)))
        {
            simulation.ResetSimulation();
        }
        GUILayout.EndArea();
    }

    void DrawIntSlider(ref int value, int min, int max, string label)
    {
        GUILayout.Label($"{label}: {value}", labelStyle);
        value = Mathf.RoundToInt(GUILayout.HorizontalSlider(value, min, max));
    }

    void DrawFloatSlider(ref float value, float min, float max, string label)
    {
        GUILayout.Label($"{label}: {value:F2}", labelStyle);
        value = GUILayout.HorizontalSlider(value, min, max);
    }
}
