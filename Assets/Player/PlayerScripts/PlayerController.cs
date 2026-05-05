using UnityEngine;
using StateMachine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    // ==================== Inspector-tunable settings ====================

    [Header("Movement")]
    public float MoveSpeed = 5.0f;

    [Tooltip("If true, WASD is camera-relative on the XY walk plane. If false, raw world XY.")]
    [SerializeField] bool cameraRelativeMovement = true;

    [Tooltip("Walk plane normal for Rigibody2D XY motion (match camera pivot Z, usually (0,0,1)).")]
    [SerializeField] Vector3 walkPlaneNormal = Vector3.forward;

    [Header("Menu / cutscene")]
    [Tooltip("If true, ignore move/attack/dash/heal/block input (e.g. menu showcase player).")]
    public bool suppressPlayerInput;


    // ==================== Components (filled in Awake) ====================
    public Rigidbody2D rb;
    public Animator animator;
    private PlayerInputSet inputActions;




    // ==================== Statemachine ====================
    private StateMachine<PlayerController> stateMachine;
    private PlayerIdleState idleState;
    private PlayerMovementState moveState;
    private PlayerAttackState attackState;
    private PlayerDashState dashState;
    private PlayerHurtState hurtState;
    private PlayerDeathState deathState;
    private PlayerHealState healState;
    private PlayerBlockState blockState;

    readonly object _hurtInputBlockerKey = new object();
    readonly object _deathInputBlockerKey = new object();
    readonly object _healInputBlockerKey = new object();
    readonly object _blockInputBlockerKey = new object();
    public object HurtInputBlockerKey => _hurtInputBlockerKey;
    public object DeathInputBlockerKey => _deathInputBlockerKey;
    public object HealInputBlockerKey => _healInputBlockerKey;
    public object BlockInputBlockerKey => _blockInputBlockerKey;

    Health _health;

    [Header("Hurt / knockback (enters Hurt state; source-agnostic)")]
    [Tooltip("Knockback speed magnitude (world units/sec); direction from damage.")]
    public float hurtKnockbackSpeed = 6f;
    [Tooltip("Hitstun duration; matches knockback hold; locks input and plays hurt anim.")]
    public float hurtStateDuration = 0.28f;
    [Tooltip("Animator hurt trigger name. Empty = do not set.")]
    public string hurtHitAnimTrigger = "Hit";
    [Tooltip("Invincibility after hurt ends (seconds); separate from Health.invincibleDuration.")]
    public float postHurtInvincibilityDuration = 0.5f;

    [Header("Death")]
    [Tooltip("Animator bool set true on death (e.g. IsDead). Empty = do not set.")]
    public string deathAnimBoolParam = "IsDead";

    [Header("Heal (inventory consumable)")]
    [Tooltip("During channel, press this key to try consuming the item.")]
    public Key healKey = Key.H;
    [Tooltip("ItemData checked and removed from inventory.")]
    public ItemData healConsumableItem;
    [Tooltip("HP restored when channel completes (Health.Heal).")]
    [Min(1)]
    public int healHpAmount = 2;
    [Tooltip("Items removed per use.")]
    [Min(1)]
    public int healItemConsumeCount = 1;
    [Tooltip("Channel time (seconds); locks input and movement.")]
    [Min(0.01f)]
    public float healChannelDuration = 1.5f;
    [Tooltip("Animator bool true while channeling (cleared on exit), e.g. IsHealing. Empty = no anim.")]
    public string healAnimBoolParam = "";

    public bool pendingDeath { get; private set; }
    public bool wantHeal { get; private set; }
    public bool healFinished { get; set; }
    public bool healChannelCompleted { get; internal set; }
    public float healStateTimer { get; set; }

    public bool pendingHurt { get; private set; }
    public Vector2 QueuedHurtDirection { get; private set; }
    public bool RefillPostureAfterHurt { get; internal set; }
    public bool hurtFinished { get; set; }
    public float hurtStateTimer { get; set; }



    // ==================== State / transition inputs ====================
    public Vector2 Moveinput;
    public bool IsMoving => Moveinput.sqrMagnitude > 0.01f;
    public Vector2 LastMoveDiraction = Vector2.down;
    // Attack detail
    [Header("Combat")]
    public float attackCoolDown = 0.05f;

    public bool wantAttack { get; private set; }
    /// <summary>True when cooldown and recovery allow attack. Dash does not reset combo index.</summary>
    public bool canAttack => attackCoolDownTimer <= 0f && _attackRecoveryTimer <= 0f;
    public bool attackFinished { get; set; }

    /// <summary>Combo step index (1-based). Next attack uses this step. Dash does not reset.</summary>
    public int attackSequenceIndex { get; set; } = 1;

    private float attackCoolDownTimer;
    private float _attackRecoveryTimer;

    public void StartAttackCooldown() => attackCoolDownTimer = attackCoolDown;
    public void SetAttackRecovery(float duration) => _attackRecoveryTimer = duration;

    [System.Serializable]
    public class AttackStepConfig
    {
        [Tooltip("This attack segment duration.")]
        public float duration = 0.35f;
        [Tooltip("Time after this segment before the next attack input is accepted.")]
        public float recoveryTime = 0.3f;
        [Tooltip("Instant lunge along facing; 0 = none.")]
        public float lungeDistance = 0f;
        [Tooltip("Damage for this segment.")]
        public float attackDamage = 1f;
        [Tooltip("Deprecated: player attacks do not apply knockback; hitbox passes 0. Kept for serialization.")]
        public float knockbackForce = 0f;
        [Tooltip("Hitbox offset along attack direction when per-direction offsets are off.")]
        public float hitboxOffset = 0.5f;
        [Tooltip("If true, use separate offsets per WASD direction.")]
        public bool usePerDirectionOffsets = false;
        [Tooltip("Hitbox offset when attacking up (W).")]
        public float hitboxOffsetUp = 0.5f;
        [Tooltip("Hitbox offset when attacking down (S).")]
        public float hitboxOffsetDown = 0.5f;
        [Tooltip("Hitbox offset when attacking left (A).")]
        public float hitboxOffsetLeft = 0.5f;
        [Tooltip("Hitbox offset when attacking right (D).")]
        public float hitboxOffsetRight = 0.5f;

        [Tooltip("Swing SFX for this segment; empty uses player default attack sound.")]
        public AudioClip swingSound;

        /// <summary>Hitbox offset for the given attack direction.</summary>
        public float GetHitboxOffsetForDirection(Vector2 dir)
        {
            if (!usePerDirectionOffsets) return hitboxOffset;
            if (dir.sqrMagnitude < 0.01f) return hitboxOffsetDown;
            if (Mathf.Abs(dir.y) >= Mathf.Abs(dir.x))
                return dir.y > 0 ? hitboxOffsetUp : hitboxOffsetDown;
            return dir.x > 0 ? hitboxOffsetRight : hitboxOffsetLeft;
        }
    }

    [Header("Dash")]
    [Tooltip("Dash travel distance.")]
    public float dashDistance = 5f;
    [Tooltip("Dash duration.")]
    public float dashDuration = 0.1f;
    [Tooltip("Dash cooldown.")]
    public float dashCooldown = 1f;

    public bool wantDash { get; private set; }
    public bool canDash => _dashCooldownTimer <= 0f;
    public bool dashFinished { get; set; }
    private float _dashCooldownTimer;
    public void SetDashCooldown(float duration) => _dashCooldownTimer = duration;

    /// <summary>Invincibility flag; true during dash (damage logic may read this).</summary>
    public bool IsInvincible { get; set; }

    [Header("Attack Sequence")]
    public AttackStepConfig[] attackSteps = new AttackStepConfig[]
    {
        new AttackStepConfig { duration = 0.35f, recoveryTime = 0.3f, lungeDistance = 0.3f, attackDamage = 1f, knockbackForce = 0f, hitboxOffset = 0.4f },
        new AttackStepConfig { duration = 0.35f, recoveryTime = 0.3f, lungeDistance = 0.35f, attackDamage = 2f, knockbackForce = 0f, hitboxOffset = 0.6f },
    };

    [Header("Attack hitbox")]
    [Tooltip("Child with Collider2D trigger.")]
    public AttackHitbox attackHitbox;

    [Header("Attack audio")]
    [Tooltip("Default swing sound; override per segment below with Swing Sound.")]
    [SerializeField] AudioClip attackSwingSound;
    [SerializeField, Range(0f, 1f)] float attackSwingVolume = 1f;
    [Tooltip("Optional. Else uses AudioSource on this object; else spawns a one-shot 2D source.")]
    [SerializeField] AudioSource attackSwingAudioSource;

    [Header("Block")]
    [Tooltip("Hold block for this many seconds after press.")]
    [Min(0.05f)]
    public float blockDuration = 1.2f;
    [Tooltip("Cooldown after block ends.")]
    public float blockCooldown = 0.35f;
    [Tooltip("Block key (Input System Key).")]
    public Key blockKey = Key.R;
    [Tooltip("Animator bool while blocking (e.g. IsBlocking). Empty = skip.")]
    public string blockShieldAnimBoolParam = "IsBlocking";
    [Header("Block success: big attack explosion")]
    [Tooltip("Prefab VFX or AreaDamageBurst2D; only damages Enemy-tagged targets (set when spawned).")]
    public GameObject blockExplosionPrefab;
    [Tooltip("Spawn offset from player. See local toggle below.")]
    public Vector2 blockExplosionSpawnOffset;
    [Tooltip("If true, offset is in local XY (follows rotation / facing).")]
    public bool blockExplosionSpawnOffsetLocal;

    public bool wantBlock { get; private set; }
    public bool blockFinished { get; set; }
    public float blockStateTimer { get; set; }
    /// <summary>After a successful block vs enemy damage, next attack consumes this and spawns the explosion.</summary>
    public bool HasChargedExplosionForNextAttack { get; private set; }

    [Header("Debug")]
    [Tooltip("If true, log state changes to the Console.")]
    public bool debugStateMachine = false;

    float _contactKnockbackUntil;
    Vector2 _contactKnockbackVelocity;
    float _blockCooldownTimer;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    bool _hasWarnedBlockExplosionPrefab;
#endif

    /// <summary>
    /// When input is locked (hurt stun) velocity is cleared each frame. Contact knockback is registered here and applied in <see cref="LateUpdate"/> over state-machine velocity.
    /// </summary>
    public void ApplyContactKnockback(Vector2 worldVelocity, float holdSeconds)
    {
        if (holdSeconds <= 0f || worldVelocity.sqrMagnitude < 1e-6f)
            return;
        _contactKnockbackVelocity = worldVelocity;
        _contactKnockbackUntil = Time.time + holdSeconds;
    }


    // ==================== Unity lifecycle ====================



    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        inputActions = new PlayerInputSet();
        animator = GetComponentInChildren<Animator>();

        if (attackHitbox != null && attackHitbox.owner == null)
            attackHitbox.owner = gameObject;

        _health = GetComponent<Health>();

        inputActions.Enable();
    }

    void OnEnable()
    {
        if (_health == null)
            _health = GetComponent<Health>();
        if (_health != null)
        {
            _health.OnDamagedWithInfo += OnPlayerDamagedWithInfo;
            _health.OnDeath.AddListener(OnPlayerDeath);
        }
    }

    void OnDisable()
    {
        if (_health != null)
        {
            _health.OnDamagedWithInfo -= OnPlayerDamagedWithInfo;
            _health.OnDeath.RemoveListener(OnPlayerDeath);
        }
    }

    void OnPlayerDeath() => pendingDeath = true;

    /// <summary>True if already in death state (avoid global death transition re-entering every frame).</summary>
    public bool IsCurrentStateDeath() => stateMachine != null && stateMachine.CurrentState is PlayerDeathState;

    public bool IsCurrentStateBlock() => stateMachine != null && stateMachine.CurrentState is PlayerBlockState;

    public bool CanEnterBlock()
    {
        if (_health == null || _health.IsDead) return false;
        if (_blockCooldownTimer > 0f) return false;
        if (stateMachine == null) return false;
        var st = stateMachine.CurrentState;
        return st == idleState || st == moveState;
    }

    public void SetBlockCooldown(float seconds) => _blockCooldownTimer = Mathf.Max(0f, seconds);

    // ==================== NPC stat upgrades (StatTrainer, etc.) ====================

    /// <summary>Add max and current HP (same as <see cref="Health.AddMaxHP"/>).</summary>
    public void ApplyTrainerBonusMaxHp(int delta)
    {
        if (delta <= 0) return;
        if (_health == null) _health = GetComponent<Health>();
        if (_health == null) return;
        _health.AddMaxHP(delta);
    }

    /// <summary>Permanent move speed bonus (stacks).</summary>
    public void ApplyTrainerBonusMoveSpeed(float delta)
    {
        if (Mathf.Approximately(delta, 0f)) return;
        MoveSpeed = Mathf.Max(0.1f, MoveSpeed + delta);
    }

    /// <summary>Add flat damage to every basic-attack step.</summary>
    public void ApplyTrainerBonusAttackDamageAllSteps(float delta)
    {
        if (Mathf.Approximately(delta, 0f) || attackSteps == null || attackSteps.Length == 0) return;
        for (int i = 0; i < attackSteps.Length; i++)
            attackSteps[i].attackDamage = Mathf.Max(0.01f, attackSteps[i].attackDamage + delta);
    }

    /// <summary>True if heal channel can start: has item, not full HP, alive.</summary>
    public bool CanStartHeal()
    {
        if (_health == null || _health.IsDead) return false;
        if (_health.CurrentHP >= _health.maxHP) return false;
        if (healConsumableItem == null || !healConsumableItem.IsValid) return false;
        if (healChannelDuration <= 0f || healHpAmount <= 0) return false;
        var inv = GetComponent<Inventory>();
        if (inv == null) return false;
        return inv.HasCount(healConsumableItem, healItemConsumeCount);
    }

    void OnPlayerDamagedWithInfo(DamageInfo info)
    {
        if (_health == null || _health.IsDead) return;
        if (info.amount <= 0f) return;
        if (info.source != null && info.source.transform.root == transform) return;

        if (IsCurrentStateBlock() && IsEnemyDamageSource(info.source))
        {
            _health.Heal(Mathf.RoundToInt(info.amount));
            HasChargedExplosionForNextAttack = true;
            return;
        }

        var cp = GetComponent<CombatPosture>();
        if (cp != null && cp.enabled)
            cp.ApplyPostureDamageFromHp(info.amount);

        bool fullReact = cp == null || !cp.enabled || cp.MaxPosture <= 0f || cp.LastHitBrokePosture;
        if (!fullReact) return;

        QueuedHurtDirection = ResolveKnockbackDirection(info);
        RefillPostureAfterHurt = fullReact;
        pendingHurt = true;
    }

    Vector2 ResolveKnockbackDirection(DamageInfo info)
    {
        if (info.knockbackDirection.sqrMagnitude > 0.01f)
            return info.knockbackDirection.normalized;
        if (info.source != null)
        {
            Vector2 away = (Vector2)transform.position - (Vector2)info.source.transform.position;
            if (away.sqrMagnitude > 1e-6f)
                return away.normalized;
        }
        return Vector2.right;
    }

    public void ConsumePendingHurtEntry() => pendingHurt = false;

    public static bool IsEnemyDamageSource(GameObject src)
    {
        if (src == null) return false;
        if (src.CompareTag("Enemy")) return true;
        if (src.GetComponentInParent<EnemyBase>() != null) return true;
        return false;
    }

    /// <summary>On attack start: if block charge is ready, spawn explosion and clear flag.</summary>
    public void TryConsumeChargedExplosionOnAttack()
    {
        if (!HasChargedExplosionForNextAttack)
            return;
        if (blockExplosionPrefab == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_hasWarnedBlockExplosionPrefab)
            {
                _hasWarnedBlockExplosionPrefab = true;
                Debug.LogWarning("[PlayerController] Block charged explosion ready but blockExplosionPrefab is not assigned (Inspector).", this);
            }
#endif
            return;
        }

        HasChargedExplosionForNextAttack = false;
        Vector2 pos = rb != null ? rb.position : (Vector2)transform.position;
        if (blockExplosionSpawnOffset.sqrMagnitude > 1e-8f)
        {
            if (blockExplosionSpawnOffsetLocal)
                pos += (Vector2)transform.TransformVector(new Vector3(blockExplosionSpawnOffset.x, blockExplosionSpawnOffset.y, 0f));
            else
                pos += blockExplosionSpawnOffset;
        }
        var go = Instantiate(blockExplosionPrefab, pos, Quaternion.identity);
        if (go.TryGetComponent<AreaDamageBurst2D>(out var burst))
        {
            burst.damageSource = gameObject;
            burst.onlyDamageEnemyTag = true;
        }
    }

    /// <summary>Per-attack swing: <see cref="AttackStepConfig.swingSound"/> first, else <see cref="attackSwingSound"/>.</summary>
    public void PlayAttackSwingSound(int stepIndex1Based)
    {
        AudioClip clip = null;
        if (attackSteps != null && stepIndex1Based >= 1 && stepIndex1Based <= attackSteps.Length)
            clip = attackSteps[stepIndex1Based - 1].swingSound;
        if (clip == null)
            clip = attackSwingSound;
        if (clip == null) return;

        AudioSource src = attackSwingAudioSource != null ? attackSwingAudioSource : GetComponent<AudioSource>();
        if (src != null)
        {
            src.PlayOneShot(clip, attackSwingVolume);
            return;
        }

        var temp = new GameObject("AttackSwingOneShot");
        temp.transform.position = transform.position;
        var one = temp.AddComponent<AudioSource>();
        one.spatialBlend = 0f;
        one.PlayOneShot(clip, attackSwingVolume);
        Destroy(temp, clip.length + 0.05f);
    }

    /// <summary>Call when hurt state ends: starts post-hurt invincibility (default 0.5s, see postHurtInvincibilityDuration).</summary>
    public void ApplyPostHurtInvincibility()
    {
        if (postHurtInvincibilityDuration <= 0f) return;
        if (_health == null) _health = GetComponent<Health>();
        if (_health != null && !_health.IsDead)
            _health.SetInvincibleTimer(postHurtInvincibilityDuration);
    }

    private void Start()
    {
        //initialize stateMachine
        idleState = new PlayerIdleState();
        moveState = new PlayerMovementState();
        attackState = new PlayerAttackState();
        dashState = new PlayerDashState();
        hurtState = new PlayerHurtState();
        deathState = new PlayerDeathState();
        healState = new PlayerHealState();
        blockState = new PlayerBlockState();

        stateMachine = new StateMachine<PlayerController>();
        stateMachine.Initialize(this, idleState);

        stateMachine.AddGlobalTransition(deathState, ctx => ctx.pendingDeath && !ctx.IsCurrentStateDeath());
        stateMachine.AddGlobalTransition(hurtState, ctx => ctx.pendingHurt);
        // Dash can interrupt from most states; death and hurt take priority.
        stateMachine.AddGlobalTransition(dashState, ctx => ctx.wantDash && ctx.canDash);
        stateMachine.AddGlobalTransition(blockState, ctx => ctx.wantBlock && ctx.CanEnterBlock());
        stateMachine.AddTransition(dashState, idleState, ctx => ctx.dashFinished);
        stateMachine.AddTransition(blockState, idleState, ctx => ctx.blockFinished);
        stateMachine.AddTransition(hurtState, idleState, ctx => ctx.hurtFinished);
        stateMachine.AddTransition(healState, idleState, ctx => ctx.healFinished);

        stateMachine.AddTransition(idleState, healState, ctx => ctx.wantHeal && ctx.CanStartHeal());
        stateMachine.AddTransition(moveState, healState, ctx => ctx.wantHeal && ctx.CanStartHeal());

        // Attack before idle/move swap: wantAttack is only true on press frame; IsMoving would steal transition otherwise.
        stateMachine.AddTransition(idleState, attackState, ctx => ctx.wantAttack && ctx.canAttack);
        stateMachine.AddTransition(moveState, attackState, ctx => ctx.wantAttack && ctx.canAttack);
        stateMachine.AddTransition(idleState, moveState, ctx => ctx.IsMoving);
        stateMachine.AddTransition(moveState, idleState, ctx => !ctx.IsMoving);
        stateMachine.AddTransition(attackState, idleState, ctx => ctx.attackFinished);

        stateMachine.OnStateChanged += (oldState, newState) =>
        {
            if (!debugStateMachine) return;
            string oldName = oldState?.GetType().Name ?? "null";
            string newName = newState?.GetType().Name ?? "null";
            Debug.Log($"[StateMachine] {oldName} → {newName}");
        };
    }

    private void Update()
    {
        if (pendingDeath && stateMachine != null && IsCurrentStateDeath())
        {
            stateMachine.Update(Time.deltaTime);
            return;
        }

        if (InputReadingBlocked())
        {
            Moveinput = Vector2.zero;
            wantAttack = false;
            wantDash = false;
            wantHeal = false;
            wantBlock = false;
        }
        else
        {
            //Movement
            Moveinput = inputActions.Player.Movement.ReadValue<Vector2>();

            //Attack
            wantAttack = inputActions.Player.Attack.WasPressedThisFrame();
            // Dash (Space; use FindAction if Input Actions were regenerated)
            var dashAction = inputActions.Player.Get().FindAction("Dash", throwIfNotFound: false);
            wantDash = dashAction != null ? dashAction.WasPressedThisFrame() : Keyboard.current?.spaceKey.wasPressedThisFrame ?? false;
            wantHeal = Keyboard.current != null && Keyboard.current[healKey].wasPressedThisFrame;
            wantBlock = Keyboard.current != null && Keyboard.current[blockKey].wasPressedThisFrame;
        }

        if (attackCoolDownTimer > 0f)
            attackCoolDownTimer -= Time.deltaTime;
        if (_attackRecoveryTimer > 0f)
            _attackRecoveryTimer -= Time.deltaTime;
        if (_dashCooldownTimer > 0f)
            _dashCooldownTimer -= Time.deltaTime;
        if (_blockCooldownTimer > 0f)
            _blockCooldownTimer -= Time.deltaTime;

        if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
        {
            string stateName = stateMachine.CurrentState?.GetType().Name ?? "null";
            Debug.Log($"[StateMachine] Current state: {stateName}");
        }

        stateMachine.Update(Time.deltaTime);
    }

    void LateUpdate()
    {
        if (rb == null) return;

        if (Time.time < _contactKnockbackUntil)
        {
            rb.linearVelocity = _contactKnockbackVelocity;
            return;
        }

        if (InputReadingBlocked())
            rb.linearVelocity = Vector2.zero;
    }

    /// <summary>
    /// Maps normalized input (Input System Movement axes) to a unit direction on world XY for <see cref="Rigidbody2D.linearVelocity"/>.
    /// </summary>
    public Vector2 TransformMoveInputToWorldPlanar(Vector2 normalizedInput)
    {
        if (normalizedInput.sqrMagnitude < 1e-6f)
            return Vector2.zero;

        Vector2 inputN = normalizedInput.normalized;
        if (!cameraRelativeMovement)
            return inputN;

        Camera cam = Camera.main;
        if (cam == null)
            return inputN;

        Vector3 n = walkPlaneNormal.sqrMagnitude > 1e-6f ? walkPlaneNormal.normalized : Vector3.forward;
        Vector3 r3 = Vector3.ProjectOnPlane(cam.transform.right, n);
        Vector3 u3 = Vector3.ProjectOnPlane(cam.transform.up, n);
        if (r3.sqrMagnitude < 1e-6f || u3.sqrMagnitude < 1e-6f)
            return inputN;
        r3.Normalize();
        u3.Normalize();
        Vector2 r2 = new Vector2(r3.x, r3.y);
        Vector2 u2 = new Vector2(u3.x, u3.y);
        Vector2 world = r2 * inputN.x + u2 * inputN.y;
        return world.sqrMagnitude > 1e-6f ? world.normalized : inputN;
    }

    bool InputReadingBlocked() => PlayerInputBlocker.IsBlocked || suppressPlayerInput;

    private void OnDestroy()
    {
        PlayerInputBlocker.Release(_hurtInputBlockerKey);
        PlayerInputBlocker.Release(_deathInputBlockerKey);
        PlayerInputBlocker.Release(_healInputBlockerKey);
        PlayerInputBlocker.Release(_blockInputBlockerKey);
        inputActions?.Disable();
    }
}
