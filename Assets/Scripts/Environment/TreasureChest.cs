using System.Collections;
using UnityEngine;

/// <summary>
/// Destructible chest: when <see cref="Health"/> dies, plays open animation + sound, spawns loot over time, then destroys self.
/// See tooltips for Animator Trigger / State, <see cref="destroyChestAfter"/>, loot stagger, and pop tuning.
/// </summary>
[RequireComponent(typeof(Health))]
public class TreasureChest : MonoBehaviour
{
    [Header("Loot")]
    [Tooltip("Loot prefabs; duplicate entries increase pick weight.")]
    [SerializeField] GameObject[] dropPrefabs;

    [Tooltip("Minimum number of spawned instances.")]
    [Min(1)]
    public int dropCountMin = 1;

    [Tooltip("Maximum number of spawned instances (inclusive).")]
    [Min(1)]
    public int dropCountMax = 3;

    [Header("Spawn layout")]
    public Vector2 spawnOffset;

    [Tooltip("Random spread between spawn positions.")]
    public float spawnSpread = 0.2f;

    [Header("Open sequence")]
    [Tooltip("Open animation. If empty, uses Animator on self or children.")]
    [SerializeField] Animator chestAnimator;

    [Tooltip("If set: animator.SetTrigger (add matching Trigger in the Controller).")]
    [SerializeField] string openAnimatorTrigger = "";

    [Tooltip("If Trigger is empty and this is set: animator.Play on layer S0 from normalized 0.")]
    [SerializeField] string openAnimatorStateName = "";

    [Tooltip("One-shot sound when opening starts.")]
    [SerializeField] AudioClip openChestSound;

    [Range(0f, 1f)]
    [SerializeField] float openSoundVolume = 1f;

    [Tooltip("Optional. Otherwise uses AudioSource on self/children, or a temporary source.")]
    [SerializeField] AudioSource openSoundSource;

    [Tooltip("Delay before the first loot spawns (sync with lid).")]
    [Min(0f)]
    public float firstLootDelay = 0.12f;

    [Tooltip("Time span from first to last loot spawn start; ignored when only one item.")]
    [Min(0f)]
    public float lootStaggerWindow = 0.55f;

    [Tooltip("Seconds after death to destroy this object. Should be >= open anim length and >= firstLootDelay + lootStaggerWindow.")]
    [Min(0f)]
    public float destroyChestAfter = 1.35f;

    [Header("Loot pop (raise arc / duration / radius vs trees/rocks)")]
    public bool popOnSpawn = true;

    [Tooltip("Arc peak height above the straight line.")]
    public float popArcHeight = 0.95f;

    [Tooltip("Flight time in seconds.")]
    public float popDuration = 0.55f;

    [Tooltip("Random radius around spawn for landing.")]
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
