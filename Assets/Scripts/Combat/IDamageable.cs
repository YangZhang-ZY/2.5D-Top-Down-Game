using UnityEngine;

/// <summary>
/// Implemented by anything that can receive <see cref="DamageInfo"/> (player, enemies, props).
/// </summary>
public interface IDamageable
{
    /// <param name="info">Damage value, source, optional knockback.</param>
    /// <returns>False if no damage was applied (e.g. invulnerable).</returns>
    bool TakeDamage(DamageInfo info);
}
