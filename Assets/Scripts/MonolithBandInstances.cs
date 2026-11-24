using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class MonolithBandInstanceSet
{
    public MonolithBandDrawGroup[] Groups = System.Array.Empty<MonolithBandDrawGroup>();
    public int RodCount;
    public int NutCount;

    public int RenderInstanceCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < Groups.Length; i++)
                count += Groups[i].LocalToContainer.Length;

            return count;
        }
    }
}

public sealed class MonolithBandDrawGroup
{
    public Mesh Mesh;
    public int SubmeshIndex;
    public Material SourceMaterial;
    public int Layer;
    public uint RenderingLayerMask;
    public LightProbeUsage LightProbeUsage;
    public ReflectionProbeUsage ReflectionProbeUsage;
    public MotionVectorGenerationMode MotionVectors;
    public Matrix4x4[] LocalToContainer = System.Array.Empty<Matrix4x4>();
    public Bounds LocalBounds;
}

public static class MonolithBandInstances
{
    public static bool TryBuild(
        Transform container,
        Transform faceAnchor,
        GameObject rodPrefab,
        MonolithBandPreviewSettings bandSettings,
        RodNutTestSettings nutSettings,
        bool reverseColumnDirection,
        out MonolithBandInstanceSet instances,
        out string error)
    {
        return TryBuild(
            container,
            faceAnchor,
            rodPrefab,
            bandSettings,
            nutSettings,
            reverseColumnDirection,
            -1,
            out instances,
            out error);
    }

    public static bool TryBuild(
        Transform container,
        Transform faceAnchor,
        GameObject rodPrefab,
        MonolithBandPreviewSettings bandSettings,
        RodNutTestSettings nutSettings,
        bool reverseColumnDirection,
        int faceIndex,
        out MonolithBandInstanceSet instances,
        out string error)
    {
        instances = null;
        error = null;

        if (container == null || faceAnchor == null || rodPrefab == null || bandSettings == null || nutSettings == null)
        {
            error = "Assign Band Settings, Nut Settings, Rod Prefab, and Face Anchor before showing the band preview.";
            return false;
        }

        if (bandSettings.ColumnCount < 1 || bandSettings.RowCount < 1)
        {
            error = "Band column count and row count must be at least 1.";
            return false;
        }

        Transform rodRoot = rodPrefab.transform;
        if (!TryResolveParts(rodRoot, out Transform rodTip, out Transform nutAnchor, out Transform referenceNut))
        {
            error = "Rod_Template must contain Rod_Tip, Nut_Anchor, and Nut.";
            return false;
        }

        float rodLength = LocalToAncestor(rodRoot, rodTip).GetPosition().z;
        if (!RodNutLayout.TryBuild(nutSettings, rodLength, out _, out error))
            return false;

        if (!TryCollectSources(rodRoot, referenceNut, out List<MeshSource> rodSources, out List<MeshSource> nutSources, out error))
            return false;

        RodGridPoint[] points = RodTestGridLayout.Build(
            bandSettings.ColumnCount,
            bandSettings.RowCount,
            bandSettings.HorizontalSpacing,
            bandSettings.VerticalSpacing,
            reverseColumnDirection);

        var groups = new List<GroupBuild>();
        int nutCount = 0;

        for (int pointIndex = 0; pointIndex < points.Length; pointIndex++)
        {
            RodGridPoint point = points[pointIndex];
            Matrix4x4 rodOnContainer = RodOnContainer(container, faceAnchor, rodRoot, point);

            for (int sourceIndex = 0; sourceIndex < rodSources.Count; sourceIndex++)
            {
                MeshSource source = rodSources[sourceIndex];
                AddMatrices(groups, source, rodOnContainer * source.LocalToPlacementRoot);
            }

            int rotationSeed = faceIndex >= 0
                ? SeedForRodOnFace(nutSettings.RotationSeed, faceIndex, point.Column, point.Row)
                : SeedForRod(nutSettings.RotationSeed, point.Column, point.Row);
            if (!RodNutLayout.TryBuild(nutSettings, rodLength, rotationSeed, out NutSlot[] slots, out error))
                return false;

            RodNutReferencePose pose = RodNutReferencePose.Capture(nutAnchor, referenceNut);
            for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
            {
                bool isReference = slotIndex == slots.Length - 1;
                Matrix4x4 nutOnRod = isReference
                    ? pose.ReferenceLocalMatrix(slots[slotIndex].RodLocalZ, slots[slotIndex].SpinDegrees)
                    : pose.CopyLocalMatrix(slots[slotIndex].RodLocalZ, slots[slotIndex].SpinDegrees);

                for (int sourceIndex = 0; sourceIndex < nutSources.Count; sourceIndex++)
                {
                    MeshSource source = nutSources[sourceIndex];
                    AddMatrices(groups, source, rodOnContainer * nutOnRod * source.LocalToPlacementRoot);
                }
            }

            nutCount += slots.Length;
        }

        instances = new MonolithBandInstanceSet
        {
            Groups = Freeze(groups),
            RodCount = points.Length,
            NutCount = nutCount
        };
        return true;
    }

    public static Bounds TransformBounds(Matrix4x4 matrix, Bounds bounds)
    {
        Vector3 center = matrix.MultiplyPoint3x4(bounds.center);
        Vector3 extents = bounds.extents;
        Vector3 right = matrix.MultiplyVector(new Vector3(extents.x, 0f, 0f));
        Vector3 up = matrix.MultiplyVector(new Vector3(0f, extents.y, 0f));
        Vector3 forward = matrix.MultiplyVector(new Vector3(0f, 0f, extents.z));
        Vector3 worldExtents = new Vector3(
            Mathf.Abs(right.x) + Mathf.Abs(up.x) + Mathf.Abs(forward.x),
            Mathf.Abs(right.y) + Mathf.Abs(up.y) + Mathf.Abs(forward.y),
            Mathf.Abs(right.z) + Mathf.Abs(up.z) + Mathf.Abs(forward.z));
        return new Bounds(center, worldExtents * 2f);
    }

    static Matrix4x4 RodOnContainer(Transform container, Transform faceAnchor, Transform rodRoot, RodGridPoint point)
    {
        Vector3 worldPosition = faceAnchor.TransformPoint(point.AnchorLocalPosition);
        Quaternion worldRotation = faceAnchor.rotation;
        Vector3 localPosition = container.InverseTransformPoint(worldPosition);
        Quaternion localRotation = Quaternion.Inverse(container.rotation) * worldRotation;
        return Matrix4x4.TRS(localPosition, localRotation, rodRoot.localScale);
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

    static int SeedForRodOnFace(int baseSeed, int faceIndex, int column, int row)
    {
        unchecked
        {
            int seed = baseSeed;
            seed = (seed * 397) ^ (faceIndex + 1);
            seed = (seed * 397) ^ (column + 1);
            seed = (seed * 397) ^ ((row + 1) * 17);
            return seed;
        }
    }

    static bool TryResolveParts(Transform rodRoot, out Transform rodTip, out Transform nutAnchor, out Transform referenceNut)
    {
        rodTip = null;
        nutAnchor = null;
        referenceNut = null;

        for (int i = 0; i < rodRoot.childCount; i++)
        {
            Transform child = rodRoot.GetChild(i);
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

        return rodTip != null && nutAnchor != null && referenceNut != null;
    }

    static bool TryCollectSources(
        Transform rodRoot,
        Transform referenceNut,
        out List<MeshSource> rodSources,
        out List<MeshSource> nutSources,
        out string error)
    {
        rodSources = new List<MeshSource>();
        nutSources = new List<MeshSource>();
        error = null;

        MeshRenderer[] renderers = rodRoot.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer renderer = renderers[i];
            Mesh mesh = renderer.GetComponent<MeshFilter>() != null
                ? renderer.GetComponent<MeshFilter>().sharedMesh
                : null;
            if (mesh == null)
                continue;

            bool isNut = renderer.transform == referenceNut || renderer.transform.IsChildOf(referenceNut);
            Transform placementRoot = isNut ? referenceNut : rodRoot;
            var source = new MeshSource
            {
                Renderer = renderer,
                Mesh = mesh,
                LocalToPlacementRoot = LocalToAncestor(placementRoot, renderer.transform)
            };

            if (isNut)
                nutSources.Add(source);
            else
                rodSources.Add(source);
        }

        if (rodSources.Count == 0)
        {
            error = "Rod_Template has no rod mesh to instance.";
            return false;
        }

        if (nutSources.Count == 0)
        {
            error = "The reference Nut has no mesh to instance.";
            return false;
        }

        return true;
    }

    static void AddMatrices(List<GroupBuild> groups, MeshSource source, Matrix4x4 localToContainer)
    {
        Material[] materials = source.Renderer.sharedMaterials;
        if (materials == null || materials.Length == 0)
            return;

        int submeshCount = source.Mesh.subMeshCount;
        for (int submesh = 0; submesh < submeshCount; submesh++)
        {
            Material material = materials[Mathf.Min(submesh, materials.Length - 1)];
            if (material == null)
                continue;

            GroupBuild group = FindOrAdd(groups, source, submesh, material);
            group.Matrices.Add(localToContainer);
            Bounds transformed = TransformBounds(localToContainer, source.Mesh.bounds);
            if (!group.HasBounds)
            {
                group.LocalBounds = transformed;
                group.HasBounds = true;
            }
            else
            {
                group.LocalBounds.Encapsulate(transformed);
            }
        }
    }

    static GroupBuild FindOrAdd(List<GroupBuild> groups, MeshSource source, int submesh, Material material)
    {
        for (int i = 0; i < groups.Count; i++)
        {
            GroupBuild group = groups[i];
            if (group.Mesh == source.Mesh && group.SubmeshIndex == submesh && group.SourceMaterial == material)
                return group;
        }

        var created = new GroupBuild
        {
            Mesh = source.Mesh,
            SubmeshIndex = submesh,
            SourceMaterial = material,
            Layer = source.Renderer.gameObject.layer,
            RenderingLayerMask = source.Renderer.renderingLayerMask,
            LightProbeUsage = source.Renderer.lightProbeUsage,
            ReflectionProbeUsage = source.Renderer.reflectionProbeUsage,
            MotionVectors = source.Renderer.motionVectorGenerationMode,
            Matrices = new List<Matrix4x4>()
        };
        groups.Add(created);
        return created;
    }

    static MonolithBandDrawGroup[] Freeze(List<GroupBuild> groups)
    {
        var frozen = new MonolithBandDrawGroup[groups.Count];
        for (int i = 0; i < groups.Count; i++)
        {
            GroupBuild group = groups[i];
            frozen[i] = new MonolithBandDrawGroup
            {
                Mesh = group.Mesh,
                SubmeshIndex = group.SubmeshIndex,
                SourceMaterial = group.SourceMaterial,
                Layer = group.Layer,
                RenderingLayerMask = group.RenderingLayerMask,
                LightProbeUsage = group.LightProbeUsage,
                ReflectionProbeUsage = group.ReflectionProbeUsage,
                MotionVectors = group.MotionVectors,
                LocalToContainer = group.Matrices.ToArray(),
                LocalBounds = group.LocalBounds
            };
        }

        return frozen;
    }

    static Matrix4x4 LocalToAncestor(Transform ancestor, Transform child)
    {
        var chain = new Transform[16];
        int count = 0;
        for (Transform current = child; current != null && current != ancestor; current = current.parent)
        {
            if (count >= chain.Length)
                break;

            chain[count++] = current;
        }

        Matrix4x4 local = Matrix4x4.identity;
        for (int i = count - 1; i >= 0; i--)
        {
            Transform node = chain[i];
            local = local * Matrix4x4.TRS(node.localPosition, node.localRotation, node.localScale);
        }

        return local;
    }

    struct MeshSource
    {
        public MeshRenderer Renderer;
        public Mesh Mesh;
        public Matrix4x4 LocalToPlacementRoot;
    }

    sealed class GroupBuild
    {
        public Mesh Mesh;
        public int SubmeshIndex;
        public Material SourceMaterial;
        public int Layer;
        public uint RenderingLayerMask;
        public LightProbeUsage LightProbeUsage;
        public ReflectionProbeUsage ReflectionProbeUsage;
        public MotionVectorGenerationMode MotionVectors;
        public List<Matrix4x4> Matrices;
        public Bounds LocalBounds;
        public bool HasBounds;
    }
}
