using UnityEngine;

public readonly struct NutSlot
{
    public NutSlot(int index, float rodLocalZ, float spinDegrees)
    {
        Index = index;
        RodLocalZ = rodLocalZ;
        SpinDegrees = spinDegrees;
    }

    public int Index { get; }
    public float RodLocalZ { get; }
    public float SpinDegrees { get; }
}

public static class RodNutLayout
{
    public static bool TryBuild(RodNutTestSettings settings, float rodLength, out NutSlot[] slots, out string error)
    {
        return TryBuild(settings, rodLength, settings.RotationSeed, out slots, out error);
    }

    public static bool TryBuild(RodNutTestSettings settings, float rodLength, int rotationSeed, out NutSlot[] slots, out string error)
    {
        slots = null;
        error = null;

        if (settings.NutCount < 1)
        {
            error = "Nut count must be at least 1.";
            return false;
        }

        if (settings.MaximumSpinDegrees < settings.MinimumSpinDegrees)
        {
            error = "Maximum spin must be at least the minimum spin.";
            return false;
        }

        if (rodLength <= 0f)
        {
            error = "Rod_Tip must sit on this rod's local +Z axis, beyond the base.";
            return false;
        }

        float halfThickness = settings.NutThickness * 0.5f;
        float pitch = settings.NutThickness + settings.Gap;
        float firstCenter = settings.EndClearance + halfThickness;
        float lastCenter = firstCenter + ((settings.NutCount - 1) * pitch);
        float outerEdge = lastCenter + halfThickness;

        if (outerEdge > rodLength)
        {
            error = "The nuts extend past Rod_Tip. Check the nut settings and Rod_Tip.";
            return false;
        }

        var random = new System.Random(rotationSeed);
        float spinRange = settings.MaximumSpinDegrees - settings.MinimumSpinDegrees;
        slots = new NutSlot[settings.NutCount];

        for (int i = 0; i < settings.NutCount; i++)
        {
            float rodLocalZ = firstCenter + (i * pitch);
            float spinDegrees = settings.MinimumSpinDegrees + ((float)random.NextDouble() * spinRange);
            slots[i] = new NutSlot(i, rodLocalZ, spinDegrees);
        }

        return true;
    }
}
