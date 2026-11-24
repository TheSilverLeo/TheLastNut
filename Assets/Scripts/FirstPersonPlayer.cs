using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonPlayer : MonoBehaviour
{
    [Tooltip("Walk speed, sprint speed, look sensitivity, jump height, and gravity.")]
    [SerializeField] FirstPersonMovementSettings settings;

    [Tooltip("Main Camera. Mouse Y rotates this transform only. Its local position is the eye height.")]
    [SerializeField] Transform eye;

    [Tooltip("Capsule mesh on this player. Hidden during play so it does not block the view.")]
    [SerializeField] MeshRenderer bodyRenderer;

    [Tooltip("Extra collider on this player. The CharacterController is the only player collider.")]
    [SerializeField] CapsuleCollider redundantCollider;

    CharacterController _controller;
    float _yaw;
    float _pitch;
    float _verticalVelocity;
    bool _hidBody;

    public bool CapturedCursorThisFrame { get; private set; }

    void Awake()
    {
        _controller = GetComponent<CharacterController>();
        if (redundantCollider != null)
            redundantCollider.enabled = false;
    }

    void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        if (bodyRenderer != null)
        {
            bodyRenderer.enabled = false;
            _hidBody = true;
        }

        _yaw = transform.localEulerAngles.y;
        _pitch = SignedAngle(eye != null ? eye.localEulerAngles.x : 0f);
        ApplyLook();
        LockCursor();
    }

    void OnDisable()
    {
        if (_hidBody && bodyRenderer != null)
            bodyRenderer.enabled = true;

        _hidBody = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        if (!Application.isPlaying || settings == null || _controller == null)
            return;

        ReadCursor();
        if (Cursor.lockState == CursorLockMode.Locked)
            ReadLook();

        Vector2 move = ReadMove();
        bool sprint = ReadSprint();
        bool jump = ReadJump();
        Move(move, sprint, jump);
    }

    void ReadCursor()
    {
        CapturedCursorThisFrame = false;
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
        {
            LockCursor();
            CapturedCursorThisFrame = true;
        }
    }

    void ReadLook()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || eye == null)
            return;

        Vector2 delta = mouse.delta.ReadValue();
        float sensitivity = settings.MouseSensitivity;
        _yaw += delta.x * sensitivity;
        _pitch -= delta.y * sensitivity;
        _pitch = Mathf.Clamp(_pitch, settings.MinimumPitch, settings.MaximumPitch);
        ApplyLook();
    }

    void ApplyLook()
    {
        transform.localRotation = Quaternion.Euler(0f, _yaw, 0f);
        if (eye != null)
            eye.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
    }

    static Vector2 ReadMove()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return Vector2.zero;

        Vector2 move = Vector2.zero;
        if (keyboard.wKey.isPressed)
            move.y += 1f;
        if (keyboard.sKey.isPressed)
            move.y -= 1f;
        if (keyboard.dKey.isPressed)
            move.x += 1f;
        if (keyboard.aKey.isPressed)
            move.x -= 1f;

        if (move.sqrMagnitude > 1f)
            move.Normalize();

        return move;
    }

    static bool ReadSprint()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard.leftShiftKey.isPressed;
    }

    static bool ReadJump()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
    }

    void Move(Vector2 move, bool sprint, bool jump)
    {
        if (_controller.isGrounded && _verticalVelocity < 0f)
            _verticalVelocity = -settings.GroundedStickSpeed;

        if (_controller.isGrounded && jump)
            _verticalVelocity = settings.JumpSpeed;

        _verticalVelocity += settings.Gravity * Time.deltaTime;

        float speed = sprint ? settings.SprintSpeed : settings.WalkSpeed;
        Vector3 wish = (transform.right * move.x) + (transform.forward * move.y);
        Vector3 motion = wish * (speed * Time.deltaTime);
        motion.y = _verticalVelocity * Time.deltaTime;
        _controller.Move(motion);

        if (_controller.isGrounded && _verticalVelocity < 0f)
            _verticalVelocity = -settings.GroundedStickSpeed;
    }

    static void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    static float SignedAngle(float degrees)
    {
        if (degrees > 180f)
            degrees -= 360f;

        return degrees;
    }
}
