using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class MonolithNutTargetSetup
{
    const string SettingsPath = "Assets/Settings/MonolithNutTargetSettings.asset";

    static int _attempts;

    static MonolithNutTargetSetup()
    {
        EditorApplication.update += Ensure;
    }

    static void Ensure()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        _attempts++;
        if (Wire() || _attempts > 30)
            EditorApplication.update -= Ensure;
    }

    static bool Wire()
    {
        var player = Object.FindAnyObjectByType<FirstPersonPlayer>();
        var band = Object.FindAnyObjectByType<MonolithFourFaceBandPreviewRenderer>();
        var rods = Object.FindAnyObjectByType<MonolithRodColliderField>();
        if (player == null || band == null || rods == null)
        {
            Debug.Log("Nut target setup waiting. Player=" + (player != null) + " band=" + (band != null) + " rods=" + (rods != null));
            return false;
        }

        MonolithNutTargetSettings settings = AssetDatabase.LoadAssetAtPath<MonolithNutTargetSettings>(SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<MonolithNutTargetSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            AssetDatabase.SaveAssets();
        }

        bool changed = false;
        var highlighter = band.GetComponent<MonolithNutHighlighter>();
        if (highlighter == null)
        {
            highlighter = band.gameObject.AddComponent<MonolithNutHighlighter>();
            changed = true;
        }

        var targeting = player.GetComponent<MonolithNutTargeting>();
        if (targeting == null)
        {
            targeting = player.gameObject.AddComponent<MonolithNutTargeting>();
            changed = true;
        }

        var interaction = player.GetComponent<MonolithNutInteraction>();
        if (interaction == null)
        {
            interaction = player.gameObject.AddComponent<MonolithNutInteraction>();
            changed = true;
        }

        Transform eye = new SerializedObject(player).FindProperty("eye").objectReferenceValue as Transform;
        Camera camera = eye != null ? eye.GetComponent<Camera>() : null;
        changed |= Assign(highlighter, "band", band);
        changed |= Assign(highlighter, "settings", settings);
        changed |= Assign(highlighter, "eye", camera);
        changed |= Assign(targeting, "settings", settings);
        changed |= Assign(targeting, "eye", eye);
        changed |= Assign(targeting, "rods", rods);
        changed |= Assign(targeting, "band", band);
        changed |= Assign(targeting, "highlights", highlighter);
        changed |= Assign(interaction, "targeting", targeting);
        changed |= Assign(interaction, "band", band);
        changed |= Assign(interaction, "highlights", highlighter);
        changed |= Assign(interaction, "player", player);

        if (!changed)
            return true;

        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        EditorSceneManager.SaveScene(player.gameObject.scene);
        Debug.Log("Nut target setup saved the open scene.");
        return true;
    }

    static bool Assign(Object component, string property, Object value)
    {
        var serialized = new SerializedObject(component);
        SerializedProperty field = serialized.FindProperty(property);
        if (field == null || field.objectReferenceValue == value)
            return false;

        field.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }
}
