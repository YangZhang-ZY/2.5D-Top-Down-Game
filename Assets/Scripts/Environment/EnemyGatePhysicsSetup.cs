using UnityEngine;

/// <summary>
/// 大门物理：在「门口缝隙」放一块带 <see cref="Collider2D"/> 的物体，图层选 <b>EnemyGate</b>。
/// 玩家主体在 <b>Default</b>、子弹在 <b>projectile</b> 时会穿过；<b>Enemy</b> 图层仍会被挡住。
/// 普通围墙继续用 Default + Collider2D 即可，无需本图层。
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
