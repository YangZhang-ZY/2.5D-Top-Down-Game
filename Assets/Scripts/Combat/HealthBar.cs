using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI bar driven by <see cref="Health"/>. Optional delayed white "damage" segment shrinks after hits.
///
/// Setup:
/// 1. Canvas (world or screen space as needed).
/// 2. Layer order bottom to top: background, optional white damage fill, coloured HP fill.
/// 3. Filled images: Horizontal fill from Left.
/// 4. Assign health, fillImage, optional damageFillImage.
/// </summary>
public class HealthBar : MonoBehaviour
{
    [Header("Bindings")]
    [Tooltip("Health to display; defaults to parent if empty.")]
    public Health health;
    [Tooltip("Main HP fill (Image, Type Filled, Horizontal, Left).")]
    public Image fillImage;
    [Tooltip("Optional delayed damage strip; leave empty to disable.")]
    public Image damageFillImage;

    [Header("Damage strip")]
    [Tooltip("How fast the white strip catches up (fillAmount per second).")]
    public float damageShrinkSpeed = 2f;

    private float _damageFillAmount;

    private void Start()
    {
        if (health == null) health = GetComponentInParent<Health>();
        if (fillImage == null) fillImage = GetComponentInChildren<Image>();

        if (health != null && fillImage != null)
        {
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            if (damageFillImage != null)
            {
                damageFillImage.type = Image.Type.Filled;
                damageFillImage.fillMethod = Image.FillMethod.Horizontal;
                damageFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
                damageFillImage.gameObject.SetActive(false);
            }
            Refresh();
            _damageFillAmount = fillImage.fillAmount;
            health.OnDamaged.AddListener(OnHealthDamaged);
            health.OnDeath.AddListener(OnHealthDeath);
        }
    }

    private void Update()
    {
        if (damageFillImage == null) return;
        if (_damageFillAmount > fillImage.fillAmount)
        {
            _damageFillAmount = Mathf.MoveTowards(_damageFillAmount, fillImage.fillAmount, damageShrinkSpeed * Time.deltaTime);
            damageFillImage.fillAmount = _damageFillAmount;
            damageFillImage.gameObject.SetActive(true);
        }
        else
        {
            damageFillImage.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDamaged.RemoveListener(OnHealthDamaged);
            health.OnDeath.RemoveListener(OnHealthDeath);
        }
    }

    private void OnHealthDamaged(float _)
    {
        float prevFill = fillImage.fillAmount;
        Refresh();
        if (damageFillImage != null)
            _damageFillAmount = prevFill;
    }

    private void OnHealthDeath() => gameObject.SetActive(false);

    /// <summary>Updates fills from current HP.</summary>
    public void Refresh()
    {
        if (health == null || fillImage == null) return;
        fillImage.fillAmount = (float)health.CurrentHP / health.maxHP;
    }
}
