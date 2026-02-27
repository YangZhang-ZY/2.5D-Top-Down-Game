using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 血条 UI。绑定 Health 组件，自动更新血条显示。
/// 支持白色伤害延迟条：受伤后白色部分会滑动缩小，不会直接变短。
///
/// 使用步骤：
/// 1. 创建 Canvas（敌人用 World Space，玩家用 Screen Space）
/// 2. 层级（从下到上）：Background → DamageFill（白）→ Fill（红/绿）
/// 3. Fill 和 DamageFill 的 Image Type=Filled，Fill Method=Horizontal，Fill Origin=Left
/// 4. 挂载本脚本，拖入 health、fillImage，可选 damageFillImage
/// </summary>
public class HealthBar : MonoBehaviour
{
    [Header("绑定")]
    [Tooltip("要显示血条的对象，留空则从父物体获取")]
    public Health health;
    [Tooltip("血条填充图（Image，Type=Filled，Fill Method=Horizontal，Fill Origin=Left）")]
    public Image fillImage;
    [Tooltip("白色伤害延迟条，受伤后从此值滑动缩小。留空则无此效果")]
    public Image damageFillImage;

    [Header("伤害动画")]
    [Tooltip("白色条缩小速度（fillAmount/秒）")]
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

    /// <summary>根据当前 HP 刷新血条显示</summary>
    public void Refresh()
    {
        if (health == null || fillImage == null) return;
        fillImage.fillAmount = (float)health.CurrentHP / health.maxHP;
    }
}
