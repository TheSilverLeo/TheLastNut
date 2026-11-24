using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(RodNutTestGenerator))]
public class RodNutTestGeneratorEditor : Editor
{
    const float ButtonHeight = 28f;
    const string GenerateUndo = "Generate 32 Test Nuts";
    const string ClearUndo = "Clear Generated Test Nuts";

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Attach this to Rod_C1_R2 only. Assign that rod's Rod_Tip, Nut_Anchor, and Nut, plus Rod Nut Test Settings. The buttons change this rod's instance and do not touch the other rods or the prefab asset.",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(Application.isPlaying || targets.Length != 1))
        {
            if (GUILayout.Button("Generate 32 Test Nuts", GUILayout.Height(ButtonHeight)))
                Generate((RodNutTestGenerator)target);

            if (GUILayout.Button("Clear Generated Test Nuts", GUILayout.Height(ButtonHeight)))
                Clear((RodNutTestGenerator)target);
        }
    }

    static void Generate(RodNutTestGenerator generator)
    {
        if (!CanEditInstance(generator) || !TryValidate(generator, out float rodLength))
            return;

        if (!RodNutLayout.TryBuild(generator.Settings, rodLength, out NutSlot[] slots, out string error))
        {
            Debug.LogError(error, generator);
            return;
        }

        WarnIfEndClearanceDiffers(generator, rodLength, slots);

        RunUndoable(GenerateUndo, () =>
        {
            Undo.RecordObject(generator, GenerateUndo);
            generator.CaptureReferencePoseIfNeeded();

            DestroyOwnedContainers(generator);
            Transform container = CreateContainer(generator);
            if (!CreateCopies(generator, container, slots))
            {
                Undo.DestroyObjectImmediate(container.gameObject);
                Debug.LogError("Stopped before moving the reference nut. No extra nuts were kept.", generator);
                return;
            }

            PlaceReference(generator, slots[slots.Length - 1]);
            EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);

            NutSlot first = slots[0];
            NutSlot last = slots[slots.Length - 1];
            Debug.Log(
                $"Placed {slots.Length} nuts on {generator.name}. Centres run from local Z {first.RodLocalZ:0.000} m to {last.RodLocalZ:0.000} m. The reference nut is the outer one.",
                generator);
        });
    }

    static void Clear(RodNutTestGenerator generator)
    {
        if (!CanEditInstance(generator))
            return;

        RunUndoable(ClearUndo, () =>
        {
            bool restored = false;
            if (generator.HasCapturedReferencePose && generator.NutAnchor != null && generator.ReferenceNut != null)
            {
                Undo.RecordObject(generator.NutAnchor, ClearUndo);
                Undo.RecordObject(generator.ReferenceNut, ClearUndo);
                Undo.RecordObject(generator, ClearUndo);
                restored = generator.TryRestoreReferencePose();
                RecordInstanceOverrides(generator.NutAnchor);
                RecordInstanceOverrides(generator.ReferenceNut);
            }

            int removed = DestroyOwnedContainers(generator);
            EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);

            if (!restored && removed == 0)
                Debug.Log("No generated test nuts to clear.", generator);
            else
                Debug.Log("Cleared the generated test nuts and restored the reference nut.", generator);
        });
    }

    static bool CanEditInstance(RodNutTestGenerator generator)
    {
        if (Application.isPlaying)
        {
            Debug.LogError("Use the nut test in Edit Mode, before entering Play.", generator);
            return false;
        }

        if (PrefabUtility.IsPartOfPrefabAsset(generator.gameObject)
            || PrefabStageUtility.GetPrefabStage(generator.gameObject) != null)
        {
            Debug.LogError("Attach this to the Rod_C1_R2 scene instance. Prefab mode would change the source prefab.", generator);
            return false;
        }

        return true;
    }

    static bool TryValidate(RodNutTestGenerator generator, out float rodLength)
    {
        rodLength = 0f;

        if (generator.Settings == null || generator.RodTip == null || generator.NutAnchor == null || generator.ReferenceNut == null)
        {
            Debug.LogError("Assign Settings, Rod Tip, Nut Anchor, and Reference Nut before generating.", generator);
            return false;
        }

        if (!generator.RodTip.IsChildOf(generator.transform))
        {
            Debug.LogError("Rod Tip must be a child of this rod.", generator.RodTip);
            return false;
        }

        if (generator.NutAnchor.parent != generator.transform)
        {
            Debug.LogError("Nut Anchor must be a direct child of this rod.", generator.NutAnchor);
            return false;
        }

        if (generator.ReferenceNut.parent != generator.NutAnchor)
        {
            Debug.LogError("Reference Nut must be the Nut parented to Nut Anchor.", generator.ReferenceNut);
            return false;
        }

        if (!LossyScaleIsOne(generator.transform))
        {
            Debug.LogWarning(
                "This rod's lossy scale is not 1, so the nut spacing will not stay in metres.",
                generator);
        }

        rodLength = generator.transform.InverseTransformPoint(generator.RodTip.position).z;
        return true;
    }

    static void WarnIfEndClearanceDiffers(RodNutTestGenerator generator, float rodLength, NutSlot[] slots)
    {
        float halfThickness = generator.Settings.NutThickness * 0.5f;
        float trailingClearance = rodLength - (slots[slots.Length - 1].RodLocalZ + halfThickness);
        float expected = generator.Settings.EndClearance;

        if (Mathf.Abs(trailingClearance - expected) > 0.001f)
        {
            Debug.LogWarning(
                $"Clearance at Rod_Tip is {trailingClearance:0.000} m. The settings expect {expected:0.000} m.",
                generator.RodTip);
        }
    }

    static bool CreateCopies(RodNutTestGenerator generator, Transform container, NutSlot[] slots)
    {
        RodNutReferencePose pose = generator.CapturedReferencePose;

        for (int i = 0; i < slots.Length - 1; i++)
        {
            var copy = Object.Instantiate(generator.ReferenceNut.gameObject, container, false);
            Undo.RegisterCreatedObjectUndo(copy, GenerateUndo);

            if (copy.transform.childCount != generator.ReferenceNut.childCount)
            {
                Undo.DestroyObjectImmediate(copy);
                Debug.LogError("The nut copy included extra children, so generation stopped.", generator);
                return false;
            }

            Undo.RecordObject(copy, GenerateUndo);
            Undo.RecordObject(copy.transform, GenerateUndo);
            copy.name = $"Nut_{slots[i].Index + 1:00}";
            pose.PlaceCopy(copy.transform, slots[i].RodLocalZ, slots[i].SpinDegrees);
        }

        return true;
    }

    static void PlaceReference(RodNutTestGenerator generator, NutSlot slot)
    {
        Undo.RecordObject(generator.NutAnchor, GenerateUndo);
        Undo.RecordObject(generator.ReferenceNut, GenerateUndo);
        generator.CapturedReferencePose.PlaceReference(
            generator.NutAnchor,
            generator.ReferenceNut,
            slot.RodLocalZ,
            slot.SpinDegrees);
        RecordInstanceOverrides(generator.NutAnchor);
        RecordInstanceOverrides(generator.ReferenceNut);
    }

    static Transform CreateContainer(RodNutTestGenerator generator)
    {
        var containerObject = new GameObject(RodNutTestGenerator.ContainerName);
        Undo.RegisterCreatedObjectUndo(containerObject, GenerateUndo);
        Undo.SetTransformParent(containerObject.transform, generator.transform, GenerateUndo);
        Undo.RecordObject(containerObject.transform, GenerateUndo);
        containerObject.transform.localPosition = Vector3.zero;
        containerObject.transform.localRotation = Quaternion.identity;
        containerObject.transform.localScale = Vector3.one;
        return containerObject.transform;
    }

    static int DestroyOwnedContainers(RodNutTestGenerator generator)
    {
        int removed = 0;

        for (int i = generator.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = generator.transform.GetChild(i);
            if (child.name != RodNutTestGenerator.ContainerName)
                continue;

            Undo.DestroyObjectImmediate(child.gameObject);
            removed++;
        }

        return removed;
    }

    static void RecordInstanceOverrides(Object instanceObject)
    {
        if (PrefabUtility.IsPartOfPrefabInstance(instanceObject))
            PrefabUtility.RecordPrefabInstancePropertyModifications(instanceObject);
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
