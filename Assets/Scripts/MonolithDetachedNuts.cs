using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Fallen nuts stay independent in world space and sleep when they settle.
// A later pass can read ActiveNuts and replace distant sleeping ones with stationary instances.
public sealed class MonolithDetachedNutPool
{
    readonly Stack<MonolithDetachedNut> _available = new Stack<MonolithDetachedNut>();
    readonly List<MonolithDetachedNut> _active = new List<MonolithDetachedNut>();
    Transform _root;
    PhysicsMaterial _material;

    public int ActiveCount => _active.Count;
    public int InactiveCount => _available.Count;
    public int TotalCount => _active.Count + _available.Count;

    public bool TrySpawn(
        Matrix4x4 worldMatrix,
        MonolithNutId identity,
        MonolithNutFlightPiece[] pieces,
        Vector3 linearVelocity,
        Vector3 angularVelocity,
        ShadowCastingMode shadows,
        bool receiveShadows,
        MonolithNutUnscrewingSettings settings,
        out bool createdNew,
        out string diagnostic,
        out bool ready)
    {
        createdNew = false;
        diagnostic = string.Empty;
        ready = false;
        if (settings == null || !TryDescribe(pieces, out Mesh mesh, out Material[] materials, out MonolithNutFlightPiece appearance))
            return false;

        if (!TryDecompose(worldMatrix, out Vector3 position, out Quaternion rotation, out Vector3 scale))
        {
            diagnostic = "The detach matrix has no usable scale, so no physics nut was created.";
            Debug.LogError(diagnostic);
            return false;
        }

        EnsureRoot();
        MonolithDetachedNut nut = Borrow(out createdNew);
        diagnostic = nut.Launch(
            _root,
            identity,
            position,
            rotation,
            scale,
            mesh,
            materials,
            appearance,
            linearVelocity,
            angularVelocity,
            shadows,
            receiveShadows,
            settings,
            SharedMaterial(settings));
        ready = nut.IsReady;
        _active.Add(nut);
        return true;
    }

    public void ReleaseAll()
    {
        for (int i = 0; i < _active.Count; i++)
        {
            MonolithDetachedNut nut = _active[i];
            if (nut == null)
                continue;

            nut.Park(_root);
            _available.Push(nut);
        }

        _active.Clear();
    }

    public void Dispose()
    {
        ReleaseAll();
        if (_root != null)
            Object.Destroy(_root.gameObject);

        _root = null;
        if (_material != null)
            Object.Destroy(_material);

        _material = null;
        _available.Clear();
    }

    void EnsureRoot()
    {
        if (_root != null)
            return;

        var rootObject = new GameObject("Detached Nuts");
        _root = rootObject.transform;
        _root.SetParent(null, false);
        _root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        _root.localScale = Vector3.one;
    }

    static bool TryDecompose(Matrix4x4 matrix, out Vector3 position, out Quaternion rotation, out Vector3 scale)
    {
        position = matrix.GetPosition();
        Vector3 right = matrix.GetColumn(0);
        Vector3 up = matrix.GetColumn(1);
        Vector3 forward = matrix.GetColumn(2);
        scale = new Vector3(right.magnitude, up.magnitude, forward.magnitude);
        rotation = Quaternion.identity;
        if (scale.x < 1e-8f || scale.y < 1e-8f || scale.z < 1e-8f)
            return false;

        right /= scale.x;
        up /= scale.y;
        forward /= scale.z;
        if (Vector3.Dot(Vector3.Cross(right, up), forward) < 0f)
        {
            scale.x = -scale.x;
            right = -right;
        }

        rotation = Quaternion.LookRotation(forward, up);
        return true;
    }

    MonolithDetachedNut Borrow(out bool createdNew)
    {
        while (_available.Count > 0)
        {
            MonolithDetachedNut pooled = _available.Pop();
            if (pooled == null)
                continue;

            createdNew = false;
            return pooled;
        }

        createdNew = true;
        var nutObject = new GameObject("Detached Nut");
        nutObject.transform.SetParent(_root, false);
        return nutObject.AddComponent<MonolithDetachedNut>();
    }

    PhysicsMaterial SharedMaterial(MonolithNutUnscrewingSettings settings)
    {
        if (_material == null)
        {
            _material = new PhysicsMaterial("Detached Nut")
            {
                hideFlags = HideFlags.HideAndDontSave,
                bounceCombine = PhysicsMaterialCombine.Average,
                frictionCombine = PhysicsMaterialCombine.Average
            };
        }

        _material.bounciness = settings.Bounciness;
        _material.dynamicFriction = settings.Friction;
        _material.staticFriction = settings.Friction;
        return _material;
    }

    static bool TryDescribe(
        MonolithNutFlightPiece[] pieces,
        out Mesh mesh,
        out Material[] materials,
        out MonolithNutFlightPiece appearance)
    {
        mesh = null;
        materials = null;
        appearance = default;
        if (pieces == null)
            return false;

        for (int i = 0; i < pieces.Length; i++)
        {
            if (pieces[i].Mesh == null)
                continue;

            mesh = pieces[i].Mesh;
            appearance = pieces[i];
            break;
        }

        if (mesh == null)
            return false;

        materials = new Material[mesh.subMeshCount];
        for (int i = 0; i < pieces.Length; i++)
        {
            if (pieces[i].Mesh != mesh || pieces[i].SourceMaterial == null)
                continue;

            int submesh = pieces[i].SubmeshIndex;
            if (submesh < 0 || submesh >= materials.Length)
                continue;

            materials[submesh] = pieces[i].SourceMaterial;
        }

        Material fallback = appearance.SourceMaterial;
        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] == null)
                materials[i] = fallback;
        }

        return fallback != null;
    }
}

public sealed class MonolithDetachedNut : MonoBehaviour
{
    const string VisualName = "Nut Visual";

    Transform _visual;
    MeshFilter _filter;
    MeshRenderer _renderer;
    BoxCollider _collider;
    Rigidbody _body;
    MonolithDetachedNutIdentity _identity;

    public bool IsSleeping => _body != null && _body.IsSleeping();
    public bool IsReady { get; private set; }

    public string Launch(
        Transform poolRoot,
        MonolithNutId identity,
        Vector3 position,
        Quaternion rotation,
        Vector3 scale,
        Mesh mesh,
        Material[] materials,
        MonolithNutFlightPiece appearance,
        Vector3 linearVelocity,
        Vector3 angularVelocity,
        ShadowCastingMode shadows,
        bool receiveShadows,
        MonolithNutUnscrewingSettings settings,
        PhysicsMaterial physicsMaterial)
    {
        EnsureParts();
        Vector3 visualScale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        _body.interpolation = RigidbodyInterpolation.None;
        _body.isKinematic = true;

        transform.SetParent(poolRoot, false);
        transform.localScale = Vector3.one;
        transform.SetPositionAndRotation(position, rotation);

        _visual.localPosition = Vector3.zero;
        _visual.localRotation = Quaternion.identity;
        _visual.localScale = visualScale;

        _filter.sharedMesh = mesh;
        _renderer.sharedMaterials = materials;
        _renderer.enabled = true;
        _renderer.forceRenderingOff = false;
        _renderer.shadowCastingMode = shadows;
        _renderer.receiveShadows = receiveShadows;
        _renderer.lightProbeUsage = appearance.LightProbeUsage;
        _renderer.reflectionProbeUsage = appearance.ReflectionProbeUsage;
        _renderer.motionVectorGenerationMode = appearance.MotionVectors;
        uint renderingLayerMask = appearance.RenderingLayerMask == 0 ? 1u : appearance.RenderingLayerMask;
        _renderer.renderingLayerMask = renderingLayerMask;
        gameObject.layer = appearance.Layer;
        _visual.gameObject.layer = appearance.Layer;

        _collider.enabled = true;
        _collider.isTrigger = false;
        _collider.sharedMaterial = physicsMaterial;
        _collider.center = Vector3.Scale(mesh.bounds.center, visualScale);
        _collider.size = Vector3.Scale(mesh.bounds.size, visualScale);

        _body.mass = settings.Mass;
        _body.constraints = RigidbodyConstraints.None;
        _body.detectCollisions = true;
        _identity.Assign(identity, _renderer);
        _visual.gameObject.SetActive(true);
        gameObject.SetActive(true);
        _body.isKinematic = false;
        _body.useGravity = true;
        _body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        _body.position = position;
        _body.rotation = rotation;
        _body.linearVelocity = linearVelocity;
        _body.angularVelocity = angularVelocity;
        _body.WakeUp();
        _body.interpolation = RigidbodyInterpolation.Interpolate;

        string report = Describe(mesh, materials, visualScale);
        IsReady = IsUsable(mesh, materials);
        return report;
    }

    string Describe(Mesh mesh, Material[] materials, Vector3 visualScale)
    {
        Bounds worldBounds = mesh != null && _visual != null
            ? MonolithBandInstances.TransformBounds(_visual.localToWorldMatrix, mesh.bounds)
            : new Bounds(transform.position, Vector3.zero);
        int materialCount = materials != null ? materials.Length : 0;
        string meshName = mesh != null ? mesh.name : "none";
        int vertexCount = mesh != null ? mesh.vertexCount : 0;
        return
            $"Mesh assigned: {meshName} ({vertexCount} vertices)\n" +
            $"Materials assigned: {materialCount}\n" +
            $"Renderer enabled: {(_renderer != null && _renderer.enabled)}\n" +
            $"Rigidbody present: {_body != null}\n" +
            $"Rigidbody isKinematic: {(_body != null && _body.isKinematic)}\n" +
            $"Rigidbody useGravity: {(_body != null && _body.useGravity)}\n" +
            $"Collider present: {_collider != null}, enabled: {(_collider != null && _collider.enabled)}\n" +
            $"activeInHierarchy: {gameObject.activeInHierarchy}\n" +
            $"World bounds: center {worldBounds.center}, size {worldBounds.size}\n" +
            $"Spawn position: {transform.position}, rotation: {transform.rotation.eulerAngles}\n" +
            $"Visual scale: {visualScale}";
    }

    bool IsUsable(Mesh mesh, Material[] materials)
    {
        if (mesh == null || mesh.vertexCount == 0)
            return false;

        if (materials == null || materials.Length == 0)
            return false;

        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] == null)
                return false;
        }

        if (_filter == null || _filter.sharedMesh == null || _renderer == null || !_renderer.enabled)
            return false;

        if (_body == null || _body.isKinematic || !_body.useGravity)
            return false;

        if (_collider == null || !_collider.enabled)
            return false;

        if (!gameObject.activeInHierarchy)
            return false;

        float span = Mathf.Max(_collider.size.x, Mathf.Max(_collider.size.y, _collider.size.z));
        return span > 0f;
    }

    public void Park(Transform root)
    {
        if (_identity != null)
            _identity.Clear();

        if (_body != null)
        {
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
            _body.interpolation = RigidbodyInterpolation.None;
            _body.Sleep();
        }

        gameObject.SetActive(false);
        transform.SetParent(root, false);
    }

    void EnsureParts()
    {
        if (_visual == null)
        {
            var visualObject = new GameObject(VisualName);
            _visual = visualObject.transform;
            _visual.SetParent(transform, false);
        }

        if (_filter == null)
        {
            _filter = _visual.GetComponent<MeshFilter>();
            if (_filter == null)
                _filter = _visual.gameObject.AddComponent<MeshFilter>();
        }

        if (_renderer == null)
        {
            _renderer = _visual.GetComponent<MeshRenderer>();
            if (_renderer == null)
                _renderer = _visual.gameObject.AddComponent<MeshRenderer>();
        }

        if (_collider == null)
        {
            _collider = GetComponent<BoxCollider>();
            if (_collider == null)
                _collider = gameObject.AddComponent<BoxCollider>();
        }

        if (_body == null)
        {
            _body = GetComponent<Rigidbody>();
            if (_body == null)
                _body = gameObject.AddComponent<Rigidbody>();
        }

        if (_identity == null)
        {
            _identity = GetComponent<MonolithDetachedNutIdentity>();
            if (_identity == null)
                _identity = gameObject.AddComponent<MonolithDetachedNutIdentity>();
        }
    }
}
