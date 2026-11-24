using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class MonolithRodColliderField : MonoBehaviour
{
    const string ContainerName = "Rod Collision Proxies";

    [Tooltip("Collider size and the distances that turn rod colliders on and off.")]
    [SerializeField] MonolithRodCollisionSettings settings;

    [Tooltip("Supplies the same face anchors, spacing, and rod axes as the GPU band.")]
    [SerializeField] MonolithFourFaceBandPreviewRenderer band;

    [Tooltip("The player whose position chooses which rods are solid.")]
    [SerializeField] Transform player;

    [Tooltip("Draw wire boxes for the colliders that are currently active.")]
    [SerializeField] bool showColliderGizmos;

    readonly List<MonolithRodLocalPose> _facePoses = new List<MonolithRodLocalPose>(128);
    readonly List<Pose> _rods = new List<Pose>(384);
    readonly List<MonolithRodCollisionProxy> _proxies = new List<MonolithRodCollisionProxy>(32);
    readonly Stack<int> _free = new Stack<int>(32);
    readonly HashSet<MonolithRodId> _disabled = new HashSet<MonolithRodId>();
    int[] _rodToProxy = System.Array.Empty<int>();
    Vector3[] _anchorPositions = System.Array.Empty<Vector3>();
    Quaternion[] _anchorRotations = System.Array.Empty<Quaternion>();
    bool[] _anchorReverse = System.Array.Empty<bool>();
    bool[] _anchorPresent = System.Array.Empty<bool>();
    int _stampColumns;
    int _stampRows;
    float _stampHorizontal;
    float _stampVertical;
    Transform _container;
    int _nearbyRods;
    bool _warned;
    bool _syncPhysics;
    bool _hasNearestRod;
    bool _hasNearestProxy;
    MonolithRodId _nearestRod;
    float _nearestRodDistance;
    Vector3 _nearestProxyWorldPosition;
    Vector3 _nearestProxyWorldSize;
    bool _nearestProxyActive;
    bool _nearestProxyColliderEnabled;
    bool _nearestProxyIsTrigger;
    int _proxyLayer;

    public int NearbyRodCount => _nearbyRods;
    public int ActiveColliderCount => _proxies.Count - _free.Count;
    public int PooledColliderCount => _proxies.Count;
    public bool HasNearestRod => _hasNearestRod;
    public MonolithRodId NearestRod => _nearestRod;
    public float NearestRodDistance => _nearestRodDistance;
    public bool HasNearestProxy => _hasNearestProxy;
    public Vector3 NearestProxyWorldPosition => _nearestProxyWorldPosition;
    public Vector3 NearestProxyWorldSize => _nearestProxyWorldSize;
    public bool NearestProxyActive => _nearestProxyActive;
    public bool NearestProxyColliderEnabled => _nearestProxyColliderEnabled;
    public bool NearestProxyIsTrigger => _nearestProxyIsTrigger;
    public int PlayerLayer => player != null ? player.gameObject.layer : -1;
    public int ProxyLayer => _proxyLayer;

    struct Pose
    {
        public MonolithRodId Rod;
        public Vector3 LocalPosition;
        public Quaternion LocalRotation;
    }

    void OnEnable()
    {
        if (!Application.isPlaying || band == null)
            return;

        EnsureContainer();
        RebuildPoses();
    }

    void OnDisable()
    {
        if (_container != null)
            Destroy(_container.gameObject);

        _container = null;
        _proxies.Clear();
        _free.Clear();
        _rods.Clear();
        _rodToProxy = System.Array.Empty<int>();
        _nearbyRods = 0;
    }

    void Update()
    {
        if (!Application.isPlaying)
            return;

        if (settings == null || band == null || player == null)
        {
            WarnOnce();
            return;
        }

        if (PosesStale())
            RebuildPoses();

        RefreshProximity();
        if (_syncPhysics)
        {
            Physics.SyncTransforms();
            _syncPhysics = false;
        }
    }

    public void SetRodSolid(MonolithRodId rod, bool solid)
    {
        if (solid)
        {
            _disabled.Remove(rod);
            return;
        }

        _disabled.Add(rod);
        ReleaseRod(rod);
    }

    public bool TryGetActiveProxy(MonolithRodId rod, out MonolithRodCollisionProxy proxy)
    {
        proxy = null;
        int index = IndexOf(rod);
        if (index < 0 || _rodToProxy[index] < 0)
            return false;

        proxy = _proxies[_rodToProxy[index]];
        return proxy != null && proxy.Occupied;
    }

    void WarnOnce()
    {
        if (_warned)
            return;

        _warned = true;
        Debug.LogWarning("Rod colliders need collision settings, the four-face band, and the Player transform.", this);
    }

    void EnsureContainer()
    {
        if (_container != null)
            return;

        var containerObject = new GameObject(ContainerName);
        _container = containerObject.transform;
        _container.SetParent(transform, false);
        _container.localPosition = Vector3.zero;
        _container.localRotation = Quaternion.identity;
        _container.localScale = Vector3.one;
    }

    bool PosesStale()
    {
        MonolithBandPreviewSettings bandSettings = band.BandSettings;
        if (bandSettings == null)
            return _rods.Count > 0;

        if (_rods.Count != ExpectedRodCount(bandSettings))
            return true;

        if (_stampColumns != bandSettings.ColumnCount
            || _stampRows != bandSettings.RowCount
            || !Mathf.Approximately(_stampHorizontal, bandSettings.HorizontalSpacing)
            || !Mathf.Approximately(_stampVertical, bandSettings.VerticalSpacing))
            return true;

        int slots = band.FaceSlotCount;
        if (_anchorPositions.Length != slots)
            return true;

        for (int i = 0; i < slots; i++)
        {
            if (!band.TryGetFace(i, out MonolithFaceAnchor slot))
            {
                if (_anchorPresent[i])
                    return true;
                continue;
            }

            if (!_anchorPresent[i])
                return true;

            Vector3 localPosition = transform.InverseTransformPoint(slot.Anchor.position);
            Quaternion localRotation = Quaternion.Inverse(transform.rotation) * slot.Anchor.rotation;
            if ((localPosition - _anchorPositions[i]).sqrMagnitude > 0.0001f)
                return true;

            if (Quaternion.Angle(localRotation, _anchorRotations[i]) > 0.5f)
                return true;

            if (_anchorReverse[i] != slot.ReverseColumnDirection)
                return true;
        }

        return false;
    }

    int ExpectedRodCount(MonolithBandPreviewSettings bandSettings)
    {
        int faces = 0;
        for (int i = 0; i < band.FaceSlotCount; i++)
        {
            if (band.TryGetFace(i, out _))
                faces++;
        }

        return faces * bandSettings.ColumnCount * bandSettings.RowCount;
    }

    void RebuildPoses()
    {
        ReleaseAll();
        _rods.Clear();
        MonolithBandPreviewSettings bandSettings = band.BandSettings;
        if (bandSettings == null)
            return;

        for (int face = 0; face < band.FaceSlotCount; face++)
        {
            if (!band.TryGetFace(face, out MonolithFaceAnchor slot))
                continue;

            _facePoses.Clear();
            if (!MonolithBandInstances.TryAppendRodPoses(
                    transform,
                    slot.Anchor,
                    bandSettings.ColumnCount,
                    bandSettings.RowCount,
                    bandSettings.HorizontalSpacing,
                    bandSettings.VerticalSpacing,
                    slot.ReverseColumnDirection,
                    _facePoses))
                continue;

            for (int i = 0; i < _facePoses.Count; i++)
            {
                MonolithRodLocalPose pose = _facePoses[i];
                _rods.Add(new Pose
                {
                    Rod = new MonolithRodId(face, pose.Column, pose.Row),
                    LocalPosition = pose.LocalPosition,
                    LocalRotation = pose.LocalRotation
                });
            }
        }

        _rodToProxy = new int[_rods.Count];
        for (int i = 0; i < _rodToProxy.Length; i++)
            _rodToProxy[i] = -1;

        _stampColumns = bandSettings.ColumnCount;
        _stampRows = bandSettings.RowCount;
        _stampHorizontal = bandSettings.HorizontalSpacing;
        _stampVertical = bandSettings.VerticalSpacing;
        int slots = band.FaceSlotCount;
        _anchorPositions = new Vector3[slots];
        _anchorRotations = new Quaternion[slots];
        _anchorReverse = new bool[slots];
        _anchorPresent = new bool[slots];
        for (int i = 0; i < slots; i++)
        {
            if (!band.TryGetFace(i, out MonolithFaceAnchor slot))
                continue;

            _anchorPresent[i] = true;

            _anchorPositions[i] = transform.InverseTransformPoint(slot.Anchor.position);
            _anchorRotations[i] = Quaternion.Inverse(transform.rotation) * slot.Anchor.rotation;
            _anchorReverse[i] = slot.ReverseColumnDirection;
        }
    }

    void RefreshProximity()
    {
        float activate = settings.ActivateDistance;
        float deactivate = settings.DeactivateDistance;
        float activateSqr = activate * activate;
        float deactivateSqr = deactivate * deactivate;
        Vector3 playerPosition = player.position;
        int nearby = 0;
        float nearestSqr = float.PositiveInfinity;
        int nearestIndex = -1;

        for (int i = 0; i < _rods.Count; i++)
        {
            Pose pose = _rods[i];
            if (_disabled.Contains(pose.Rod))
            {
                if (_rodToProxy[i] >= 0)
                    ReleaseIndex(i);
                continue;
            }

            float distanceSqr = DistanceSqrToRod(pose, playerPosition);
            if (distanceSqr < nearestSqr)
            {
                nearestSqr = distanceSqr;
                nearestIndex = i;
            }

            bool assigned = _rodToProxy[i] >= 0;
            if (!assigned && distanceSqr <= activateSqr)
                Bind(i, pose);
            else if (assigned && distanceSqr > deactivateSqr)
                ReleaseIndex(i);

            if (distanceSqr <= activateSqr)
                nearby++;
        }

        _nearbyRods = nearby;
        RememberNearest(nearestIndex, nearestSqr);
    }

    float DistanceSqrToRod(Pose pose, Vector3 playerPosition)
    {
        Vector3 center = settings.Center;
        Vector3 size = settings.Size;
        float start = center.z - (size.z * 0.5f);
        float end = center.z + (size.z * 0.5f);
        Vector3 worldStart = transform.TransformPoint(pose.LocalPosition + (pose.LocalRotation * (Vector3.forward * start)));
        Vector3 worldEnd = transform.TransformPoint(pose.LocalPosition + (pose.LocalRotation * (Vector3.forward * end)));
        return SquaredDistanceToSegment(playerPosition, worldStart, worldEnd);
    }

    static float SquaredDistanceToSegment(Vector3 point, Vector3 start, Vector3 end)
    {
        Vector3 segment = end - start;
        float lengthSqr = segment.sqrMagnitude;
        if (lengthSqr <= 0f)
            return (point - start).sqrMagnitude;

        float t = Vector3.Dot(point - start, segment) / lengthSqr;
        if (t < 0f)
            t = 0f;
        else if (t > 1f)
            t = 1f;

        Vector3 closest = start + (segment * t);
        return (point - closest).sqrMagnitude;
    }

    void Bind(int rodIndex, Pose pose)
    {
        EnsureContainer();
        int proxyIndex = _free.Count > 0 ? _free.Pop() : CreateProxy();
        MonolithRodCollisionProxy proxy = _proxies[proxyIndex];
        proxy.Bind(pose.Rod, pose.LocalPosition, pose.LocalRotation, settings.Center, settings.Size, player.gameObject.layer);
        _rodToProxy[rodIndex] = proxyIndex;
        _syncPhysics = true;
    }

    int CreateProxy()
    {
        var proxyObject = new GameObject("Rod Collider");
        proxyObject.transform.SetParent(_container, false);
        MonolithRodCollisionProxy proxy = proxyObject.AddComponent<MonolithRodCollisionProxy>();
        _proxies.Add(proxy);
        return _proxies.Count - 1;
    }

    void ReleaseRod(MonolithRodId rod)
    {
        ReleaseIndex(IndexOf(rod));
    }

    void ReleaseIndex(int rodIndex)
    {
        if (rodIndex < 0 || rodIndex >= _rodToProxy.Length)
            return;

        int proxyIndex = _rodToProxy[rodIndex];
        if (proxyIndex < 0)
            return;

        _proxies[proxyIndex].ReleaseToPool();
        _free.Push(proxyIndex);
        _rodToProxy[rodIndex] = -1;
        _syncPhysics = true;
    }

    void ReleaseAll()
    {
        for (int i = 0; i < _rodToProxy.Length; i++)
            ReleaseIndex(i);
    }

    int IndexOf(MonolithRodId rod)
    {
        for (int i = 0; i < _rods.Count; i++)
        {
            if (_rods[i].Rod.Equals(rod))
                return i;
        }

        return -1;
    }

    void RememberNearest(int rodIndex, float distanceSqr)
    {
        _hasNearestRod = rodIndex >= 0;
        _hasNearestProxy = false;
        _nearestProxyActive = false;
        _nearestProxyColliderEnabled = false;
        _nearestProxyIsTrigger = false;
        _nearestProxyWorldPosition = Vector3.zero;
        _nearestProxyWorldSize = Vector3.zero;
        if (!_hasNearestRod)
        {
            _nearestRodDistance = 0f;
            return;
        }

        _nearestRod = _rods[rodIndex].Rod;
        _nearestRodDistance = Mathf.Sqrt(distanceSqr);
        int proxyIndex = _rodToProxy[rodIndex];
        if (proxyIndex < 0)
            return;

        MonolithRodCollisionProxy proxy = _proxies[proxyIndex];
        _hasNearestProxy = proxy != null;
        if (proxy == null)
            return;

        _proxyLayer = proxy.gameObject.layer;
        _nearestProxyActive = proxy.gameObject.activeInHierarchy;
        _nearestProxyColliderEnabled = proxy.ColliderEnabled;
        _nearestProxyIsTrigger = proxy.ColliderIsTrigger;
        _nearestProxyWorldPosition = proxy.transform.TransformPoint(proxy.ColliderCenter);
        Vector3 scale = proxy.transform.lossyScale;
        Vector3 size = proxy.ColliderSize;
        _nearestProxyWorldSize = new Vector3(
            Mathf.Abs(size.x * scale.x),
            Mathf.Abs(size.y * scale.y),
            Mathf.Abs(size.z * scale.z));
    }

    void OnGUI()
    {
        if (!Application.isPlaying)
            return;

        GUILayout.BeginArea(new Rect(12f, 12f, 430f, 250f), GUI.skin.box);
        GUILayout.Label("Rod Collision");
        GUILayout.Label("Nearby rods detected: " + _nearbyRods);
        GUILayout.Label("Active collision proxies: " + ActiveColliderCount);
        GUILayout.Label("Total pooled proxies: " + _proxies.Count);
        if (_hasNearestRod)
        {
            GUILayout.Label("Nearest rod: Face " + _nearestRod.Face + ", Column " + _nearestRod.Column + ", Row " + _nearestRod.Row);
            GUILayout.Label("Nearest rod distance: " + _nearestRodDistance.ToString("0.00") + " m");
        }
        else
        {
            GUILayout.Label("Nearest rod: none");
        }

        GUILayout.Label("Nearest proxy position: " + (_hasNearestProxy ? _nearestProxyWorldPosition.ToString("0.00") : "none"));
        GUILayout.Label("Nearest proxy size: " + (_hasNearestProxy ? _nearestProxyWorldSize.ToString("0.00") : "none"));
        GUILayout.Label("Active: " + _nearestProxyActive + "    Collider enabled: " + _nearestProxyColliderEnabled + "    Trigger: " + _nearestProxyIsTrigger);
        GUILayout.Label("Player layer: " + PlayerLayer + "    Proxy layer: " + _proxyLayer);
        GUILayout.EndArea();
    }

    void OnDrawGizmos()
    {
        if (!showColliderGizmos)
            return;

        Gizmos.color = new Color(0.25f, 0.95f, 0.45f, 0.9f);
        for (int i = 0; i < _proxies.Count; i++)
        {
            MonolithRodCollisionProxy proxy = _proxies[i];
            if (proxy == null || !proxy.Occupied)
                continue;

            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.matrix = proxy.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(proxy.ColliderCenter, proxy.ColliderSize);
            Gizmos.matrix = previous;
        }
    }
}

public sealed class MonolithRodCollisionProxy : MonoBehaviour
{
    BoxCollider _collider;

    public MonolithRodId Rod { get; private set; }
    public bool Occupied { get; private set; }
    public Vector3 ColliderCenter => _collider != null ? _collider.center : Vector3.zero;
    public Vector3 ColliderSize => _collider != null ? _collider.size : Vector3.zero;
    public bool ColliderEnabled => _collider != null && _collider.enabled;
    public bool ColliderIsTrigger => _collider != null && _collider.isTrigger;

    public void Bind(
        MonolithRodId rod,
        Vector3 localPosition,
        Quaternion localRotation,
        Vector3 center,
        Vector3 size,
        int layer)
    {
        EnsureCollider();
        Rod = rod;
        Occupied = true;
        gameObject.layer = layer;
        gameObject.name = "Rod F" + rod.Face + " C" + rod.Column + " R" + rod.Row;
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        transform.localPosition = localPosition;
        transform.localRotation = localRotation;
        transform.localScale = Vector3.one;
        _collider.center = center;
        _collider.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
        _collider.isTrigger = false;
        _collider.enabled = true;
    }

    public void ReleaseToPool()
    {
        Occupied = false;
        if (_collider != null)
            _collider.enabled = false;

        gameObject.SetActive(false);
    }

    void EnsureCollider()
    {
        if (_collider != null)
            return;

        _collider = GetComponent<BoxCollider>();
        if (_collider == null)
            _collider = gameObject.AddComponent<BoxCollider>();
    }
}
