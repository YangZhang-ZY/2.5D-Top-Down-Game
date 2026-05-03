using System.Collections;
using StateMachine;
using UnityEngine;

/// <summary>
/// Boss：Idle / Move、两种攻击、治疗、受击（削韧）、眩晕（破防）、死亡。
/// 建议挂载 <see cref="CombatPosture"/>：削韧时进 Hit，破防进 Stun；无架势时每击视为破防进 Stun。
/// 动画：Bool Attack1、Attack2、Heal、Recovery、Stunned；Trigger Hit；Bool IsDead；可选 Float FaceX / Speed。
/// 命中帧：在 Animator 片段上添加 Event，经 <see cref="BossAnimationEventBridge"/> 调用
/// OnBossAttack1Hit / OnBossAttack1Hit2 / OnBossAttack2Hit / OnBossAttack2Hit2；攻击结束 OnBossAttackEnd；后摇结束 OnBossRecoveryEnd（可选）。
/// </summary>
[RequireComponent(typeof(Health), typeof(Rigidbody2D))]
public class BossController : StatefulEnemyControllerBase<BossController>
{
    [Header("Victory")]
    [Tooltip("勾选后 Boss 死亡时自动调用 VictoryManager.TriggerVictory()；也可只用 Health.OnDeath 绑定。")]
    [SerializeField] bool triggerVictoryOnDeath = true;

    [Header("Boss — animator")]
    [SerializeField] string animParamAttack1 = "Attack1";
    [SerializeField] string animParamAttack2 = "Attack2";
    [SerializeField] string animParamHeal = "Heal";
    [Tooltip("眩晕 Loop，与 ChaseMelee 的 Stunned 相同用法")]
    [SerializeField] string animParamStunned = "Stunned";

    [Tooltip("可选，朝向混合树")]
    [SerializeField] string animParamFaceX = "FaceX";

    [Tooltip("Bool：攻击后摇（与 ChaseMelee Recovery 一致）")]
    [SerializeField] string animParamRecovery = "Recovery";

    [Header("Boss — attack recovery")]
    [Tooltip("后摇锁定时间；动画可发 OnBossRecoveryEnd 提前结束")]
    public float recoveryStateDuration = 0.5f;

    [Header("Boss — attack 1")]
    public float attack1Damage = 2f;
    [Tooltip("第二段判定伤害；≤0 则与第一段相同")]
    public float attack1Hit2Damage = 0f;

    [Tooltip("若动画未发 OnBossAttackEnd，超过此秒数强制回到 Idle")]
    public float attack1AnimSafetyTimeout = 2f;

    public float attack1Knockback = 3f;
    [Tooltip("第二段击退；≤0 则与第一段相同")]
    public float attack1Hit2Knockback = 0f;

    [Tooltip("每次命中帧时沿攻击方向小位移（米）")]
    public float attack1Displacement = 0.2f;

    [Header("Boss — attack 2")]
    public float attack2Damage = 3f;
    [Tooltip("第二段伤害；≤0 则与第一段相同")]
    public float attack2Hit2Damage = 0f;

    [Tooltip("若动画未发 OnBossAttackEnd，超过此秒数强制结束")]
    public float attack2AnimSafetyTimeout = 2f;

    public float attack2Knockback = 4f;
    [Tooltip("第二段击退；≤0 则与第一段相同")]
    public float attack2Hit2Knockback = 0f;

    public float attack2Displacement = 0.2f;

    [Header("Boss — hitbox")]
    public AttackHitbox attackHitbox;

    [Tooltip("各段「前伸距离」若为 0，则使用此默认值")]
    public float attackHitboxOffset = 0.7f;

    [Tooltip("每段相对「朝向目标」方向的平面旋转角（度），逆时针为正；击退方向与此一致")]
    public float attack1Hit1AngleDeg;
    public float attack1Hit2AngleDeg;
    public float attack2Hit1AngleDeg;
    public float attack2Hit2AngleDeg;

    [Tooltip("该段 Hitbox 沿当前攻击方向平移的距离；0 则用 attackHitboxOffset")]
    public float attack1Hit1BoxOffset;
    public float attack1Hit2BoxOffset;
    public float attack2Hit1BoxOffset;
    public float attack2Hit2BoxOffset;

    [Tooltip("每次 EnableHitbox 后自动关闭碰撞体前的秒数（多段攻击每段会重设计时）")]
    public float attackHitboxActiveDuration = 0.25f;

    [Header("Boss — heal")]
    [Tooltip("每次进入治疗状态恢复的血量")]
    public int healAmount = 15;
    [Tooltip("治疗动画/硬直时长（秒）")]
    public float healStateDuration = 1.2f;
    public float healCooldown = 10f;
    [Range(0f, 1f)] public float healHpThreshold = 0.55f;

    [Header("Boss — stagger")]
    [Tooltip("削韧受击（短摆）时长")]
    public float chipHitDuration = 0.2f;
    [Tooltip("破防眩晕时长")]
    public float stunDuration = 0.9f;

    [Header("Boss — AI weights (same state, random pick)")]
    public float attack1Weight = 1f;
    public float attack2Weight = 1f;
    public float healWeight = 0.5f;

    [Header("Boss — debug")]
    [SerializeField] bool logStateTransitions;

    CombatPosture _posture;
    public CombatPosture Posture => _posture;
    float _faceX = 1f;
    float _healCooldownTimer;
    float _aiDecisionTimer;

    BossStateIdle _idleState;
    BossStateMove _moveState;
    BossStateAttack1 _attack1State;
    BossStateAttack2 _attack2State;
    BossStateHeal _healState;
    BossStateRecovery _recoveryState;
    BossStateHit _hitState;
    BossStateStun _stunState;
    BossStateDeath _deathState;

    float _attackAnimSafetyTimer;
    Coroutine _hitboxDisableRoutine;

    public bool pendingHit { get; private set; }
    public bool pendingStun { get; private set; }

    public bool wantsAttack1 { get; private set; }
    public bool wantsAttack2 { get; private set; }
    public bool wantsHeal { get; private set; }

    public bool attackFinished { get; set; }
    public bool healFinished { get; set; }
    public bool hitReactionFinished { get; set; }
    public bool stunFinished { get; set; }
    public bool recoveryFinished { get; set; }

    public Animator Animator => animator;

    public void StopMovingPublic() => StopMoving();

    public void MoveTowardsCurrentTargetPublic() => MoveTowardsCurrentTarget();

    public void ResetAttackCooldownPublic() => ResetAttackCooldown();

    protected override void Awake()
    {
        base.Awake();
        _posture = GetComponent<CombatPosture>();
        if (attackHitbox != null && attackHitbox.owner == null)
            attackHitbox.owner = gameObject;
    }

    protected override void InitializeStateMachine()
    {
        _idleState = new BossStateIdle();
        _moveState = new BossStateMove();
        _attack1State = new BossStateAttack1();
        _attack2State = new BossStateAttack2();
        _healState = new BossStateHeal();
        _recoveryState = new BossStateRecovery();
        _hitState = new BossStateHit();
        _stunState = new BossStateStun();
        _deathState = new BossStateDeath();

        _stateMachine = new StateMachine<BossController>();
        _stateMachine.Initialize(this, _idleState);
        _stateMachine.OnStateChanged += OnFsmStateChanged;

        _stateMachine.AddGlobalTransition(_stunState, ctx => ctx.pendingStun);
        _stateMachine.AddGlobalTransition(_hitState, ctx => ctx.pendingHit && !ctx.pendingStun);

        _stateMachine.AddTransition(_idleState, _attack1State, ctx => ctx.wantsAttack1);
        _stateMachine.AddTransition(_idleState, _attack2State, ctx => ctx.wantsAttack2);
        _stateMachine.AddTransition(_idleState, _healState, ctx => ctx.wantsHeal);
        _stateMachine.AddTransition(_idleState, _moveState, ctx =>
            ctx.GetMoveTarget() != null
            && !ctx.IsCurrentTargetInAttackRange()
            && ctx.IsCurrentTargetInChaseRange());

        _stateMachine.AddTransition(_moveState, _idleState, ctx =>
            ctx.GetMoveTarget() == null
            || ctx.IsCurrentTargetInAttackRange()
            || !ctx.IsCurrentTargetInChaseRange());

        _stateMachine.AddTransition(_attack1State, _recoveryState, ctx => ctx.attackFinished);
        _stateMachine.AddTransition(_attack2State, _recoveryState, ctx => ctx.attackFinished);
        _stateMachine.AddTransition(_recoveryState, _idleState, ctx => ctx.recoveryFinished);
        _stateMachine.AddTransition(_healState, _idleState, ctx => ctx.healFinished);
        _stateMachine.AddTransition(_hitState, _idleState, ctx => ctx.hitReactionFinished);
        _stateMachine.AddTransition(_stunState, _idleState, ctx => ctx.stunFinished);

        RegisterDeathState(_deathState);
    }

    void OnFsmStateChanged(IState<BossController> oldState, IState<BossController> newState)
    {
        if (!logStateTransitions) return;
        string o = oldState != null ? oldState.GetType().Name : "null";
        string n = newState != null ? newState.GetType().Name : "null";
        Debug.Log($"[Boss] {name}: {o} -> {n}", this);
    }

    protected override void OnEnemyExtraTimers(float deltaTime)
    {
        if (_healCooldownTimer > 0f)
            _healCooldownTimer -= deltaTime;
    }

    protected override void OnEnemyTickBeforeStateMachine()
    {
        if (_stateMachine == null) return;
        if (_stateMachine.CurrentState == _idleState)
            TickBossAIDecision(Time.deltaTime);
    }

    void TickBossAIDecision(float dt)
    {
        wantsAttack1 = wantsAttack2 = wantsHeal = false;
        if (_aiDecisionTimer > 0f)
            _aiDecisionTimer -= dt;
        if (_aiDecisionTimer > 0f) return;
        _aiDecisionTimer = 0.35f;

        if (GetMoveTarget() == null || !IsCurrentTargetInAttackRange())
            return;

        bool canH = HealAvailable();
        bool canA = CanAttack();

        float w1 = attack1Weight;
        float w2 = attack2Weight;
        float wh = canH ? healWeight : 0f;
        if (!canA)
            w1 = w2 = 0f;

        float sum = w1 + w2 + wh;
        if (sum <= 0f) return;

        float r = Random.value * sum;
        if (r < w1)
            wantsAttack1 = true;
        else if (r < w1 + w2)
            wantsAttack2 = true;
        else
            wantsHeal = true;
    }

    bool HealAvailable() =>
        _healCooldownTimer <= 0f
        && health != null
        && health.CurrentHP < Mathf.CeilToInt(health.maxHP * healHpThreshold);

    public void ResetHealCooldown() => _healCooldownTimer = healCooldown;

    public void ClearAttackIntent()
    {
        wantsAttack1 = wantsAttack2 = wantsHeal = false;
    }

    protected override void OnDamaged(float dmg)
    {
        if (isDead) return;

        bool brokeOrFull;
        if (_posture == null || !_posture.enabled || _posture.MaxPosture <= 0f)
            brokeOrFull = true;
        else
            brokeOrFull = _posture.ApplyPostureDamageFromHp(dmg);

        bool inReact = _stateMachine != null
            && (_stateMachine.CurrentState == _hitState || _stateMachine.CurrentState == _stunState);

        if (!inReact)
        {
            if (brokeOrFull)
                pendingStun = true;
            else
                pendingHit = true;
        }

        base.OnDamaged(dmg);
    }

    protected override void PlayHitEffects()
    {
        if (hitSfx != null)
            AudioSource.PlayClipAtPoint(hitSfx, transform.position);
    }

    protected override bool ShouldApplyKnockbackFromDamage(DamageInfo info)
    {
        return canBeKnockedBack && (
            _posture == null || !_posture.enabled || _posture.MaxPosture <= 0f ||
            _posture.LastHitBrokePosture);
    }

    protected override void OnDeath()
    {
        base.OnDeath();
        if (triggerVictoryOnDeath && VictoryManager.Instance != null)
            VictoryManager.Instance.TriggerVictory();
    }

    public void ConsumePendingHit()
    {
        pendingHit = false;
        hitReactionFinished = false;
    }

    public void ConsumePendingStun()
    {
        pendingStun = false;
        pendingHit = false;
        stunFinished = false;
    }

    public void PlayHitAnimTriggerOnce()
    {
        if (animator != null && !string.IsNullOrEmpty(animTriggerHit))
            animator.SetTrigger(animTriggerHit);
    }

    public void SetStunnedAnim(bool value)
    {
        if (animator == null || string.IsNullOrEmpty(animParamStunned)) return;
        animator.SetBool(animParamStunned, value);
    }

    public void SetAttack1Anim(bool v)
    {
        if (animator != null && !string.IsNullOrEmpty(animParamAttack1))
            animator.SetBool(animParamAttack1, v);
    }

    public void SetAttack2Anim(bool v)
    {
        if (animator != null && !string.IsNullOrEmpty(animParamAttack2))
            animator.SetBool(animParamAttack2, v);
    }

    public void SetHealAnim(bool v)
    {
        if (animator != null && !string.IsNullOrEmpty(animParamHeal))
            animator.SetBool(animParamHeal, v);
    }

    public void SetRecoveryAnim(bool v)
    {
        if (animator != null && !string.IsNullOrEmpty(animParamRecovery))
            animator.SetBool(animParamRecovery, v);
    }

    public void UpdateFacingTowards(Vector2 worldDir)
    {
        if (worldDir.sqrMagnitude < 0.01f) return;
        _faceX = worldDir.x >= 0f ? 1f : -1f;
        if (animator != null && !string.IsNullOrEmpty(animParamFaceX))
            animator.SetFloat(animParamFaceX, _faceX);
    }

    public void BeginAttackAnimSafetyTimeout(bool isAttack1)
    {
        _attackAnimSafetyTimer = isAttack1 ? attack1AnimSafetyTimeout : attack2AnimSafetyTimeout;
        attackFinished = false;
    }

    public void TickAttackAnimSafetyTimeout(float dt)
    {
        _attackAnimSafetyTimer -= dt;
        if (_attackAnimSafetyTimer <= 0f)
            attackFinished = true;
    }

    /// <summary>动画事件：攻击1 第一段。</summary>
    public void OnBossAttack1Hit() => PerformBossAttackHit(true, secondSegment: false);

    /// <summary>动画事件：攻击1 第二段（同一条攻击动画上再挂一个 Event）。</summary>
    public void OnBossAttack1Hit2() => PerformBossAttackHit(true, secondSegment: true);

    /// <summary>动画事件：攻击2 第一段。</summary>
    public void OnBossAttack2Hit() => PerformBossAttackHit(false, secondSegment: false);

    /// <summary>动画事件：攻击2 第二段（可选）。</summary>
    public void OnBossAttack2Hit2() => PerformBossAttackHit(false, secondSegment: true);

    void PerformBossAttackHit(bool isAttack1, bool secondSegment)
    {
        IState<BossController> expected = isAttack1
            ? (IState<BossController>)_attack1State
            : (IState<BossController>)_attack2State;
        if (_stateMachine == null || _stateMachine.CurrentState != expected)
            return;

        var tgt = GetMoveTarget();
        if (attackHitbox == null || tgt == null) return;

        Vector2 dirToTarget = (Vector2)tgt.position - (Vector2)transform.position;
        if (dirToTarget.sqrMagnitude < 0.01f) dirToTarget = Vector2.right;
        dirToTarget.Normalize();
        UpdateFacingTowards(dirToTarget);

        float segAngle = GetHitboxSegmentAngleDeg(isAttack1, secondSegment);
        Vector2 hitDir = RotateVector2Deg(dirToTarget, segAngle);
        if (hitDir.sqrMagnitude < 0.01f)
            hitDir = dirToTarget;

        float boxDist = GetHitboxSegmentOffset(isAttack1, secondSegment);

        float d1 = isAttack1 ? attack1Damage : attack2Damage;
        float d2 = isAttack1 ? attack1Hit2Damage : attack2Hit2Damage;
        float dmg = secondSegment ? (d2 > 0f ? d2 : d1) : d1;

        float disp = isAttack1 ? attack1Displacement : attack2Displacement;

        CancelPendingHitboxDisableRoutine();

        attackHitbox.EnableHitbox(dmg, hitDir, boxDist, 0f);
        ApplyAttackDisplacement(hitDir, disp);
        _hitboxDisableRoutine = StartCoroutine(DisableHitboxAfterActiveDuration());
    }

    IEnumerator DisableHitboxAfterActiveDuration()
    {
        yield return new WaitForSeconds(attackHitboxActiveDuration);
        DisableAttackHitboxSafe();
        _hitboxDisableRoutine = null;
    }

    void ApplyAttackDisplacement(Vector2 direction, float distance)
    {
        if (distance <= 0f) return;
        if (direction.sqrMagnitude < 0.01f) return;
        transform.position += (Vector3)(direction.normalized * distance);
    }

    static Vector2 RotateVector2Deg(Vector2 v, float degrees)
    {
        if (Mathf.Abs(degrees) < 0.001f) return v;
        float rad = degrees * Mathf.Deg2Rad;
        float c = Mathf.Cos(rad);
        float s = Mathf.Sin(rad);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    float GetHitboxSegmentOffset(bool isAttack1, bool secondSegment)
    {
        float o = isAttack1
            ? (secondSegment ? attack1Hit2BoxOffset : attack1Hit1BoxOffset)
            : (secondSegment ? attack2Hit2BoxOffset : attack2Hit1BoxOffset);
        return o > 0f ? o : attackHitboxOffset;
    }

    float GetHitboxSegmentAngleDeg(bool isAttack1, bool secondSegment)
    {
        return isAttack1
            ? (secondSegment ? attack1Hit2AngleDeg : attack1Hit1AngleDeg)
            : (secondSegment ? attack2Hit2AngleDeg : attack2Hit1AngleDeg);
    }

    public void CancelPendingHitboxDisableRoutine()
    {
        if (_hitboxDisableRoutine != null)
        {
            StopCoroutine(_hitboxDisableRoutine);
            _hitboxDisableRoutine = null;
        }
    }

    public void DisableAttackHitboxSafe()
    {
        if (attackHitbox != null)
            attackHitbox.DisableHitbox();
    }

    public void BeginHealPhase()
    {
        healFinished = false;
        if (health != null && healAmount > 0)
            health.Heal(healAmount);
        ResetHealCooldown();
    }

    protected override void UpdateAnimatorParameters()
    {
        if (animator == null) return;
        animator.SetFloat(animParamSpeed, rb != null ? rb.linearVelocity.magnitude : 0f);
        if (rb != null && rb.linearVelocity.sqrMagnitude > 0.01f)
            _faceX = rb.linearVelocity.x >= 0f ? 1f : -1f;
        if (!string.IsNullOrEmpty(animParamFaceX))
            animator.SetFloat(animParamFaceX, _faceX);
    }

    public void OnBossAttackEnd()
    {
        if (_stateMachine == null) return;
        if (_stateMachine.CurrentState != _attack1State && _stateMachine.CurrentState != _attack2State)
            return;
        CancelPendingHitboxDisableRoutine();

        attackFinished = true;
        SetAttack1Anim(false);
        SetAttack2Anim(false);
        DisableAttackHitboxSafe();
    }

    /// <summary>动画事件：后摇结束（片段末尾或提前收招）。</summary>
    public void OnBossRecoveryEnd()
    {
        if (_stateMachine == null) return;
        if (_stateMachine.CurrentState != _recoveryState) return;
        recoveryFinished = true;
    }
}
