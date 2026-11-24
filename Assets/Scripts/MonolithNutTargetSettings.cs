using UnityEngine;

[CreateAssetMenu(
    fileName = "MonolithNutTargetSettings",
    menuName = "The Last Rivet/Nut Target Settings")]
public class MonolithNutTargetSettings : ScriptableObject
{
    [Tooltip("Metres. The crosshair can select a nut only when the rod surface is within this distance.")]
    [SerializeField, Min(0.1f)] float range = 4f;

    [Tooltip("Amber tint colour. The transparency sliders below control how much of the rusty metal shows through.")]
    [SerializeField] Color primaryHighlight = new Color(1f, 0.45f, 0.08f, 1f);

    [Tooltip("Softer amber for nuts a later upgrade adds around the primary target.")]
    [SerializeField] Color additionalHighlight = new Color(1f, 0.62f, 0.28f, 1f);

    [Tooltip("How opaque the primary tint is. 0.16 leaves the rusty metal clearly visible.")]
    [SerializeField, Range(0f, 1f)] float primaryAlpha = 0.16f;

    [Tooltip("How opaque the extra tint is for nuts added by a later upgrade.")]
    [SerializeField, Range(0f, 1f)] float additionalAlpha = 0.08f;

    [Tooltip("Metres. A small push toward the camera keeps the tint on the metal without covering the hole or flickering.")]
    [SerializeField, Min(0f)] float surfaceOffset = 0.0004f;

    public float Range => range;
    public Color PrimaryHighlight => primaryHighlight;
    public Color AdditionalHighlight => additionalHighlight;
    public float PrimaryAlpha => primaryAlpha;
    public float AdditionalAlpha => additionalAlpha;
    public float SurfaceOffset => surfaceOffset;
}
