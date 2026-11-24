public readonly struct MonolithRodId : System.IEquatable<MonolithRodId>
{
    public MonolithRodId(int face, int column, int row)
    {
        Face = face;
        Column = column;
        Row = row;
    }

    public int Face { get; }
    public int Column { get; }
    public int Row { get; }

    public bool Equals(MonolithRodId other)
    {
        return Face == other.Face && Column == other.Column && Row == other.Row;
    }

    public override bool Equals(object obj)
    {
        return obj is MonolithRodId other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = Face;
            hash = (hash * 397) ^ Column;
            hash = (hash * 397) ^ Row;
            return hash;
        }
    }
}

public readonly struct MonolithNutId : System.IEquatable<MonolithNutId>
{
    public MonolithNutId(int face, int column, int row, int nutIndex)
    {
        Face = face;
        Column = column;
        Row = row;
        NutIndex = nutIndex;
    }

    public int Face { get; }
    public int Column { get; }
    public int Row { get; }
    public int NutIndex { get; }
    public MonolithRodId Rod => new MonolithRodId(Face, Column, Row);

    public bool Equals(MonolithNutId other)
    {
        return Face == other.Face && Column == other.Column && Row == other.Row && NutIndex == other.NutIndex;
    }

    public override bool Equals(object obj)
    {
        return obj is MonolithNutId other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = Face;
            hash = (hash * 397) ^ Column;
            hash = (hash * 397) ^ Row;
            hash = (hash * 397) ^ NutIndex;
            return hash;
        }
    }
}

public enum MonolithNutTargetType
{
    None,
    Attached,
    Unscrewing,
    Detached
}

public sealed class MonolithNutState
{
    readonly ushort[] _remaining;
    readonly int _faceCount;
    readonly int _columnCount;
    readonly int _rowCount;
    readonly int _nutsPerRod;
    readonly int _rodsPerFace;
    int _removedCount;

    public MonolithNutState(int faceCount, int columnCount, int rowCount, int nutsPerRod)
    {
        if (faceCount < 1 || columnCount < 1 || rowCount < 1 || nutsPerRod < 1 || nutsPerRod > ushort.MaxValue)
            throw new System.ArgumentOutOfRangeException(nameof(nutsPerRod));

        _faceCount = faceCount;
        _columnCount = columnCount;
        _rowCount = rowCount;
        _nutsPerRod = nutsPerRod;
        _rodsPerFace = columnCount * rowCount;
        _remaining = new ushort[faceCount * _rodsPerFace];
        Reset();
    }

    public int FaceCount => _faceCount;
    public int ColumnCount => _columnCount;
    public int RowCount => _rowCount;
    public int NutsPerRod => _nutsPerRod;
    public int RodCount => _remaining.Length;
    public int RemovedCount => _removedCount;

    public bool Matches(int faceCount, int columnCount, int rowCount, int nutsPerRod)
    {
        return _faceCount == faceCount
            && _columnCount == columnCount
            && _rowCount == rowCount
            && _nutsPerRod == nutsPerRod;
    }

    public bool Contains(MonolithRodId rod)
    {
        return rod.Face >= 0
            && rod.Face < _faceCount
            && rod.Column >= 0
            && rod.Column < _columnCount
            && rod.Row >= 0
            && rod.Row < _rowCount;
    }

    public int RodIndexOnFace(MonolithRodId rod)
    {
        if (!Contains(rod))
            return -1;

        return (rod.Row * _columnCount) + rod.Column;
    }

    public int Remaining(MonolithRodId rod)
    {
        if (!TryIndex(rod, out int index))
            return 0;

        return _remaining[index];
    }

    public bool FaceHasRemoval(int faceIndex)
    {
        if (faceIndex < 0 || faceIndex >= _faceCount)
            return false;

        int start = faceIndex * _rodsPerFace;
        int end = start + _rodsPerFace;
        ushort full = (ushort)_nutsPerRod;
        for (int i = start; i < end; i++)
        {
            if (_remaining[i] != full)
                return true;
        }

        return false;
    }

    // Slot 0 sits nearest the wall. The last slot is the outermost nut, nearest the tip.
    // A remaining count of N keeps slots 0..N-1.
    public bool IsSlotPresent(MonolithRodId rod, int slotIndex)
    {
        if (slotIndex < 0 || !TryIndex(rod, out int index))
            return false;

        return slotIndex < _remaining[index];
    }

    public bool TryRemoveOutermost(MonolithRodId rod)
    {
        if (!TryIndex(rod, out int index) || _remaining[index] == 0)
            return false;

        _remaining[index]--;
        _removedCount++;
        return true;
    }

    public void Reset()
    {
        ushort full = (ushort)_nutsPerRod;
        for (int i = 0; i < _remaining.Length; i++)
            _remaining[i] = full;

        _removedCount = 0;
    }

    public static void FaceCoordinates(int rodIndexOnFace, int columnCount, out int column, out int row)
    {
        column = rodIndexOnFace % columnCount;
        row = rodIndexOnFace / columnCount;
    }

    bool TryIndex(MonolithRodId rod, out int index)
    {
        if (!Contains(rod))
        {
            index = -1;
            return false;
        }

        index = ((rod.Face * _rowCount) + rod.Row) * _columnCount + rod.Column;
        return true;
    }
}
