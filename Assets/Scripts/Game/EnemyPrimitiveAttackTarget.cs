using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 挂在围墙、防御塔、水晶等可被近战敌人优先拆毁的物体上。
/// 需配合 <see cref="Health"/> 与可用于命中的 <see cref="Collider2D"/>（可与 Health 同物体或子物体）。
/// Chase 系近战敌人会优先走向<strong>距离自己最近</strong>的存活目标，而不是只盯着远处的水晶。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Health))]
public class EnemyPrimitiveAttackTarget : MonoBehaviour
{
    static readonly List<EnemyPrimitiveAttackTarget> Instances = new();

    [Tooltip("用于算距离与攻击朝向的点（例如 Collider 中心）。不填则用本物体 Transform。")]
    [SerializeField] Transform aimPoint;

    Health _health;

    /// <summary>世界空间下的追击/朝向锚点。</summary>
    public Transform AimTransform => aimPoint != null ? aimPoint : transform;

    void Awake()
    {
        _health = GetComponent<Health>();
    }

    void OnEnable()
    {
        if (!Instances.Contains(this))
            Instances.Add(this);
    }

    void OnDisable()
    {
        Instances.Remove(this);
    }

    /// <summary>可被 AI 选中（激活且未因血量死亡）。</summary>
    public bool IsAliveTarget() =>
        isActiveAndEnabled && _health != null && !_health.IsDead;

    /// <summary>从父链查找 Transform 是否属于某个primitive 目标（含 aimPoint 挂在子物体上的情况）。</summary>
    public static EnemyPrimitiveAttackTarget FindForTransform(Transform t) =>
        t != null ? t.GetComponentInParent<EnemyPrimitiveAttackTarget>() : null;

    /// <summary>离 <paramref name="from"/> 最近的存活 primitive 锚点 Transform；没有则 null。</summary>
    public static Transform GetNearestAimTransform(Vector3 from)
    {
        EnemyPrimitiveAttackTarget best = null;
        float bestSqr = float.MaxValue;

        for (int i = Instances.Count - 1; i >= 0; i--)
        {
            var e = Instances[i];
            if (e == null)
            {
                Instances.RemoveAt(i);
                continue;
            }

            if (!e.IsAliveTarget())
                continue;

            Vector3 p = e.AimTransform.position;
            float sqr = (p - from).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = e;
            }
        }

        return best != null ? best.AimTransform : null;
    }
}
