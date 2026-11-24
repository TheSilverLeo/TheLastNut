using UnityEngine;

[CreateAssetMenu(
    fileName = "RodTestGridSettings",
    menuName = "The Last Rivet/Rod Test Grid Settings")]
public class RodTestGridSettings : ScriptableObject
{
    [Tooltip("Rods across the face. Column 0 sits on the anchor.")]
    [SerializeField, Min(1)] int columnCount = 3;

    [Tooltip("Rods up the face. The middle row sits on the anchor.")]
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
