using UnityEngine;
using StateMachine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    // ==================== 属性配置（Inspector 可调） ====================

    [Header("Movement")]
    public float MoveSpeed = 5.0f;


    // ==================== 组件引用（Awake 获取） ====================
    public Rigidbody2D rb;
    public Animator animator;
    private PlayerInputSet inputActions;




    // ==================== 状态机 ====================
    private StateMachine<PlayerController> stateMachine;
    private PlayerIdleState idleState;
    private PlayerMovementState moveState;



    // ==================== 供状态和转换条件使用的属性 ====================
    public Vector2 Moveinput;
    public bool IsMoving => Moveinput.sqrMagnitude > 0.01f;
    public Vector2 LastMoveDiraction = Vector2.down;
    // 攻击冷却计时




    // ==================== Unity 生命周期 ====================



    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        inputActions = new PlayerInputSet();
        animator = GetComponentInChildren<Animator>();

        inputActions.Enable();
       
    }

    private void Start()
    {
        //initialize stateMachine
        idleState = new PlayerIdleState();
        moveState = new PlayerMovementState();
        
        stateMachine = new StateMachine<PlayerController>();
        stateMachine.Initialize(this, idleState);

        stateMachine.AddTransition(idleState, moveState, ctx => ctx.IsMoving);
        stateMachine.AddTransition(moveState,idleState, ctx =>! ctx.IsMoving);
    }

    private void Update()
    {
        Moveinput = inputActions.Player.Movement.ReadValue<Vector2>();

        stateMachine.Update(Time.deltaTime);
    }

    private void OnDestroy()
    {
        inputActions?.Disable();
    }
}
