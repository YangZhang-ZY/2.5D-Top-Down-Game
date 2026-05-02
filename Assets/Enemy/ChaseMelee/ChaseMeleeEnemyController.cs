using System.Collections;
using StateMachine;
using UnityEngine;

/// <summary>
/// Melee chaser aligned with <see cref="WarriorController"/> flow: Idle (decision) → Chase → Attack (one swing) → Recovery → Idle.
/// <see cref="EnemyBase.GetMoveTarget"/> handles crystal vs player; provoke / aggro on <see cref="EnemyBase"/>.
/// Animator: bool Attack; bool Recovery; trigger Hit; 可选 bool Stunned（眩晕 Loop，见 animParamStunned）; bool IsDead; float FaceX.
/// Events: OnChaseMeleeAttackHit (strike), OnChaseMeleeAttackEnd (clip end → Recovery state).
/// </summary>
[RequireComponent(typeof(Health), typeof(Rigidbody2D))]
public class ChaseMeleeEnemyController : StatefulEnemyControllerBase<ChaseMeleeEnemyController>
{
    [Header("Chase melee — attack")]
    [Tooltip("Damage for the single attack segment.")]
    public float attackDamage = 1f;

    [Tooltip("AttackHitbox forward offset.")]
    public float attackHitboxOffset = 0.5f;

    [Tooltip("玩家被命中时叠到 linearVelocity 上的击退速度（单位/秒），乘玩家侧抗性若有；与质量无关。")]
    public float attackKnockbackForce = 2f;

    [Tooltip("Hitbox auto-disable after this many seconds.")]
    public float attackHitboxDuration = 0.25f;

    [Tooltip("Small forward lunge on strike frame.")]
    public float attackDisplacement = 0.25f;

    [Header("Chase melee — recovery")]
    [Tooltip("After attack clip ends: time locked in Recovery (Warrior Stay–like); play Recovery animation.")]
    public float recoveryStateDuration = 0.5f;

    [Header("Chase melee — hit stun")]
    [Tooltip("破防后在 Hit 状态锁定的秒数 = 眩晕/硬直时长；与 Animator 里 Stunned 为 true 的时长一致。")]
    public float hitStunDuration = 0.25f;

    [Tooltip("可选：眩晕持续状态用 Bool（如 Loop 眩晕动画）。在 Hit 状态 Enter 为 true，Leave Hit 进 Recovery 前为 false。留空则只用 Hit Trigger。")]
    public string animParamStunned = "";

    [Header("Chase melee — animator")]
    [Tooltip("Bool: true while attacking (blend tree / layer).")]
    public string animParamAttack = "Attack";

    [Tooltip("Bool: true during post-attack Recovery state.")]
    public string animParamRecovery = "Recovery";

    [Tooltip("Float on Animator: -1 = left, 1 = right (Warrior-style blend trees). Leave empty to skip.")]
    public string animParamFaceX = "FaceX";

    [Header("Chase melee — references")]
    public AttackHitbox attackHitbox;

    CombatPosture _posture;

    [Header("Debug")]
    [Tooltip("Unity Console: one line when AI state changes (old -> new). Enable on Bat prefab while tuning.")]
    [SerializeField] protected bool logAIStateTransitions;

    ChaseMeleeIdleState _idleState;
    ChaseMeleeChaseState _chaseState;
    ChaseMeleeHitState _hitState;
    ChaseMeleeAttackState _attackState;
    ChaseMeleeRecoveryState _recoveryState;
    ChaseMeleeDeathState _deathState;

    float _faceX = 1f;

    public float AttackCooldownRemaining => attackCooldownTimer;

    /// <summary>Animation event sets this at end of attack clip; triggers transition to Recovery.</summary>
    public bool attackFinished { get; set; }

    /// <summary>Recovery state sets true when <see cref="recoveryStateDuration"/> elapses.</summary>
    public bool recoveryFinished { get; set; }

    /// <summary>Hit state sets true when stun timer ends.</summary>
    public bool hitRecoveryFinished { get; set; }

    /// <summary>Queued from <see cref="OnDamaged"/>; consumed on entering Hit.</summary>
    public bool pendingHit { get; private set; }

    /// <summary>Remaining hit stun (updated by <see cref="ChaseMeleeHitState"/>).</summary>
    public float hitStunTimer;

    protected override void Awake()
    {
        base.Awake();
        _posture = GetComponent<CombatPosture>();
        if (attackHitbox != null && attackHitbox.owner == null)
            attackHitbox.owner = gameObject;
    }

    /// <summary>仅当从 <see cref="ChaseMeleeHitState"/> 进入下一次 Recovery 时在 Exit 补满架势。</summary>
    public bool NextRecoveryShouldRefillPosture { get; set; }

    public CombatPosture Posture => _posture;

    protected override void InitializeStateMachine()
    {
        _idleState = new ChaseMeleeIdleState();
        _chaseState = new ChaseMeleeChaseState();
        _hitState = new ChaseMeleeHitState();
        _attackState = new ChaseMeleeAttackState();
        _recoveryState = new ChaseMeleeRecoveryState();
        _deathState = new ChaseMeleeDeathState();

        _stateMachine = new StateMachine<ChaseMeleeEnemyController>();
        _stateMachine.Initialize(this, _idleState);
        _stateMachine.OnStateChanged += (oldState, newState) =>
        {
            if (!logAIStateTransitions) return;
            string oldN = oldState != null ? oldState.GetType().Name : "null";
            string newN = newState != null ? newState.GetType().Name : "null";
            Debug.Log($"[ChaseMelee] {name}: {oldN} -> {newN}", this);
        };

        _stateMachine.AddTransition(_idleState, _hitState, ctx => ctx.pendingHit);
        _stateMachine.AddTransition(_chaseState, _hitState, ctx => ctx.pendingHit);
        _stateMachine.AddTransition(_attackState, _hitState, ctx => ctx.pendingHit);
        _stateMachine.AddTransition(_recoveryState, _hitState, ctx => ctx.pendingHit);

        _stateMachine.AddTransition(_attackState, _recoveryState, ctx => ctx.attackFinished);

        _stateMachine.AddTransition(_hitState, _recoveryState, ctx => ctx.hitRecoveryFinished);

        _stateMachine.AddTransition(_recoveryState, _idleState, ctx => ctx.recoveryFinished);

        _stateMachine.AddTransition(_idleState, _attackState, ctx =>
            ctx.GetMoveTarget() != null && ctx.IsCurrentTargetInAttackRange() && ctx.CanAttackPublic());

        _stateMachine.AddTransition(_idleState, _chaseState, ctx =>
            ctx.GetMoveTarget() != null && !ctx.IsCurrentTargetInAttackRange());

        _stateMachine.AddTransition(_chaseState, _attackState, ctx =>
            ctx.IsCurrentTargetInAttackRange() && ctx.CanAttackPublic());

        _stateMachine.AddTransition(_chaseState, _idleState, ctx =>
            ctx.GetMoveTarget() == null
            || (ctx.IsCurrentTargetInAttackRange() && !ctx.CanAttackPublic()));

        RegisterDeathState(_deathState);
    }

    protected override void OnEnemyTickBeforeStateMachine()
    {
    }

    protected override void OnDamaged(float dmg)
    {
        if (isDead) return;
        bool stagger;
        if (_posture == null || !_posture.enabled || _posture.MaxPosture <= 0f)
            stagger = true;
        else
            stagger = _posture.ApplyPostureDamageFromHp(dmg);

        var inHit = _stateMachine?.CurrentState == _hitState;
        if (stagger && !inHit)
            pendingHit = true;
        base.OnDamaged(dmg);
    }

    protected override bool ShouldApplyKnockbackFromDamage(DamageInfo info)
    {
        return _posture == null || !_posture.enabled || _posture.MaxPosture <= 0f ||
               _posture.LastHitBrokePosture;
    }

    /// <summary>Suppress default Hit trigger here; <see cref="ChaseMeleeHitState"/> fires it once per stun.</summary>
    protected override void PlayHitEffects()
    {
        if (hitSfx != null)
            AudioSource.PlayClipAtPoint(hitSfx, transform.position);
    }

    public void ConsumePendingHit()
    {
        pendingHit = false;
        hitStunTimer = hitStunDuration;
        hitRecoveryFinished = false;
    }

    public void PlayHitAnimTrigger()
    {
        if (animator != null && !string.IsNullOrEmpty(animTriggerHit))
            animator.SetTrigger(animTriggerHit);
    }

    public void SetStunnedAnim(bool value)
    {
        if (animator == null || string.IsNullOrEmpty(animParamStunned)) return;
        animator.SetBool(animParamStunned, value);
    }

    public bool CanAttackPublic() => CanAttack();

    public void ResetAttackCooldownPublic() => ResetAttackCooldown();

    public void StopMovingPublic() => StopMoving();

    public void MoveTowardsCurrentTargetPublic() => MoveTowardsCurrentTarget();

    public Animator Animator => animator;

    public void SetAttackAnim(bool value)
    {
        if (animator != null && !string.IsNullOrEmpty(animParamAttack))
            animator.SetBool(animParamAttack, value);
    }

    public void SetRecoveryAnim(bool value)
    {
        if (animator != null && !string.IsNullOrEmpty(animParamRecovery))
            animator.SetBool(animParamRecovery, value);
    }

    public void UpdateFacing(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.01f) return;
        _faceX = direction.x >= 0 ? 1f : -1f;
        if (animator != null && !string.IsNullOrEmpty(animParamFaceX))
            animator.SetFloat(animParamFaceX, _faceX);
    }

    protected override void UpdateAnimatorParameters()
    {
        if (animator == null) return;
        animator.SetFloat(animParamSpeed, rb != null ? rb.linearVelocity.magnitude : 0f);
        if (rb != null && rb.linearVelocity.sqrMagnitude > 0.01f)
            _faceX = rb.linearVelocity.x >= 0 ? 1f : -1f;
        if (!string.IsNullOrEmpty(animParamFaceX))
            animator.SetFloat(animParamFaceX, _faceX);
    }

    /// <summary>Animation event at end of attack animation — only while in Attack state.</summary>
    public void OnChaseMeleeAttackEnd()
    {
        if (_stateMachine?.CurrentState != _attackState) return;
        attackFinished = true;
        SetAttackAnim(false);
    }

    /// <summary>Optional animation event on strike frame (same pattern as Warrior OnAttackHit).</summary>
    public void OnChaseMeleeAttackHit()
    {
        if (_stateMachine?.CurrentState != _attackState) return;
        var tgt = GetMoveTarget();
        if (attackHitbox == null || tgt == null) return;
        Vector2 dir = ((Vector2)tgt.position - (Vector2)transform.position).normalized;
        if (dir.sqrMagnitude < 0.01f) dir = Vector2.down;
        UpdateFacing(dir);
        attackHitbox.EnableHitbox(attackDamage, dir, attackHitboxOffset, attackKnockbackForce);
        ApplyAttackDisplacement(dir, attackDisplacement);
        StartCoroutine(DisableHitboxAfterDuration());
    }

    IEnumerator DisableHitboxAfterDuration()
    {
        yield return new WaitForSeconds(attackHitboxDuration);
        DisableAttackHitboxPublic();
    }

    public void DisableAttackHitboxPublic()
    {
        if (attackHitbox != null)
            attackHitbox.DisableHitbox();
    }

    public void ApplyAttackDisplacement(Vector2 direction, float distance)
    {
        if (rb == null || distance <= 0f) return;
        if (direction.sqrMagnitude < 0.01f) return;
        transform.position += (Vector3)(direction.normalized * distance);
    }
}
