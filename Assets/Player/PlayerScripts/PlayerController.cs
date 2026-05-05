using UnityEngine;
using StateMachine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    // ==================== 属性配置（Inspector 可调） ====================

    [Header("Movement")]
    public float MoveSpeed = 5.0f;

    [Tooltip("开启后，WASD 沿「摄像机在 XY 行走平面上」的左右/上下移动；转镜头后按键相对屏幕不变。关闭则用世界 XY。")]
    [SerializeField] bool cameraRelativeMovement = true;

    [Tooltip("Rigidbody2D 在 XY 上移动时与 Camera Pivot 绕 Z 转一致，填 (0,0,1)。")]
    [SerializeField] Vector3 walkPlaneNormal = Vector3.forward;


    // ==================== 组件引用（Awake 获取） ====================
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

    [Header("受击 / 击退（统一由受伤进 Hurt 状态，与伤害来源无关）")]
    [Tooltip("受伤时击退速度大小（世界单位/秒），方向由伤害来源决定")]
    public float hurtKnockbackSpeed = 6f;
    [Tooltip("硬直时间：与击退保持时间一致，期间锁输入并播放受击动画")]
    public float hurtStateDuration = 0.28f;
    [Tooltip("Animator 受击 Trigger 名（空则不调）")]
    public string hurtHitAnimTrigger = "Hit";
    [Tooltip("受击状态结束后开始的无敌时间（秒），与 Health.invincibleDuration 无关")]
    public float postHurtInvincibilityDuration = 0.5f;

    [Header("死亡")]
    [Tooltip("进入死亡状态后 Animator Bool 置为 true（如 IsDead）；空则不调")]
    public string deathAnimBoolParam = "IsDead";

    [Header("回血（背包消耗品）")]
    [Tooltip("读条期间按此键尝试使用消耗品（可在本组件上改键）")]
    public Key healKey = Key.H;
    [Tooltip("使用时检查并扣除的 ItemData（拖入资源）")]
    public ItemData healConsumableItem;
    [Tooltip("读条结束后恢复的生命值（Health.Heal）")]
    [Min(1)]
    public int healHpAmount = 2;
    [Tooltip("每次使用扣除的数量")]
    [Min(1)]
    public int healItemConsumeCount = 1;
    [Tooltip("读条时间（秒），期间锁输入、无法移动")]
    [Min(0.01f)]
    public float healChannelDuration = 1.5f;
    [Tooltip("读条期间 Animator Bool 为 true（退出时自动 false），如 IsHealing；空则不调动画")]
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



    // ==================== 供状态和转换条件使用的属性 ====================
    public Vector2 Moveinput;
    public bool IsMoving => Moveinput.sqrMagnitude > 0.01f;
    public Vector2 LastMoveDiraction = Vector2.down;
    // Attack detail
    [Header("Combat")]
    public float attackCoolDown = 0.05f;

    public bool wantAttack { get; private set; }
    /// <summary>冷却和恢复都结束时可攻击。冲刺不刷新攻击顺序。</summary>
    public bool canAttack => attackCoolDownTimer <= 0f && _attackRecoveryTimer <= 0f;
    public bool attackFinished { get; set; }

    /// <summary>攻击顺序索引（1,2,3...），下次按下攻击将执行该段。冲刺不重置。</summary>
    public int attackSequenceIndex { get; set; } = 1;

    private float attackCoolDownTimer;
    private float _attackRecoveryTimer;

    public void StartAttackCooldown() => attackCoolDownTimer = attackCoolDown;
    public void SetAttackRecovery(float duration) => _attackRecoveryTimer = duration;

    [System.Serializable]
    public class AttackStepConfig
    {
        [Tooltip("本段攻击动画时长")]
        public float duration = 0.35f;
        [Tooltip("本段攻击结束后，多久后才能按下一次攻击")]
        public float recoveryTime = 0.3f;
        [Tooltip("攻击时沿朝向的瞬时前冲距离，0=无位移")]
        public float lungeDistance = 0f;
        [Tooltip("本段攻击造成的伤害值")]
        public float attackDamage = 1f;
        [Tooltip("已废弃：玩家攻击不再施加击退，Hitbox 始终传 0。保留字段仅为避免序列化丢数据")]
        public float knockbackForce = 0f;
        [Tooltip("本段攻击 Hitbox 相对玩家的偏移距离（朝攻击方向）。未启用分方向时使用")]
        public float hitboxOffset = 0.5f;
        [Tooltip("勾选后，上下左右(WASD)使用各自的 Hitbox 偏移")]
        public bool usePerDirectionOffsets = false;
        [Tooltip("朝上(W)攻击时的 Hitbox 偏移")]
        public float hitboxOffsetUp = 0.5f;
        [Tooltip("朝下(S)攻击时的 Hitbox 偏移")]
        public float hitboxOffsetDown = 0.5f;
        [Tooltip("朝左(A)攻击时的 Hitbox 偏移")]
        public float hitboxOffsetLeft = 0.5f;
        [Tooltip("朝右(D)攻击时的 Hitbox 偏移")]
        public float hitboxOffsetRight = 0.5f;

        /// <summary>根据攻击方向返回对应的 Hitbox 偏移</summary>
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
    [Tooltip("冲刺位移距离")]
    public float dashDistance = 5f;
    [Tooltip("冲刺持续时间")]
    public float dashDuration = 0.1f;
    [Tooltip("冲刺冷却时间")]
    public float dashCooldown = 1f;

    public bool wantDash { get; private set; }
    public bool canDash => _dashCooldownTimer <= 0f;
    public bool dashFinished { get; set; }
    private float _dashCooldownTimer;
    public void SetDashCooldown(float duration) => _dashCooldownTimer = duration;

    /// <summary>无敌状态，冲刺时 true，后续受伤逻辑可据此判断</summary>
    public bool IsInvincible { get; set; }

    [Header("Attack Sequence")]
    public AttackStepConfig[] attackSteps = new AttackStepConfig[]
    {
        new AttackStepConfig { duration = 0.35f, recoveryTime = 0.3f, lungeDistance = 0.3f, attackDamage = 1f, knockbackForce = 0f, hitboxOffset = 0.4f },
        new AttackStepConfig { duration = 0.35f, recoveryTime = 0.3f, lungeDistance = 0.35f, attackDamage = 2f, knockbackForce = 0f, hitboxOffset = 0.6f },
    };

    [Header("攻击碰撞")]
    [Tooltip("攻击碰撞盒（玩家子物体，带 Collider2D 的 Trigger）")]
    public AttackHitbox attackHitbox;

    [Header("格挡")]
    [Tooltip("按下后开始格挡并持续这么多秒")]
    [Min(0.05f)]
    public float blockDuration = 1.2f;
    [Tooltip("格挡结束后冷却")]
    public float blockCooldown = 0.35f;
    [Tooltip("格挡键（新输入系统 Key）")]
    public Key blockKey = Key.R;
    [Tooltip("格挡时 Animator Bool（如 IsBlocking / Shield）；空则不调")]
    public string blockShieldAnimBoolParam = "IsBlocking";
    [Header("格挡成功：下一次攻击的大爆炸")]
    [Tooltip("预制体可纯特效；若要伤害请挂 AreaDamageBurst2D（仅对带 Enemy 标签的目标造成伤害，由脚本在生成时开启）")]
    public GameObject blockExplosionPrefab;
    [Tooltip("爆炸生成点相对玩家位置的偏移；勾选下面一项时为本地 XY（随物体旋转）")]
    public Vector2 blockExplosionSpawnOffset;
    [Tooltip("为 true 时偏移按本 Transform 的旋转映射到世界方向（例如本地 +X 朝角色面向）")]
    public bool blockExplosionSpawnOffsetLocal;

    public bool wantBlock { get; private set; }
    public bool blockFinished { get; set; }
    public float blockStateTimer { get; set; }
    /// <summary>格挡挡下敌人伤害后，下一次攻击会消耗并生成爆炸。</summary>
    public bool HasChargedExplosionForNextAttack { get; private set; }

    [Header("Debug")]
    [Tooltip("勾选后，每次切换状态会在 Console 打印")]
    public bool debugStateMachine = false;

    float _contactKnockbackUntil;
    Vector2 _contactKnockbackVelocity;
    float _blockCooldownTimer;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    bool _hasWarnedBlockExplosionPrefab;
#endif

    /// <summary>
    /// 输入被锁时（受击硬直）每帧会把速度清零；接触伤击退由此处登记，在 <see cref="LateUpdate"/> 里覆盖状态机写入的速度。
    /// </summary>
    public void ApplyContactKnockback(Vector2 worldVelocity, float holdSeconds)
    {
        if (holdSeconds <= 0f || worldVelocity.sqrMagnitude < 1e-6f)
            return;
        _contactKnockbackVelocity = worldVelocity;
        _contactKnockbackUntil = Time.time + holdSeconds;
    }


    // ==================== Unity 生命周期 ====================



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

    /// <summary>是否已在死亡状态（避免死亡全局转换每帧重复 Enter）。</summary>
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

    // ==================== NPC 属性强化（StatTrainer 等） ====================

    /// <summary>增加最大生命与当前生命（与 <see cref="Health.AddMaxHP"/> 相同）。</summary>
    public void ApplyTrainerBonusMaxHp(int delta)
    {
        if (delta <= 0) return;
        if (_health == null) _health = GetComponent<Health>();
        if (_health == null) return;
        _health.AddMaxHP(delta);
    }

    /// <summary>永久增加移动速度（可多次叠加）。</summary>
    public void ApplyTrainerBonusMoveSpeed(float delta)
    {
        if (Mathf.Approximately(delta, 0f)) return;
        MoveSpeed = Mathf.Max(0.1f, MoveSpeed + delta);
    }

    /// <summary>为每一段普攻连招增加固定伤害。</summary>
    public void ApplyTrainerBonusAttackDamageAllSteps(float delta)
    {
        if (Mathf.Approximately(delta, 0f) || attackSteps == null || attackSteps.Length == 0) return;
        for (int i = 0; i < attackSteps.Length; i++)
            attackSteps[i].attackDamage = Mathf.Max(0.01f, attackSteps[i].attackDamage + delta);
    }

    /// <summary>能否开始背包回血读条：有物品、未满血、未死。</summary>
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

    /// <summary>在攻击开始时调用：若格挡充能存在则生成爆炸并清除标记。</summary>
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
                Debug.LogWarning("[PlayerController] 格挡已充能大爆炸，但未指定 blockExplosionPrefab（Inspector）.", this);
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

    /// <summary>受击状态结束时调用：从这一刻起刷新无敌计时（默认 0.5s，见 postHurtInvincibilityDuration）。</summary>
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
        // 冲刺可从任意状态打断（含攻击）；死亡与受击优先
        stateMachine.AddGlobalTransition(dashState, ctx => ctx.wantDash && ctx.canDash);
        stateMachine.AddGlobalTransition(blockState, ctx => ctx.wantBlock && ctx.CanEnterBlock());
        stateMachine.AddTransition(dashState, idleState, ctx => ctx.dashFinished);
        stateMachine.AddTransition(blockState, idleState, ctx => ctx.blockFinished);
        stateMachine.AddTransition(hurtState, idleState, ctx => ctx.hurtFinished);
        stateMachine.AddTransition(healState, idleState, ctx => ctx.healFinished);

        stateMachine.AddTransition(idleState, healState, ctx => ctx.wantHeal && ctx.CanStartHeal());
        stateMachine.AddTransition(moveState, healState, ctx => ctx.wantHeal && ctx.CanStartHeal());

        // 攻击优先于 idle/move 互切：wantAttack 仅在按下当帧为 true，否则会被 IsMoving 抢走导致进不了攻击（格挡充能爆炸不触发）。
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

        if (PlayerInputBlocker.IsBlocked)
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
            //Dash（Space 键，Input Actions 重新生成后可用 FindAction）
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
            Debug.Log($"[StateMachine] 当前状态: {stateName}");
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

        if (PlayerInputBlocker.IsBlocked)
            rb.linearVelocity = Vector2.zero;
    }

    /// <summary>
    /// 将归一化输入方向（与 Input System 的 Movement 轴向一致）映射到世界 XY 上的单位方向，用于 <see cref="Rigidbody2D.linearVelocity"/> 等。
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

    private void OnDestroy()
    {
        PlayerInputBlocker.Release(_hurtInputBlockerKey);
        PlayerInputBlocker.Release(_deathInputBlockerKey);
        PlayerInputBlocker.Release(_healInputBlockerKey);
        PlayerInputBlocker.Release(_blockInputBlockerKey);
        inputActions?.Disable();
    }
}
