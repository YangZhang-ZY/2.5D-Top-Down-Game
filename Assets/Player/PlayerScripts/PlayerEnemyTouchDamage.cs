using UnityEngine;

/// <summary>
/// 玩家碰到敌人<strong>身体</strong>时：短暂冷却内受伤。
/// 击退速度与硬直由 <see cref="PlayerController"/> 的受击状态统一处理（通过 Health 事件）。
/// <para>
/// 与 Dynamic 刚体<strong>实体</strong>对撞时，物理引擎会持续推挤；请把<strong>敌人身体</strong>主 Collider2D 勾上
/// <b>Is Trigger</b>，这样不会和玩家刚体硬挤，但仍会触发 OnTrigger 与伤害逻辑。
/// </para>
/// </summary>
[DisallowMultipleComponent]
public class PlayerEnemyTouchDamage : MonoBehaviour
{
    [SerializeField] Health playerHealth;

    [Header("Who counts as enemy")]
    [SerializeField] LayerMask enemyLayers;
    [Tooltip("若勾选，则还要求 Collider 或其父节点带 Enemy 标签")]
    [SerializeField] bool requireEnemyTag;
    [Tooltip("为 true 时要求对方父级带 EnemyBase")]
    [SerializeField] bool requireEnemyBase;

    [Header("Contact")]
    [SerializeField] float contactDamage = 1f;
    [Tooltip("两次接触伤害最短间隔（秒），可与 Health.invincibleDuration 配合）")]
    [SerializeField] float cooldownSeconds = 0.55f;

    [Header("Skip colliders")]
    [Tooltip("名字包含该串的子物体 Collider 忽略（例如 AttackHitbox）")]
    [SerializeField] string ignoreNameContains = "Hitbox";

    float _nextAllowedHitTime;

#if UNITY_EDITOR
    void Reset()
    {
        enemyLayers = LayerMask.GetMask("Enemy");
    }
#endif

    void Awake()
    {
        if (playerHealth == null)
            playerHealth = GetComponentInParent<Health>();

        var col = GetComponent<Collider2D>();
        if (col != null && !col.isTrigger)
            Debug.LogWarning($"{nameof(PlayerEnemyTouchDamage)}: {name} 的 Collider2D 建议设为 Trigger，避免与敌人实体互相推挤。", this);
    }

    void OnTriggerEnter2D(Collider2D other) => TryContact(other);
    void OnTriggerStay2D(Collider2D other) => TryContact(other);

    void TryContact(Collider2D other)
    {
        if (playerHealth == null || playerHealth.IsDead) return;
        if (Time.time < _nextAllowedHitTime) return;
        if (ShouldIgnoreCollider(other)) return;

        if ((enemyLayers.value & (1 << other.gameObject.layer)) == 0)
            return;

        if (requireEnemyTag && !HasEnemyTag(other.transform))
            return;

        var enemyHealth = other.GetComponentInParent<Health>();
        if (enemyHealth == null)
            enemyHealth = other.GetComponent<Health>();
        if (enemyHealth == null || enemyHealth.IsDead) return;

        if (requireEnemyBase && other.GetComponentInParent<EnemyBase>() == null)
            return;

        GameObject enemyRoot = enemyHealth.gameObject;
        if (enemyRoot == playerHealth.gameObject) return;

        var info = DamageInfo.Create(contactDamage, enemyRoot);
        if (!playerHealth.TakeDamage(info))
            return;

        _nextAllowedHitTime = Time.time + cooldownSeconds;
    }

    bool ShouldIgnoreCollider(Collider2D other)
    {
        if (other.GetComponent<AttackHitbox>() != null) return true;
        if (!string.IsNullOrEmpty(ignoreNameContains) &&
            other.name.IndexOf(ignoreNameContains, System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        return false;
    }

    static bool HasEnemyTag(Transform t)
    {
        for (var x = t; x != null; x = x.parent)
        {
            if (x.CompareTag("Enemy")) return true;
        }
        return false;
    }
}
