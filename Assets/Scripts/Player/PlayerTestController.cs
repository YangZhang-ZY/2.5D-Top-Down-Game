using UnityEngine;

public class PlayerTestController : MonoBehaviour
{
    [Header("移动参数")]
    public float moveSpeed = 3f;          // 普通移动速度
    public float sprintMultiplier = 1.8f; // 冲刺倍率

    [Header("动画")]
    public Animator animator;             // 在 Inspector 里把 Player 的 Animator 拖进来

    private Vector2 inputDir;

    private void Update()
    {
        HandleMovement();
        HandleAttack();
    }

    private void HandleMovement()
    {
        // 获取键盘输入（WASD / 方向键）
        float h = Input.GetAxisRaw("Horizontal"); // A(-1) D(1)
        float v = Input.GetAxisRaw("Vertical");   // S(-1) W(1)
        inputDir = new Vector2(h, v).normalized;

        // Shift 冲刺
        float currentSpeed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            currentSpeed *= sprintMultiplier;
        }

        // 实际移动（世界空间），适合 2D Top-Down
        Vector3 move = new Vector3(inputDir.x, inputDir.y, 0f) * currentSpeed * Time.deltaTime;
        transform.Translate(move, Space.World);

        // 动画参数（按你 Animator 的参数名来）
        if (animator != null)
        {
            animator.SetFloat("MoveX", inputDir.x);
            animator.SetFloat("MoveY", inputDir.y);
            animator.SetFloat("Speed", inputDir.sqrMagnitude);
        }
    }

    private void HandleAttack()
    {
        // 鼠标右键攻击
        if (Input.GetMouseButtonDown(1))
        {
            if (animator != null)
            {
                animator.SetTrigger("Attack"); // Animator 里建一个 "Attack" 的 Trigger
            }
        }
    }
}
