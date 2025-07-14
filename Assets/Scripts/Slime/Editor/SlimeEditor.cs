using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(Simulation))]
public class SlimeEditor : Editor
{

	Editor settingsEditor;
	bool settingsFoldout;

        public override void OnInspectorGUI()
        {
                DrawDefaultInspector();
                Simulation sim = target as Simulation;

		if (sim.settings != null) {
			DrawSettingsEditor(sim.settings, ref settingsFoldout, ref settingsEditor);
			EditorPrefs.SetBool (nameof (settingsFoldout), settingsFoldout);
                }
        }

        void OnSceneGUI () {
                Simulation sim = target as Simulation;
                if (sim == null || sim.settings == null) {
                        return;
                }

                var meshRenderer = sim.GetComponentInChildren<MeshRenderer>();
                if (meshRenderer == null) {
                        return;
                }

                Transform t = meshRenderer.transform;
                float width = t.lossyScale.x;
                float height = t.lossyScale.y;

                using (new Handles.DrawingScope(Matrix4x4.TRS(t.position, t.rotation, Vector3.one))) {
                        Handles.color = Color.cyan;
                        Handles.DrawWireCube(Vector3.zero, new Vector3(width, height, 0));

                        float radiusRatio = 0;
                        if (sim.settings.spawnMode == Simulation.SpawnMode.InwardCircle) {
                                radiusRatio = 0.5f;
                        }
                        else if (sim.settings.spawnMode == Simulation.SpawnMode.RandomCircle) {
                                radiusRatio = 0.15f;
                        }

                        if (radiusRatio > 0) {
                                float radius = height * radiusRatio;
                                Handles.color = Color.yellow;
                                Handles.DrawWireDisc(Vector3.zero, Vector3.forward, radius);
                        }
                }
        }

	void DrawSettingsEditor(Object settings, ref bool foldout, ref Editor editor)
	{
		if (settings != null)
		{
			foldout = EditorGUILayout.InspectorTitlebar(foldout, settings);
			if (foldout)
			{
				CreateCachedEditor(settings, null, ref editor);
				editor.OnInspectorGUI();
			}

		}
	}

	private void OnEnable () {
		settingsFoldout = EditorPrefs.GetBool (nameof (settingsFoldout), false);
	}
}
