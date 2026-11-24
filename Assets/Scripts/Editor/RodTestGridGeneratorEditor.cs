using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(RodTestGridGenerator))]
public class RodTestGridGeneratorEditor : Editor
{
    const float ButtonHeight = 28f;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Attach this to Monolith_Controller. Generate Test Grid creates Generated_Rods_Test as a child of this object, outside Monolith_Root, and fills every rod with nuts. Assign the grid settings, nut settings, Rod_Template, and Face_01_Anchor. Nothing is created until you press a button.",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(Application.isPlaying || targets.Length != 1))
        {
            if (GUILayout.Button("Generate Test Grid", GUILayout.Height(ButtonHeight)))
                Generate((RodTestGridGenerator)target);

            if (GUILayout.Button("Clear Test Grid", GUILayout.Height(ButtonHeight)))
                Clear((RodTestGridGenerator)target);
        }
    }

    static void Generate(RodTestGridGenerator generator)
    {
        if (Application.isPlaying)
        {
            Debug.LogError("Generate the test grid in Edit Mode, before entering Play.", generator);
            return;
        }

        if (!TryValidate(generator))
            return;

        RunUndoable("Generate Test Grid", () =>
        {
            DestroyOwnedContainers(generator);
            Transform container = CreateContainer(generator);
            RodGridPoint[] points = RodTestGridLayout.Build(
                generator.Settings,
                generator.ReverseColumnDirection);

            int nutTotal = 0;
            for (int i = 0; i < points.Length; i++)
            {
                if (!CreateRod(generator, container, points[i], out int nutsOnRod))
                    return;

                nutTotal += nutsOnRod;
            }

            RememberContainer(generator, container);
            EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);
            Debug.Log($"Generated {points.Length} rods and {nutTotal} nuts under {container.name}.", container);
        });
    }

    static void Clear(RodTestGridGenerator generator)
    {
        if (Application.isPlaying)
        {
            Debug.LogError("Clear the test grid in Edit Mode, before entering Play.", generator);
            return;
        }

        RunUndoable("Clear Test Grid", () =>
        {
            int removed = DestroyOwnedContainers(generator);
            RememberContainer(generator, null);
            EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);

            if (removed == 0)
                Debug.Log("No test grid to clear.", generator);
            else
                Debug.Log("Cleared the rod test grid and its nuts.", generator);
        });
    }

    static bool TryValidate(RodTestGridGenerator generator)
    {
        if (generator.Settings == null || generator.NutSettings == null || generator.RodPrefab == null || generator.FaceAnchor == null)
        {
            Debug.LogError("Assign Settings, Nut Settings, Rod Prefab, and Face Anchor before generating.", generator);
            return false;
        }

        if (!TryResolveRodParts(generator.RodPrefab.transform, out Transform rodTip, out _, out _))
        {
            Debug.LogError("Rod_Template must contain Rod_Tip, Nut_Anchor, and Nut.", generator.RodPrefab);
            return false;
        }

        float rodLength = generator.RodPrefab.transform.InverseTransformPoint(rodTip.position).z;
        if (!RodNutLayout.TryBuild(generator.NutSettings, rodLength, out _, out string nutError))
        {
            Debug.LogError(nutError, generator.NutSettings);
            return false;
        }

        if (generator.Settings.ColumnCount < 1 || generator.Settings.RowCount < 1)
        {
            Debug.LogError("Column count and row count must be at least 1.", generator.Settings);
            return false;
        }

        PrefabAssetType assetType = PrefabUtility.GetPrefabAssetType(generator.RodPrefab);
        if (assetType == PrefabAssetType.NotAPrefab || assetType == PrefabAssetType.MissingAsset)
        {
            Debug.LogError("Rod Prefab must be the Rod_Template prefab asset, not a scene object.", generator);
            return false;
        }

        if (!LossyScaleIsOne(generator.transform))
        {
            Debug.LogWarning(
                "This generator's lossy scale is not 1. Attach it to Monolith_Controller so the rods are not scaled by Monolith_Root.",
                generator);
        }

        if (!LossyScaleIsOne(generator.FaceAnchor))
        {
            Debug.LogWarning(
                "Face Anchor's lossy scale is not 1. Spacing is measured in that transform's local axes, so a scaled anchor stretches the grid.",
                generator.FaceAnchor);
        }

        return true;
    }

    static Transform CreateContainer(RodTestGridGenerator generator)
    {
        var containerObject = new GameObject(RodTestGridGenerator.ContainerName);
        Undo.RegisterCreatedObjectUndo(containerObject, "Generate Test Grid");
        Undo.SetTransformParent(containerObject.transform, generator.transform, "Generate Test Grid");
        Undo.RecordObject(containerObject.transform, "Generate Test Grid");
        containerObject.transform.localPosition = Vector3.zero;
        containerObject.transform.localRotation = Quaternion.identity;
        containerObject.transform.localScale = Vector3.one;
        return containerObject.transform;
    }

    static bool CreateRod(RodTestGridGenerator generator, Transform container, RodGridPoint point, out int nutsOnRod)
    {
        nutsOnRod = 0;
        var instance = PrefabUtility.InstantiatePrefab(generator.RodPrefab, container) as GameObject;
        if (instance == null)
        {
            Debug.LogError("Could not create a prefab instance from Rod Prefab.", generator);
            return false;
        }

        Undo.RegisterCreatedObjectUndo(instance, "Generate Test Grid");
        Undo.RecordObject(instance, "Generate Test Grid");
        Undo.RecordObject(instance.transform, "Generate Test Grid");

        instance.name = $"Rod_C{point.Column + 1}_R{point.Row + 1}";

        Vector3 worldPosition = generator.FaceAnchor.TransformPoint(point.AnchorLocalPosition);
        Quaternion worldRotation = generator.FaceAnchor.rotation;
        instance.transform.localPosition = container.InverseTransformPoint(worldPosition);
        instance.transform.localRotation = Quaternion.Inverse(container.rotation) * worldRotation;

        PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
        PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);

        int rotationSeed = SeedForRod(generator.NutSettings.RotationSeed, point.Column, point.Row);
        if (!PopulateRod(instance.transform, generator.NutSettings, rotationSeed))
            return false;

        nutsOnRod = generator.NutSettings.NutCount;
        return true;
    }

    static bool PopulateRod(Transform rod, RodNutTestSettings settings, int rotationSeed)
    {
        const string undoName = "Generate Test Grid";

        if (!TryResolveRodParts(rod, out Transform rodTip, out Transform nutAnchor, out Transform referenceNut))
        {
            Debug.LogError("This rod is missing Rod_Tip, Nut_Anchor, or Nut.", rod);
            return false;
        }

        float rodLength = rod.InverseTransformPoint(rodTip.position).z;
        if (!RodNutLayout.TryBuild(settings, rodLength, rotationSeed, out NutSlot[] slots, out string error))
        {
            Debug.LogError(error, rod);
            return false;
        }

        RodNutReferencePose pose = RodNutReferencePose.Capture(nutAnchor, referenceNut);
        Transform nutContainer = CreateNutContainer(rod, undoName);

        for (int i = 0; i < slots.Length - 1; i++)
        {
            var copy = Object.Instantiate(referenceNut.gameObject, nutContainer, false);
            Undo.RegisterCreatedObjectUndo(copy, undoName);

            if (copy.transform.childCount != referenceNut.childCount)
            {
                Undo.DestroyObjectImmediate(copy);
                Debug.LogError("The nut copy included extra children, so generation stopped.", rod);
                return false;
            }

            Undo.RecordObject(copy, undoName);
            Undo.RecordObject(copy.transform, undoName);
            copy.name = $"Nut_{slots[i].Index + 1:00}";
            pose.PlaceCopy(copy.transform, slots[i].RodLocalZ, slots[i].SpinDegrees);
        }

        Undo.RecordObject(nutAnchor, undoName);
        Undo.RecordObject(referenceNut, undoName);
        NutSlot outerNut = slots[slots.Length - 1];
        pose.PlaceReference(nutAnchor, referenceNut, outerNut.RodLocalZ, outerNut.SpinDegrees);

        if (PrefabUtility.IsPartOfPrefabInstance(nutAnchor))
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(nutAnchor);
            PrefabUtility.RecordPrefabInstancePropertyModifications(referenceNut);
        }

        return true;
    }

    static Transform CreateNutContainer(Transform rod, string undoName)
    {
        var containerObject = new GameObject(RodNutTestGenerator.ContainerName);
        Undo.RegisterCreatedObjectUndo(containerObject, undoName);
        Undo.SetTransformParent(containerObject.transform, rod, undoName);
        Undo.RecordObject(containerObject.transform, undoName);
        containerObject.transform.localPosition = Vector3.zero;
        containerObject.transform.localRotation = Quaternion.identity;
        containerObject.transform.localScale = Vector3.one;
        return containerObject.transform;
    }

    static bool TryResolveRodParts(Transform rod, out Transform rodTip, out Transform nutAnchor, out Transform referenceNut)
    {
        rodTip = null;
        nutAnchor = null;
        referenceNut = null;

        for (int i = 0; i < rod.childCount; i++)
        {
            Transform child = rod.GetChild(i);
            if (child.name == "Rod_Tip")
                rodTip = child;
            else if (child.name == "Nut_Anchor")
                nutAnchor = child;
        }

        if (nutAnchor != null)
        {
            for (int i = 0; i < nutAnchor.childCount; i++)
            {
                Transform child = nutAnchor.GetChild(i);
                if (child.name != "Nut")
                    continue;

                referenceNut = child;
                break;
            }
        }

        return rodTip != null && nutAnchor != null && referenceNut != null && referenceNut.parent == nutAnchor;
    }

    static int SeedForRod(int baseSeed, int column, int row)
    {
        unchecked
        {
            int seed = baseSeed;
            seed = (seed * 397) ^ (column + 1);
            seed = (seed * 397) ^ ((row + 1) * 17);
            return seed;
        }
    }

    static int DestroyOwnedContainers(RodTestGridGenerator generator)
    {
        int removed = 0;

        for (int i = generator.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = generator.transform.GetChild(i);
            if (child.name != RodTestGridGenerator.ContainerName)
                continue;

            Undo.DestroyObjectImmediate(child.gameObject);
            removed++;
        }

        return removed;
    }

    static void RememberContainer(RodTestGridGenerator generator, Transform container)
    {
        var serializedGenerator = new SerializedObject(generator);
        serializedGenerator.FindProperty("generatedContainer").objectReferenceValue = container;
        serializedGenerator.ApplyModifiedProperties();
    }

    static void RunUndoable(string undoName, System.Action action)
    {
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(undoName);

        try
        {
            action();
        }
        finally
        {
            Undo.CollapseUndoOperations(group);
        }
    }

    static bool LossyScaleIsOne(Transform transform)
    {
        Vector3 scale = transform.lossyScale;
        return Mathf.Approximately(scale.x, 1f)
            && Mathf.Approximately(scale.y, 1f)
            && Mathf.Approximately(scale.z, 1f);
    }
}
