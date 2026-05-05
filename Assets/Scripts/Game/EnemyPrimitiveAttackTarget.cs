using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Put on walls, turrets, crystals, and other structures melee chasers should tear down first.
/// Requires <see cref="Health"/> and a hittable <see cref="Collider2D"/> (same object or child).
/// Chase melee AI prefers the <strong>nearest alive</strong> primitive target instead of only the distant crystal.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Health))]
public class EnemyPrimitiveAttackTarget : MonoBehaviour
{
    static readonly List<EnemyPrimitiveAttackTarget> Instances = new();

    [Tooltip("Aim point for range and facing (e.g. collider center). If empty, uses this object's transform.")]
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

    /// <summary>Selectable by AI if active and not dead by health.</summary>
    public bool IsAliveTarget() =>
        isActiveAndEnabled && _health != null && !_health.IsDead;

    /// <summary>从父链查找 Transform 是否属于某个primitive 目标（含 aimPoint 挂在子物体上的情况）。</summary>
    public static EnemyPrimitiveAttackTarget FindForTransform(Transform t) =>
        t != null ? t.GetComponentInParent<EnemyPrimitiveAttackTarget>() : null;

    /// <summary>Nearest alive primitive aim transform to <paramref name="from"/>; null if none.</summary>
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
