using UnityEngine;
using UnityEngine.InputSystem;

// Hold to unscrew the outermost nut on one rod. Later upgrades can widen the
// rod set or the nuts started per step; the flight board still plays each nut.
[DisallowMultipleComponent]
[DefaultExecutionOrder(60)]
public class MonolithNutInteraction : MonoBehaviour
{
    const float NoticeSeconds = 1.2f;
    const string OuterFirst = "REMOVE OUTER NUTS FIRST";

    [Tooltip("Crosshair target. Interaction never changes which nut is highlighted until a hold has started.")]
    [SerializeField] MonolithNutTargeting targeting;

    [Tooltip("Existing unscrew, removal, and physics entry point.")]
    [SerializeField] MonolithFourFaceBandPreviewRenderer band;

    [Tooltip("Amber tint. During a hold it stays on the nut that is unscrewing.")]
    [SerializeField] MonolithNutHighlighter highlights;

    [Tooltip("Cursor lock. The click that captures the cursor does not start an unscrew.")]
    [SerializeField] FirstPersonPlayer player;

    bool _heldLastFrame;
    bool _armed;
    bool _waiting;
    bool _ignoreHoldUntilRelease;
    int _startedRemaining;
    float _noticeUntil;
    MonolithRodId _lockedRod;
    Texture2D _pixel;
    GUIStyle _noticeStyle;

    public bool IsHoldActive => _heldLastFrame;
    public bool IsInteractionLocked => _waiting;
    public MonolithRodId LockedRod => _lockedRod;

    void OnDisable()
    {
        _waiting = false;
        _armed = false;
        _heldLastFrame = false;
        _ignoreHoldUntilRelease = false;
        if (_pixel != null)
        {
            Destroy(_pixel);
            _pixel = null;
        }
    }

    void Update()
    {
        if (!Application.isPlaying || targeting == null || band == null || player == null)
        {
            _waiting = false;
            return;
        }

        bool hold = WantsHold();
        if (hold && !_heldLastFrame)
            _armed = true;
        if (!hold)
            _armed = false;

        _heldLastFrame = hold;

        if (_waiting)
        {
            if (band.IsRodAnimating(_lockedRod))
            {
                FollowUnscrewingNut();
                return;
            }

            bool removed = band.NutState != null && band.NutState.Remaining(_lockedRod) < _startedRemaining;
            bool stillOnRod = hold && targeting.HasAimedRod && targeting.AimedRod.Equals(_lockedRod);
            if (removed && stillOnRod && band.TryAnimateOneNut(_lockedRod))
            {
                _startedRemaining = band.NutState != null ? band.NutState.Remaining(_lockedRod) : 0;
                FollowUnscrewingNut();
                return;
            }

            _waiting = false;
        }

        if (!_armed || !targeting.HasValidNutTarget)
            return;

        if (targeting.TargetType != MonolithNutTargetType.Attached
            && targeting.TargetType != MonolithNutTargetType.Unscrewing)
            return;

        MonolithNutId nut = targeting.Target;
        if (!IsOutermost(band.NutState, nut))
        {
            _noticeUntil = Time.unscaledTime + NoticeSeconds;
            return;
        }

        if (band.IsRodAnimating(nut.Rod) || !band.TryAnimateOneNut(nut.Rod))
            return;

        _lockedRod = nut.Rod;
        _startedRemaining = band.NutState != null ? band.NutState.Remaining(nut.Rod) : 0;
        _waiting = true;
        _armed = false;
        FollowUnscrewingNut();
    }

    void OnGUI()
    {
        if (!Application.isPlaying || Event.current.type != EventType.Repaint)
            return;

        Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        if (_waiting && band != null && band.TryGetUnscrewingProgress(_lockedRod, out float progress))
            DrawProgress(center, progress);

        if (Time.unscaledTime > _noticeUntil)
            return;

        EnsureStyle();
        GUI.Label(new Rect(center.x - 140f, center.y + 34f, 280f, 18f), OuterFirst, _noticeStyle);
    }

    bool WantsHold()
    {
        if (player.CapturedCursorThisFrame)
            _ignoreHoldUntilRelease = true;

        Mouse mouse = Mouse.current;
        bool pressed = mouse != null && mouse.leftButton.isPressed;
        if (!pressed)
            _ignoreHoldUntilRelease = false;

        if (_ignoreHoldUntilRelease || Cursor.lockState != CursorLockMode.Locked)
            return false;

        return pressed;
    }

    void FollowUnscrewingNut()
    {
        if (highlights == null || !band.TryGetUnscrewingNut(_lockedRod, out int nutIndex, out _))
            return;

        var nut = new MonolithNutId(_lockedRod.Face, _lockedRod.Column, _lockedRod.Row, nutIndex);
        targeting.PresentHeldNut(nut);
        if (highlights != null)
            highlights.SetHighlights(true, nut, null, MonolithNutTargetType.Unscrewing, null);
    }

    static bool IsOutermost(MonolithNutState state, MonolithNutId nut)
    {
        if (state == null)
            return false;

        int remaining = state.Remaining(nut.Rod);
        return remaining > 0 && nut.NutIndex == remaining - 1;
    }

    void DrawProgress(Vector2 center, float progress)
    {
        EnsurePixel();
        const float width = 36f;
        const float height = 3f;
        float x = center.x - (width * 0.5f);
        float y = center.y + 22f;
        GUI.color = new Color(0f, 0f, 0f, 0.45f);
        GUI.DrawTexture(new Rect(x, y, width, height), _pixel);
        GUI.color = new Color(1f, 0.55f, 0.12f, 0.9f);
        GUI.DrawTexture(new Rect(x, y, width * Mathf.Clamp01(progress), height), _pixel);
        GUI.color = Color.white;
    }

    void EnsurePixel()
    {
        if (_pixel != null)
            return;

        _pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        _pixel.SetPixel(0, 0, Color.white);
        _pixel.Apply();
        _pixel.hideFlags = HideFlags.HideAndDontSave;
    }

    void EnsureStyle()
    {
        if (_noticeStyle != null)
            return;

        _noticeStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 12
        };
        _noticeStyle.normal.textColor = new Color(1f, 0.78f, 0.45f, 0.95f);
    }
}
