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
        [Tooltip("击退：叠到敌人 linearVelocity 上的速度大小（世界单位/秒），再乘敌人 knockbackResistance；与敌人 Rigidbody2D 质量无关。0=无击退")]
        public float knockbackForce = 2f;
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
        new AttackStepConfig { duration = 0.35f, recoveryTime = 0.3f, lungeDistance = 0.3f, attackDamage = 1f, knockbackForce = 2f, hitboxOffset = 0.4f },
        new AttackStepConfig { duration = 0.35f, recoveryTime = 0.3f, lungeDistance = 0.35f, attackDamage = 2f, knockbackForce = 2.5f, hitboxOffset = 0.6f },
    };

    [Header("攻击碰撞")]
    [Tooltip("攻击碰撞盒（玩家子物体，带 Collider2D 的 Trigger）")]
    public AttackHitbox attackHitbox;

    [Header("Debug")]
    [Tooltip("勾选后，每次切换状态会在 Console 打印")]
    public bool debugStateMachine = false;


    // ==================== Unity 生命周期 ====================



    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        inputActions = new PlayerInputSet();
        animator = GetComponentInChildren<Animator>();

        if (attackHitbox != null && attackHitbox.owner == null)
            attackHitbox.owner = gameObject;

        inputActions.Enable();
    }

    private void Start()
    {
        //initialize stateMachine
        idleState = new PlayerIdleState();
        moveState = new PlayerMovementState();
        attackState = new PlayerAttackState();
        dashState = new PlayerDashState();

        stateMachine = new StateMachine<PlayerController>();
        stateMachine.Initialize(this, idleState);

        // 冲刺优先级最高，可从任意状态打断（含攻击）
        stateMachine.AddGlobalTransition(dashState, ctx => ctx.wantDash && ctx.canDash);
        stateMachine.AddTransition(dashState, idleState, ctx => ctx.dashFinished);

        stateMachine.AddTransition(idleState, moveState, ctx => ctx.IsMoving);
        stateMachine.AddTransition(moveState, idleState, ctx => !ctx.IsMoving);
        stateMachine.AddTransition(idleState, attackState, ctx => ctx.wantAttack && ctx.canAttack);
        stateMachine.AddTransition(moveState, attackState, ctx => ctx.wantAttack && ctx.canAttack);
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
        if (PlayerInputBlocker.IsBlocked)
        {
            Moveinput = Vector2.zero;
            wantAttack = false;
            wantDash = false;
            if (rb != null)
                rb.linearVelocity = Vector2.zero;
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
        }

        if (attackCoolDownTimer > 0f)
            attackCoolDownTimer -= Time.deltaTime;
        if (_attackRecoveryTimer > 0f)
            _attackRecoveryTimer -= Time.deltaTime;
        if (_dashCooldownTimer > 0f)
            _dashCooldownTimer -= Time.deltaTime;

        if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
        {
            string stateName = stateMachine.CurrentState?.GetType().Name ?? "null";
            Debug.Log($"[StateMachine] 当前状态: {stateName}");
        }

        stateMachine.Update(Time.deltaTime);
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
        inputActions?.Disable();
    }
}
