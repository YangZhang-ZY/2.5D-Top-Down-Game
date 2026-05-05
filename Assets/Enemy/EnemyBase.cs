using UnityEngine;

/// <summary>
/// Shared enemy base.
///
/// Features
/// - TD focus: primary target (e.g. crystal) first; player taunt or taking damage pulls aggro; leash returns to primary.
/// - Auto-find player (Tag Player).
/// - Attack cooldown.
/// - On hit: Hit anim, SFX, flash hook, knockback.
/// - On death: stop, death anim, disable collider, destroy after <see cref="destroyDelayAfterDeath"/>.
///
/// Required components
/// - Same object: Health, Rigidbody2D.
/// - Self or child: Animator (Idle/Run/Hit/Death, etc.).
///
/// Subclasses must
/// - Override <see cref="UpdateAI"/>: when to idle / chase / attack / heal.
/// - Override <see cref="PerformAttack"/>: melee swing / ranged shot / heal, etc.
///
/// Setup
/// 1. Create a script inheriting <see cref="EnemyBase"/> (e.g. Warrior).
/// 2. Implement <see cref="UpdateAI"/> and <see cref="PerformAttack"/>.
/// 3. Add Health, Rigidbody2D, and the subclass on the enemy.
/// 4. Ensure the player's Tag is "Player".
/// </summary>
[RequireComponent(typeof(Health), typeof(Rigidbody2D))]
public abstract class EnemyBase : MonoBehaviour
{
    // ==================== Inspector ====================

    [Header("TD target (crystal, etc.)")]
    [Tooltip("Primary objective (base crystal). If unset, resolved in Start via BaseTarget or Tag.")]
    public Transform primaryTarget;

    [Tooltip("When Primary Target is empty, find by this Tag (define in Tag Manager). BaseTarget singleton wins if present.")]
    public string autoFindBaseTag = "Base";

    [Tooltip("If enabled, movement ignores Primary Target until the player is in chase range or already aggroed. Good for patrol mobs in a hazard zone.")]
    public bool ignorePrimaryTargetForMovement;

    [Tooltip("Within this distance, prefer chasing the player (taunt).")]
    public float playerProvokeRange = 2.5f;

    [Header("Movement & sensing")]
    [Tooltip("Move speed.")]
    public float moveSpeed = 2f;

    [Tooltip("Start chasing the player within this world distance (player as target only).")]
    public float chaseRange = 8f;

    [Tooltip("If >0, leash when focused on player uses max(chaseRange, this). Bosses can use a larger value so GetMoveTarget is not null when the player starts outside chaseRange.")]
    public float maxPlayerEngageRange = 0f;

    [Tooltip("Can attack inside this range (subclasses refine melee vs ranged).")]
    public float attackRange = 1.5f;

    [Tooltip("How close to stop when chasing; if >0, separate from attackRange. Ranged bosses can use a huge attackRange and a small stop distance so they still walk into position.")]
    public float moveStopDistance = 0f;

    [Tooltip("Seconds between attacks.")]
    public float attackCooldown = 1.0f;

    [Header("Hit & knockback")]
    [Tooltip("Whether this enemy can be knocked back (false for Boss / heavy).")]
    public bool canBeKnockedBack = true;

    [Tooltip("Knockback resistance: 0 = full, 1 = immune.")]
    [Range(0f, 1f)]
    public float knockbackResistance = 0f;

    [Tooltip("After knockback, AI skips writing linearVelocity / StopMoving briefly so Chase/Idle does not cancel knock each frame (independent of Rigidbody2D mass).")]
    public float knockbackMovementPauseDuration = 0.15f;

    [Tooltip("Hit flash duration in seconds; pair with HitFlash-style scripts.")]
    public float hitFlashDuration = 0.1f;

    [Tooltip("Hit sound (optional).")]
    public AudioClip hitSfx;

    [Header("Death")]
    [Tooltip("Delay before destroying this object (includes Boss) so death anim can play. 0 destroys ASAP (next frame end).")]
    [Min(0f)]
    public float destroyDelayAfterDeath = 2f;

    [Header("Animator param names (match Controller)")]
    [Tooltip("Float, usually magnitude for Idle/Run blend.")]
    public string animParamSpeed = "Speed";

    [Tooltip("Bool, true when dead.")]
    public string animParamIsDead = "IsDead";

    [Tooltip("Trigger on hurt.")]
    public string animTriggerHit = "Hit";


    // ==================== Cached components ====================

    /// <summary>Player transform; found in Awake via Tag Player.</summary>
    protected Transform player;

    /// <summary>2D body for move and knockback.</summary>
    protected Rigidbody2D rb;

    /// <summary>Animator, often on a child (sprite).</summary>
    protected Animator animator;

    /// <summary>Health component.</summary>
    protected Health health;


    // ==================== Runtime state ====================

    /// <summary>Attack cooldown remaining; can attack when &lt;= 0.</summary>
    protected float attackCooldownTimer;

    /// <summary>Dead flag for state machine transitions.</summary>
    public bool isDead { get; protected set; }

    /// <summary>Last damage info for knockback direction/strength</summary>
    protected DamageInfo lastDamageInfo;

    /// <summary>Whether <see cref="lastDamageInfo"/> is valid this hit.</summary>
    protected bool hasLastDamageInfo;

    /// <summary>Prefer player when taunted or damaged (walls etc. can extend later).</summary>
    protected bool playerAggroActive;

    /// <summary>Time left on knockback pause (<see cref="knockbackMovementPauseDuration"/>).</summary>
    protected float knockbackMovementPauseTimer;


    // ==================== Unity lifecycle ====================

    /// <summary>
    /// Grabs components and finds the player.
    /// Subclasses: call base.Awake() first if overriding.
    /// </summary>
    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponentInChildren<Animator>();
        health = GetComponent<Health>();

        // Find by Tag (set the player object's Tag to "Player").
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;
    }

    /// <summary>
    /// Resolves Primary Target. Subclasses overriding Start should call base.Start().
    /// </summary>
    protected virtual void Start()
    {
        ResolvePrimaryTarget();
    }

    /// <summary>
    /// When primaryTarget is unset: prefer <see cref="BaseTarget"/>, else Tag lookup.
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
            // Tag not defined in project — ignore.
        }
    }

    /// <summary>
    /// Subscribe to Health damage/death.
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
    /// Unsubscribe to avoid duplicate handlers.
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
    /// Tick cooldown, run AI, push animator params.
    /// </summary>
    protected virtual void Update()
    {
        if (isDead) return;

        // Attack cooldown
        if (attackCooldownTimer > 0f)
            attackCooldownTimer -= Time.deltaTime;

        // Subclass AI
        UpdateAI();

        // Animator (e.g. Speed)
        UpdateAnimatorParameters();
    }


    // ==================== Abstract API ====================

    /// <summary>
    /// AI entry: idle / chase / attack / heal from distances; may use MoveTowardsPlayer, StopMoving, CanAttack, PerformAttack.
    /// </summary>
    protected abstract void UpdateAI();

    /// <summary>
    /// Attack implementation: melee anim + hitbox events, spawn projectile, self-heal + Heal anim, etc.
    /// </summary>
    protected abstract void PerformAttack();


    // ==================== Animation (override as needed) ====================

    /// <summary>
    /// Default sets Speed only. Override for AttackIndex, IsBlocking, etc.
    /// </summary>
    protected virtual void UpdateAnimatorParameters()
    {
        if (animator == null) return;
        animator.SetFloat(animParamSpeed, rb.linearVelocity.magnitude);
    }


    // ==================== Damage & death ====================

    /// <summary>
    /// Health OnDamaged (amount only): hit feedback; knockback uses OnDamagedWithInfo.
    /// </summary>
    protected virtual void OnDamaged(float dmg)
    {
        if (isDead) return;
        PlayHitEffects();
    }

    /// <summary>
    /// Health OnDamagedWithInfo: store knockback and apply this frame.
    /// </summary>
    protected virtual void OnDamagedWithInfo(DamageInfo info)
    {
        if (isDead) return;
        lastDamageInfo = info;
        hasLastDamageInfo = true;
        if (IsDamageFromPlayer(info))
            playerAggroActive = true;
        if (ShouldApplyKnockbackFromDamage(info))
            ApplyKnockbackFromLastDamage();
    }

    /// <summary>Default: always knock back; override e.g. only on posture break.</summary>
    protected virtual bool ShouldApplyKnockbackFromDamage(DamageInfo info) => true;

    /// <summary>
    /// Health OnDeath.
    /// </summary>
    protected virtual void OnDeath()
    {
        isDead = true;
        knockbackMovementPauseTimer = 0f;

        // Stop
        rb.linearVelocity = Vector2.zero;

        // Death anim
        if (animator != null && !string.IsNullOrEmpty(animParamIsDead))
            animator.SetBool(animParamIsDead, true);

        // Disable collider so corpse does not block
        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        Destroy(gameObject, Mathf.Max(0f, destroyDelayAfterDeath));
    }

    /// <summary>
    /// Hit anim + SFX; flash via separate HitFlash-style component if desired.
    /// </summary>
    protected virtual void PlayHitEffects()
    {
        if (animator != null && !string.IsNullOrEmpty(animTriggerHit))
            animator.SetTrigger(animTriggerHit);

        if (hitSfx != null)
            AudioSource.PlayClipAtPoint(hitSfx, transform.position);
    }

    /// <summary>
    /// Apply knockback from last DamageInfo: add to velocity; knockbackForce is world units/sec scaled by <see cref="knockbackResistance"/>, not divided by mass.
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
        rb.linearVelocity += dir * effectiveForce;

        knockbackMovementPauseTimer = Mathf.Max(knockbackMovementPauseTimer, knockbackMovementPauseDuration);
    }

    /// <summary>Decrement knockback pause (call at start of state machine Update).</summary>
    protected void TickKnockbackMovementPause(float deltaTime)
    {
        if (knockbackMovementPauseTimer > 0f)
            knockbackMovementPauseTimer -= deltaTime;
    }

    /// <summary>While true, AI should not write velocity or zero it in StopMoving.</summary>
    protected bool IsKnockbackPausingMovement => knockbackMovementPauseTimer > 0f;

    /// <summary>Move toward a world point at <see cref="moveSpeed"/>; skipped during knockback pause.</summary>
    protected void MoveTowardsWorldPoint(Vector2 worldPoint)
    {
        Vector2 dir = worldPoint - (Vector2)transform.position;
        ApplyChaseVelocityFromDirection(dir);
    }

    void ApplyChaseVelocityFromDirection(Vector2 toTargetDelta)
    {
        if (rb == null) return;
        if (IsKnockbackPausingMovement) return;
        if (toTargetDelta.sqrMagnitude < 0.01f) return;
        rb.linearVelocity = toTargetDelta.normalized * moveSpeed;
    }


    // ==================== Helpers for subclasses ====================

    /// <summary>Whether the player is within <paramref name="range"/>.</summary>
    /// <param name="sqrDist">Squared distance to player (no sqrt).</param>
    protected bool IsPlayerInRange(float range, out float sqrDist)
    {
        sqrDist = float.MaxValue;
        if (player == null) return false;
        sqrDist = (player.position - transform.position).sqrMagnitude;
        return sqrDist <= range * range;
    }

    /// <summary>True when attack cooldown has elapsed.</summary>
    protected bool CanAttack()
    {
        return attackCooldownTimer <= 0f;
    }

    /// <summary>Reset attack cooldown timer.</summary>
    protected void ResetAttackCooldown()
    {
        attackCooldownTimer = attackCooldown;
    }

    /// <summary>Move toward the player.</summary>
    protected void MoveTowardsPlayer()
    {
        if (player == null) return;
        Vector2 dir = ((Vector2)player.position - (Vector2)transform.position);
        ApplyChaseVelocityFromDirection(dir);
    }

    /// <summary>Leash radius when focused on the player (matches disengage).</summary>
    protected float PlayerLeashRadius => maxPlayerEngageRange > 0f ? Mathf.Max(chaseRange, maxPlayerEngageRange) : chaseRange;

    /// <summary>Move focus: aggro player, else primary, else in-range player.</summary>
    public virtual Transform GetMoveTarget()
    {
        if (playerAggroActive && player != null)
            return player;
        if (!ignorePrimaryTargetForMovement && primaryTarget != null)
            return primaryTarget;
        if (player != null)
        {
            float _;
            if (IsPlayerInRange(PlayerLeashRadius, out _))
                return player;
        }
        return null;
    }

    /// <summary>Chase current target (crystal or player).</summary>
    protected void MoveTowardsCurrentTarget()
    {
        var t = GetMoveTarget();
        if (t == null || rb == null) return;
        Vector2 dir = ((Vector2)t.position - (Vector2)transform.position);
        ApplyChaseVelocityFromDirection(dir);
    }

    /// <summary>Current target within attack range.</summary>
    public bool IsCurrentTargetInAttackRange()
    {
        var t = GetMoveTarget();
        if (t == null) return false;
        float sqr = (t.position - transform.position).sqrMagnitude;
        return sqr <= attackRange * attackRange;
    }

    /// <summary>Idle vs move: use <see cref="moveStopDistance"/> if set, else <see cref="attackRange"/>.</summary>
    public float GetEffectiveMoveStopDistance()
    {
        return moveStopDistance > 1e-4f ? moveStopDistance : attackRange;
    }

    /// <summary>Still farther than stop distance from current move target.</summary>
    public bool IsCurrentTargetBeyondMoveStopDistance()
    {
        var t = GetMoveTarget();
        if (t == null) return false;
        float stop = GetEffectiveMoveStopDistance();
        float sqr = (t.position - transform.position).sqrMagnitude;
        return sqr > stop * stop;
    }

    /// <summary>
    /// For Stay→Chase: always true for primary; for player, inside chase radius.
    /// </summary>
    public virtual bool IsCurrentTargetInChaseRange()
    {
        var t = GetMoveTarget();
        if (t == null) return false;
        if (!ignorePrimaryTargetForMovement && primaryTarget != null && t == primaryTarget) return true;
        float r = t == player ? PlayerLeashRadius : chaseRange;
        return (t.position - transform.position).sqrMagnitude <= r * r;
    }

    /// <summary>Tick taunt range and leash (call from subclass Update).</summary>
    protected void UpdateAggroProvoke()
    {
        if (player == null) return;
        float sqr = (player.position - transform.position).sqrMagnitude;
        if (sqr <= playerProvokeRange * playerProvokeRange)
            playerAggroActive = true;
        else if (playerAggroActive && sqr > PlayerLeashRadius * PlayerLeashRadius)
            playerAggroActive = false;
    }

    protected static bool IsDamageFromPlayer(DamageInfo info)
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

    /// <summary>Stop movement.</summary>
    protected void StopMoving()
    {
        if (rb == null) return;
        if (IsKnockbackPausingMovement) return;
        rb.linearVelocity = Vector2.zero;
    }
}
