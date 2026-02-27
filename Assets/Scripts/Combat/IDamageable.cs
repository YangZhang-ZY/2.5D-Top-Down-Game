using UnityEngine;

/// <summary>
/// 可受伤接口。任何能受到伤害的对象都应实现此接口。
/// 敌人、可破坏物、玩家都可以实现它，实现统一的伤害处理。
/// </summary>
public interface IDamageable
{
    /// <summary>
    /// 受到伤害时调用。
    /// </summary>
    /// <param name="info">伤害信息，包含伤害值、来源、击退等</param>
    /// <returns>是否成功造成伤害（例如无敌时返回 false）</returns>
    bool TakeDamage(DamageInfo info);
}
