using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Hit points for anything that can take damage. Implements <see cref="IDamageable"/>.
/// Requires a Collider2D on this object or a child for hit detection.
///
/// Setup:
/// 1. Add Health to the GameObject.
/// 2. Set maxHP.
/// 3. Ensure Collider2D exists for attacks.
/// 4. Optionally bind OnDeath in the Inspector.
/// </summary>
public class Health : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [Tooltip("Maximum hit points.")]
    public int maxHP = 3;

    [Tooltip("Runtime HP (visible in Inspector during play).")]
    [SerializeField] private int _currentHP;

    [Header("Invulnerability")]
    [Tooltip("When true, TakeDamage does nothing.")]
    public bool ignoreDamage;

    [Tooltip("Invulnerability duration after a hit; 0 = none.")]
    public float invincibleDuration = 0f;

    [Header("Events")]
    [Tooltip("Fired with damage amount.")]
    public UnityEvent<float> OnDamaged;

    /// <summary>Code-only subscribers; receives full <see cref="DamageInfo"/> (e.g. knockback).</summary>
    public event System.Action<DamageInfo> OnDamagedWithInfo;

    [Tooltip("Fired when HP reaches zero.")]
    public UnityEvent OnDeath;

    private float _invincibleTimer;

    /// <summary>Current HP.</summary>
    public int CurrentHP => _currentHP;

    /// <summary>True when HP is zero or below.</summary>
    public bool IsDead => _currentHP <= 0;

    private void Awake()
    {
        _currentHP = maxHP;
    }

    private void Update()
    {
        if (_invincibleTimer > 0f)
            _invincibleTimer -= Time.deltaTime;
    }

    /// <summary>Applies damage from weapons, hazards, etc.</summary>
    public bool TakeDamage(DamageInfo info)
    {
        if (IsDead) return false;
        if (ignoreDamage) return false;
        if (_invincibleTimer > 0f) return false;

        _currentHP -= Mathf.RoundToInt(info.amount);
        _currentHP = Mathf.Max(0, _currentHP);

        _invincibleTimer = invincibleDuration;

        OnDamaged?.Invoke(info.amount);
        OnDamagedWithInfo?.Invoke(info);

        if (IsDead)
            OnDeath?.Invoke();

        return true;
    }

    /// <summary>Toggles ignoreDamage (e.g. dash iframes).</summary>
    public void SetIgnoreDamage(bool ignore)
    {
        ignoreDamage = ignore;
    }

    /// <summary>Restores HP to max (respawn, heal).</summary>
    public void ResetHP()
    {
        _currentHP = maxHP;
        _invincibleTimer = 0f;
    }

    /// <summary>Increases max HP and current HP by the same amount (e.g. wall upgrade).</summary>
    public void AddMaxHP(int delta)
    {
        if (delta <= 0) return;
        maxHP += delta;
        _currentHP += delta;
    }
}
