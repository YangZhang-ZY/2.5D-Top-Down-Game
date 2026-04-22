using UnityEngine;

/// <summary>
/// Destroys the GameObject when <see cref="Health"/> dies (crates, jars, etc.).
/// </summary>
[RequireComponent(typeof(Health))]
public class Destructible : MonoBehaviour
{
    [Header("On destroy")]
    [Tooltip("Optional VFX prefab.")]
    public GameObject destroyEffectPrefab;

    [Tooltip("Optional one-shot sound.")]
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
