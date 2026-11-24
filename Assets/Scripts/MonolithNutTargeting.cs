using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(50)]
public class MonolithNutTargeting : MonoBehaviour
{
    [Tooltip("How far the crosshair can reach.")]
    [SerializeField] MonolithNutTargetSettings settings;

    [Tooltip("Main Camera. The ray starts at its centre.")]
    [SerializeField] Transform eye;

    [Tooltip("Rod proxies used to find which rod the crosshair hits.")]
    [SerializeField] MonolithRodColliderField rods;

    [Tooltip("Nut placement, removal state, and unscrewing animation.")]
    [SerializeField] MonolithFourFaceBandPreviewRenderer band;

    [Tooltip("Draws the amber tint. Additional nuts can be supplied later without changing this component.")]
    [SerializeField] MonolithNutHighlighter highlights;

    readonly float[] _centers = new float[64];
    Texture2D _pixel;
    Mesh _nutMesh;
    Camera _eyeCamera;
    MonolithRodId _cachedRod;
    bool _hasCenters;
    bool _hasTarget;
    bool _hasAimedRod;
    int _centerCount;
    MonolithNutId _target;
    MonolithRodId _aimedRod;
    MonolithNutTargetType _type;
    float _distance;
    Vector3 _worldPosition;
    int _remaining;

    public bool HasValidNutTarget => _hasTarget;
    public bool HasAimedRod => _hasAimedRod;
    public MonolithRodId AimedRod => _aimedRod;
    public MonolithNutId Target => _target;
    public MonolithNutTargetType TargetType => _hasTarget ? _type : MonolithNutTargetType.None;
    public float TargetDistance => _distance;
    public Vector3 SelectedNutWorldPosition => _worldPosition;
    public int RemainingNutsOnRod => _remaining;

    public void PresentHeldNut(MonolithNutId nut)
    {
        _hasTarget = true;
        _type = MonolithNutTargetType.Unscrewing;
        _target = nut;
        _remaining = band != null && band.NutState != null ? band.NutState.Remaining(nut.Rod) : 0;
        _worldPosition = Vector3.zero;
        _distance = 0f;
        if (band != null && band.TryGetNutWorldMatrix(nut, out Matrix4x4 world))
        {
            _worldPosition = world.MultiplyPoint3x4(Vector3.zero);
            if (_nutMesh == null)
                MonolithBandInstances.TryGetNutMesh(band.RodPrefab, out _nutMesh);

            if (_nutMesh != null)
                _worldPosition = world.MultiplyPoint3x4(_nutMesh.bounds.center);

            if (eye != null)
                _distance = Vector3.Distance(eye.position, _worldPosition);
        }
    }

    void Awake()
    {
        if (eye != null)
            _eyeCamera = eye.GetComponent<Camera>();
    }

    void OnDisable()
    {
        ClearTarget();
        if (_pixel != null)
        {
            Destroy(_pixel);
            _pixel = null;
        }
    }

    void Update()
    {
        if (!Application.isPlaying || settings == null || eye == null || _eyeCamera == null || rods == null || band == null)
        {
            ClearTarget();
            return;
        }

        _hasAimedRod = false;
        if (!TryHit(out RaycastHit hit))
        {
            ClearTarget();
            return;
        }

        MonolithDetachedNutIdentity detached = hit.collider.GetComponent<MonolithDetachedNutIdentity>();
        if (detached != null)
        {
            if (!TrySelectDetached(detached))
                ClearTarget();
            return;
        }

        MonolithRodCollisionProxy proxy = hit.collider.GetComponent<MonolithRodCollisionProxy>();
        if (proxy != null && proxy.Occupied)
        {
            _hasAimedRod = true;
            _aimedRod = proxy.Rod;
            if (!TrySelectNut(proxy, hit))
                ClearNut();
            return;
        }

        ClearTarget();
    }

    void OnGUI()
    {
        if (!Application.isPlaying || Event.current.type != EventType.Repaint)
            return;

        EnsurePixel();
        Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Color color = _hasTarget ? new Color(1f, 0.55f, 0.12f, 0.95f) : new Color(1f, 1f, 1f, 0.85f);
        float arm = _hasTarget ? 9f : 7f;
        float gap = 4f;
        float thickness = 2f;
        DrawBar(center.x - gap - arm, center.y - thickness * 0.5f, arm, thickness, color);
        DrawBar(center.x + gap, center.y - thickness * 0.5f, arm, thickness, color);
        DrawBar(center.x - thickness * 0.5f, center.y - gap - arm, thickness, arm, color);
        DrawBar(center.x - thickness * 0.5f, center.y + gap, thickness, arm, color);

        if (!_hasTarget)
            return;

        string label = "F" + _target.Face + " C" + _target.Column + " R" + _target.Row + " — NUT " + _target.NutIndex;
        GUI.color = color;
        GUI.Label(new Rect(center.x + 14f, center.y + 10f, 220f, 22f), label);
        GUI.color = Color.white;
    }

    bool TryHit(out RaycastHit closest)
    {
        closest = default;
        Ray ray = _eyeCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!Physics.Raycast(ray, out closest, settings.Range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return false;

        return closest.collider != null && !IsPlayer(closest.collider.transform);
    }

    bool TrySelectDetached(MonolithDetachedNutIdentity identity)
    {
        if (identity == null || !identity.HasIdentity || !identity.gameObject.activeInHierarchy)
            return false;

        MeshRenderer visual = identity.Visual;
        if (visual == null || !visual.gameObject.activeInHierarchy)
            return false;

        _hasTarget = true;
        _type = MonolithNutTargetType.Detached;
        _target = identity.Nut;
        _worldPosition = visual.bounds.center;
        _distance = Vector3.Distance(eye.position, _worldPosition);
        _remaining = band.NutState != null ? band.NutState.Remaining(identity.Nut.Rod) : 0;
        if (highlights != null)
            highlights.SetHighlights(true, identity.Nut, null, MonolithNutTargetType.Detached, visual);

        return true;
    }

    bool TrySelectNut(MonolithRodCollisionProxy proxy, RaycastHit hit)
    {
        MonolithRodId rod = proxy.Rod;
        if (!_hasCenters || !_cachedRod.Equals(rod))
        {
            _hasCenters = MonolithBandInstances.TryCopyNutCenters(
                band.RodPrefab,
                band.NutSettings,
                rod.Face,
                rod.Column,
                rod.Row,
                _centers,
                out _centerCount);
            _cachedRod = rod;
        }

        if (!_hasCenters || band.NutState == null || band.NutSettings == null)
            return false;

        float half = band.NutSettings.NutThickness * 0.5f;
        float limit = half + (band.NutSettings.Gap * 0.5f);
        float rodZ = proxy.transform.InverseTransformPoint(hit.point).z;
        int nutIndex = -1;
        float best = limit;
        bool found = false;
        for (int i = 0; i < _centerCount; i++)
        {
            var candidate = new MonolithNutId(rod.Face, rod.Column, rod.Row, i);
            bool visible = band.NutState.IsSlotPresent(rod, i) && !band.IsNutHidden(candidate);
            if (!visible)
                continue;

            float along = Mathf.Abs(rodZ - _centers[i]);
            if (along > limit)
                continue;

            if (found && along >= best)
                continue;

            found = true;
            best = along;
            nutIndex = i;
        }

        bool unscrewing = band.TryGetUnscrewingNut(rod, out int movingIndex, out float movingZ);
        if (unscrewing)
        {
            float along = Mathf.Abs(rodZ - movingZ);
            if (along <= limit && (!found || along < best))
                nutIndex = movingIndex;
        }

        if (nutIndex < 0)
            return false;

        var nut = new MonolithNutId(rod.Face, rod.Column, rod.Row, nutIndex);
        if (!band.TryGetNutWorldMatrix(nut, out Matrix4x4 world))
            return false;

        _hasTarget = true;
        _type = unscrewing && nutIndex == movingIndex
            ? MonolithNutTargetType.Unscrewing
            : MonolithNutTargetType.Attached;
        _target = nut;
        _worldPosition = world.MultiplyPoint3x4(Vector3.zero);
        if (_nutMesh == null)
            MonolithBandInstances.TryGetNutMesh(band.RodPrefab, out _nutMesh);

        if (_nutMesh != null)
            _worldPosition = world.MultiplyPoint3x4(_nutMesh.bounds.center);

        _distance = Vector3.Distance(eye.position, _worldPosition);
        _remaining = band.NutState.Remaining(rod);
        if (highlights != null)
            highlights.SetHighlights(true, nut, null, _type, null);

        return true;
    }

    void ClearTarget()
    {
        _hasAimedRod = false;
        ClearNut();
    }

    void ClearNut()
    {
        _hasTarget = false;
        _type = MonolithNutTargetType.None;
        _distance = 0f;
        _worldPosition = Vector3.zero;
        _remaining = 0;
        if (highlights != null)
            highlights.ClearHighlights();
    }

    bool IsPlayer(Transform hit)
    {
        return hit == transform || hit.IsChildOf(transform);
    }

    void EnsurePixel()
    {
        if (_pixel != null)
            return;

        _pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        _pixel.SetPixel(0, 0, Color.white);
        _pixel.Apply();
        _pixel.hideFlags = HideFlags.HideAndDontSave;
    }

    void DrawBar(float x, float y, float width, float height, Color color)
    {
        GUI.color = color;
        GUI.DrawTexture(new Rect(x, y, width, height), _pixel);
    }
}
