using UnityEngine;

public readonly struct RodGridPoint
{
    public RodGridPoint(int column, int row, Vector3 anchorLocalPosition)
    {
        Column = column;
        Row = row;
        AnchorLocalPosition = anchorLocalPosition;
    }

    public int Column { get; }
    public int Row { get; }
    public Vector3 AnchorLocalPosition { get; }
}

public static class RodTestGridLayout
{
    public static RodGridPoint[] Build(RodTestGridSettings settings, bool reverseColumnDirection)
    {
        return Build(
            settings.ColumnCount,
            settings.RowCount,
            settings.HorizontalSpacing,
            settings.VerticalSpacing,
            reverseColumnDirection);
    }

    public static RodGridPoint[] Build(
        int columnCount,
        int rowCount,
        float horizontalSpacing,
        float verticalSpacing,
        bool reverseColumnDirection)
    {
        var points = new RodGridPoint[columnCount * rowCount];
        float middleRow = (rowCount - 1) / 2f;
        float columnDirection = reverseColumnDirection ? -1f : 1f;
        int index = 0;

        for (int row = 0; row < rowCount; row++)
        {
            float vertical = (row - middleRow) * verticalSpacing;

            for (int column = 0; column < columnCount; column++)
            {
                float horizontal = column * horizontalSpacing * columnDirection;
                Vector3 anchorLocalPosition = (Vector3.right * horizontal) + (Vector3.up * vertical);
                points[index] = new RodGridPoint(column, row, anchorLocalPosition);
                index++;
            }
        }

        return points;
    }
}
