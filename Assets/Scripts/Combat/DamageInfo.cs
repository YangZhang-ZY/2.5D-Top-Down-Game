using UnityEngine;

/// <summary>
/// 伤害信息结构体。传递一次伤害的完整信息。
/// 使用 struct 避免频繁 new 对象，后续可扩展（伤害类型、暴击等）。
/// </summary>
public struct DamageInfo
{
    /// <summary>伤害数值</summary>
    public float amount;

    /// <summary>伤害来源（谁造成的伤害，如玩家、陷阱）</summary>
    public GameObject source;

    /// <summary>击退方向（归一化向量，Vector2.zero 表示无击退）</summary>
    public Vector2 knockbackDirection;

    /// <summary>击退力度（0 表示无击退）</summary>
    public float knockbackForce;

    /// <summary>
    /// 创建简单的伤害信息（无击退）
    /// </summary>
    public static DamageInfo Create(float amount, GameObject source)
    {
        return new DamageInfo
        {
            amount = amount,
            source = source,
            knockbackDirection = Vector2.zero,
            knockbackForce = 0f
        };
    }

    /// <summary>
    /// 创建带击退的伤害信息
    /// </summary>
    /// <param name="direction">击退方向，会自动归一化</param>
    public static DamageInfo CreateWithKnockback(float amount, GameObject source, Vector2 direction, float force)
    {
        return new DamageInfo
        {
            amount = amount,
            source = source,
            knockbackDirection = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector2.zero,
            knockbackForce = force
        };
    }
}
