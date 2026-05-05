using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 鼠标悬停时略微放大按钮，移出后恢复。需挂在带 <see cref="Button"/> 且能接收指针的物体上（通常与 Button 同节点）。
/// 也可由 <see cref="UIGlobalButtonFeedback"/> 在运行时自动添加。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class UIButtonHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("相对原始 localScale 的悬停倍数，例如 1.06")]
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
