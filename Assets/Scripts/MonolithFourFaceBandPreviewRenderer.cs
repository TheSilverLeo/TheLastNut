using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[DisallowMultipleComponent]
public class MonolithFourFaceBandPreviewRenderer : MonoBehaviour
{
    public const int FaceCount = 4;
    public const int MaxInstancesPerDraw = MonolithBandPreviewRenderer.MaxInstancesPerDraw;

    [SerializeField] bool showPreview;

    [Tooltip("Column count, row count, and spacing shared by all four faces.")]
    [SerializeField] MonolithBandPreviewSettings bandSettings;

    [Tooltip("Nut count, spacing, and 0–360° spin. Each rod gets its own seed from the face, column, and row.")]
    [SerializeField] RodNutTestSettings nutSettings;

    [Tooltip("Rod_Template prefab asset. Meshes and materials are read from it.")]
    [SerializeField] GameObject rodPrefab;

    [Tooltip("The one-face preview on this controller. While that preview is showing, this band does not draw.")]
    [SerializeField] MonolithBandPreviewRenderer oneFacePreview;

    [SerializeField] MonolithFaceAnchor[] faces = new MonolithFaceAnchor[FaceCount];

    [SerializeField] ShadowCastingMode shadowCastingMode = ShadowCastingMode.On;

    [SerializeField] bool receiveShadows = true;

    [SerializeField, Range(1, MaxInstancesPerDraw)] int maxInstancesPerDraw = MaxInstancesPerDraw;

    readonly Dictionary<Material, Material> _materialCopies = new Dictionary<Material, Material>();

    MonolithBandInstanceSet[] _faces;
    Matrix4x4[] _worldBatch = System.Array.Empty<Matrix4x4>();
    BuildSignature _builtSignature;
    BuildSignature _failureSignature;
    bool _hasBuilt;
    bool _hasFailure;
    bool _loggedOneFaceOverlap;

    public MonolithBandPreviewSettings BandSettings => bandSettings;
    public RodNutTestSettings NutSettings => nutSettings;
    public bool ShowPreview => showPreview;
    public MonolithBandPreviewRenderer OneFacePreview => oneFacePreview;

    public int BuiltRodCount
    {
        get
        {
            CountBuilt(out int rods, out _, out _);
            return rods;
        }
    }

    public int BuiltNutCount
    {
        get
        {
            CountBuilt(out _, out int nuts, out _);
            return nuts;
        }
    }

    public int BuiltRenderInstanceCount
    {
        get
        {
            CountBuilt(out _, out _, out int renderInstances);
            return renderInstances;
        }
    }

    void Reset()
    {
        EnsureFaces();
        if (oneFacePreview == null)
            oneFacePreview = GetComponent<MonolithBandPreviewRenderer>();
    }

    void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
#if UNITY_EDITOR
        if (!Application.isPlaying && showPreview)
            UnityEditor.SceneView.RepaintAll();
#endif
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        ReleaseInstances();
    }

    void OnValidate()
    {
        EnsureFaces();
#if UNITY_EDITOR
        UnityEditor.SceneView.RepaintAll();
#endif
    }

    void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (!showPreview || !isActiveAndEnabled || !ShouldRender(camera))
            return;

        if (OneFaceIsShowing())
        {
            if (!_loggedOneFaceOverlap)
            {
                _loggedOneFaceOverlap = true;
                Debug.LogWarning(
                    "Four-face band is hidden because the one-face preview is showing. Hide the one-face preview, then show the four-face band.",
                    this);
            }

            return;
        }

        _loggedOneFaceOverlap = false;
        BuildSignature signature = BuildSignature.Capture(this);
        if (!_hasBuilt || !_builtSignature.Equals(signature))
        {
            if (!TryRebuild(signature))
                return;
        }

        if (_faces == null)
            return;

        Draw(camera);
    }

    bool OneFaceIsShowing()
    {
        MonolithBandPreviewRenderer preview = oneFacePreview != null
            ? oneFacePreview
            : GetComponent<MonolithBandPreviewRenderer>();

        return preview != null && preview.isActiveAndEnabled && preview.ShowPreview;
    }

    bool TryRebuild(BuildSignature signature)
    {
        ReleaseMaterials();
        bool announce = _faces == null;
        _faces = null;
        _builtSignature = signature;
        _hasBuilt = true;
        EnsureFaces();

        var built = new MonolithBandInstanceSet[FaceCount];
        for (int faceIndex = 0; faceIndex < FaceCount; faceIndex++)
        {
            MonolithFaceAnchor face = faces[faceIndex];
            if (face.Anchor == null)
            {
                RememberFailure(signature, $"Assign all four face anchors. Face {faceIndex + 1} is missing.");
                return false;
            }

            for (int earlier = 0; earlier < faceIndex; earlier++)
            {
                if (faces[earlier].Anchor != face.Anchor)
                    continue;

                RememberFailure(
                    signature,
                    $"Face {faceIndex + 1} uses the same anchor as Face {earlier + 1}. Each face needs its own anchor.");
                return false;
            }

            if (!MonolithBandInstances.TryBuild(
                    transform,
                    face.Anchor,
                    rodPrefab,
                    bandSettings,
                    nutSettings,
                    face.ReverseColumnDirection,
                    faceIndex,
                    out built[faceIndex],
                    out string error))
            {
                RememberFailure(signature, $"Face {faceIndex + 1}: {error}");
                return false;
            }
        }

        _hasFailure = false;
        _faces = built;
        if (announce)
        {
            CountBuilt(out int rods, out int nuts, out int renderInstances);
            Debug.Log($"Four-face band: {rods} rods, {nuts} nuts, {renderInstances} render instances.", this);
        }

        return true;
    }

    void RememberFailure(BuildSignature signature, string error)
    {
        if (_hasFailure && _failureSignature.Equals(signature))
            return;

        _hasFailure = true;
        _failureSignature = signature;
        Debug.LogError(error, this);
    }

    void Draw(Camera camera)
    {
        int batchSize = Mathf.Clamp(maxInstancesPerDraw, 1, MaxInstancesPerDraw);
        if (_worldBatch.Length != batchSize)
            _worldBatch = new Matrix4x4[batchSize];

        Matrix4x4 containerWorld = transform.localToWorldMatrix;
        for (int faceIndex = 0; faceIndex < _faces.Length; faceIndex++)
        {
            MonolithBandInstanceSet face = _faces[faceIndex];
            if (face == null)
                continue;

            MonolithBandDrawGroup[] groups = face.Groups;
            for (int groupIndex = 0; groupIndex < groups.Length; groupIndex++)
            {
                MonolithBandDrawGroup group = groups[groupIndex];
                if (group.Mesh == null || group.SourceMaterial == null || group.LocalToContainer.Length == 0)
                    continue;

                Material material = InstancedMaterial(group.SourceMaterial);
                Bounds worldBounds = MonolithBandInstances.TransformBounds(containerWorld, group.LocalBounds);
                Matrix4x4[] locals = group.LocalToContainer;

                for (int start = 0; start < locals.Length; start += batchSize)
                {
                    int count = Mathf.Min(batchSize, locals.Length - start);
                    for (int i = 0; i < count; i++)
                        _worldBatch[i] = containerWorld * locals[start + i];

                    var renderParams = new RenderParams(material)
                    {
                        camera = camera,
                        worldBounds = worldBounds,
                        layer = group.Layer,
                        renderingLayerMask = group.RenderingLayerMask,
                        shadowCastingMode = shadowCastingMode,
                        receiveShadows = receiveShadows,
                        lightProbeUsage = group.LightProbeUsage,
                        reflectionProbeUsage = group.ReflectionProbeUsage,
                        motionVectorMode = group.MotionVectors
                    };
                    Graphics.RenderMeshInstanced(renderParams, group.Mesh, group.SubmeshIndex, _worldBatch, count);
                }
            }
        }
    }

    Material InstancedMaterial(Material source)
    {
        if (_materialCopies.TryGetValue(source, out Material copy) && copy != null)
            return copy;

        copy = new Material(source)
        {
            hideFlags = HideFlags.HideAndDontSave,
            name = source.name + " (Four-Face Band)",
            enableInstancing = true
        };
        _materialCopies[source] = copy;
        return copy;
    }

    void ReleaseInstances()
    {
        ReleaseMaterials();
        _faces = null;
        _hasBuilt = false;
        _hasFailure = false;
        _loggedOneFaceOverlap = false;
        _worldBatch = System.Array.Empty<Matrix4x4>();
    }

    void ReleaseMaterials()
    {
        foreach (Material material in _materialCopies.Values)
        {
            if (material == null)
                continue;

            if (Application.isPlaying)
                Destroy(material);
            else
                DestroyImmediate(material);
        }

        _materialCopies.Clear();
    }

    void EnsureFaces()
    {
        if (faces != null && faces.Length == FaceCount)
            return;

        var resized = new MonolithFaceAnchor[FaceCount];
        if (faces != null)
        {
            int count = Mathf.Min(faces.Length, FaceCount);
            for (int i = 0; i < count; i++)
                resized[i] = faces[i];
        }

        faces = resized;
    }

    void CountBuilt(out int rods, out int nuts, out int renderInstances)
    {
        rods = 0;
        nuts = 0;
        renderInstances = 0;
        if (_faces == null)
            return;

        for (int i = 0; i < _faces.Length; i++)
        {
            MonolithBandInstanceSet face = _faces[i];
            if (face == null)
                continue;

            rods += face.RodCount;
            nuts += face.NutCount;
            renderInstances += face.RenderInstanceCount;
        }
    }

    static bool ShouldRender(Camera camera)
    {
        if (camera == null)
            return false;

        switch (camera.cameraType)
        {
            case CameraType.SceneView:
                // The Scene View camera component stays disabled and is rendered
                // explicitly. isActiveAndEnabled is false for it, so the Game
                // camera test would skip every Edit Mode draw.
                return true;
            case CameraType.Game:
            case CameraType.VR:
                return camera.isActiveAndEnabled;
            default:
                return false;
        }
    }

    readonly struct BuildSignature : System.IEquatable<BuildSignature>
    {
        readonly int _hash;
        readonly Vector3 _containerLossyScale;
        readonly Vector3 _anchor0;
        readonly Vector3 _anchor1;
        readonly Vector3 _anchor2;
        readonly Vector3 _anchor3;
        readonly Quaternion _rotation0;
        readonly Quaternion _rotation1;
        readonly Quaternion _rotation2;
        readonly Quaternion _rotation3;

        BuildSignature(
            int hash,
            Vector3 containerLossyScale,
            Vector3 anchor0,
            Vector3 anchor1,
            Vector3 anchor2,
            Vector3 anchor3,
            Quaternion rotation0,
            Quaternion rotation1,
            Quaternion rotation2,
            Quaternion rotation3)
        {
            _hash = hash;
            _containerLossyScale = containerLossyScale;
            _anchor0 = anchor0;
            _anchor1 = anchor1;
            _anchor2 = anchor2;
            _anchor3 = anchor3;
            _rotation0 = rotation0;
            _rotation1 = rotation1;
            _rotation2 = rotation2;
            _rotation3 = rotation3;
        }

        public static BuildSignature Capture(MonolithFourFaceBandPreviewRenderer preview)
        {
            Transform container = preview.transform;
            unchecked
            {
                int hash = 17;
                hash = (hash * 397) ^ Id(preview.bandSettings);
                hash = (hash * 397) ^ Id(preview.nutSettings);
                hash = (hash * 397) ^ Id(preview.rodPrefab);
                hash = Mix(hash, preview.bandSettings);
                hash = Mix(hash, preview.nutSettings);
                ReadAnchor(preview, container, 0, ref hash, out Vector3 anchor0, out Quaternion rotation0);
                ReadAnchor(preview, container, 1, ref hash, out Vector3 anchor1, out Quaternion rotation1);
                ReadAnchor(preview, container, 2, ref hash, out Vector3 anchor2, out Quaternion rotation2);
                ReadAnchor(preview, container, 3, ref hash, out Vector3 anchor3, out Quaternion rotation3);

                return new BuildSignature(
                    hash,
                    container.lossyScale,
                    anchor0,
                    anchor1,
                    anchor2,
                    anchor3,
                    rotation0,
                    rotation1,
                    rotation2,
                    rotation3);
            }
        }

        public bool Equals(BuildSignature other)
        {
            return _hash == other._hash
                && _containerLossyScale == other._containerLossyScale
                && _anchor0 == other._anchor0
                && _anchor1 == other._anchor1
                && _anchor2 == other._anchor2
                && _anchor3 == other._anchor3
                && _rotation0 == other._rotation0
                && _rotation1 == other._rotation1
                && _rotation2 == other._rotation2
                && _rotation3 == other._rotation3;
        }

        public override bool Equals(object obj)
        {
            return obj is BuildSignature other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _hash;
        }

        static void ReadAnchor(
            MonolithFourFaceBandPreviewRenderer preview,
            Transform container,
            int index,
            ref int hash,
            out Vector3 position,
            out Quaternion rotation)
        {
            Transform anchor = preview.faces != null && index < preview.faces.Length
                ? preview.faces[index].Anchor
                : null;
            bool reverse = preview.faces != null
                && index < preview.faces.Length
                && preview.faces[index].ReverseColumnDirection;

            unchecked
            {
                hash = (hash * 397) ^ (reverse ? 1 : 0);
                hash = (hash * 397) ^ Id(anchor);
            }

            if (anchor == null)
            {
                position = Vector3.zero;
                rotation = Quaternion.identity;
                return;
            }

            position = container.InverseTransformPoint(anchor.position);
            rotation = Quaternion.Inverse(container.rotation) * anchor.rotation;
        }

        static int Id(Object obj)
        {
            return obj != null ? obj.GetInstanceID() : 0;
        }

        static int Mix(int hash, MonolithBandPreviewSettings settings)
        {
            if (settings == null)
                return hash;

            unchecked
            {
                hash = (hash * 397) ^ settings.ColumnCount;
                hash = (hash * 397) ^ settings.RowCount;
                hash = (hash * 397) ^ settings.HorizontalSpacing.GetHashCode();
                hash = (hash * 397) ^ settings.VerticalSpacing.GetHashCode();
                return hash;
            }
        }

        static int Mix(int hash, RodNutTestSettings settings)
        {
            if (settings == null)
                return hash;

            unchecked
            {
                hash = (hash * 397) ^ settings.NutCount;
                hash = (hash * 397) ^ settings.RotationSeed;
                hash = (hash * 397) ^ settings.NutThickness.GetHashCode();
                hash = (hash * 397) ^ settings.Gap.GetHashCode();
                hash = (hash * 397) ^ settings.EndClearance.GetHashCode();
                hash = (hash * 397) ^ settings.MinimumSpinDegrees.GetHashCode();
                hash = (hash * 397) ^ settings.MaximumSpinDegrees.GetHashCode();
                return hash;
            }
        }
    }
}

[System.Serializable]
public struct MonolithFaceAnchor
{
    [Tooltip("Anchor under Rod_Placement_Guides. Column 0 and the middle row sit on this transform. Rods point along its local +Z.")]
    [SerializeField] Transform anchor;

    [Tooltip("Off: later columns step along this anchor's local +X. On: they step along local -X. Column 0 stays on the anchor.")]
    [SerializeField] bool reverseColumnDirection;

    public Transform Anchor => anchor;
    public bool ReverseColumnDirection => reverseColumnDirection;
}
