using UnityEngine;

/// <summary>
/// 将同物体上的 <see cref="HealthBar"/> 绑定到玩家的 <see cref="Health"/>（Tag &quot;Player&quot;）。
/// 适用于屏幕 HUD：HealthBar 不与玩家在同一层级时无法在父级上找到 Health。
/// 若在 Inspector 里已手动指定 <see cref="HealthBar.health"/>，本脚本不会覆盖。
/// </summary>
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(HealthBar))]
public class PlayerHudHealthBinder : MonoBehaviour
{
    [Tooltip("手动指定玩家 Health；为空则在运行时按 Tag Player 查找。")]
    [SerializeField] Health playerHealthOverride;

    void Awake()
    {
        var bar = GetComponent<HealthBar>();
        if (bar == null) return;
        if (bar.health != null) return;

        if (playerHealthOverride != null)
            bar.health = playerHealthOverride;
        else
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
                bar.health = p.GetComponent<Health>();
        }

        if (bar.health == null)
            Debug.LogWarning("[PlayerHudHealthBinder] 未找到玩家 Health：请指定 Player Tag 或 playerHealthOverride。", this);
    }
}
