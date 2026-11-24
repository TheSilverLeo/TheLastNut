using UnityEngine;

[CreateAssetMenu(
    fileName = "MonolithNutUnscrewingSettings",
    menuName = "The Last Rivet/Nut Unscrewing Settings")]
public class MonolithNutUnscrewingSettings : ScriptableObject
{
    [Tooltip("Metres per second along the rod's local +Z, from the nut's start toward the tip.")]
    [SerializeField, Min(0.01f)] float movementSpeed = 0.4f;

    [Tooltip("Extra degrees per second around the rod's local +Z. Negative spins the other way.")]
    [SerializeField] float rotationSpeed = 720f;

    [Tooltip("Seconds. A nut close to the tip still takes at least this long, so the spin stays visible.")]
    [SerializeField, Min(0.05f)] float minimumDuration = 0.8f;

    [Tooltip("Kilograms. A small solid steel nut.")]
    [SerializeField, Min(0.01f)] float mass = 0.4f;

    [Tooltip("0 is no bounce. 1 keeps all speed. Combined with the floor.")]
    [SerializeField, Range(0f, 1f)] float bounciness = 0.2f;

    [Tooltip("Static and dynamic friction. Higher values stop the roll sooner.")]
    [SerializeField, Min(0f)] float friction = 0.45f;

    [Tooltip("Metres per second along the rod's outward axis when the nut leaves the tip.")]
    [SerializeField, Min(0f)] float outwardVelocity = 0.5f;

    [Tooltip("Degrees per second around the rod's outward axis when the nut leaves the tip.")]
    [SerializeField] float angularVelocity = 720f;

    public float MovementSpeed => movementSpeed;
    public float RotationSpeed => rotationSpeed;
    public float MinimumDuration => minimumDuration;
    public float Mass => mass;
    public float Bounciness => bounciness;
    public float Friction => friction;
    public float OutwardVelocity => outwardVelocity;
    public float AngularVelocity => angularVelocity;
}
