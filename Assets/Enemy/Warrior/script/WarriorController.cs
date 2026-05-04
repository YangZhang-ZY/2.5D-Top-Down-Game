using UnityEngine;
using System.Collections;
using StateMachine;

/// <summary>
/// Warrior 敌人：使用状态机管理 Idle / Chase / Attack / Block / Death 状态。
/// 继承 <see cref="StatefulEnemyControllerBase{TSelf}"/>，与 ChaseMelee 等共用同一套状态机驱动框架。
///
/// 【状态机流程】
/// Patrol → Chase（有移动目标且未进近战：水晶或玩家）
/// Chase → Patrol（失去移动目标）
/// Chase → Attack1（玩家在攻击范围内，WarriorController 决策随机攻击）
/// Chase → Block（玩家在攻击范围内，WarriorController 决策随机格挡）
/// Attack1 → Attack2（第一段结束，由动画事件 OnAttack1End 触发）
/// Attack2 → Recovery（第二段结束，由动画事件 OnAttack2End 触发）
/// Recovery → Stay（后摇计时结束或动画事件 OnWarriorRecoveryEnd）
/// Block → Stay（格挡结束）
/// Stay → Patrol 或 Chase（停留 1~2 秒后）
/// 任意状态 → Death（HP 归零）
///
/// 【Animator 参数】
/// - Speed (float)：移动速度
/// - FaceX (float)：面向，-1=左，1=右，用于双向动画
/// - Attack1 (bool)：第一段攻击
/// - Attack2 (bool)：第二段攻击
/// - Recovery (bool)：连击结束后后摇（与 ChaseMelee / Boss 一致，参数名见 animParamRecovery）
/// - IsBlocking (bool)：格挡
/// - IsDead (bool)：死亡
/// - Hit (Trigger)：受击
/// 详见同目录下 Animator配置说明.md
///
/// 【使用步骤】
/// 1. 给 Warrior 添加 Health、Rigidbody2D、本脚本
/// 2. 创建子物体 AttackHitbox（Circle Collider2D IsTrigger + AttackHitbox 脚本，owner 设为自身）
/// 3. 在 Animator 中配置参数和状态
/// </summary>
[RequireComponent(typeof(Health), typeof(Rigidbody2D))]
public class WarriorController : StatefulEnemyControllerBase<WarriorController>
{
    // ==================== 一、Inspector 参数 ====================

    [Header("Warrior 攻击")]
    [Tooltip("第一段攻击伤害")]
    public float attackDamage1 = 1f;

    [Tooltip("第二段攻击伤害")]
    public float attackDamage2 = 2f;

    [Tooltip("攻击 Hitbox 相对自身的偏移距离")]
    public float attackHitboxOffset = 0.5f;

    [Tooltip("击退力度（对玩家造成击退）")]
    public float attackKnockbackForce = 2f;

    [Tooltip("Hitbox 开启后自动关闭的时长")]
    public float attackHitboxDuration = 0.25f;

    [Tooltip("第一段攻击位移距离")]
    public float attackDisplacement1 = 0.3f;

    [Tooltip("第二段攻击位移距离")]
    public float attackDisplacement2 = 0.4f;

    [Space(8)]
    [Header("Warrior 连击后摇（第二段 Attack2 结束后）")]
    [Tooltip("后摇状态最短持续秒数（未发 OnWarriorRecoveryEnd 时至少锁这么久）；可与 Recovery 动画同长")]
    [Min(0f)]
    public float recoveryStateDuration = 0.5f;

    [Tooltip("Animator 里后摇用的 Bool 参数名，默认同 ChaseMelee：Recovery")]
    public string animParamRecovery = "Recovery";

    [Header("Warrior 格挡")]
    [Tooltip("格挡持续时间（秒）")]
    public float blockDuration = 1f;

    [Tooltip("格挡冷却（格挡结束后多久才能再次格挡）")]
    public float blockCooldown = 2f;

    [Tooltip("进入攻击范围时，选择格挡的概率（0~1）")]
    [Range(0f, 1f)]
    public float blockChance = 0.3f;

    [Header("Warrior 停留")]
    [Tooltip("攻击/格挡结束后停留时间最小值（秒）")]
    public float stayDurationMin = 1f;

    [Tooltip("攻击/格挡结束后停留时间最大值（秒）")]
    public float stayDurationMax = 2f;

    [Header("目标（危险区巡逻）")]
    [Tooltip("开启后：不追 Primary Target/基地，只在 chaseRange 内发现玩家、或走进 playerProvokeRange、或被打后追击玩家。")]
    [SerializeField] bool patrolOnlyEngagePlayer = true;

    [Header("Warrior 巡逻")]
    [Tooltip("巡逻范围半径（以出生点为圆心，在此范围内随机选点）")]
    public float patrolRange = 5f;

    [Tooltip("到达巡逻点后随机停留时间（秒）")]
    public float patrolWaitMin = 2f;

    [Tooltip("到达巡逻点后随机停留时间（秒）")]
    public float patrolWaitMax = 3f;

    [Header("Warrior 双向动画")]
    [Tooltip("Animator 面向参数名，-1=左，1=右")]
    public string animParamFaceX = "FaceX";

    [Tooltip("Animator 第一段攻击参数名")]
    public string animParamAttack1 = "Attack1";

    [Tooltip("Animator 第二段攻击参数名")]
    public string animParamAttack2 = "Attack2";

    [Header("引用")]
    [Tooltip("攻击碰撞盒（子物体，需有 AttackHitbox 和 Collider2D）")]
    public AttackHitbox attackHitbox;

    CombatPosture _posture;


    // ==================== 二、状态机与状态实例 ====================

    private WarriorPatrolState _patrolState;
    private WarriorChaseState _chaseState;
    private WarriorAttack1State _attack1State;
    private WarriorAttack2State _attack2State;
    private WarriorRecoveryState _recoveryState;
    private WarriorBlockState _blockState;
    private WarriorStayState _stayState;
    private WarriorDeathState _deathState;


    // ==================== 三、供状态和转换条件使用的属性 ====================

    /// <summary>第一段攻击是否结束（由动画事件 OnAttack1End 设置）</summary>
    public bool attack1Finished { get; set; }

    /// <summary>第二段攻击是否结束（由动画事件 OnAttack2End 设置）</summary>
    public bool attack2Finished { get; set; }

    /// <summary>攻击后摇是否结束（计时或 OnWarriorRecoveryEnd）</summary>
    public bool recoveryFinished { get; set; }

    /// <summary>格挡是否结束（Block 状态完成后设为 true）</summary>
    public bool blockFinished { get; set; }

    /// <summary>是否想攻击（Chase 状态在满足条件时设置，下一帧转换到 Attack）</summary>
    public bool wantToAttack { get; set; }

    /// <summary>是否想格挡（WarriorController 决策设置，下一帧转换到 Block）</summary>
    public bool wantToBlock { get; set; }

    /// <summary>停留是否结束（Stay 状态完成后设为 true）</summary>
    public bool stayFinished { get; set; }

    /// <summary>当前攻击段数（1 或 2），供 EnableAttackHitbox 使用</summary>
    private int _currentAttackIndex;

    /// <summary>格挡冷却剩余时间</summary>
    private float _blockCooldownTimer;

    /// <summary>巡逻中心点（出生位置）</summary>
    private Vector2 _patrolOrigin;

    /// <summary>面向：-1=左，1=右</summary>
    private float _faceX = 1f;


    // ==================== 四、Unity 生命周期 ====================

    /// <summary>有架势时仅破防播受击动画。</summary>
    bool _warriorPlayHitEffects;

    protected override void Awake()
    {
        base.Awake();
        if (patrolOnlyEngagePlayer)
            ignorePrimaryTargetForMovement = true;
        _posture = GetComponent<CombatPosture>();
        if (attackHitbox != null && attackHitbox.owner == null)
            attackHitbox.owner = gameObject;
    }

    protected override void InitializeStateMachine()
    {
        _patrolOrigin = transform.position;

        // 创建各状态实例
        _patrolState = new WarriorPatrolState();
        _chaseState = new WarriorChaseState();
        _attack1State = new WarriorAttack1State();
        _attack2State = new WarriorAttack2State();
        _recoveryState = new WarriorRecoveryState();
        _blockState = new WarriorBlockState();
        _stayState = new WarriorStayState();
        _deathState = new WarriorDeathState();

        // 初始化状态机，从 Patrol 开始
        _stateMachine = new StateMachine<WarriorController>();
        _stateMachine.Initialize(this, _patrolState);

        // ---------- 添加状态转换 ----------
        // Death：由基类 OnDeath 切入 _deathState，此处不再 AddGlobalTransition

        // Patrol → Chase：有移动目标且尚未进入近战
        _stateMachine.AddTransition(_patrolState, _chaseState, ctx =>
            ctx.GetMoveTarget() != null && !ctx.IsCurrentTargetInAttackRange());

        // Chase → Patrol：失去移动目标（无水晶且无玩家仇恨等）
        _stateMachine.AddTransition(_chaseState, _patrolState, ctx => ctx.GetMoveTarget() == null);

        // Chase → Attack1：wantToAttack 且攻击冷却已好
        _stateMachine.AddTransition(_chaseState, _attack1State, ctx => ctx.wantToAttack && ctx.CanAttackPublic());

        // Chase → Block：wantToBlock 且格挡冷却已好
        _stateMachine.AddTransition(_chaseState, _blockState, ctx => ctx.wantToBlock && ctx.IsBlockCooldownReady());

        // Attack1 → Attack2：第一段结束
        _stateMachine.AddTransition(_attack1State, _attack2State, ctx => ctx.attack1Finished);

        // Attack2 → Recovery：第二段结束
        _stateMachine.AddTransition(_attack2State, _recoveryState, ctx => ctx.attack2Finished);

        // Recovery → Stay：后摇结束
        _stateMachine.AddTransition(_recoveryState, _stayState, ctx => ctx.recoveryFinished);

        // Block → Stay：格挡结束
        _stateMachine.AddTransition(_blockState, _stayState, ctx => ctx.blockFinished);

        // Stay → Patrol：停留结束且无目标可追
        _stateMachine.AddTransition(_stayState, _patrolState, ctx =>
            ctx.stayFinished && ctx.GetMoveTarget() == null);

        // Stay → Chase：停留结束且仍可追击
        _stateMachine.AddTransition(_stayState, _chaseState, ctx =>
            ctx.stayFinished && ctx.IsCurrentTargetInChaseRange());

        RegisterDeathState(_deathState);
    }

    protected override void OnEnemyExtraTimers(float dt)
    {
        if (_blockCooldownTimer > 0f)
            _blockCooldownTimer -= dt;
    }

    protected override void OnEnemyTickBeforeStateMachine()
    {
        TickAIDecision();
    }


    // ==================== 五、AI 决策（集中在 WarriorController） ====================

    /// <summary>
    /// 每帧 AI 决策。当处于 Chase 且玩家在攻击范围内、攻击和格挡冷却均好时，
    /// 按 blockChance 随机选择攻击或格挡。
    /// </summary>
    private void TickAIDecision()
    {
        if (_stateMachine?.CurrentState != _chaseState) return;
        if (!IsCurrentTargetInAttackRange()) return;
        if (!CanAttackPublic()) return;

        var tgt = GetMoveTarget();
        if (tgt == null) return;

        if (player != null && tgt == player)
        {
            if (Random.value < blockChance)
                wantToBlock = true;
            else
                wantToAttack = true;
        }
        else
        {
            wantToAttack = true;
        }
    }

    // ==================== 六、供状态和转换条件调用的方法 ====================

    /// <summary>供状态类调用：停止移动</summary>
    public void StopMovingPublic() => StopMoving();

    /// <summary>供状态类调用：朝玩家移动</summary>
    public void MoveTowardsPlayerPublic() => MoveTowardsPlayer();

    /// <summary>供状态类调用：朝当前目标（水晶/玩家）移动</summary>
    public void MoveTowardsCurrentTargetPublic() => MoveTowardsCurrentTarget();

    /// <summary>供状态类调用：朝指定点移动</summary>
    public void MoveTowardsPointPublic(Vector2 target)
    {
        MoveTowardsWorldPoint(target);
    }

    /// <summary>在巡逻范围内随机获取一个目标点</summary>
    public Vector2 GetRandomPatrolPoint()
    {
        Vector2 offset = Random.insideUnitCircle * Mathf.Max(0.1f, patrolRange);
        return _patrolOrigin + offset;
    }

    /// <summary>供状态类调用：是否可攻击（冷却已好）</summary>
    public bool CanAttackPublic() => CanAttack();

    /// <summary>供状态类调用：重置攻击冷却</summary>
    public void ResetAttackCooldownPublic() => ResetAttackCooldown();

    /// <summary>玩家是否在追击范围内（调试用）</summary>
    public bool IsPlayerInChaseRange()
    {
        float _;
        return IsPlayerInRange(chaseRange, out _);
    }

    /// <summary>玩家是否在攻击范围内（调试用）</summary>
    public bool IsPlayerInAttackRange()
    {
        float _;
        return IsPlayerInRange(attackRange, out _);
    }

    /// <summary>供状态类访问 Animator（基类 animator 为 protected）</summary>
    public Animator Animator => animator;

    /// <summary>当前状态名（供 WarriorDebugDisplay 使用）</summary>
    public string CurrentStateName
    {
        get
        {
            var s = _stateMachine?.CurrentState?.GetType().Name ?? "";
            if (s.Contains("Patrol")) return "Patrol";
            if (s.Contains("Chase")) return "Chase";
            if (s.Contains("Attack1")) return "Attack1";
            if (s.Contains("Attack2")) return "Attack2";
            if (s.Contains("Recovery")) return "Recovery";
            if (s.Contains("Block")) return "Block";
            if (s.Contains("Stay")) return "Stay";
            if (s.Contains("Death")) return "Death";
            return s;
        }
    }

    /// <summary>连击当前段数（供 WarriorDebugDisplay 使用）：Attack1=1，Attack2=2，其他=0</summary>
    public int ComboSegmentForDebug
    {
        get
        {
            var s = _stateMachine?.CurrentState?.GetType().Name ?? "";
            if (s.Contains("Attack1")) return 1;
            if (s.Contains("Attack2")) return 2;
            return 0;
        }
    }

    /// <summary>供状态类访问玩家 Transform</summary>
    public Transform Player => player;

    /// <summary>格挡冷却是否已结束（可再次格挡）</summary>
    public bool IsBlockCooldownReady()
    {
        return _blockCooldownTimer <= 0f;
    }

    /// <summary>设置本次攻击段数（供 EnableAttackHitbox 使用）</summary>
    public void SetCurrentAttackIndex(int index)
    {
        _currentAttackIndex = index;
    }

    /// <summary>开启或关闭格挡：免疫伤害 + 设置 Animator 的 IsBlocking</summary>
    public void SetBlocking(bool blocking)
    {
        if (Animator != null)
            Animator.SetBool("IsBlocking", blocking);

        if (health != null)
            health.SetIgnoreDamage(blocking);

        if (!blocking)
            _blockCooldownTimer = blockCooldown;
    }


    // ==================== 七、攻击 Hitbox 控制 ====================

    /// <summary>
    /// 开启攻击 Hitbox。由 WarriorAttack1State / WarriorAttack2State 或 Animation Event 调用。
    /// </summary>
    /// <param name="index">1=第一段，2=第二段；0 则使用 _currentAttackIndex</param>
    public void EnableAttackHitbox(int index = 0)
    {
        var tgt = GetMoveTarget();
        if (attackHitbox == null || tgt == null) return;
        if (index <= 0) index = _currentAttackIndex;

        Vector2 dir = ((Vector2)tgt.position - (Vector2)transform.position).normalized;
        if (dir.sqrMagnitude < 0.01f) dir = Vector2.down;

        float damage = index == 1 ? attackDamage1 : attackDamage2;
        attackHitbox.EnableHitbox(damage, dir, attackHitboxOffset, 0f);
    }

    /// <summary>关闭攻击 Hitbox。可由 Animation Event 调用。</summary>
    public void DisableAttackHitbox()
    {
        if (attackHitbox != null)
            attackHitbox.DisableHitbox();
    }

    /// <summary>设置 Animator Attack1 参数</summary>
    public void SetAttack1(bool value)
    {
        if (Animator != null && !string.IsNullOrEmpty(animParamAttack1))
            Animator.SetBool(animParamAttack1, value);
    }

    /// <summary>设置 Animator Attack2 参数</summary>
    public void SetAttack2(bool value)
    {
        if (Animator != null && !string.IsNullOrEmpty(animParamAttack2))
            Animator.SetBool(animParamAttack2, value);
    }

    /// <summary>设置 Animator Recovery（连击后摇）</summary>
    public void SetRecoveryAnim(bool value)
    {
        if (Animator != null && !string.IsNullOrEmpty(animParamRecovery))
            Animator.SetBool(animParamRecovery, value);
    }

    /// <summary>
    /// 动画事件：可选，在 Recovery 动画提前结束时调用（片段末尾），否则用 recoveryStateDuration。
    /// </summary>
    public void OnWarriorRecoveryEnd()
    {
        if (_stateMachine == null || _stateMachine.CurrentState != _recoveryState) return;
        recoveryFinished = true;
    }

    /// <summary>
    /// 动画事件：挥砍帧调用。开启 Hitbox、应用位移，并在 attackHitboxDuration 后关闭。
    /// 在 Attack1、Attack2 的挥砍帧添加 Event，Function 填 OnAttackHit，Int 填 1 或 2。
    /// </summary>
    public void OnAttackHit(int index)
    {
        var tgt = GetMoveTarget();
        if (tgt == null) return;
        Vector2 dir = ((Vector2)tgt.position - (Vector2)transform.position).normalized;
        if (dir.sqrMagnitude < 0.01f) dir = Vector2.down;
        UpdateFacing(dir);

        SetCurrentAttackIndex(index);
        EnableAttackHitbox(index);
        float dist = index == 1 ? attackDisplacement1 : attackDisplacement2;
        ApplyAttackDisplacement(dir, dist);

        StartCoroutine(DisableHitboxAfterDuration());
    }

    private IEnumerator DisableHitboxAfterDuration()
    {
        yield return new WaitForSeconds(attackHitboxDuration);
        DisableAttackHitbox();
    }

    /// <summary>
    /// 动画事件：第一段攻击动画最后一帧调用。在 Attack1 动画末尾添加 Event，Function 填 OnAttack1End。
    /// </summary>
    public void OnAttack1End()
    {
        attack1Finished = true;
    }

    /// <summary>
    /// 动画事件：第二段攻击动画最后一帧调用。在 Attack2 动画末尾添加 Event，Function 填 OnAttack2End。
    /// </summary>
    public void OnAttack2End()
    {
        attack2Finished = true;
        SetAttack1(false);
        SetAttack2(false);
    }

    /// <summary>根据方向更新面向（-1=左，1=右），供双向动画使用</summary>
    public void UpdateFacing(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.01f) return;
        _faceX = direction.x >= 0 ? 1f : -1f;
    }

    /// <summary>攻击时朝攻击方向产生位移</summary>
    public void ApplyAttackDisplacement(Vector2 direction, float distance)
    {
        if (rb == null || distance <= 0f) return;
        if (direction.sqrMagnitude < 0.01f) return;
        Vector2 pos = transform.position;
        pos += direction.normalized * distance;
        transform.position = pos;
    }

    protected override void UpdateAnimatorParameters()
    {
        if (animator == null) return;
        animator.SetFloat(animParamSpeed, rb.linearVelocity.magnitude);
        // 移动时根据速度方向更新面向
        if (rb.linearVelocity.sqrMagnitude > 0.01f)
            _faceX = rb.linearVelocity.x >= 0 ? 1f : -1f;
        if (!string.IsNullOrEmpty(animParamFaceX))
            animator.SetFloat(animParamFaceX, _faceX);
    }

    protected override void OnDamaged(float dmg)
    {
        if (isDead) return;
        if (_posture == null || !_posture.enabled || _posture.MaxPosture <= 0f)
            _warriorPlayHitEffects = true;
        else
            _warriorPlayHitEffects = _posture.ApplyPostureDamageFromHp(dmg);

        base.OnDamaged(dmg);
    }

    protected override void PlayHitEffects()
    {
        if (!_warriorPlayHitEffects)
            return;
        base.PlayHitEffects();
        _posture?.RefillPosture();
    }

    protected override bool ShouldApplyKnockbackFromDamage(DamageInfo info)
    {
        return _posture == null || !_posture.enabled || _posture.MaxPosture <= 0f ||
               _posture.LastHitBrokePosture;
    }
}
