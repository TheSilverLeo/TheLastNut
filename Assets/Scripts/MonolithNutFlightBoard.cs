using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public struct MonolithNutFlightPiece
{
    public Mesh Mesh;
    public int SubmeshIndex;
    public Material SourceMaterial;
    public Matrix4x4 BaseLocalToContainer;
    public int Layer;
    public uint RenderingLayerMask;
    public LightProbeUsage LightProbeUsage;
    public ReflectionProbeUsage ReflectionProbeUsage;
    public MotionVectorGenerationMode MotionVectors;
}

public sealed class MonolithNutFlight
{
    public MonolithRodId Rod;
    public int RodOnFace;
    public int SlotIndex;
    public float StartRodZ;
    public float EndRodZ;
    public float Duration;
    public float Elapsed;
    public float DegreesPerSecond;
    public bool ReadyToCommit;
    public MonolithNutFlightPhase Phase;
    public Matrix4x4 RodOnContainer;
    public Matrix4x4 ContainerToRod;
    public Bounds LocalPathBounds;
    public MonolithNutFlightPiece[] Pieces = System.Array.Empty<MonolithNutFlightPiece>();
}

public enum MonolithNutFlightPhase
{
    Unscrewing,
    Detaching
}

public static class MonolithNutMotion
{
    public static float EndRodZ(float rodLength, float nutThickness)
    {
        return rodLength + (nutThickness * 0.5f);
    }

    public static float Duration(float startRodZ, float endRodZ, float metersPerSecond, float minimumSeconds)
    {
        float distance = endRodZ - startRodZ;
        if (distance < 0f)
            distance = 0f;

        float timed = metersPerSecond > 0f ? distance / metersPerSecond : 0f;
        return minimumSeconds > timed ? minimumSeconds : timed;
    }

    public static void Sample(
        float elapsed,
        float duration,
        float startRodZ,
        float endRodZ,
        float degreesPerSecond,
        out float rodZ,
        out float extraDegrees)
    {
        float span = duration > 0f ? elapsed / duration : 1f;
        if (span < 0f)
            span = 0f;
        else if (span > 1f)
            span = 1f;

        rodZ = startRodZ + ((endRodZ - startRodZ) * span);
        extraDegrees = degreesPerSecond * elapsed;
    }

    public static Matrix4x4 RodShift(MonolithNutFlight flight, float rodZ, float extraDegrees)
    {
        Matrix4x4 delta = Matrix4x4.TRS(
            Vector3.forward * (rodZ - flight.StartRodZ),
            Quaternion.AngleAxis(extraDegrees, Vector3.forward),
            Vector3.one);
        return flight.RodOnContainer * delta * flight.ContainerToRod;
    }
}

public sealed class MonolithNutFlightBoard
{
    readonly List<MonolithNutFlight> _flights = new List<MonolithNutFlight>();

    public int Count => _flights.Count;

    public MonolithNutFlight FlightAt(int index)
    {
        return _flights[index];
    }

    public int UnscrewingCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < _flights.Count; i++)
            {
                if (_flights[i].Phase == MonolithNutFlightPhase.Unscrewing)
                    count++;
            }

            return count;
        }
    }

    public bool IsRodAnimating(MonolithRodId rod)
    {
        for (int i = 0; i < _flights.Count; i++)
        {
            if (_flights[i].Rod.Equals(rod))
                return true;
        }

        return false;
    }

    public bool FaceHasHiddenNut(int faceIndex)
    {
        for (int i = 0; i < _flights.Count; i++)
        {
            if (_flights[i].Rod.Face == faceIndex)
                return true;
        }

        return false;
    }

    public bool IsHidden(int faceIndex, int rodOnFace, int slotIndex)
    {
        for (int i = 0; i < _flights.Count; i++)
        {
            MonolithNutFlight flight = _flights[i];
            if (flight.Rod.Face == faceIndex && flight.RodOnFace == rodOnFace && flight.SlotIndex == slotIndex)
                return true;
        }

        return false;
    }

    public void Add(MonolithNutFlight flight)
    {
        _flights.Add(flight);
    }

    public void CancelAll()
    {
        _flights.Clear();
    }

    public void Tick(float deltaTime)
    {
        for (int i = 0; i < _flights.Count; i++)
        {
            MonolithNutFlight flight = _flights[i];
            if (flight.Phase != MonolithNutFlightPhase.Unscrewing || flight.ReadyToCommit)
                continue;

            flight.Elapsed += deltaTime;
            if (flight.Elapsed >= flight.Duration)
                flight.ReadyToCommit = true;
        }
    }

    public void CopyReady(List<MonolithNutFlight> ready)
    {
        ready.Clear();
        for (int i = 0; i < _flights.Count; i++)
        {
            MonolithNutFlight flight = _flights[i];
            if (flight.Phase == MonolithNutFlightPhase.Unscrewing && flight.ReadyToCommit)
                ready.Add(flight);
        }
    }

    public bool TryBeginDetach(MonolithNutFlight flight)
    {
        if (flight == null || flight.Phase != MonolithNutFlightPhase.Unscrewing || !flight.ReadyToCommit)
            return false;

        flight.Phase = MonolithNutFlightPhase.Detaching;
        return true;
    }

    public void Release(MonolithNutFlight flight)
    {
        _flights.Remove(flight);
    }
}
