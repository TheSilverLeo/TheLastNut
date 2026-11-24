using UnityEngine;

[CreateAssetMenu(
    fileName = "MonolithBandPreviewSettings",
    menuName = "The Last Rivet/Monolith Band Preview Settings")]
public class MonolithBandPreviewSettings : ScriptableObject
{
    [Tooltip("Rods along the anchor's local X axis. Column 0 sits on the anchor.")]
    [SerializeField, Min(1)] int columnCount = 32;

    [Tooltip("Rods along the anchor's local Y axis. The middle row sits on the anchor.")]
    [SerializeField, Min(1)] int rowCount = 3;

    [Tooltip("Metres between columns along the anchor's local X axis.")]
    [SerializeField, Min(0f)] float horizontalSpacing = 0.95f;

    [Tooltip("Metres between rows along the anchor's local Y axis.")]
    [SerializeField, Min(0f)] float verticalSpacing = 1f;

    public int ColumnCount => columnCount;
    public int RowCount => rowCount;
    public float HorizontalSpacing => horizontalSpacing;
    public float VerticalSpacing => verticalSpacing;
}
