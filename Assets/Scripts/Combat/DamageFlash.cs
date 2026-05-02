using System.Collections;
using UnityEngine;

/// <summary>
/// 订阅 <see cref="Health.OnDamaged"/>：在 <see cref="SpriteRenderer"/> 上闪一下高亮。
/// </summary>
[DisallowMultipleComponent]
public class DamageFlash : MonoBehaviour
{
    [SerializeField] Health health;

    [Tooltip("闪一下时叠加 toward 的颜色。")]
    [SerializeField] Color flashColor = Color.white;

    [Tooltip("整段闪大约这么多秒。")]
    [SerializeField] float flashDuration = 0.12f;

    [SerializeField] bool includeChildren = true;

    SpriteRenderer[] _renderers;
    Color[] _originalColors;
    Coroutine _flashRoutine;

    void Awake()
    {
        if (health == null)
            health = GetComponent<Health>() ?? GetComponentInParent<Health>();
        CacheRenderersAndColors();
    }

    void OnEnable()
    {
        if (health != null)
            health.OnDamaged.AddListener(OnDamaged);
    }

    void OnDisable()
    {
        if (health != null)
            health.OnDamaged.RemoveListener(OnDamaged);

        if (_flashRoutine != null)
        {
            StopCoroutine(_flashRoutine);
            _flashRoutine = null;
        }

        RestoreColors();
    }

    void OnDamaged(float _)
    {
        if (_renderers == null || _renderers.Length == 0) return;
        if (_flashRoutine != null)
            StopCoroutine(_flashRoutine);
        _flashRoutine = StartCoroutine(FlashRoutine());
    }

    void CacheRenderersAndColors()
    {
        _renderers = includeChildren
            ? GetComponentsInChildren<SpriteRenderer>(true)
            : GetComponents<SpriteRenderer>();
        if (_renderers == null || _renderers.Length == 0) return;
        _originalColors = new Color[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
            _originalColors[i] = _renderers[i].color;
    }

    IEnumerator FlashRoutine()
    {
        float half = Mathf.Max(0.01f, flashDuration * 0.5f);
        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            BlendColors(Mathf.Clamp01(t / half));
            yield return null;
        }

        BlendColors(1f);
        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            BlendColors(1f - Mathf.Clamp01(t / half));
            yield return null;
        }

        RestoreColors();
        _flashRoutine = null;
    }

    void BlendColors(float flashWeight)
    {
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;
            _renderers[i].color = Color.Lerp(_originalColors[i], flashColor, flashWeight);
        }
    }

    void RestoreColors()
    {
        if (_renderers == null || _originalColors == null) return;
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;
            _renderers[i].color = _originalColors[i];
        }
    }
}
