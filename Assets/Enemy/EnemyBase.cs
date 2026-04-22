using UnityEngine;

/// <summary>
/// 敌人通用基类。
///
/// 【功能】
/// - 塔防目标：首要目标（如水晶）优先；玩家挑衅或被打后仇恨玩家；脱战后回到首要目标
/// - 自动查找玩家（Tag Player）
/// - 攻击冷却管理
/// - 受击时：播放 Hit 动画、音效、闪白（可扩展）、击退
/// - 死亡时：停止移动、播放死亡动画、关闭碰撞
///
/// 【依赖组件】
/// - 同物体上必须有：Health、Rigidbody2D
/// - 自己或子物体上应有：Animator（用于 Idle/Run/Hit/Death 等）
///
/// 【子类需要做的】
/// - 重写 UpdateAI()：决定什么时候 Idle / 追击 / 攻击 / 治疗
/// - 重写 PerformAttack()：具体攻击方式（近战挥砍 / 远程发射 / 治疗）
///
/// 【使用步骤】
/// 1. 新建脚本继承 EnemyBase（如 WarriorEnemy）
/// 2. 实现 UpdateAI 和 PerformAttack
/// 3. 给敌人物体添加 Health、Rigidbody2D、子类脚本
/// 4. 确保 Player 的 Tag 是 "Player"
/// </summary>
[RequireComponent(typeof(Health), typeof(Rigidbody2D))]
public abstract class EnemyBase : MonoBehaviour
{
    // ==================== 一、Inspector 可调参数 ====================

    [Header("塔防目标（水晶等）")]
    [Tooltip("首要目标（基地水晶 Transform）。未赋值时可在 Start 中通过 BaseTarget 或 Tag 自动查找。")]
    public Transform primaryTarget;

    [Tooltip("Primary Target 为空时，按此 Tag 查找（需在 Tag Manager 中创建）。若场景有 BaseTarget 组件则优先用其单例。")]
    public string autoFindBaseTag = "Base";

    [Tooltip("玩家进入此距离内会优先追击玩家（挑衅）")]
    public float playerProvokeRange = 2.5f;

    [Header("移动与感知")]
    [Tooltip("敌人移动速度")]
    public float moveSpeed = 2f;

    [Tooltip("玩家进入此范围后开始追击（单位：世界距离）；仅针对玩家目标时有效")]
    public float chaseRange = 8f;

    [Tooltip("进入此范围后可以攻击（近战/远程子类可自定义逻辑）")]
    public float attackRange = 1.5f;

    [Tooltip("两次攻击之间的冷却时间（秒）")]
    public float attackCooldown = 1.0f;

    [Header("受击与击退")]
    [Tooltip("是否会被击退（Boss 或重型敌人可设为 false）")]
    public bool canBeKnockedBack = true;

    [Tooltip("击退抗性：0=吃满击退，1=完全免疫击退")]
    [Range(0f, 1f)]
    public float knockbackResistance = 0f;

    [Tooltip("受击闪白持续时间（秒），闪白效果需配合 HitFlash 等脚本")]
    public float hitFlashDuration = 0.1f;

    [Tooltip("受击音效（不填则不播放）")]
    public AudioClip hitSfx;

    [Header("动画参数名（需与 Animator 中的参数一致）")]
    [Tooltip("Float 参数，用于 Idle/Run 切换，通常为移动速度")]
    public string animParamSpeed = "Speed";

    [Tooltip("Bool 参数，死亡时设为 true")]
    public string animParamIsDead = "IsDead";

    [Tooltip("Trigger 参数，受伤时触发")]
    public string animTriggerHit = "Hit";


    // ==================== 二、组件引用（运行时获取） ====================

    /// <summary>玩家 Transform，Awake 时通过 Tag="Player" 查找</summary>
    protected Transform player;

    /// <summary>2D 刚体，用于移动和击退</summary>
    protected Rigidbody2D rb;

    /// <summary>Animator，通常挂在子物体（如 Sprite）上</summary>
    protected Animator animator;

    /// <summary>生命组件</summary>
    protected Health health;


    // ==================== 三、运行时状态 ====================

    /// <summary>攻击冷却剩余时间，≤0 时可再次攻击</summary>
    protected float attackCooldownTimer;

    /// <summary>是否已死亡（子类状态机转换条件可访问）</summary>
    public bool isDead { get; protected set; }

    /// <summary>最近一次受到的伤害信息（用于击退方向和力度）</summary>
    protected DamageInfo lastDamageInfo;

    /// <summary>是否有有效的 lastDamageInfo</summary>
    protected bool hasLastDamageInfo;

    /// <summary>是否因玩家挑衅或被打而优先追击玩家（后续可扩展城墙等目标）</summary>
    protected bool playerAggroActive;


    // ==================== 四、Unity 生命周期 ====================

    /// <summary>
    /// 初始化：获取组件、查找玩家。
    /// 子类如需扩展，请先调用 base.Awake()。
    /// </summary>
    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponentInChildren<Animator>();
        health = GetComponent<Health>();

        // 通过 Tag 查找玩家（请确保 Player 物体的 Tag 设为 "Player"）
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;
    }

    /// <summary>
    /// 子类若实现 Start，请先调用 base.Start()，以便自动解析 Primary Target。
    /// </summary>
    protected virtual void Start()
    {
        ResolvePrimaryTarget();
    }

    /// <summary>
    /// 未手动指定 Primary Target 时：优先 <see cref="BaseTarget"/>，否则按 Tag 查找。
    /// </summary>
    protected void ResolvePrimaryTarget()
    {
        if (primaryTarget != null) return;

        if (BaseTarget.Instance != null)
        {
            primaryTarget = BaseTarget.Instance;
            return;
        }

        if (string.IsNullOrEmpty(autoFindBaseTag))
            return;

        try
        {
            GameObject go = GameObject.FindGameObjectWithTag(autoFindBaseTag);
            if (go != null)
                primaryTarget = go.transform;
        }
        catch (UnityException)
        {
            // Tag 未在项目中定义时忽略
        }
    }

    /// <summary>
    /// 启用时：订阅 Health 的受伤和死亡事件。
    /// </summary>
    protected virtual void OnEnable()
    {
        if (health != null)
        {
            health.OnDamaged.AddListener(OnDamaged);
            health.OnDamagedWithInfo += OnDamagedWithInfo;
            health.OnDeath.AddListener(OnDeath);
        }
    }

    /// <summary>
    /// 禁用时：取消订阅，避免重复绑定。
    /// </summary>
    protected virtual void OnDisable()
    {
        if (health != null)
        {
            health.OnDamaged.RemoveListener(OnDamaged);
            health.OnDamagedWithInfo -= OnDamagedWithInfo;
            health.OnDeath.RemoveListener(OnDeath);
        }
    }

    /// <summary>
    /// 每帧：更新攻击冷却、执行 AI 决策、更新动画参数。
    /// </summary>
    protected virtual void Update()
    {
        if (isDead) return;

        // 攻击冷却倒计时
        if (attackCooldownTimer > 0f)
            attackCooldownTimer -= Time.deltaTime;

        // 子类实现的 AI 逻辑（Idle/Chase/Attack 等）
        UpdateAI();

        // 更新 Animator 参数（如 Speed）
        UpdateAnimatorParameters();
    }


    // ==================== 五、子类必须实现的抽象方法 ====================

    /// <summary>
    /// AI 决策入口。子类在此实现：
    /// - 根据玩家距离决定 Idle / 追击 / 攻击 / 治疗
    /// - 可调用 MoveTowardsPlayer()、StopMoving()、CanAttack()、PerformAttack()
    /// </summary>
    protected abstract void UpdateAI();

    /// <summary>
    /// 具体攻击行为。子类在此实现：
    /// - 近战：设置 Animator 的 AttackIndex，在动画事件中启用 Hitbox
    /// - 远程：生成子弹 Prefab，朝玩家方向发射
    /// - 治疗：给自己或队友加血，播放 Heal 动画
    /// </summary>
    protected abstract void PerformAttack();


    // ==================== 六、动画与状态（可重写） ====================

    /// <summary>
    /// 更新 Animator 参数。默认只设置 Speed。
    /// 子类可重写以添加 AttackIndex、IsBlocking 等参数。
    /// </summary>
    protected virtual void UpdateAnimatorParameters()
    {
        if (animator == null) return;
        animator.SetFloat(animParamSpeed, rb.linearVelocity.magnitude);
    }


    // ==================== 七、受伤与死亡处理 ====================

    /// <summary>
    /// Health 受伤事件回调（只有伤害数值）。
    /// 用于播放受击表现，击退在 OnDamagedWithInfo 中处理。
    /// </summary>
    protected virtual void OnDamaged(float dmg)
    {
        if (isDead) return;
        PlayHitEffects();
    }

    /// <summary>
    /// Health 受伤事件回调（完整 DamageInfo）。
    /// 用于存储击退信息，并在本帧应用击退。
    /// </summary>
    protected virtual void OnDamagedWithInfo(DamageInfo info)
    {
        if (isDead) return;
        lastDamageInfo = info;
        hasLastDamageInfo = true;
        if (IsDamageFromPlayer(info))
            playerAggroActive = true;
        ApplyKnockbackFromLastDamage();
    }

    /// <summary>
    /// Health 死亡事件回调。
    /// </summary>
    protected virtual void OnDeath()
    {
        isDead = true;

        // 停止移动
        rb.linearVelocity = Vector2.zero;

        // 播放死亡动画
        if (animator != null && !string.IsNullOrEmpty(animParamIsDead))
            animator.SetBool(animParamIsDead, true);

        // 关闭碰撞，避免尸体挡路
        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
    }

    /// <summary>
    /// 受击表现：触发 Hit 动画、播放音效。
    /// 闪白效果可配合单独的 HitFlash 脚本实现。
    /// </summary>
    protected virtual void PlayHitEffects()
    {
        if (animator != null && !string.IsNullOrEmpty(animTriggerHit))
            animator.SetTrigger(animTriggerHit);

        if (hitSfx != null)
            AudioSource.PlayClipAtPoint(hitSfx, transform.position);
    }

    /// <summary>
    /// 根据最近一次 DamageInfo 应用击退。
    /// 若 canBeKnockedBack 为 false 或 knockbackResistance 为 1，则不击退。
    /// </summary>
    protected virtual void ApplyKnockbackFromLastDamage()
    {
        if (!canBeKnockedBack) return;
        if (!hasLastDamageInfo) return;
        if (rb == null) return;
        if (lastDamageInfo.knockbackForce <= 0.01f) return;
        if (lastDamageInfo.knockbackDirection.sqrMagnitude < 0.01f) return;

        float effectiveForce = lastDamageInfo.knockbackForce * (1f - knockbackResistance);
        if (effectiveForce <= 0.01f) return;

        Vector2 dir = lastDamageInfo.knockbackDirection.normalized;
        rb.AddForce(dir * effectiveForce, ForceMode2D.Impulse);
    }


    // ==================== 八、工具方法（供子类调用） ====================

    /// <summary>玩家是否在指定范围内</summary>
    /// <param name="range">范围半径</param>
    /// <param name="sqrDist">输出：与玩家距离的平方（避免开方）</param>
    protected bool IsPlayerInRange(float range, out float sqrDist)
    {
        sqrDist = float.MaxValue;
        if (player == null) return false;
        sqrDist = (player.position - transform.position).sqrMagnitude;
        return sqrDist <= range * range;
    }

    /// <summary>当前是否可以攻击（冷却已结束）</summary>
    protected bool CanAttack()
    {
        return attackCooldownTimer <= 0f;
    }

    /// <summary>重置攻击冷却</summary>
    protected void ResetAttackCooldown()
    {
        attackCooldownTimer = attackCooldown;
    }

    /// <summary>朝玩家方向移动</summary>
    protected void MoveTowardsPlayer()
    {
        if (player == null) return;
        Vector2 dir = ((Vector2)player.position - (Vector2)transform.position).normalized;
        rb.linearVelocity = dir * moveSpeed;
    }

    /// <summary>当前 AI 移动目标：优先仇恨玩家，否则首要目标（水晶），否则追击范围内的玩家。</summary>
    public Transform GetMoveTarget()
    {
        if (playerAggroActive && player != null)
            return player;
        if (primaryTarget != null)
            return primaryTarget;
        if (player != null)
        {
            float _;
            if (IsPlayerInRange(chaseRange, out _))
                return player;
        }
        return null;
    }

    /// <summary>朝当前移动目标移动（水晶 / 玩家）</summary>
    protected void MoveTowardsCurrentTarget()
    {
        var t = GetMoveTarget();
        if (t == null || rb == null) return;
        Vector2 dir = ((Vector2)t.position - (Vector2)transform.position).normalized;
        if (dir.sqrMagnitude < 0.01f) return;
        rb.linearVelocity = dir * moveSpeed;
    }

    /// <summary>与当前移动目标的距离是否在攻击范围内</summary>
    public bool IsCurrentTargetInAttackRange()
    {
        var t = GetMoveTarget();
        if (t == null) return false;
        float sqr = (t.position - transform.position).sqrMagnitude;
        return sqr <= attackRange * attackRange;
    }

    /// <summary>
    /// 用于 Stay→Chase 等：首要目标时始终为 true；玩家目标时在追击半径内。
    /// </summary>
    public bool IsCurrentTargetInChaseRange()
    {
        var t = GetMoveTarget();
        if (t == null) return false;
        if (primaryTarget != null && t == primaryTarget) return true;
        return (t.position - transform.position).sqrMagnitude <= chaseRange * chaseRange;
    }

    /// <summary>每帧更新：挑衅距离、脱战（子类在 Update 中调用）</summary>
    protected void UpdateAggroProvoke()
    {
        if (player == null) return;
        float sqr = (player.position - transform.position).sqrMagnitude;
        if (sqr <= playerProvokeRange * playerProvokeRange)
            playerAggroActive = true;
        else if (playerAggroActive && sqr > chaseRange * chaseRange)
            playerAggroActive = false;
    }

    static bool IsDamageFromPlayer(DamageInfo info)
    {
        if (info.source == null) return false;
        if (info.source.CompareTag("Player")) return true;
        var t = info.source.transform;
        while (t != null)
        {
            if (t.CompareTag("Player")) return true;
            t = t.parent;
        }
        return false;
    }

    /// <summary>停止移动</summary>
    protected void StopMoving()
    {
        rb.linearVelocity = Vector2.zero;
    }
}
