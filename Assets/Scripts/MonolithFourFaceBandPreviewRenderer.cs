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

    [Tooltip("Movement speed, spin speed, and the shortest unscrewing time.")]
    [SerializeField] MonolithNutUnscrewingSettings unscrewingSettings;

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
    bool _visibilityDirty;
    MonolithNutState _nutState;
    readonly MonolithNutFlightBoard _flights = new MonolithNutFlightBoard();
    readonly List<MonolithNutFlight> _readyFlights = new List<MonolithNutFlight>(4);
    readonly MonolithDetachedNutPool _detachedNuts = new MonolithDetachedNutPool();
    readonly Matrix4x4[] _flyingMatrix = new Matrix4x4[1];
    int _completedDetachments;
    int _lastGameplayFrame = -1;
    int _playGeneration;
    bool _advancingGameplay;
#if UNITY_EDITOR
    bool _repaintQueued;
#endif

    public MonolithBandPreviewSettings BandSettings => bandSettings;
    public GameObject RodPrefab => rodPrefab;
    public int FaceSlotCount => faces != null ? faces.Length : 0;
    public RodNutTestSettings NutSettings => nutSettings;
    public bool ShowPreview => showPreview;
    public MonolithBandPreviewRenderer OneFacePreview => oneFacePreview;
    public MonolithNutState NutState => Application.isPlaying ? _nutState : null;
    public MonolithNutUnscrewingSettings UnscrewingSettings => unscrewingSettings;
    public bool HasActiveUnscrewing => _flights.Count > 0;
    public int ActiveUnscrewingCount => _flights.UnscrewingCount;
    public int CompletedDetachmentCount => _completedDetachments;
    public int ActivePhysicsNutCount => _detachedNuts.ActiveCount;
    public int InactivePooledNutCount => _detachedNuts.InactiveCount;
    public int TotalPoolSize => _detachedNuts.TotalCount;
    public int TotalRemovedNuts => _nutState != null ? _nutState.RemovedCount : 0;

    public bool TryGetFace(int index, out MonolithFaceAnchor face)
    {
        if (faces == null || index < 0 || index >= faces.Length)
        {
            face = default;
            return false;
        }

        face = faces[index];
        return face.Anchor != null;
    }

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
        if (Application.isPlaying)
        {
            if (_playGeneration == 0)
            {
                _playGeneration = 1;
                _nutState = null;
                _completedDetachments = 0;
                _lastGameplayFrame = -1;
            }

            EnsureNutState();
            _visibilityDirty = true;
        }
        else
        {
            _playGeneration = 0;
            _nutState = null;
            _completedDetachments = 0;
            _lastGameplayFrame = -1;
            _visibilityDirty = false;
        }
#if UNITY_EDITOR
        if (!Application.isPlaying && showPreview)
            UnityEditor.SceneView.RepaintAll();
#endif
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        ReleaseInstances();
        _flights.CancelAll();
        _detachedNuts.Dispose();
        _nutState = null;
        _completedDetachments = 0;
        _lastGameplayFrame = -1;
        _playGeneration = 0;
        _advancingGameplay = false;
        _visibilityDirty = false;
    }

    void OnValidate()
    {
        EnsureFaces();
#if UNITY_EDITOR
        if (!Application.isPlaying)
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

            _visibilityDirty = true;
        }

        if (_faces == null)
            return;

        if (Application.isPlaying)
        {
            EnsureNutState();
            if (_visibilityDirty)
                ApplyAllVisibility();
        }

        Draw(camera);
        DrawFlights(camera);
        BandRendered?.Invoke(camera);
    }

    void Update()
    {
        // Gameplay advances once per player frame. Camera rendering, Scene View
        // repaints, and URP passes must not re-enter this step.
        if (!Application.isPlaying || _advancingGameplay || _flights.Count == 0)
            return;

        int frame = Time.frameCount;
        if (frame == _lastGameplayFrame)
            return;

        _advancingGameplay = true;
        _lastGameplayFrame = frame;
        try
        {
            _flights.CopyReady(_readyFlights);
            for (int i = 0; i < _readyFlights.Count; i++)
            {
                MonolithNutFlight flight = _readyFlights[i];
                if (!_flights.TryBeginDetach(flight))
                    continue;

                if (!TryDetach(flight))
                {
                    flight.Phase = MonolithNutFlightPhase.Unscrewing;
                    continue;
                }

                _flights.Release(flight);
                _completedDetachments++;
                if (_nutState != null)
                    _nutState.TryRemoveOutermost(flight.Rod);

                if (!ApplyVisibility(flight.Rod.Face))
                    _visibilityDirty = true;
            }

            _flights.Tick(Time.deltaTime);
            RequestPreviewRepaint();
        }
        finally
        {
            _advancingGameplay = false;
        }
    }

    bool TryDetach(MonolithNutFlight flight)
    {
        if (flight.Phase != MonolithNutFlightPhase.Detaching)
            return false;

        if (unscrewingSettings == null || !TryFlightWorldMatrix(flight, out Matrix4x4 world))
            return false;

        Quaternion rodWorld = transform.rotation * flight.RodOnContainer.rotation;
        Vector3 outward = rodWorld * Vector3.forward;
        var detachedId = new MonolithNutId(flight.Rod.Face, flight.Rod.Column, flight.Rod.Row, flight.SlotIndex);
        if (!_detachedNuts.TrySpawn(
                world,
                detachedId,
                flight.Pieces,
                outward * unscrewingSettings.OutwardVelocity,
                outward * (unscrewingSettings.AngularVelocity * Mathf.Deg2Rad),
                shadowCastingMode,
                receiveShadows,
                unscrewingSettings,
                out bool createdNew,
                out string diagnostic,
                out bool ready))
            return false;

        string message =
            $"Detached nut face {flight.Rod.Face}, column {flight.Rod.Column}, row {flight.Rod.Row}, nut index {flight.SlotIndex}. " +
            (createdNew ? "Created a new physics nut." : "Reused a pooled physics nut.") +
            "\n" + diagnostic;
        if (ready)
            Debug.Log(message, this);
        else
            Debug.LogError(message, this);

        return true;
    }

    bool TryFlightWorldMatrix(MonolithNutFlight flight, out Matrix4x4 world)
    {
        world = Matrix4x4.identity;
        if (flight.Pieces == null || flight.Pieces.Length == 0 || flight.Pieces[0].Mesh == null)
            return false;

        MonolithNutMotion.Sample(
            flight.Elapsed,
            flight.Duration,
            flight.StartRodZ,
            flight.EndRodZ,
            flight.DegreesPerSecond,
            out float rodZ,
            out float extraDegrees);
        Matrix4x4 shift = MonolithNutMotion.RodShift(flight, rodZ, extraDegrees);
        world = transform.localToWorldMatrix * shift * flight.Pieces[0].BaseLocalToContainer;
        return true;
    }

    public event System.Action<Camera> BandRendered;

    public bool IsRodAnimating(MonolithRodId rod)
    {
        return _flights.IsRodAnimating(rod);
    }

    public bool IsNutHidden(MonolithNutId nut)
    {
        if (_nutState == null)
            return false;

        int rodOnFace = _nutState.RodIndexOnFace(nut.Rod);
        return rodOnFace >= 0 && _flights.IsHidden(nut.Face, rodOnFace, nut.NutIndex);
    }

    public bool TryGetUnscrewingNut(MonolithRodId rod, out int nutIndex, out float rodLocalZ)
    {
        nutIndex = -1;
        rodLocalZ = 0f;
        for (int i = 0; i < _flights.Count; i++)
        {
            MonolithNutFlight flight = _flights.FlightAt(i);
            if (!flight.Rod.Equals(rod) || flight.Phase != MonolithNutFlightPhase.Unscrewing)
                continue;

            MonolithNutMotion.Sample(
                flight.Elapsed,
                flight.Duration,
                flight.StartRodZ,
                flight.EndRodZ,
                flight.DegreesPerSecond,
                out rodLocalZ,
                out _);
            nutIndex = flight.SlotIndex;
            return true;
        }

        return false;
    }

    public bool TryGetUnscrewingProgress(MonolithRodId rod, out float progress)
    {
        progress = 0f;
        for (int i = 0; i < _flights.Count; i++)
        {
            MonolithNutFlight flight = _flights.FlightAt(i);
            if (!flight.Rod.Equals(rod) || flight.Phase != MonolithNutFlightPhase.Unscrewing)
                continue;

            progress = flight.Duration > 0f ? Mathf.Clamp01(flight.Elapsed / flight.Duration) : 1f;
            return true;
        }

        return false;
    }

    public bool TryGetNutWorldMatrix(MonolithNutId nut, out Matrix4x4 world)
    {
        world = Matrix4x4.identity;
        if (!Application.isPlaying || _nutState == null || bandSettings == null || nutSettings == null || rodPrefab == null)
            return false;

        if (!_nutState.IsSlotPresent(nut.Rod, nut.NutIndex))
            return false;

        if (!TryGetFace(nut.Face, out MonolithFaceAnchor face))
            return false;

        if (!MonolithBandInstances.TryNutLocalToContainer(
                transform,
                face.Anchor,
                rodPrefab,
                bandSettings,
                nutSettings,
                face.ReverseColumnDirection,
                nut.Face,
                nut.Column,
                nut.Row,
                nut.NutIndex,
                out Matrix4x4 local))
            return false;

        if (TryGetUnscrewingShift(nut, out Matrix4x4 shift))
            local = shift * local;
        else if (IsNutHidden(nut))
            return false;

        world = transform.localToWorldMatrix * local;
        return true;
    }

    bool TryGetUnscrewingShift(MonolithNutId nut, out Matrix4x4 shift)
    {
        shift = Matrix4x4.identity;
        for (int i = 0; i < _flights.Count; i++)
        {
            MonolithNutFlight flight = _flights.FlightAt(i);
            if (flight.Phase != MonolithNutFlightPhase.Unscrewing || flight.SlotIndex != nut.NutIndex || !flight.Rod.Equals(nut.Rod))
                continue;

            MonolithNutMotion.Sample(
                flight.Elapsed,
                flight.Duration,
                flight.StartRodZ,
                flight.EndRodZ,
                flight.DegreesPerSecond,
                out float rodZ,
                out float extraDegrees);
            shift = MonolithNutMotion.RodShift(flight, rodZ, extraDegrees);
            return true;
        }

        return false;
    }

    public bool TryAnimateOneNut(MonolithRodId rod)
    {
        if (!Application.isPlaying || bandSettings == null || nutSettings == null || rodPrefab == null || unscrewingSettings == null)
            return false;

        EnsureNutState();
        EnsureFaces();
        if (_nutState == null || _faces == null || !_nutState.Contains(rod) || _flights.IsRodAnimating(rod))
            return false;

        int remaining = _nutState.Remaining(rod);
        if (remaining <= 0 || rod.Face >= _faces.Length || _faces[rod.Face] == null)
            return false;

        int slot = remaining - 1;
        int rodOnFace = _nutState.RodIndexOnFace(rod);
        if (!MonolithBandInstances.TryMeasureRodLength(rodPrefab, out float rodLength, out string error))
        {
            Debug.LogWarning(error, this);
            return false;
        }

        if (!MonolithBandInstances.TryNutRodZ(
                nutSettings,
                rodLength,
                rod.Face,
                rod.Column,
                rod.Row,
                slot,
                out float startRodZ,
                out error))
        {
            Debug.LogWarning(error, this);
            return false;
        }

        MonolithFaceAnchor face = faces[rod.Face];
        if (!MonolithBandInstances.TryRodOnContainer(
                transform,
                face.Anchor,
                rodPrefab,
                bandSettings.ColumnCount,
                bandSettings.RowCount,
                bandSettings.HorizontalSpacing,
                bandSettings.VerticalSpacing,
                face.ReverseColumnDirection,
                rod.Column,
                rod.Row,
                out Matrix4x4 rodOnContainer))
        {
            Debug.LogWarning("Could not place the unscrewing nut on its rod.", this);
            return false;
        }

        if (!TryCapturePieces(_faces[rod.Face], rodOnFace, slot, out MonolithNutFlightPiece[] pieces))
        {
            Debug.LogWarning("Could not find that nut in the instanced band.", this);
            return false;
        }

        float endRodZ = MonolithNutMotion.EndRodZ(rodLength, nutSettings.NutThickness);
        var flight = new MonolithNutFlight
        {
            Rod = rod,
            RodOnFace = rodOnFace,
            SlotIndex = slot,
            StartRodZ = startRodZ,
            EndRodZ = endRodZ,
            Duration = MonolithNutMotion.Duration(
                startRodZ,
                endRodZ,
                unscrewingSettings.MovementSpeed,
                unscrewingSettings.MinimumDuration),
            DegreesPerSecond = unscrewingSettings.RotationSpeed,
            RodOnContainer = rodOnContainer,
            ContainerToRod = Matrix4x4.Inverse(rodOnContainer),
            Pieces = pieces,
            LocalPathBounds = PathBounds(rodOnContainer, endRodZ - startRodZ, pieces)
        };

        _flights.Add(flight);
        if (!ApplyVisibility(rod.Face))
            _visibilityDirty = true;

        RequestPreviewRepaint();
        return true;
    }

    static bool TryCapturePieces(MonolithBandInstanceSet set, int rodOnFace, int slot, out MonolithNutFlightPiece[] pieces)
    {
        pieces = null;
        MonolithBandDrawGroup[] groups = set.Groups;
        int count = 0;
        for (int groupIndex = 0; groupIndex < groups.Length; groupIndex++)
        {
            MonolithBandDrawGroup group = groups[groupIndex];
            if (!group.TracksNuts)
                continue;

            int matrixCount = group.LocalToContainer.Length;
            for (int i = 0; i < matrixCount; i++)
            {
                if (group.NutRodIndex[i] == rodOnFace && group.NutSlotIndex[i] == slot)
                    count++;
            }
        }

        if (count == 0)
            return false;

        pieces = new MonolithNutFlightPiece[count];
        int write = 0;
        for (int groupIndex = 0; groupIndex < groups.Length; groupIndex++)
        {
            MonolithBandDrawGroup group = groups[groupIndex];
            if (!group.TracksNuts)
                continue;

            int matrixCount = group.LocalToContainer.Length;
            for (int i = 0; i < matrixCount; i++)
            {
                if (group.NutRodIndex[i] != rodOnFace || group.NutSlotIndex[i] != slot)
                    continue;

                pieces[write++] = new MonolithNutFlightPiece
                {
                    Mesh = group.Mesh,
                    SubmeshIndex = group.SubmeshIndex,
                    SourceMaterial = group.SourceMaterial,
                    BaseLocalToContainer = group.LocalToContainer[i],
                    Layer = group.Layer,
                    RenderingLayerMask = group.RenderingLayerMask,
                    LightProbeUsage = group.LightProbeUsage,
                    ReflectionProbeUsage = group.ReflectionProbeUsage,
                    MotionVectors = group.MotionVectors
                };
            }
        }

        return true;
    }

    static Bounds PathBounds(Matrix4x4 rodOnContainer, float travelDistance, MonolithNutFlightPiece[] pieces)
    {
        bool hasBounds = false;
        Bounds bounds = new Bounds(rodOnContainer.GetPosition(), Vector3.zero);
        Vector3 travel = rodOnContainer.MultiplyVector(Vector3.forward * travelDistance);
        for (int i = 0; i < pieces.Length; i++)
        {
            if (pieces[i].Mesh == null)
                continue;

            Bounds start = MonolithBandInstances.TransformBounds(pieces[i].BaseLocalToContainer, pieces[i].Mesh.bounds);
            if (!hasBounds)
            {
                bounds = start;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(start);
            }

            Bounds end = start;
            end.center += travel;
            bounds.Encapsulate(end);
        }

        return bounds;
    }

    public bool TryRemoveOneNut(MonolithRodId rod)
    {
        if (!Application.isPlaying)
            return false;

        EnsureNutState();
        if (_nutState == null || _flights.IsRodAnimating(rod) || !_nutState.TryRemoveOutermost(rod))
            return false;

        if (!ApplyVisibility(rod.Face))
            _visibilityDirty = true;

        RequestPreviewRepaint();
        return true;
    }

    public void ResetAllNuts()
    {
        if (!Application.isPlaying)
            return;

        _flights.CancelAll();
        _detachedNuts.ReleaseAll();
        _completedDetachments = 0;
        EnsureNutState();
        if (_nutState == null)
            return;

        _nutState.Reset();
        if (!ApplyAllVisibility())
            _visibilityDirty = true;

        RequestPreviewRepaint();
    }

    void EnsureNutState()
    {
        if (!Application.isPlaying || bandSettings == null || nutSettings == null)
            return;

        if (bandSettings.ColumnCount < 1
            || bandSettings.RowCount < 1
            || nutSettings.NutCount < 1
            || nutSettings.NutCount > ushort.MaxValue)
            return;

        if (_nutState != null && _nutState.Matches(
                FaceCount,
                bandSettings.ColumnCount,
                bandSettings.RowCount,
                nutSettings.NutCount))
            return;

        _nutState = new MonolithNutState(
            FaceCount,
            bandSettings.ColumnCount,
            bandSettings.RowCount,
            nutSettings.NutCount);
        _visibilityDirty = true;
    }

    bool ApplyVisibility(int faceIndex)
    {
        if (_faces == null || _nutState == null || faceIndex < 0 || faceIndex >= _faces.Length || _faces[faceIndex] == null)
            return false;

        _faces[faceIndex].ApplyNutVisibility(_nutState, faceIndex, _flights);
        return true;
    }

    bool ApplyAllVisibility()
    {
        if (_faces == null || _nutState == null)
            return false;

        for (int i = 0; i < _faces.Length; i++)
        {
            if (_faces[i] != null)
                _faces[i].ApplyNutVisibility(_nutState, i, _flights);
        }

        _visibilityDirty = false;
        return true;
    }

    void RequestPreviewRepaint()
    {
#if UNITY_EDITOR
        if (_repaintQueued)
            return;

        _repaintQueued = true;
        UnityEditor.EditorApplication.delayCall += FlushPreviewRepaint;
#endif
    }

#if UNITY_EDITOR
    void FlushPreviewRepaint()
    {
        _repaintQueued = false;
        if (this == null)
            return;

        UnityEditor.SceneView.RepaintAll();
    }
#endif

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
                Matrix4x4[] locals = group.UseVisibility ? group.VisibleToContainer : group.LocalToContainer;
                int length = group.UseVisibility ? group.VisibleCount : group.LocalToContainer.Length;
                if (locals == null || length <= 0)
                    continue;

                for (int start = 0; start < length; start += batchSize)
                {
                    int count = Mathf.Min(batchSize, length - start);
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

    void DrawFlights(Camera camera)
    {
        if (!Application.isPlaying || _flights.Count == 0)
            return;

        Matrix4x4 containerWorld = transform.localToWorldMatrix;
        for (int flightIndex = 0; flightIndex < _flights.Count; flightIndex++)
        {
            MonolithNutFlight flight = _flights.FlightAt(flightIndex);
            if (flight.Phase != MonolithNutFlightPhase.Unscrewing)
                continue;

            MonolithNutMotion.Sample(
                flight.Elapsed,
                flight.Duration,
                flight.StartRodZ,
                flight.EndRodZ,
                flight.DegreesPerSecond,
                out float rodZ,
                out float extraDegrees);

            Matrix4x4 shift = MonolithNutMotion.RodShift(flight, rodZ, extraDegrees);
            Bounds worldBounds = MonolithBandInstances.TransformBounds(containerWorld, flight.LocalPathBounds);
            MonolithNutFlightPiece[] pieces = flight.Pieces;
            for (int pieceIndex = 0; pieceIndex < pieces.Length; pieceIndex++)
            {
                MonolithNutFlightPiece piece = pieces[pieceIndex];
                if (piece.Mesh == null || piece.SourceMaterial == null)
                    continue;

                _flyingMatrix[0] = containerWorld * shift * piece.BaseLocalToContainer;
                var renderParams = new RenderParams(InstancedMaterial(piece.SourceMaterial))
                {
                    camera = camera,
                    worldBounds = worldBounds,
                    layer = piece.Layer,
                    renderingLayerMask = piece.RenderingLayerMask,
                    shadowCastingMode = shadowCastingMode,
                    receiveShadows = receiveShadows,
                    lightProbeUsage = piece.LightProbeUsage,
                    reflectionProbeUsage = piece.ReflectionProbeUsage,
                    motionVectorMode = piece.MotionVectors
                };
                Graphics.RenderMeshInstanced(renderParams, piece.Mesh, piece.SubmeshIndex, _flyingMatrix, 1);
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
