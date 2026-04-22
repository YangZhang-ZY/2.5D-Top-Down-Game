using UnityEngine;

/// <summary>
/// One damage event. Struct to avoid heap allocations; extend with damage types later if needed.
/// </summary>
public struct DamageInfo
{
    public float amount;
    public GameObject source;
    public Vector2 knockbackDirection;
    public float knockbackForce;

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

    /// <param name="direction">Normalized internally when magnitude &gt; 0.</param>
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
