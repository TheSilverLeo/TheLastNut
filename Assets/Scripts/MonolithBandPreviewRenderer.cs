using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[DisallowMultipleComponent]
public class MonolithBandPreviewRenderer : MonoBehaviour
{
    public const int MaxInstancesPerDraw = 1023;

    [SerializeField] bool showPreview;

    [Tooltip("Column count, row count, and spacing for this band.")]
    [SerializeField] MonolithBandPreviewSettings bandSettings;

    [Tooltip("Nut count, spacing, and 0–360° spin. Each rod gets its own seed from this asset.")]
    [SerializeField] RodNutTestSettings nutSettings;

    [Tooltip("Rod_Template prefab asset. Meshes and materials are read from it.")]
    [SerializeField] GameObject rodPrefab;

    [Tooltip("First column and middle row. Rods point along this transform's local +Z.")]
    [SerializeField] Transform faceAnchor;

    [Tooltip("Off: later columns step along the anchor's local +X. On: they step along local -X. The first column stays on the anchor.")]
    [SerializeField] bool reverseColumnDirection;

    [SerializeField] ShadowCastingMode shadowCastingMode = ShadowCastingMode.On;

    [SerializeField] bool receiveShadows = true;

    [SerializeField, Range(1, MaxInstancesPerDraw)] int maxInstancesPerDraw = MaxInstancesPerDraw;

    readonly Dictionary<Material, Material> _materialCopies = new Dictionary<Material, Material>();
    MonolithBandInstanceSet _instances;
    Matrix4x4[] _worldBatch = System.Array.Empty<Matrix4x4>();
    BuildSignature _builtSignature;
    BuildSignature _failureSignature;
    bool _hasBuilt;
    bool _hasFailure;

    public MonolithBandPreviewSettings BandSettings => bandSettings;
    public RodNutTestSettings NutSettings => nutSettings;
    public bool ShowPreview => showPreview;

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
#if UNITY_EDITOR
        UnityEditor.SceneView.RepaintAll();
#endif
    }

    void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (!showPreview || !isActiveAndEnabled || !ShouldRender(camera))
            return;

        BuildSignature signature = BuildSignature.Capture(this);
        if (!_hasBuilt || !_builtSignature.Equals(signature))
        {
            if (!TryRebuild(signature))
                return;
        }

        if (_instances == null)
            return;

        Draw(camera);
    }

    bool TryRebuild(BuildSignature signature)
    {
        ReleaseMaterials();
        bool announce = _instances == null;
        _instances = null;
        _builtSignature = signature;
        _hasBuilt = true;

        if (!MonolithBandInstances.TryBuild(
                transform,
                faceAnchor,
                rodPrefab,
                bandSettings,
                nutSettings,
                reverseColumnDirection,
                out MonolithBandInstanceSet built,
                out string error))
        {
            if (!_hasFailure || !_failureSignature.Equals(signature))
            {
                _hasFailure = true;
                _failureSignature = signature;
                Debug.LogError(error, this);
            }

            return false;
        }

        _hasFailure = false;
        _instances = built;
        if (announce)
            Debug.Log($"Band preview: {built.RodCount} rods and {built.NutCount} nuts.", this);

        return true;
    }

    void Draw(Camera camera)
    {
        int batchSize = Mathf.Clamp(maxInstancesPerDraw, 1, MaxInstancesPerDraw);
        if (_worldBatch.Length != batchSize)
            _worldBatch = new Matrix4x4[batchSize];

        Matrix4x4 containerWorld = transform.localToWorldMatrix;
        MonolithBandDrawGroup[] groups = _instances.Groups;
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

    Material InstancedMaterial(Material source)
    {
        if (_materialCopies.TryGetValue(source, out Material copy) && copy != null)
            return copy;

        copy = new Material(source)
        {
            hideFlags = HideFlags.HideAndDontSave,
            name = source.name + " (Band Preview)",
            enableInstancing = true
        };
        _materialCopies[source] = copy;
        return copy;
    }

    void ReleaseInstances()
    {
        ReleaseMaterials();
        _instances = null;
        _hasBuilt = false;
        _hasFailure = false;
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
        readonly Vector3 _anchorLocalPosition;
        readonly Quaternion _anchorLocalRotation;
        readonly Vector3 _containerLossyScale;

        BuildSignature(int hash, Vector3 anchorLocalPosition, Quaternion anchorLocalRotation, Vector3 containerLossyScale)
        {
            _hash = hash;
            _anchorLocalPosition = anchorLocalPosition;
            _anchorLocalRotation = anchorLocalRotation;
            _containerLossyScale = containerLossyScale;
        }

        public static BuildSignature Capture(MonolithBandPreviewRenderer preview)
        {
            Transform container = preview.transform;
            Transform anchor = preview.faceAnchor;
            Vector3 anchorLocalPosition = anchor != null
                ? container.InverseTransformPoint(anchor.position)
                : Vector3.zero;
            Quaternion anchorLocalRotation = anchor != null
                ? Quaternion.Inverse(container.rotation) * anchor.rotation
                : Quaternion.identity;

            unchecked
            {
                int hash = 17;
                hash = (hash * 397) ^ (preview.reverseColumnDirection ? 1 : 0);
                hash = (hash * 397) ^ Id(preview.bandSettings);
                hash = (hash * 397) ^ Id(preview.nutSettings);
                hash = (hash * 397) ^ Id(preview.rodPrefab);
                hash = (hash * 397) ^ Id(anchor);
                hash = Mix(hash, preview.bandSettings);
                hash = Mix(hash, preview.nutSettings);
                return new BuildSignature(hash, anchorLocalPosition, anchorLocalRotation, container.lossyScale);
            }
        }

        public bool Equals(BuildSignature other)
        {
            return _hash == other._hash
                && _anchorLocalPosition == other._anchorLocalPosition
                && _anchorLocalRotation == other._anchorLocalRotation
                && _containerLossyScale == other._containerLossyScale;
        }

        public override bool Equals(object obj)
        {
            return obj is BuildSignature other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _hash;
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
