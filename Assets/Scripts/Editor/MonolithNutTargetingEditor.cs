using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MonolithNutTargeting))]
public class MonolithNutTargetingEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var targeting = (MonolithNutTargeting)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Nut Target", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Has Valid Nut Target", targeting.HasValidNutTarget.ToString());
        EditorGUILayout.LabelField("Target Type", targeting.TargetType.ToString());
        if (targeting.HasValidNutTarget)
        {
            MonolithNutId nut = targeting.Target;
            EditorGUILayout.LabelField("Face", nut.Face.ToString());
            EditorGUILayout.LabelField("Column", nut.Column.ToString());
            EditorGUILayout.LabelField("Row", nut.Row.ToString());
            EditorGUILayout.LabelField("Nut Index", nut.NutIndex.ToString());
            EditorGUILayout.LabelField("Target Distance", targeting.TargetDistance.ToString("0.00") + " m");
            EditorGUILayout.LabelField("Selected Nut World Position", targeting.SelectedNutWorldPosition.ToString("0.00"));
            EditorGUILayout.LabelField("Remaining Nuts on Rod", targeting.RemainingNutsOnRod.ToString());
        }
        else
        {
            EditorGUILayout.LabelField("Face", "—");
            EditorGUILayout.LabelField("Column", "—");
            EditorGUILayout.LabelField("Row", "—");
            EditorGUILayout.LabelField("Nut Index", "—");
            EditorGUILayout.LabelField("Target Distance", "—");
            EditorGUILayout.LabelField("Selected Nut World Position", "—");
            EditorGUILayout.LabelField("Remaining Nuts on Rod", "—");
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Enter Play Mode and aim the crosshair at a nut. Nut 0 is nearest the wall. Nut 31 is nearest the tip.",
                MessageType.Info);
        }
    }

    public override bool RequiresConstantRepaint()
    {
        return Application.isPlaying;
    }
}
