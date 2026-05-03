using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 「护盾特效」显隐：可把特效做成 Player 子物体（默认 Inactive），拖到 <see cref="shieldVisualRoot"/>。
/// 可单独按键切换，也可勾选 <see cref="followPlayerBlockState"/> 与 <see cref="PlayerController"/> 格挡同步（只按格挡键即可）。
/// </summary>
public class PlayerShieldVisualToggle : MonoBehaviour
{
    [Tooltip("护盾特效根物体；建议 Inspector 里取消勾选（Inactive），由本脚本开启")]
    [SerializeField] GameObject shieldVisualRoot;

    [Tooltip("与玩家格挡状态一致：格挡持续时间内显示，结束隐藏。勾选后不再使用 toggleKey。")]
    [SerializeField] bool followPlayerBlockState;

    [Tooltip("默认在同物体上找 PlayerController；空则用本物体 GetComponent")]
    [SerializeField] PlayerController playerController;

    [Tooltip("切换/按住时用的键（Unity 新输入系统 Key）；followPlayerBlockState 为 true 时忽略")]
    [SerializeField] Key toggleKey = Key.Q;

    [Tooltip("false = 同一键反复按会在 显示/隐藏 间切换；true = 按住显示、松开隐藏")]
    [SerializeField] bool holdToShow;

    [Tooltip("勾选后，即便 PlayerInputBlocker 锁输入也能切护盾（仅手动键模式；跟随格挡时始终同步）")]
    [SerializeField] bool allowWhenInputBlocked = true;

    void Awake()
    {
        if (playerController == null)
            playerController = GetComponent<PlayerController>();
    }

    void Update()
    {
        if (shieldVisualRoot == null) return;

        if (followPlayerBlockState)
        {
            if (playerController == null)
                playerController = GetComponent<PlayerController>();
            if (playerController == null) return;

            bool show = playerController.IsCurrentStateBlock();
            if (shieldVisualRoot.activeSelf != show)
                shieldVisualRoot.SetActive(show);
            return;
        }

        if (Keyboard.current == null) return;
        if (!allowWhenInputBlocked && PlayerInputBlocker.IsBlocked) return;

        if (holdToShow)
        {
            bool show = Keyboard.current[toggleKey].isPressed;
            if (shieldVisualRoot.activeSelf != show)
                shieldVisualRoot.SetActive(show);
            return;
        }

        if (Keyboard.current[toggleKey].wasPressedThisFrame)
            shieldVisualRoot.SetActive(!shieldVisualRoot.activeSelf);
    }
}
