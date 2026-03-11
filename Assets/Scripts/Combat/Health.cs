using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 生命值组件。挂到任何需要受伤的对象上（敌人、可破坏物、玩家）。
/// 实现 IDamageable 接口，与伤害系统对接。
///
/// 使用步骤：
/// 1. 给 GameObject 添加 Health 组件
/// 2. 设置 maxHP
/// 3. 确保该物体或其子物体有 Collider2D（用于被攻击检测）
/// 4. 可选：在 OnDeath 事件中绑定死亡逻辑
/// </summary>
/// <summary>
/// 注意：Health 所在物体或其子物体需要有 Collider2D，否则攻击无法检测到。
/// </summary>
public class Health : MonoBehaviour, IDamageable
{
    [Header("生命值设置")]
    [Tooltip("最大生命值")]
    public int maxHP = 3;

    [Tooltip("当前生命值（运行时，Inspector 中可查看）")]
    [SerializeField] private int _currentHP;

    [Header("无敌设置")]
    [Tooltip("是否忽略伤害（如玩家冲刺时）")]
    public bool ignoreDamage;

    [Tooltip("受伤后无敌帧时长，0 表示无")]
    public float invincibleDuration = 0f;

    [Header("事件（可在 Inspector 中绑定）")]
    [Tooltip("受到伤害时触发，参数为实际伤害值")]
    public UnityEvent<float> OnDamaged;

    /// <summary>受到伤害时触发，参数为完整 DamageInfo（用于 EnemyBase 击退等）。仅代码订阅。</summary>
    public event System.Action<DamageInfo> OnDamagedWithInfo;

    [Tooltip("死亡时触发")]
    public UnityEvent OnDeath;

    /// <summary>当前是否无敌（受伤后的无敌帧内）</summary>
    private float _invincibleTimer;

    /// <summary>当前生命值</summary>
    public int CurrentHP => _currentHP;

    /// <summary>是否已死亡</summary>
    public bool IsDead => _currentHP <= 0;

    private void Awake()
    {
        _currentHP = maxHP;
    }

    private void Update()
    {
        if (_invincibleTimer > 0f)
            _invincibleTimer -= Time.deltaTime;
    }

    /// <summary>
    /// 受到伤害。由 AttackHitbox 或其他伤害源调用。
    /// </summary>
    public bool TakeDamage(DamageInfo info)
    {
        if (IsDead) return false;
        if (ignoreDamage) return false;
        if (_invincibleTimer > 0f) return false;

        _currentHP -= Mathf.RoundToInt(info.amount);
        _currentHP = Mathf.Max(0, _currentHP);

        _invincibleTimer = invincibleDuration;

        OnDamaged?.Invoke(info.amount);
        OnDamagedWithInfo?.Invoke(info);

        if (IsDead)
            OnDeath?.Invoke();

        return true;
    }

    /// <summary>
    /// 外部设置 ignoreDamage（如玩家冲刺时设为 true）
    /// </summary>
    public void SetIgnoreDamage(bool ignore)
    {
        ignoreDamage = ignore;
    }

    /// <summary>
    /// 重置生命值（如玩家重生）
    /// </summary>
    public void ResetHP()
    {
        _currentHP = maxHP;
        _invincibleTimer = 0f;
    }
}
