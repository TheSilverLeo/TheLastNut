using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MonolithFourFaceBandPreviewRenderer))]
public class MonolithFourFaceBandPreviewRendererEditor : Editor
{
    const float ButtonHeight = 28f;
    const string FaceKey = "TheLastRivet.NutRemoval.Face";
    const string ColumnKey = "TheLastRivet.NutRemoval.Column";
    const string RowKey = "TheLastRivet.NutRemoval.Row";

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
        EditorGUILayout.PropertyField(serializedObject.FindProperty("unscrewingSettings"));
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

        DrawNutRemoval(preview);
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

    void DrawNutRemoval(MonolithFourFaceBandPreviewRenderer preview)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Nut Removal", EditorStyles.boldLabel);
        serializedObject.Update();
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("unscrewingSettings"),
            new GUIContent("Unscrewing Settings"));
        serializedObject.ApplyModifiedProperties();
        DrawUnscrewingSettings(preview);
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Enter Play Mode to remove nuts. Edit Mode keeps every nut visible. Removal state resets each time Play Mode starts.",
                MessageType.Info);
            return;
        }

        MonolithNutState state = preview.NutState;
        if (preview.BandSettings == null || preview.NutSettings == null || state == null)
        {
            EditorGUILayout.HelpBox("Show the four-face band so the nut state can be created.", MessageType.Warning);
            return;
        }

        if (!preview.ShowPreview)
        {
            EditorGUILayout.HelpBox("Show Four-Face Band is off, so removals will not be visible until it is turned on.", MessageType.Warning);
        }

        int face = SessionState.GetInt(FaceKey, 0);
        int column = SessionState.GetInt(ColumnKey, 0);
        int row = SessionState.GetInt(RowKey, 0);
        face = Mathf.Clamp(face, 0, state.FaceCount - 1);
        column = Mathf.Clamp(column, 0, state.ColumnCount - 1);
        row = Mathf.Clamp(row, 0, state.RowCount - 1);

        EditorGUI.BeginChangeCheck();
        face = EditorGUILayout.IntSlider("Face", face, 0, state.FaceCount - 1);
        column = EditorGUILayout.IntSlider("Column", column, 0, state.ColumnCount - 1);
        row = EditorGUILayout.IntSlider("Row", row, 0, state.RowCount - 1);
        if (EditorGUI.EndChangeCheck())
        {
            SessionState.SetInt(FaceKey, face);
            SessionState.SetInt(ColumnKey, column);
            SessionState.SetInt(RowKey, row);
        }

        var rod = new MonolithRodId(face, column, row);
        bool animating = preview.IsRodAnimating(rod);
        EditorGUILayout.LabelField("Selected rod remaining nuts", state.Remaining(rod).ToString());
        EditorGUILayout.LabelField("Total removed nuts", state.RemovedCount.ToString());
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Detached Nut Debug", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Active Unscrewing Animations", preview.ActiveUnscrewingCount.ToString());
        EditorGUILayout.LabelField("Completed Detachments", preview.CompletedDetachmentCount.ToString());
        EditorGUILayout.LabelField("Active Physics Nuts", preview.ActivePhysicsNutCount.ToString());
        EditorGUILayout.LabelField("Inactive Pooled Nuts", preview.InactivePooledNutCount.ToString());
        EditorGUILayout.LabelField("Total Pool Size", preview.TotalPoolSize.ToString());
        EditorGUILayout.LabelField("Total Removed Nuts", preview.TotalRemovedNuts.ToString());
        EditorGUILayout.HelpBox(
            "Remove One Nut hides the outermost nut immediately. Animate One Nut spins that nut off the tip, then drops it as a physics object. The counter changes once, when the nut leaves the rod.",
            MessageType.None);

        if (animating)
        {
            EditorGUILayout.HelpBox(
                "This rod is unscrewing. Remaining nuts and Total Removed stay unchanged until the nut clears the tip.",
                MessageType.None);
        }

        using (new EditorGUI.DisabledScope(state.Remaining(rod) == 0 || animating))
        {
            if (GUILayout.Button("Remove One Nut", GUILayout.Height(ButtonHeight)))
                preview.TryRemoveOneNut(rod);
        }

        using (new EditorGUI.DisabledScope(state.Remaining(rod) == 0 || animating || preview.UnscrewingSettings == null))
        {
            if (GUILayout.Button("Animate One Nut", GUILayout.Height(ButtonHeight)))
                preview.TryAnimateOneNut(rod);
        }

        using (new EditorGUI.DisabledScope(state.RemovedCount == 0 && !preview.HasActiveUnscrewing))
        {
            if (GUILayout.Button("Reset All Nuts", GUILayout.Height(ButtonHeight)))
                preview.ResetAllNuts();
        }
    }

    static void DrawUnscrewingSettings(MonolithFourFaceBandPreviewRenderer preview)
    {
        if (preview.UnscrewingSettings == null)
        {
            EditorGUILayout.HelpBox(
                "Assign Nut Unscrewing Settings. The asset is Assets/Settings/MonolithNutUnscrewingSettings.",
                MessageType.Warning);
            return;
        }

        var settings = new SerializedObject(preview.UnscrewingSettings);
        settings.Update();
        EditorGUILayout.PropertyField(settings.FindProperty("movementSpeed"), new GUIContent("Movement Speed"));
        EditorGUILayout.PropertyField(settings.FindProperty("rotationSpeed"), new GUIContent("Rotation Speed"));
        EditorGUILayout.PropertyField(settings.FindProperty("minimumDuration"), new GUIContent("Minimum Duration"));
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Detached Nut Physics", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(settings.FindProperty("mass"), new GUIContent("Mass"));
        EditorGUILayout.PropertyField(settings.FindProperty("bounciness"), new GUIContent("Bounciness"));
        EditorGUILayout.PropertyField(settings.FindProperty("friction"), new GUIContent("Friction"));
        EditorGUILayout.PropertyField(settings.FindProperty("outwardVelocity"), new GUIContent("Outward Velocity"));
        EditorGUILayout.PropertyField(settings.FindProperty("angularVelocity"), new GUIContent("Angular Velocity"));
        settings.ApplyModifiedProperties();
    }
}
