using UnityEngine;

/// <summary>
/// 可破坏物：如木箱、罐子。受到伤害后销毁。
/// 挂到可破坏物上，需配合 Health 组件和 Collider2D。
/// </summary>
[RequireComponent(typeof(Health))]
public class Destructible : MonoBehaviour
{
    [Header("死亡效果")]
    [Tooltip("可选：死亡时生成的预制体（如碎片）")]
    public GameObject destroyEffectPrefab;

    [Tooltip("可选：死亡时播放的音效")]
    public AudioClip destroySound;

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
}
