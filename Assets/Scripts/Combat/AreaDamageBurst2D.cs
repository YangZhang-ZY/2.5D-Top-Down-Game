using UnityEngine;

/// <summary>
/// 一次性范围伤害：生成时对圆形内带 <see cref="IDamageable"/> 的目标各打一次伤害，之后可按延迟销毁。
/// 用于格挡成功后的「大爆炸」预制体；可不挂本组件，仅做特效。
/// </summary>
public class AreaDamageBurst2D : MonoBehaviour
{
    [Tooltip("伤害半径（世界单位）")]
    public float radius = 4f;
    [Tooltip("伤害数值")]
    public float damage = 30f;
    [Tooltip("几秒后销毁整个物体（粒子可设得略长）")]
    public float destroyAfterSeconds = 2f;

    /// <summary>由 <see cref="PlayerController"/> 在生成后赋值，用于排除自伤</summary>
    public GameObject damageSource;

    void Start()
    {
        ApplyBurst();
        if (destroyAfterSeconds > 0f)
            Destroy(gameObject, destroyAfterSeconds);
    }

    void ApplyBurst()
    {
        if (radius <= 0f || damage <= 0f) return;

        var src = damageSource != null ? damageSource : gameObject;
        var hits = Physics2D.OverlapCircleAll(transform.position, radius);
        foreach (var col in hits)
        {
            if (col == null) continue;
            var dmg = col.GetComponent<IDamageable>() ?? col.GetComponentInParent<IDamageable>();
            if (dmg == null) continue;
            var host = dmg as MonoBehaviour;
            if (host != null && (host.gameObject == src || host.transform.IsChildOf(src.transform)))
                continue;

            dmg.TakeDamage(DamageInfo.Create(damage, src));
        }
    }
}
