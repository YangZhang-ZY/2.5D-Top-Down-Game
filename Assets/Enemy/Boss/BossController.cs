using System.Collections;
using StateMachine;
using UnityEngine;

/// <summary>
/// Boss: Patrol (random inside radius) → on player spotted, targets player only (not base); Idle / Move, attacks, heal, death.
/// Animator: Bools Attack1, Attack2, Heal, Recovery; Trigger Hit; Bool IsDead; optional FaceX / Speed.
/// </summary>
[RequireComponent(typeof(Health), typeof(Rigidbody2D))]
[DefaultExecutionOrder(1000)]
public class BossController : StatefulEnemyControllerBase<BossController>
{
    [Header("Victory")]
    [Tooltip("If enabled, calls VictoryManager.TriggerVictory() on Boss death; can also wire Health.OnDeath instead.")]
    [SerializeField] bool triggerVictoryOnDeath = true;

    [Header("Boss — animator")]
    [SerializeField] string animParamAttack1 = "Attack1";
    [SerializeField] string animParamAttack2 = "Attack2";
    [SerializeField] string animParamHeal = "Heal";

    [Tooltip("Optional facing blend tree parameter.")]
    [SerializeField] string animParamFaceX = "FaceX";

    [Tooltip("Bool: post-attack recovery (same idea as ChaseMelee Recovery).")]
    [SerializeField] string animParamRecovery = "Recovery";

    [Header("Boss — attack recovery")]
    [Tooltip("Recovery lock duration; animation can fire OnBossRecoveryEnd early.")]
    public float recoveryStateDuration = 0.5f;

    [Header("Boss — attack 1")]
    public float attack1Damage = 2f;
    [Tooltip("Second-segment damage; ≤0 uses first-segment value.")]
    public float attack1Hit2Damage = 0f;

    [Tooltip("If OnBossAttackEnd never fires, force Idle after this many seconds.")]
    public float attack1AnimSafetyTimeout = 2f;

    public float attack1Knockback = 3f;
    [Tooltip("Second-segment knockback; ≤0 uses first-segment value.")]
    public float attack1Hit2Knockback = 0f;

    [Tooltip("Small nudge along attack direction on each hit frame (meters).")]
    public float attack1Displacement = 0.2f;

    [Header("Boss — attack 2")]
    public float attack2Damage = 3f;
    [Tooltip("Second hit damage; ≤0 uses first hit.")]
    public float attack2Hit2Damage = 0f;

    [Tooltip("If OnBossAttackEnd never fires, force end after this many seconds.")]
    public float attack2AnimSafetyTimeout = 2f;

    public float attack2Knockback = 4f;
    [Tooltip("Second-segment knockback; ≤0 uses first-segment value.")]
    public float attack2Hit2Knockback = 0f;

    public float attack2Displacement = 0.2f;

    [Header("Boss — hitbox")]
    public AttackHitbox attackHitbox;

    [Tooltip("When facing right (FaceX ≥ 0), default forward offset if a segment Box Offset is 0.")]
    public float attackHitboxOffset = 0.7f;

    [Tooltip("Per-segment yaw in the plane vs face-toward-target (degrees, CCW+); knockback matches this.")]
    public float attack1Hit1AngleDeg;
    public float attack1Hit2AngleDeg;
    public float attack2Hit1AngleDeg;
    public float attack2Hit2AngleDeg;

    [Tooltip("When facing right, slide hitbox this far along attack dir; 0 falls back to attackHitboxOffset.")]
    public float attack1Hit1BoxOffset;
    public float attack1Hit2BoxOffset;
    public float attack2Hit1BoxOffset;
    public float attack2Hit2BoxOffset;

    [Tooltip("When facing left, if segment left offset is 0, use matching right value then attackHitboxOffsetLeft / attackHitboxOffset.")]
    public float attackHitboxOffsetLeft;

    public float attack1Hit1BoxOffsetLeft;
    public float attack1Hit2BoxOffsetLeft;
    public float attack2Hit1BoxOffsetLeft;
    public float attack2Hit2BoxOffsetLeft;

    [Tooltip("Seconds before auto-disabling collider after each EnableHitbox (per combo segment retimes).")]
    public float attackHitboxActiveDuration = 0.25f;

    [Header("Boss — heal")]
    [Tooltip("Total heal budget for one heal state (applied in ticks).")]
    public int healAmount = 20;

    [Tooltip("Each tick calls Health.Heal by this amount; last tick tops up remainder.")]
    [Min(1)]
    public int healPerTick = 5;

    [Tooltip("Seconds between heal ticks. ≤0 spreads evenly across healStateDuration.")]
    public float healTickInterval = 0.25f;

    [Tooltip("Heal anim / lock duration (seconds).")]
    public float healStateDuration = 1.2f;
    public float healCooldown = 10f;
    [Range(0f, 1f)] public float healHpThreshold = 0.55f;

    [Tooltip("Optional child anchor (feet/chest); empty uses this transform.")]
    public Transform healEffectAnchor;

    [Tooltip("Spawned when entering heal; removed on exit heal, death, or Boss destroy (or detach+delay, see destroyHealEffectWhenLeavingHealState).")]
    public GameObject healEffectPrefab;

    [Tooltip("True: Destroy VFX immediately when leaving heal. False: unparent and Destroy after particle lifetime (more visible).")]
    [SerializeField] bool destroyHealEffectWhenLeavingHealState;

    [Header("Boss — AI weights (same state, random pick)")]
    public float attack1Weight = 1f;
    public float attack2Weight = 1f;
    public float healWeight = 0.5f;

    [Header("Boss — patrol & combat start")]
    [Tooltip("Patrol region center; empty uses Boss world position at Awake.")]
    [SerializeField] Transform patrolRegionCenter;

    [Tooltip("Random wander radius around patrol center (meters).")]
    [Min(0.5f)]
    public float patrolRadius = 20f;

    [Tooltip("Treat waypoint reached inside this distance; then pause before picking next.")]
    [Min(0.05f)]
    public float patrolWaypointReach = 0.45f;

    [Tooltip("Idle seconds at a waypoint before choosing the next.")]
    [Min(0f)]
    public float patrolPauseAtWaypointDuration = 10f;

    [Tooltip("Player within this radius engages combat; Boss stops patrolling after.")]
    [Min(0.5f)]
    public float playerDetectRadius = 20f;

    [Header("Boss — chase player")]
    [Tooltip("With chaseRange, use the larger value as player leash / disengage radius.")]
    [SerializeField] float arenaPlayerLeashRange = 24f;

    [Header("Boss — aggro audio")]
    [Tooltip("Played once when the Boss spots the player or takes damage and starts chasing (combat engage).")]
    [SerializeField] AudioClip chaseEngageClip;

    [SerializeField] [Range(0f, 1f)] float chaseEngageVolume = 1f;

    float _faceX = 1f;
    float _healCooldownTimer;
    float _aiDecisionTimer;

    Vector2 _patrolSpawnPivot;
    Vector2 _patrolDestination;
    float _patrolWaitTimer;
    bool _bossCombatEngaged;

    /// <summary>Combat started (spotted or damaged); state machine uses this to leave Patrol for Idle.</summary>
    public bool BossCombatEngaged => _bossCombatEngaged;

    BossStatePatrol _patrolState;
    BossStateIdle _idleState;
    BossStateMove _moveState;
    BossStateAttack1 _attack1State;
    BossStateAttack2 _attack2State;
    BossStateHeal _healState;
    BossStateRecovery _recoveryState;
    BossStateDeath _deathState;

    float _attackAnimSafetyTimer;
    Coroutine _hitboxDisableRoutine;
    Coroutine _healRoutine;
    GameObject _healEffectInstance;

    public bool wantsAttack1 { get; private set; }
    public bool wantsAttack2 { get; private set; }
    public bool wantsHeal { get; private set; }

    public bool attackFinished { get; set; }
    public bool healFinished { get; set; }
    public bool recoveryFinished { get; set; }

    public Animator Animator => animator;

    public void StopMovingPublic() => StopMoving();

    public void MoveTowardsCurrentTargetPublic() => MoveTowardsCurrentTarget();

    public void ResetAttackCooldownPublic() => ResetAttackCooldown();

    protected override void Awake()
    {
        base.Awake();
        if (attackHitbox != null && attackHitbox.owner == null)
            attackHitbox.owner = gameObject;
        if (arenaPlayerLeashRange > 0f)
            maxPlayerEngageRange = Mathf.Max(maxPlayerEngageRange, arenaPlayerLeashRange);

        CaptureRigidbodyConstraintsTargetForChase();
        EnsureRigidbodyPositionNotFrozenForChase();

        _patrolSpawnPivot = transform.position;
        ignorePrimaryTargetForMovement = true;
    }

    void CaptureRigidbodyConstraintsTargetForChase()
    {
        if (rb == null) return;
        var ins = rb.constraints;
        if (ins.HasFlag(RigidbodyConstraints2D.FreezeAll))
            _rbConstraintsAfterPositionUnfreeze = RigidbodyConstraints2D.FreezeRotation;
        else if (ins.HasFlag(RigidbodyConstraints2D.FreezeRotation))
            _rbConstraintsAfterPositionUnfreeze = RigidbodyConstraints2D.FreezeRotation;
        else
            _rbConstraintsAfterPositionUnfreeze = RigidbodyConstraints2D.None;
    }

    protected override void Start()
    {
        base.Start();
        CaptureRigidbodyConstraintsTargetForChase();
        // Other components may restore constraints after Awake; enforce again so velocity can separate bodies.
        EnsureRigidbodyPositionNotFrozenForChase();
    }

    void EnsureRigidbodyPositionNotFrozenForChase() => EnforceRigidbodyPositionUnfrozen();

    RigidbodyConstraints2D _rbConstraintsAfterPositionUnfreeze;

    /// <summary>
    /// Other scripts may re-enable Freeze Position after Awake/Start.
    /// Re-apply in FixedUpdate before the physics step.
    /// </summary>
    void EnforceRigidbodyPositionUnfrozen()
    {
        if (rb == null || rb.bodyType != RigidbodyType2D.Dynamic) return;
        if (rb.constraints == _rbConstraintsAfterPositionUnfreeze) return;
        rb.constraints = _rbConstraintsAfterPositionUnfreeze;
    }

    void FixedUpdate()
    {
        if (!isDead)
            EnforceRigidbodyPositionUnfrozen();
    }

    /// <summary>
    /// Before combat: do not chase crystal or player (Patrol handles motion). In combat: chase player only.
    /// </summary>
    public override Transform GetMoveTarget()
    {
        if (!_bossCombatEngaged || player == null)
            return null;
        return player;
    }

    protected override void InitializeStateMachine()
    {
        _patrolState = new BossStatePatrol();
        _idleState = new BossStateIdle();
        _moveState = new BossStateMove();
        _attack1State = new BossStateAttack1();
        _attack2State = new BossStateAttack2();
        _healState = new BossStateHeal();
        _recoveryState = new BossStateRecovery();
        _deathState = new BossStateDeath();

        _stateMachine = new StateMachine<BossController>();
        _stateMachine.Initialize(this, _patrolState);

        _stateMachine.AddTransition(_patrolState, _idleState, ctx => ctx.BossCombatEngaged);

        // Move before attack/heal: large attackRange would keep wantsAttack true every frame otherwise.
        _stateMachine.AddTransition(_idleState, _moveState, ctx =>
            ctx.GetMoveTarget() != null
            && ctx.IsCurrentTargetBeyondMoveStopDistance()
            && ctx.IsCurrentTargetInChaseRange());

        _stateMachine.AddTransition(_idleState, _attack1State, ctx => ctx.wantsAttack1);
        _stateMachine.AddTransition(_idleState, _attack2State, ctx => ctx.wantsAttack2);
        _stateMachine.AddTransition(_idleState, _healState, ctx => ctx.wantsHeal);

        _stateMachine.AddTransition(_moveState, _idleState, ctx =>
            ctx.GetMoveTarget() == null
            || !ctx.IsCurrentTargetBeyondMoveStopDistance()
            || !ctx.IsCurrentTargetInChaseRange());

        _stateMachine.AddTransition(_attack1State, _recoveryState, ctx => ctx.attackFinished);
        _stateMachine.AddTransition(_attack2State, _recoveryState, ctx => ctx.attackFinished);
        _stateMachine.AddTransition(_recoveryState, _idleState, ctx => ctx.recoveryFinished);
        _stateMachine.AddTransition(_healState, _idleState, ctx => ctx.healFinished);

        RegisterDeathState(_deathState);
    }

    protected override void OnEnemyExtraTimers(float deltaTime)
    {
        if (_healCooldownTimer > 0f)
            _healCooldownTimer -= deltaTime;
    }

    protected override void OnEnemyTickBeforeStateMachine()
    {
        TryEngageBossCombatIfNeeded();
        if (_stateMachine == null || !BossCombatEngaged) return;
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

    void TryEngageBossCombatIfNeeded()
    {
        if (_bossCombatEngaged || player == null) return;
        if (playerAggroActive)
        {
            _bossCombatEngaged = true;
            return;
        }
        float r = playerDetectRadius;
        if ((player.position - transform.position).sqrMagnitude <= r * r)
            _bossCombatEngaged = true;
    }

    Vector2 PatrolAnchorWorld =>
        patrolRegionCenter != null ? (Vector2)patrolRegionCenter.position : _patrolSpawnPivot;

    public void PickNewPatrolDestination()
    {
        Vector2 anchor = PatrolAnchorWorld;
        _patrolDestination = anchor + (Vector2)(Random.insideUnitCircle * patrolRadius);
    }

    /// <summary>Called when entering patrol: clear wait timer and pick a point.</summary>
    public void ResetPatrolWaitAndPickDestination()
    {
        _patrolWaitTimer = 0f;
        ClearAttackIntent();
        PickNewPatrolDestination();
    }

    public void TickPatrolMovement(float dt)
    {
        if (_patrolWaitTimer > 0f)
        {
            StopMoving();
            _patrolWaitTimer -= dt;
            if (_patrolWaitTimer <= 0f)
                PickNewPatrolDestination();
            return;
        }

        Vector2 pos = (Vector2)transform.position;
        float reach = Mathf.Max(0.05f, patrolWaypointReach);
        float reachSqr = reach * reach;
        Vector2 to = _patrolDestination - pos;

        if (to.sqrMagnitude <= reachSqr)
        {
            StopMoving();
            float pause = Mathf.Max(0f, patrolPauseAtWaypointDuration);
            if (pause <= 0f)
                PickNewPatrolDestination();
            else
                _patrolWaitTimer = pause;
            return;
        }

        MoveTowardsWorldPoint(_patrolDestination);
        if (to.sqrMagnitude > 0.01f)
            UpdateFacingTowards(to);
    }

    protected override void OnDamagedWithInfo(DamageInfo info)
    {
        base.OnDamagedWithInfo(info);
        TryEngageBossCombatIfNeeded();
    }

    protected override void PlayHitEffects()
    {
        if (hitSfx != null)
            AudioSource.PlayClipAtPoint(hitSfx, transform.position);
    }

    protected override void OnDeath()
    {
        base.OnDeath();
        if (triggerVictoryOnDeath && VictoryManager.Instance != null)
            VictoryManager.Instance.TriggerVictory();
    }

    public void PlayHitAnimTriggerOnce()
    {
        if (animator != null && !string.IsNullOrEmpty(animTriggerHit))
            animator.SetTrigger(animTriggerHit);
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

    /// <summary>Anim event: attack 1, first hit.</summary>
    public void OnBossAttack1Hit() => PerformBossAttackHit(true, secondSegment: false);

    /// <summary>Anim event: attack 1, second hit (second event on same clip).</summary>
    public void OnBossAttack1Hit2() => PerformBossAttackHit(true, secondSegment: true);

    /// <summary>Anim event: attack 2, first hit.</summary>
    public void OnBossAttack2Hit() => PerformBossAttackHit(false, secondSegment: false);

    /// <summary>Anim event: attack 2, second hit (optional).</summary>
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
        float oR = isAttack1
            ? (secondSegment ? attack1Hit2BoxOffset : attack1Hit1BoxOffset)
            : (secondSegment ? attack2Hit2BoxOffset : attack2Hit1BoxOffset);
        float oL = isAttack1
            ? (secondSegment ? attack1Hit2BoxOffsetLeft : attack1Hit1BoxOffsetLeft)
            : (secondSegment ? attack2Hit2BoxOffsetLeft : attack2Hit1BoxOffsetLeft);

        bool faceRight = _faceX >= 0f;

        float seg = faceRight
            ? (oR > 0f ? oR : 0f)
            : (oL > 0f ? oL : (oR > 0f ? oR : 0f));

        if (seg > 0f)
            return seg;

        float defR = attackHitboxOffset;
        float defL = attackHitboxOffsetLeft > 0f ? attackHitboxOffsetLeft : attackHitboxOffset;
        return faceRight ? defR : defL;
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
        ResetHealCooldown();
        StopHealRoutine();
        DespawnHealEffect();
        SpawnHealEffect();
        if (health != null && healAmount > 0 && healPerTick > 0)
            _healRoutine = StartCoroutine(CoGradualHeal());
    }

    /// <summary>Leaving heal: stop tick heal; VFX per <see cref="destroyHealEffectWhenLeavingHealState"/>.</summary>
    public void EndHealStateCleanup()
    {
        StopHealRoutine();
        if (destroyHealEffectWhenLeavingHealState)
            DespawnHealEffect();
        else
            DetachHealEffectScheduledDestroy();
    }

    /// <summary>Death or destroy: always stop heal ticks and remove heal VFX.</summary>
    public void CleanupBossHeal()
    {
        StopHealRoutine();
        DespawnHealEffect();
    }

    void StopHealRoutine()
    {
        if (_healRoutine == null) return;
        StopCoroutine(_healRoutine);
        _healRoutine = null;
    }

    void SpawnHealEffect()
    {
        if (healEffectPrefab == null)
            return;

        Transform anchor = healEffectAnchor;
        Vector3 pos = anchor != null ? anchor.position : transform.position;
        Quaternion rot = anchor != null ? anchor.rotation : transform.rotation;

        // Inactive anchor makes children inactiveInHierarchy so VFX won't render; parent under Boss root but keep anchor world pose.
        Transform parentTransform = (anchor != null && anchor.gameObject.activeInHierarchy) ? anchor : transform;

        _healEffectInstance = Instantiate(healEffectPrefab, pos, rot, parentTransform);
        _healEffectInstance.name = healEffectPrefab.name + " (spawned)";
        _healEffectInstance.SetActive(true);

        // Includes inactive children; prefab roots are often saved inactive.
        foreach (var ps in _healEffectInstance.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.gameObject.SetActive(true);
            var em = ps.emission;
            em.enabled = true;
            ps.Clear(true);
            ps.Play(true);
        }

        foreach (var sr in _healEffectInstance.GetComponentsInChildren<SpriteRenderer>(true))
        {
            sr.gameObject.SetActive(true);
            sr.enabled = true;
        }

        foreach (var anim in _healEffectInstance.GetComponentsInChildren<Animator>(true))
        {
            anim.enabled = true;
            anim.Rebind();
            anim.Update(0f);
            if (anim.runtimeAnimatorController != null)
                anim.Play(0, -1, 0f);
        }
    }

    void DetachHealEffectScheduledDestroy()
    {
        if (_healEffectInstance == null) return;
        Transform fx = _healEffectInstance.transform;
        fx.SetParent(null, true);
        float life = EstimateHealFxLifetime(_healEffectInstance);
        Destroy(_healEffectInstance, Mathf.Max(0.5f, life));
        _healEffectInstance = null;
    }

    static float EstimateHealFxLifetime(GameObject root)
    {
        float maxEnd = 0f;
        bool any = false;
        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            any = true;
            var main = ps.main;
            float startMax = main.startLifetime.constantMax;
            maxEnd = Mathf.Max(maxEnd, main.duration + startMax);
        }
        return any ? maxEnd : 2f;
    }

    void DespawnHealEffect()
    {
        if (_healEffectInstance == null) return;
        Destroy(_healEffectInstance);
        _healEffectInstance = null;
    }

    float HealTickWaitSeconds()
    {
        if (healTickInterval > 0f)
            return healTickInterval;
        int ticks = Mathf.Max(1, Mathf.CeilToInt(healAmount / (float)Mathf.Max(1, healPerTick)));
        return Mathf.Max(0.05f, healStateDuration / ticks);
    }

    IEnumerator CoGradualHeal()
    {
        int remaining = Mathf.Max(0, healAmount);
        float wait = HealTickWaitSeconds();

        while (remaining > 0)
        {
            if (health == null || health.IsDead)
                yield break;

            int room = health.maxHP - health.CurrentHP;
            if (room <= 0)
                break;

            int step = Mathf.Min(healPerTick, remaining, room);
            health.Heal(step);
            remaining -= step;

            if (remaining <= 0)
                break;

            yield return new WaitForSeconds(wait);
        }

        _healRoutine = null;
    }

    void OnDestroy()
    {
        CleanupBossHeal();
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

    /// <summary>Anim event: recovery end (clip end or early cancel).</summary>
    public void OnBossRecoveryEnd()
    {
        if (_stateMachine == null) return;
        if (_stateMachine.CurrentState != _recoveryState) return;
        recoveryFinished = true;
    }
}
