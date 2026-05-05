using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Slightly scales up a button on hover; restores scale on exit. Put on the same GameObject as a <see cref="Button"/> that receives pointer events.
/// May be added at runtime by <see cref="UIGlobalButtonFeedback"/>.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class UIButtonHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("Hover scale multiplier relative to original localScale (e.g. 1.06).")]
    [SerializeField] [Min(1f)] float hoverMultiplier = 1.06f;

    Button _button;
    RectTransform _rect;
    Vector3 _baseScale;

    public float HoverMultiplier
    {
        get => hoverMultiplier;
        set => hoverMultiplier = Mathf.Max(1f, value);
    }

    void Awake()
    {
        _button = GetComponent<Button>();
        _rect = transform as RectTransform;
        if (_rect != null)
            _baseScale = _rect.localScale;
    }

    void OnDisable()
    {
        ResetScale();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_rect == null) return;
        if (_button != null && !_button.interactable) return;

        _rect.localScale = _baseScale * hoverMultiplier;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ResetScale();
    }

    void ResetScale()
    {
        if (_rect != null)
            _rect.localScale = _baseScale;
    }
}
