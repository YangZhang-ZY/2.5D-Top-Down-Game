using System.Collections;
using UnityEngine;

/// <summary>
/// 可破坏宝箱：<see cref="Health"/> 清零后播放开箱动画与音效，并在时间轴上陆续弹出掉落物，最后再销毁自身。
/// 配置要点见各字段 Tooltip（Animator Trigger / State、destroyChestAfter、loot stagger、pop 数值）。
/// </summary>
[RequireComponent(typeof(Health))]
public class TreasureChest : MonoBehaviour
{
    [Header("Loot")]
    [Tooltip("可掉落的预制体列表；同一预制体可多格拖入以增加权重。")]
    [SerializeField] GameObject[] dropPrefabs;

    [Tooltip("最少生成几个掉落实例。")]
    [Min(1)]
    public int dropCountMin = 1;

    [Tooltip("最多生成几个掉落实例（含）。")]
    [Min(1)]
    public int dropCountMax = 3;

    [Header("Spawn layout")]
    public Vector2 spawnOffset;

    [Tooltip("多个掉落物生成点水平分散。")]
    public float spawnSpread = 0.2f;

    [Header("Open sequence")]
    [Tooltip("开箱动画。不拖则从自身或子物体上找 Animator。")]
    [SerializeField] Animator chestAnimator;

    [Tooltip("非空：animator.SetTrigger（Controller 里须建同名 Trigger）。")]
    [SerializeField] string openAnimatorTrigger = "";

    [Tooltip("当 Trigger 留空且此项非空：animator.Play(层0，从开始播放该状态)")]
    [SerializeField] string openAnimatorStateName = "";

    [Tooltip("开箱瞬间播放的音效。")]
    [SerializeField] AudioClip openChestSound;

    [Range(0f, 1f)]
    [SerializeField] float openSoundVolume = 1f;

    [Tooltip("若不指定，优先用本物体/子物体上的 AudioSource；否则用临时物体播放。")]
    [SerializeField] AudioSource openSoundSource;

    [Tooltip("第一次掉落前等待（可与箱盖抬起对齐）。")]
    [Min(0f)]
    public float firstLootDelay = 0.12f;

    [Tooltip("从第一件掉落到「开始生成」最后一件之间的时间跨度；只有 1 件时忽略。")]
    [Min(0f)]
    public float lootStaggerWindow = 0.55f;

    [Tooltip("从死亡瞬间起多少秒后销毁宝箱物体。建议 ≥ 开箱动画时长，且 ≥ firstLootDelay + lootStaggerWindow，避免动画被截断。")]
    [Min(0f)]
    public float destroyChestAfter = 1.35f;

    [Header("Loot pop（比树/石更夸张时可加大弧高、时长、落点半径）")]
    public bool popOnSpawn = true;

    [Tooltip("抛物线最高点额外高度（越大弹得越明显）。")]
    public float popArcHeight = 0.95f;

    [Tooltip("从飞出到落地时间（秒）。")]
    public float popDuration = 0.55f;

    [Tooltip("落点相对生成点的随机半径（越大散得越开）。")]
    public float popLandRadius = 0.85f;

    [Header("On destroy (optional)")]
    public GameObject destroyEffectPrefab;

    public AudioClip destroySound;

    const int MaxPickAttempts = 16;

    Health _health;
    bool _sequenceStarted;

    void Awake() => _health = GetComponent<Health>();

#if UNITY_EDITOR
    void OnValidate()
    {
        dropCountMin = Mathf.Max(1, dropCountMin);
        dropCountMax = Mathf.Max(1, dropCountMax);
        if (dropCountMax < dropCountMin)
            dropCountMax = dropCountMin;
    }
#endif

    void OnEnable() => _health.OnDeath.AddListener(OnDeath);

    void OnDisable() => _health.OnDeath.RemoveListener(OnDeath);

    void OnDeath()
    {
        if (_sequenceStarted) return;
        _sequenceStarted = true;
        _health.OnDeath.RemoveListener(OnDeath);
        StartCoroutine(OpenAndLootRoutine());
    }

    IEnumerator OpenAndLootRoutine()
    {
        float tStart = Time.time;
        SetChestCollidersEnabled(false);

        PlayOpenAnimation();
        PlayOpenSound();

        int n = ComputeDropCount();
        for (int i = 0; i < n; i++)
        {
            if (i == 0)
            {
                if (firstLootDelay > 0f)
                    yield return new WaitForSeconds(firstLootDelay);
            }
            else if (n > 1 && lootStaggerWindow > 0f)
                yield return new WaitForSeconds(lootStaggerWindow / (n - 1));

            if (TryPickRandomPrefab(out GameObject prefab))
                SpawnLootInstance(prefab);
        }

        float elapsed = Time.time - tStart;
        float waitDestroy = destroyChestAfter - elapsed;
        if (waitDestroy > 0f)
            yield return new WaitForSeconds(waitDestroy);

        if (destroyEffectPrefab != null)
            Instantiate(destroyEffectPrefab, transform.position, Quaternion.identity);

        if (destroySound != null)
        {
            var go = new GameObject("TempAudio");
            var src = go.AddComponent<AudioSource>();
            src.PlayOneShot(destroySound);
            Destroy(go, destroySound.length + 0.1f);
        }

        Destroy(gameObject);
    }

    int ComputeDropCount()
    {
        if (dropPrefabs == null || dropPrefabs.Length == 0) return 0;
        int min = Mathf.Max(1, dropCountMin);
        int max = Mathf.Max(min, dropCountMax);
        return Random.Range(min, max + 1);
    }

    void PlayOpenAnimation()
    {
        Animator anim = chestAnimator != null ? chestAnimator : GetComponentInChildren<Animator>(true);
        if (anim == null) return;

        if (!string.IsNullOrEmpty(openAnimatorTrigger))
            anim.SetTrigger(Animator.StringToHash(openAnimatorTrigger));
        else if (!string.IsNullOrEmpty(openAnimatorStateName))
            anim.Play(openAnimatorStateName, 0, 0f);
    }

    void PlayOpenSound()
    {
        if (openChestSound == null) return;

        AudioSource src = openSoundSource;
        if (src == null)
            src = GetComponent<AudioSource>() ?? GetComponentInChildren<AudioSource>(true);

        if (src != null)
            src.PlayOneShot(openChestSound, openSoundVolume);
        else
        {
            var go = new GameObject("TreasureChestOpenAudio");
            go.transform.position = transform.position;
            var temp = go.AddComponent<AudioSource>();
            temp.spatialBlend = 0f;
            temp.PlayOneShot(openChestSound, openSoundVolume);
            Destroy(go, openChestSound.length + 0.1f);
        }
    }

    void SetChestCollidersEnabled(bool enabled)
    {
        foreach (var c in GetComponentsInChildren<Collider2D>(true))
            c.enabled = enabled;
    }

    void SpawnLootInstance(GameObject prefab)
    {
        Vector2 jitter = Random.insideUnitCircle * spawnSpread;
        Vector3 spawnPos = transform.position + (Vector3)spawnOffset + new Vector3(jitter.x, jitter.y, 0f);
        Vector2 landJitter = Random.insideUnitCircle * popLandRadius;
        Vector3 landPos = spawnPos + new Vector3(landJitter.x, landJitter.y, 0f);

        var instance = Instantiate(prefab, spawnPos, Quaternion.identity);

        if (popOnSpawn)
        {
            var pop = instance.GetComponent<LootPopout>();
            if (pop == null)
                pop = instance.AddComponent<LootPopout>();
            pop.Configure(popArcHeight, popDuration);
            pop.Play(spawnPos, landPos);
        }
    }

    bool TryPickRandomPrefab(out GameObject prefab)
    {
        prefab = null;
        if (dropPrefabs == null || dropPrefabs.Length == 0) return false;

        for (int t = 0; t < MaxPickAttempts; t++)
        {
            var p = dropPrefabs[Random.Range(0, dropPrefabs.Length)];
            if (p != null)
            {
                prefab = p;
                return true;
            }
        }

        return false;
    }
}
