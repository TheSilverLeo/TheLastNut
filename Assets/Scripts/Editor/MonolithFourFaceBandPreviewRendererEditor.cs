using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MonolithFourFaceBandPreviewRenderer))]
public class MonolithFourFaceBandPreviewRendererEditor : Editor
{
    const float ButtonHeight = 28f;

    void OnEnable()
    {
        var preview = (MonolithFourFaceBandPreviewRenderer)target;
        MonolithBandPreviewRenderer oneFace = preview.GetComponent<MonolithBandPreviewRenderer>();
        if (oneFace == null)
            return;

        serializedObject.Update();
        SerializedProperty link = serializedObject.FindProperty("oneFacePreview");
        if (link.objectReferenceValue != null)
            return;

        link.objectReferenceValue = oneFace;
        serializedObject.ApplyModifiedProperties();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("showPreview"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("bandSettings"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("nutSettings"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("rodPrefab"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("oneFacePreview"));
        DrawFaces();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("shadowCastingMode"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("receiveShadows"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxInstancesPerDraw"));
        serializedObject.ApplyModifiedProperties();

        var preview = (MonolithFourFaceBandPreviewRenderer)target;
        DrawBandLayout(preview);
        DrawCounts(preview);

        if (preview.OneFacePreview != null && preview.OneFacePreview.ShowPreview)
        {
            EditorGUILayout.HelpBox(
                "The one-face preview is showing, so this band is not drawn. Hide Band Preview, or click Show Four-Face Band.",
                MessageType.Warning);
        }

        EditorGUILayout.HelpBox(
            "Attach this to Monolith_Controller, beside the one-face preview. Assign the same band settings, Rod Nut Test Settings, and Rod_Template. Assign the four face anchors under Rod_Placement_Guides. Each face has its own Reverse Column Direction. Show Four-Face Band turns the one-face preview off. Clear the 3×3 test grid first so those rods do not sit on top of the band.",
            MessageType.Info);

        EditorGUILayout.Space();
        if (GUILayout.Button("Show Four-Face Band", GUILayout.Height(ButtonHeight)))
            SetPreviewVisible(true);

        if (GUILayout.Button("Hide Four-Face Band", GUILayout.Height(ButtonHeight)))
            SetPreviewVisible(false);
    }

    public override bool RequiresConstantRepaint()
    {
        var preview = (MonolithFourFaceBandPreviewRenderer)target;
        return preview != null && preview.ShowPreview;
    }

    void DrawFaces()
    {
        SerializedProperty faces = serializedObject.FindProperty("faces");
        if (faces.arraySize != MonolithFourFaceBandPreviewRenderer.FaceCount)
            faces.arraySize = MonolithFourFaceBandPreviewRenderer.FaceCount;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Faces", EditorStyles.boldLabel);
        for (int i = 0; i < faces.arraySize; i++)
        {
            SerializedProperty face = faces.GetArrayElementAtIndex(i);
            EditorGUILayout.LabelField($"Face {i + 1}", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(face.FindPropertyRelative("anchor"), new GUIContent("Face Anchor"));
            EditorGUILayout.PropertyField(
                face.FindPropertyRelative("reverseColumnDirection"),
                new GUIContent("Reverse Column Direction"));
        }
    }

    static void DrawBandLayout(MonolithFourFaceBandPreviewRenderer preview)
    {
        if (preview.BandSettings == null)
            return;

        var band = new SerializedObject(preview.BandSettings);
        band.Update();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Shared Band Layout", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(band.FindProperty("columnCount"));
        EditorGUILayout.PropertyField(band.FindProperty("rowCount"));
        EditorGUILayout.PropertyField(band.FindProperty("horizontalSpacing"));
        EditorGUILayout.PropertyField(band.FindProperty("verticalSpacing"));
        if (band.ApplyModifiedProperties())
            SceneView.RepaintAll();
    }

    static void DrawCounts(MonolithFourFaceBandPreviewRenderer preview)
    {
        if (preview.BandSettings == null || preview.NutSettings == null)
        {
            EditorGUILayout.HelpBox("Assign Band Settings and Nut Settings.", MessageType.Warning);
            return;
        }

        int rods = MonolithFourFaceBandPreviewRenderer.FaceCount
            * preview.BandSettings.ColumnCount
            * preview.BandSettings.RowCount;
        int nuts = rods * preview.NutSettings.NutCount;
        string instances = preview.BuiltRenderInstanceCount > 0
            ? preview.BuiltRenderInstanceCount.ToString("N0")
            : "not built yet";

        EditorGUILayout.HelpBox(
            $"Rods: {rods:N0}\nNuts: {nuts:N0}\nRender instances: {instances}\n\nRender instances count every rod mesh and every nut material slot. They are GPU instances, not GameObjects.",
            MessageType.None);

        if (preview.BuiltRodCount > 0 && (preview.BuiltRodCount != rods || preview.BuiltNutCount != nuts))
        {
            EditorGUILayout.HelpBox(
                $"The built band has {preview.BuiltRodCount:N0} rods and {preview.BuiltNutCount:N0} nuts, which does not match these settings.",
                MessageType.Warning);
        }
    }

    void SetPreviewVisible(bool visible)
    {
        if (visible)
            HideOneFacePreview();

        serializedObject.Update();
        serializedObject.FindProperty("showPreview").boolValue = visible;
        serializedObject.ApplyModifiedProperties();
        SceneView.RepaintAll();
    }

    void HideOneFacePreview()
    {
        var other = ((Component)target).GetComponent<MonolithBandPreviewRenderer>();
        if (other == null || !other.ShowPreview)
            return;

        var otherObject = new SerializedObject(other);
        otherObject.FindProperty("showPreview").boolValue = false;
        otherObject.ApplyModifiedProperties();
    }
}
