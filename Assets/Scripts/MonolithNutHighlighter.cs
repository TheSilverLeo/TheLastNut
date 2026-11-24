using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class MonolithNutHighlighter : MonoBehaviour
{
    const int AdditionalCapacity = 128;

    [Tooltip("Four-face band. Highlights are drawn after its nuts, using the same placement.")]
    [SerializeField] MonolithFourFaceBandPreviewRenderer band;

    [Tooltip("Highlight colour, and how far the tint sits off the metal.")]
    [SerializeField] MonolithNutTargetSettings settings;

    [Tooltip("Main Camera. Highlights draw only for this camera.")]
    [SerializeField] Camera eye;

    readonly MonolithNutId[] _additional = new MonolithNutId[AdditionalCapacity];
    Material _material;
    Mesh _mesh;
    bool _hasPrimary;
    bool _warned;
    int _additionalCount;
    MonolithNutId _primary;
    MonolithNutTargetType _primaryType;
    MeshRenderer _detachedVisual;

    public bool HasPrimary => _hasPrimary;
    public MonolithNutId Primary => _primary;
    public int AdditionalCount => _additionalCount;

    public MonolithNutId AdditionalAt(int index)
    {
        return _additional[index];
    }

    public void SetHighlights(
        bool hasPrimary,
        MonolithNutId primary,
        IReadOnlyList<MonolithNutId> additional,
        MonolithNutTargetType primaryType = MonolithNutTargetType.Attached,
        MeshRenderer detachedVisual = null)
    {
        _hasPrimary = hasPrimary;
        _primary = primary;
        _primaryType = hasPrimary ? primaryType : MonolithNutTargetType.None;
        _detachedVisual = primaryType == MonolithNutTargetType.Detached ? detachedVisual : null;
        _additionalCount = 0;
        if (additional == null)
            return;

        for (int i = 0; i < additional.Count && _additionalCount < _additional.Length; i++)
        {
            MonolithNutId nut = additional[i];
            if (hasPrimary && nut.Equals(primary))
                continue;

            _additional[_additionalCount++] = nut;
        }
    }

    public void ClearHighlights()
    {
        _hasPrimary = false;
        _additionalCount = 0;
        _primaryType = MonolithNutTargetType.None;
        _detachedVisual = null;
    }

    void OnEnable()
    {
        if (band != null)
        {
            band.BandRendered -= DrawHighlights;
            band.BandRendered += DrawHighlights;
        }
    }

    void OnDisable()
    {
        if (band != null)
            band.BandRendered -= DrawHighlights;

        ClearHighlights();
        ReleaseMaterial();
    }

    void DrawHighlights(Camera camera)
    {
        if (!Application.isPlaying || camera != eye || settings == null || band == null)
            return;

        if (!_hasPrimary && _additionalCount == 0)
            return;

        if (!EnsureMaterial())
            return;

        bool needsBandMesh = _additionalCount > 0 || (_hasPrimary && _primaryType != MonolithNutTargetType.Detached);
        if (needsBandMesh && !EnsureMesh())
            return;

        Color extra = settings.AdditionalHighlight;
        extra.a = settings.AdditionalAlpha;
        for (int i = 0; i < _additionalCount; i++)
            DrawAttached(_additional[i], extra, camera);

        if (!_hasPrimary)
            return;

        Color primary = settings.PrimaryHighlight;
        primary.a = settings.PrimaryAlpha;
        if (_primaryType == MonolithNutTargetType.Detached)
            DrawDetached(_detachedVisual, primary, camera);
        else
            DrawAttached(_primary, primary, camera);
    }

    void DrawAttached(MonolithNutId nut, Color color, Camera camera)
    {
        if (!band.TryGetNutWorldMatrix(nut, out Matrix4x4 world))
            return;

        Submit(camera, color, world, _mesh);
    }

    void DrawDetached(MeshRenderer visual, Color color, Camera camera)
    {
        if (visual == null || !visual.gameObject.activeInHierarchy)
        {
            _detachedVisual = null;
            return;
        }

        MonolithDetachedNutIdentity identity = visual.GetComponentInParent<MonolithDetachedNutIdentity>();
        if (identity == null || !identity.HasIdentity)
        {
            _detachedVisual = null;
            return;
        }

        MeshFilter filter = visual.GetComponent<MeshFilter>();
        Mesh mesh = filter != null ? filter.sharedMesh : null;
        if (mesh == null)
            return;

        Submit(camera, color, visual.transform.localToWorldMatrix, mesh);
    }

    void Submit(Camera camera, Color color, Matrix4x4 world, Mesh mesh)
    {
        _material.SetColor("_BaseColor", color);
        _material.SetFloat("_SurfaceOffset", settings.SurfaceOffset);
        var renderParams = new RenderParams(_material)
        {
            camera = camera,
            worldBounds = MonolithBandInstances.TransformBounds(world, mesh.bounds),
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = false,
            layer = gameObject.layer
        };

        int submeshes = mesh.subMeshCount;
        for (int submesh = 0; submesh < submeshes; submesh++)
            Graphics.RenderMesh(renderParams, mesh, submesh, world);
    }

    bool EnsureMaterial()
    {
        if (_material != null)
            return true;

        Shader shader = Shader.Find("The Last Rivet/Nut Highlight");
        if (shader == null)
        {
            WarnOnce("Nut highlight shader is missing.");
            return false;
        }

        _material = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave,
            name = "Nut Highlight",
            renderQueue = 3100
        };
        return true;
    }

    bool EnsureMesh()
    {
        if (_mesh != null)
            return true;

        if (!MonolithBandInstances.TryGetNutMesh(band.RodPrefab, out _mesh))
        {
            WarnOnce("Nut highlight could not read the nut mesh from Rod_Template.");
            return false;
        }

        return true;
    }

    void ReleaseMaterial()
    {
        if (_material == null)
            return;

        if (Application.isPlaying)
            Destroy(_material);
        else
            DestroyImmediate(_material);

        _material = null;
    }

    void WarnOnce(string message)
    {
        if (_warned)
            return;

        _warned = true;
        Debug.LogWarning(message, this);
    }
}
