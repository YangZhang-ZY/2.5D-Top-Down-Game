using UnityEngine;

/// <summary>
/// Gate physics: place a <see cref="Collider2D"/> in the doorway gap on layer <b>EnemyGate</b>.
/// Player on <b>Default</b> and projectiles on <b>projectile</b> pass through; <b>Enemy</b> is still blocked.
/// Normal walls stay on Default + Collider2D; no need for this layer.
/// </summary>
public static class EnemyGatePhysicsSetup
{
    public const string EnemyGateLayerName = "EnemyGate";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void RegisterLayerCollisions()
    {
        int gate = LayerMask.NameToLayer(EnemyGateLayerName);
        if (gate < 0)
            return;

        int def = LayerMask.NameToLayer("Default");
        if (def >= 0)
            Physics2D.IgnoreLayerCollision(gate, def, true);

        int projectile = LayerMask.NameToLayer("projectile");
        if (projectile >= 0)
            Physics2D.IgnoreLayerCollision(gate, projectile, true);
    }
}
