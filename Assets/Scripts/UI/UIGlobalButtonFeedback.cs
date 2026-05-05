using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 挂在菜单或 HUD 的 Canvas 根节点上：为本节点下所有子级 <see cref="Button"/> 增加点击音效，并可选自动挂上悬停缩放。
/// 在 Build 里都放入对应场景的 Canvas 即可覆盖该界面所有按钮，无需逐个绑定。
/// </summary>
public class UIGlobalButtonFeedback : MonoBehaviour
{
    [Header("Click")]
    [SerializeField] AudioClip clickSound;
    [SerializeField] [Range(0f, 1f)] float clickVolume = 1f;
    [SerializeField] bool wireClickSound = true;

    [Header("Hover")]
    [SerializeField] bool addHoverScale = true;
    [SerializeField] [Min(1f)] float hoverScaleMultiplier = 1.06f;

    AudioSource _audio;

    void Awake()
    {
        _audio = GetComponent<AudioSource>();
        if (_audio == null)
            _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.spatialBlend = 0f;

        var buttons = GetComponentsInChildren<Button>(true);
        foreach (var btn in buttons)
        {
            if (btn == null) continue;

            if (wireClickSound)
                btn.onClick.AddListener(PlayClick);

            if (addHoverScale && btn.GetComponent<UIButtonHoverScale>() == null)
            {
                var hover = btn.gameObject.AddComponent<UIButtonHoverScale>();
                hover.HoverMultiplier = hoverScaleMultiplier;
            }
        }
    }

    void OnDestroy()
    {
        var buttons = GetComponentsInChildren<Button>(true);
        foreach (var btn in buttons)
        {
            if (btn != null && wireClickSound)
                btn.onClick.RemoveListener(PlayClick);
        }
    }

    void PlayClick()
    {
        if (clickSound == null || _audio == null) return;
        _audio.PlayOneShot(clickSound, clickVolume);
    }
}
