using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MonolithNutInteraction))]
public class MonolithNutInteractionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var interaction = (MonolithNutInteraction)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Hold Unscrewing", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Button Held", interaction.IsHoldActive.ToString());
        EditorGUILayout.LabelField("Rod Locked", interaction.IsInteractionLocked.ToString());
        if (interaction.IsInteractionLocked)
        {
            MonolithRodId rod = interaction.LockedRod;
            EditorGUILayout.LabelField("Locked Rod", "F" + rod.Face + " C" + rod.Column + " R" + rod.Row);
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Aim at the outermost nut and hold Left Mouse. Inner nuts stay highlighted and show REMOVE OUTER NUTS FIRST.",
                MessageType.Info);
        }
    }

    public override bool RequiresConstantRepaint()
    {
        return Application.isPlaying;
    }
}
