using UnityEngine;

[CreateAssetMenu(
    fileName = "RodNutTestSettings",
    menuName = "The Last Rivet/Rod Nut Test Settings")]
public class RodNutTestSettings : ScriptableObject
{
    [Tooltip("Nuts on the test rod, including the reference nut.")]
    [SerializeField, Min(1)] int nutCount = 32;

    [Tooltip("Metres of nut thickness along the rod's local +Z axis.")]
    [SerializeField, Min(0f)] float nutThickness = 0.06f;

    [Tooltip("Metres of empty space between adjacent nuts.")]
    [SerializeField, Min(0f)] float gap = 0.002f;

    [Tooltip("Metres of clearance between the rod ends and the outer faces of the first and last nuts.")]
    [SerializeField, Min(0f)] float endClearance = 0.009f;

    [Tooltip("Minimum spin around the rod's local +Z, in degrees.")]
    [SerializeField] float minimumSpinDegrees = 0f;

    [Tooltip("Maximum spin around the rod's local +Z, in degrees.")]
    [SerializeField] float maximumSpinDegrees = 360f;

    [Tooltip("Same seed produces the same spins every time you generate.")]
    [SerializeField] int rotationSeed = 32;

    public int NutCount => nutCount;
    public float NutThickness => nutThickness;
    public float Gap => gap;
    public float EndClearance => endClearance;
    public float MinimumSpinDegrees => minimumSpinDegrees;
    public float MaximumSpinDegrees => maximumSpinDegrees;
    public int RotationSeed => rotationSeed;
}
