using System.Collections;
using UnityEngine;

/// <summary>
/// 配合 <see cref="Health"/> + <see cref="CombatPosture"/>：仅架势被打空时触发 Hit / Recovery、输入锁定与击退。
/// 未挂 <see cref="CombatPosture"/> 或 maxPosture≤0 时行为与「每击都硬直」一致。
/// </summary>
[DisallowMultipleComponent]
public class PlayerPostureReactor : MonoBehaviour
{
    [SerializeField] Health health;
    [SerializeField] CombatPosture posture;
    [SerializeField] Rigidbody2D rb;
    [SerializeField] Animator animator;

    [Header("Animator")]
    [SerializeField] string hitTriggerParam = "Hit";
    [Tooltip("可选：眩晕 Loop；在 hitStunDuration 内为 true，结束前关掉再接 Recovery。")]
    [SerializeField] string stunnedBoolParam = "";
    [SerializeField] bool useRecoveryBool;
    [SerializeField] string recoveryBoolParam = "Recovery";

    [Header("Timing")]
    [Tooltip("破防后锁定输入、并保持 Stunned（若有）的秒数；随后进入 Recovery（若开启）。")]
    [SerializeField] float hitStunDuration = 0.28f;

    [SerializeField] float recoveryDuration = 0.45f;

    [Tooltip("相对 DamageInfo 击退力度的乘数。")]
    [SerializeField] float knockbackMultiplier = 1f;

    Coroutine _routine;
    object _blockerKey;

    void Awake()
    {
        if (health == null) health = GetComponent<Health>();
        if (posture == null) posture = GetComponent<CombatPosture>();
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        _blockerKey = this;
    }

    void OnEnable()
    {
        if (health == null) return;
        health.OnDamaged.AddListener(OnHpDamaged);
        health.OnDamagedWithInfo += OnHpDamagedInfo;
    }

    void OnDisable()
    {
        if (health != null)
        {
            health.OnDamaged.RemoveListener(OnHpDamaged);
            health.OnDamagedWithInfo -= OnHpDamagedInfo;
        }

        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }

        PlayerInputBlocker.Release(_blockerKey);
        if (useRecoveryBool && animator != null && !string.IsNullOrEmpty(recoveryBoolParam))
            animator.SetBool(recoveryBoolParam, false);
        if (animator != null && !string.IsNullOrEmpty(stunnedBoolParam))
            animator.SetBool(stunnedBoolParam, false);
    }

    void OnHpDamaged(float dmg)
    {
        if (health != null && health.IsDead) return;

        bool stagger;
        if (posture == null || !posture.enabled || posture.MaxPosture <= 0f)
            stagger = true;
        else
            stagger = posture.ApplyPostureDamageFromHp(dmg);

        if (!stagger) return;
        if (_routine != null)
            StopCoroutine(_routine);
        _routine = StartCoroutine(HitReactSequence());
    }

    void OnHpDamagedInfo(DamageInfo info)
    {
        if (health != null && health.IsDead) return;

        bool shouldKb = posture == null || !posture.enabled || posture.MaxPosture <= 0f || posture.LastHitBrokePosture;
        if (!shouldKb) return;

        if (rb != null && info.knockbackForce > 0.01f && info.knockbackDirection.sqrMagnitude > 0.01f)
        {
            Vector2 d = info.knockbackDirection.normalized;
            rb.linearVelocity += d * (info.knockbackForce * knockbackMultiplier);
        }
    }

    IEnumerator HitReactSequence()
    {
        PlayerInputBlocker.Request(_blockerKey);

        if (animator != null && !string.IsNullOrEmpty(hitTriggerParam))
            animator.SetTrigger(hitTriggerParam);

        if (animator != null && !string.IsNullOrEmpty(stunnedBoolParam))
            animator.SetBool(stunnedBoolParam, true);

        yield return new WaitForSeconds(hitStunDuration);

        if (animator != null && !string.IsNullOrEmpty(stunnedBoolParam))
            animator.SetBool(stunnedBoolParam, false);

        if (useRecoveryBool && animator != null && !string.IsNullOrEmpty(recoveryBoolParam))
        {
            animator.SetBool(recoveryBoolParam, true);
            yield return new WaitForSeconds(recoveryDuration);
            animator.SetBool(recoveryBoolParam, false);
        }

        posture?.RefillPosture();
        PlayerInputBlocker.Release(_blockerKey);
        _routine = null;
    }
}
