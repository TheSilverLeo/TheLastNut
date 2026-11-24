using UnityEngine;

// Lives on the physical nut that owns the collider. Targeting reads this instead of the object name.
[DisallowMultipleComponent]
public sealed class MonolithDetachedNutIdentity : MonoBehaviour
{
    MeshRenderer _visual;

    public bool HasIdentity { get; private set; }
    public MonolithNutId Nut { get; private set; }
    public MeshRenderer Visual => _visual;

    public void Assign(MonolithNutId nut, MeshRenderer visual)
    {
        Nut = nut;
        _visual = visual;
        HasIdentity = visual != null;
    }

    public void Clear()
    {
        HasIdentity = false;
        _visual = null;
    }
}
