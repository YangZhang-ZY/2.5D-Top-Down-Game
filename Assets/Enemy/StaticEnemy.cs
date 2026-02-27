using UnityEngine;

/// <summary>
/// 站桩敌人：不移动，只受击。用于测试伤害系统。
/// 挂到敌人 GameObject 上，需配合 Health 组件和 Collider2D。
/// </summary>
[RequireComponent(typeof(Health))]
public class StaticEnemy : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("可选：死亡时播放的动画")]
    public Animator animator;

    [Tooltip("可选：死亡动画的 Trigger 参数名")]
    public string deathTriggerName = "Death";

    private Health _health;
    public GameObject deathEffect;
    public Vector2 EffectPositon;
    public float EffectDelay = 2;

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
        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
        


        if (deathEffect!= null)
        {
            Vector3 spawnPos = transform.position + (Vector3)EffectPositon;
            var effect = Instantiate(deathEffect, spawnPos, Quaternion.identity);
            Destroy(effect,1f);
        }

        Destroy(gameObject);

    }
}
