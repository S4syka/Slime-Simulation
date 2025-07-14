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

    void OnGUI()
    {
        if (simulation == null || simulation.settings == null)
            return;

        var settings = simulation.settings;
        GUILayout.BeginArea(new Rect(10, 10, 220, 200), GUI.skin.box);
        GUILayout.Label("Simulation Settings");

        settings.stepsPerFrame = Mathf.RoundToInt(GUILayout.HorizontalSlider(settings.stepsPerFrame, 1, 10));
        GUILayout.Label($"Steps Per Frame: {settings.stepsPerFrame}");

        settings.trailWeight = GUILayout.HorizontalSlider(settings.trailWeight, 0f, 10f);
        GUILayout.Label($"Trail Weight: {settings.trailWeight:F2}");

        settings.decayRate = GUILayout.HorizontalSlider(settings.decayRate, 0f, 5f);
        GUILayout.Label($"Decay Rate: {settings.decayRate:F2}");

        settings.diffuseRate = GUILayout.HorizontalSlider(settings.diffuseRate, 0f, 5f);
        GUILayout.Label($"Diffuse Rate: {settings.diffuseRate:F2}");

        if (GUILayout.Button("Apply"))
        {
            simulation.ResetSimulation();
        }
        GUILayout.EndArea();
    }
}
