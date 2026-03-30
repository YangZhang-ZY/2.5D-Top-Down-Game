using UnityEngine;

/// <summary>
/// 可采集资源节点（树木、矿石等）。依赖 <see cref="Health"/>：血量归零时生成掉落物并销毁自身。
/// 掉落物需为带 <see cref="WorldItemPickup"/> 的预制体（或仅特效，则不设置 count）。
/// </summary>
[RequireComponent(typeof(Health))]
public class ResourceNode : MonoBehaviour
{
    [Header("掉落")]
    [Tooltip("死亡时在地面生成的预制体（通常为 WorldItemPickup）")]
    public GameObject dropPrefab;

    [Tooltip("写入掉落物的数量（覆盖预制体上的 count；若预制体无 WorldItemPickup 则忽略）")]
    [Min(1)]
    public int dropCount = 1;

    [Tooltip("相对树根的生成位置偏移")]
    public Vector2 spawnOffset;

    [Tooltip("水平随机偏移半径，避免多个掉落重叠")]
    public float spawnSpread = 0.2f;

    [Header("弹出动画")]
    [Tooltip("为 false 时掉落在生成点直接出现，无抛物线")]
    public bool popOnSpawn = true;

    [Tooltip("抛物线最高点相对落点连线的额外高度")]
    public float popArcHeight = 0.45f;

    [Tooltip("弹出到落地的时间（秒）")]
    public float popDuration = 0.4f;

    [Tooltip("落点相对生成点的随机水平半径")]
    public float popLandRadius = 0.55f;

    private Health _health;

    private void Awake()
    {
        _health = GetComponent<Health>();
    }

    private void OnEnable()
    {
        _health.OnDeath.AddListener(OnDeath);
    }

    private void OnDisable()
    {
        _health.OnDeath.RemoveListener(OnDeath);
    }

    private void OnDeath()
    {
        if (dropPrefab != null)
        {
            Vector2 jitter = Random.insideUnitCircle * spawnSpread;
            Vector3 spawnPos = transform.position + (Vector3)spawnOffset + new Vector3(jitter.x, jitter.y, 0f);
            Vector2 landJitter = Random.insideUnitCircle * popLandRadius;
            Vector3 landPos = spawnPos + new Vector3(landJitter.x, landJitter.y, 0f);

            var instance = Instantiate(dropPrefab, spawnPos, Quaternion.identity);

            var pickup = instance.GetComponent<WorldItemPickup>();
            if (pickup != null && dropCount >= 1)
                pickup.count = dropCount;

            if (popOnSpawn)
            {
                var pop = instance.GetComponent<LootPopout>();
                if (pop == null)
                    pop = instance.AddComponent<LootPopout>();
                pop.Configure(popArcHeight, popDuration);
                pop.Play(spawnPos, landPos);
            }
        }

        Destroy(gameObject);
    }
}
