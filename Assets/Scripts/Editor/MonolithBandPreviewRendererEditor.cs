using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
static class MonolithBandPreviewSceneView
{
    static MonolithBandPreviewSceneView()
    {
        SceneView.duringSceneGui -= DuringSceneGui;
        SceneView.duringSceneGui += DuringSceneGui;
        EditorApplication.delayCall += SceneView.RepaintAll;
    }

    static void DuringSceneGui(SceneView view)
    {
        // URP renders view.camera, and beginCameraRendering submits the instanced
        // band for that camera, before this overlay callback. EventType.Repaint
        // here is only the gizmo pass. Submitting RenderMeshInstanced again would
        // draw the band twice or miss the camera target. RepaintAll is requested
        // when the preview is enabled, disabled, or reloaded.
        if (Event.current == null || Event.current.type != EventType.Repaint || view == null)
            return;
    }
}

[CustomEditor(typeof(MonolithBandPreviewRenderer))]
public class MonolithBandPreviewRendererEditor : Editor
{
    const float ButtonHeight = 28f;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var preview = (MonolithBandPreviewRenderer)target;
        DrawBandLayout(preview);
        if (preview.BandSettings != null && preview.NutSettings != null)
        {
            int rods = preview.BandSettings.ColumnCount * preview.BandSettings.RowCount;
            int nuts = rods * preview.NutSettings.NutCount;
            EditorGUILayout.HelpBox(
                $"{rods} rods and {nuts} nuts. They are drawn with GPU instancing, not as individual GameObjects.",
                MessageType.None);
        }

        EditorGUILayout.HelpBox(
            "Attach this to Monolith_Controller. Assign the band settings, Rod Nut Test Settings, Rod_Template, and one face anchor. Show Band Preview draws that one face in the Scene view and the Game view. It turns off the four-face band so the two previews do not overlap. Clear the 3×3 test grid first as well.",
            MessageType.Info);

        EditorGUILayout.Space();
        if (GUILayout.Button("Show Band Preview", GUILayout.Height(ButtonHeight)))
            SetPreviewVisible(true);

        if (GUILayout.Button("Hide Band Preview", GUILayout.Height(ButtonHeight)))
            SetPreviewVisible(false);
    }

    static void DrawBandLayout(MonolithBandPreviewRenderer preview)
    {
        if (preview.BandSettings == null)
            return;

        var band = new SerializedObject(preview.BandSettings);
        band.Update();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Band Layout", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(band.FindProperty("columnCount"));
        EditorGUILayout.PropertyField(band.FindProperty("rowCount"));
        EditorGUILayout.PropertyField(band.FindProperty("horizontalSpacing"));
        EditorGUILayout.PropertyField(band.FindProperty("verticalSpacing"));
        if (band.ApplyModifiedProperties())
            SceneView.RepaintAll();
    }

    void SetPreviewVisible(bool visible)
    {
        if (visible)
            HideFourFacePreview();

        serializedObject.Update();
        serializedObject.FindProperty("showPreview").boolValue = visible;
        serializedObject.ApplyModifiedProperties();
        SceneView.RepaintAll();
    }

    void HideFourFacePreview()
    {
        var other = ((Component)target).GetComponent<MonolithFourFaceBandPreviewRenderer>();
        if (other == null || !other.ShowPreview)
            return;

        var otherObject = new SerializedObject(other);
        otherObject.FindProperty("showPreview").boolValue = false;
        otherObject.ApplyModifiedProperties();
    }
}
