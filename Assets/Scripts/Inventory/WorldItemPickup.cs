using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 场景中的可拾取物。玩家进入 Trigger 时把物品加入背包（可改为按键拾取）。
/// 需配合：Collider2D Is Trigger、Player 带 Tag "Player" 与 Collider2D、Player 上有 Inventory。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class WorldItemPickup : MonoBehaviour
{
    [Header("物品")]
    [Tooltip("要加入背包的物品数据")]
    public ItemData item;

    [Tooltip("拾取数量")]
    [Min(1)]
    public int count = 1;

    [Header("拾取方式")]
    [Tooltip("勾选：进入范围后按交互键拾取；不勾选：进入范围立刻拾取")]
    public bool requireInteractKey;

    [Tooltip("交互键（仅 requireInteractKey 为 true 时有效）")]
    public KeyCode interactKey = KeyCode.E;

    [Header("拾取后")]
    [Tooltip("背包装不下全部时是否仍销毁物体（不推荐勾选）")]
    public bool destroyWhenPartialPickup;

    [Tooltip("成功拾取并销毁自身时触发（可接音效）")]
    public UnityEvent onPickedUp;

    private Collider2D _trigger;

    private void Awake()
    {
        _trigger = GetComponent<Collider2D>();
        if (_trigger != null && !_trigger.isTrigger)
            Debug.LogWarning($"[WorldItemPickup] {name} 的 Collider2D 建议勾选 Is Trigger。", this);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayer(other)) return;
        if (!requireInteractKey)
            TryPickup(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!requireInteractKey) return;
        if (!IsPlayer(other)) return;
        if (Input.GetKeyDown(interactKey))
            TryPickup(other);
    }

    private static bool IsPlayer(Collider2D other)
    {
        return other.CompareTag("Player");
    }

    private void TryPickup(Collider2D playerCollider)
    {
        if (item == null || !item.IsValid)
        {
            Debug.LogWarning($"[WorldItemPickup] {name} 未设置有效的 ItemData（id 不能为空）。", this);
            return;
        }

        var inventory = playerCollider.GetComponent<Inventory>()
                        ?? playerCollider.GetComponentInParent<Inventory>();

        if (inventory == null)
        {
            Debug.LogWarning($"[WorldItemPickup] 未在 {playerCollider.name} 上找到 Inventory 组件。", this);
            return;
        }

        int added = inventory.AddItem(item, count);
        if (added <= 0)
        {
            Debug.Log($"[WorldItemPickup] 背包无法装入 {item.displayName}（可能满或超重）。");
            return;
        }

        if (added < count && !destroyWhenPartialPickup)
        {
            count -= added;
            return;
        }

        onPickedUp?.Invoke();
        Destroy(gameObject);
    }
}
