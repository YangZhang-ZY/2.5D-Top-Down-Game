using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Put on a menu or HUD Canvas root: wires click sounds for all child <see cref="Button"/>s and optionally adds hover scale.
/// Include on each scene's Canvas in builds to cover every button without per-button setup.
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
