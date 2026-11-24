using UnityEngine;

[CreateAssetMenu(
    fileName = "FirstPersonMovementSettings",
    menuName = "The Last Rivet/First Person Movement Settings")]
public class FirstPersonMovementSettings : ScriptableObject
{
    [Tooltip("Metres per second while walking.")]
    [SerializeField, Min(0f)] float walkSpeed = 4f;

    [Tooltip("Metres per second while Left Shift is held.")]
    [SerializeField, Min(0f)] float sprintSpeed = 6.5f;

    [Tooltip("Degrees of look rotation for each pixel the mouse moves.")]
    [SerializeField, Min(0f)] float mouseSensitivity = 0.12f;

    [Tooltip("Metres the jump rises above the ground.")]
    [SerializeField, Min(0f)] float jumpHeight = 1.2f;

    [Tooltip("Metres per second squared. Negative pulls the player down.")]
    [SerializeField] float gravity = -20f;

    [Tooltip("Lowest camera pitch in degrees. Negative looks down.")]
    [SerializeField] float minimumPitch = -85f;

    [Tooltip("Highest camera pitch in degrees. Positive looks up.")]
    [SerializeField] float maximumPitch = 85f;

    [Tooltip("Downward speed applied while grounded so the controller stays on the floor.")]
    [SerializeField, Min(0f)] float groundedStickSpeed = 2f;

    public float WalkSpeed => walkSpeed;
    public float SprintSpeed => sprintSpeed;
    public float MouseSensitivity => mouseSensitivity;
    public float JumpHeight => jumpHeight;
    public float Gravity => gravity;
    public float MinimumPitch => minimumPitch;
    public float MaximumPitch => maximumPitch;
    public float GroundedStickSpeed => groundedStickSpeed;

    public float JumpSpeed
    {
        get
        {
            float downward = gravity < 0f ? -gravity : gravity;
            return Mathf.Sqrt(2f * jumpHeight * downward);
        }
    }
}
