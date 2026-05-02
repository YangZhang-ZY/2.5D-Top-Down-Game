using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 架势（韧性）：每次扣血同时扣架势；仅当本击<b>破防</b>（架势从大于 0 被打到 0）时应付完整受击；
/// 跌破后至 Refill 前不再触发破防反应，但仍可继续扣血。
/// 未挂载、maxPosture≤0 或 disabled 时由调用方视为「每击都破防」（兼容旧逻辑）。
/// </summary>
public class CombatPosture : MonoBehaviour
{
    [Tooltip("最大架势；设为 0 或关闭本组件则不做架势判定（等价于每下都破防）。")]
    [SerializeField] float maxPosture = 100f;

    [SerializeField] float currentPosture;

    [Tooltip("每 1 点生命值伤害对应扣除的架势。")]
    [SerializeField] float postureLossPerHpDamage = 8f;

    [Tooltip("破防后超过此秒数才开始回复架势（受击会重置计时）。")]
    [SerializeField] float regenDelayAfterHit = 1.25f;

    [Tooltip("每秒回复架势（仅限未在「架势已空且未补满」的锁定流程中时；也可由 RefillPosture 一次性回满）。")]
    [SerializeField] float postureRegenPerSecond = 12f;

    [Tooltip("当前架势 / 最大架势（供 UI 绑定）。")]
    public UnityEvent<float, float> OnPostureRatioChanged;

    /// <summary>本击是否算破防（用于击退等，与 OnDamaged 同帧先于 OnDamagedWithInfo 设置）。</summary>
    public bool LastHitBrokePosture { get; private set; }

    public float MaxPosture => maxPosture;
    public float CurrentPosture => currentPosture;

    float _regenTimer;

    void Awake()
    {
        if (maxPosture > 0f)
            currentPosture = maxPosture;
        EmitRatio();
    }

    void Update()
    {
        if (!enabled || maxPosture <= 0f) return;
        if (currentPosture >= maxPosture) return;

        _regenTimer += Time.deltaTime;
        if (_regenTimer < regenDelayAfterHit) return;

        currentPosture = Mathf.Min(maxPosture, currentPosture + postureRegenPerSecond * Time.deltaTime);
        EmitRatio();
    }

    /// <summary>
    /// 受到与生命值挂钩的伤害时调用。返回是否应触发完整受击（破防或旧模式）。
    /// </summary>
    public bool ApplyPostureDamageFromHp(float hpDamage)
    {
        LastHitBrokePosture = false;

        if (!enabled || maxPosture <= 0f)
        {
            LastHitBrokePosture = true;
            return true;
        }

        if (currentPosture <= 0f)
            return false;

        _regenTimer = 0f;
        float prev = currentPosture;
        currentPosture -= Mathf.Abs(hpDamage) * postureLossPerHpDamage;
        if (currentPosture <= 0f)
        {
            currentPosture = 0f;
            LastHitBrokePosture = prev > 0f;
            EmitRatio();
            return LastHitBrokePosture;
        }

        EmitRatio();
        return false;
    }

    /// <summary>破防流程结束后将架势回满（如受击 Recovery 结束）。</summary>
    public void RefillPosture()
    {
        if (maxPosture <= 0f) return;
        currentPosture = maxPosture;
        _regenTimer = 0f;
        EmitRatio();
    }

    void EmitRatio()
    {
        if (maxPosture <= 0f)
            OnPostureRatioChanged?.Invoke(1f, 1f);
        else
            OnPostureRatioChanged?.Invoke(currentPosture, maxPosture);
    }
}
