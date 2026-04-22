using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// World pickup: adds items to the player's inventory on trigger enter, or on Interact when configured.
/// Needs Collider2D as trigger, player tag Player with Collider2D, and Inventory on the player.
/// For key pickup: assign Input Action Reference from InputSystem_Actions → Player → Interact.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class WorldItemPickup : MonoBehaviour
{
    [Header("Item")]
    [Tooltip("ItemData to add.")]
    public ItemData item;

    [Tooltip("How many to pick up.")]
    [Min(1)]
    public int count = 1;

    [Header("Pickup mode")]
    [Tooltip("If true, player must press Interact inside the trigger; otherwise pickup is automatic on enter.")]
    public bool requireInteractKey;

    [Tooltip("Must match Player/Interact in Input Actions (create an Input Action Reference).")]
    [SerializeField] private InputActionReference interactAction;

    [Header("After pickup")]
    [Tooltip("If true, destroy this pickup even when only part of the stack was added. If false, reduce remaining count and keep the pickup.")]
    public bool destroyWhenPartialPickup;

    [Tooltip("Invoked when pickup succeeds and this object is destroyed (e.g. play SFX).")]
    public UnityEvent onPickedUp;

    private Collider2D _trigger;

    private void Awake()
    {
        _trigger = GetComponent<Collider2D>();
        if (_trigger != null && !_trigger.isTrigger)
            Debug.LogWarning($"[WorldItemPickup] Collider2D on {name} should be a trigger.", this);

        if (requireInteractKey && (interactAction == null || interactAction.action == null))
            Debug.LogWarning(
                $"[WorldItemPickup] {name}: requireInteractKey is set but Interact action is not assigned.",
                this);
    }

    private void OnEnable()
    {
        if (interactAction != null && interactAction.action != null)
            interactAction.action.Enable();
    }

    private void OnDisable()
    {
        if (interactAction != null && interactAction.action != null)
            interactAction.action.Disable();
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

        if (interactAction == null || interactAction.action == null)
            return;

        if (interactAction.action.WasPressedThisFrame())
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
            Debug.LogWarning($"[WorldItemPickup] {name}: assign valid ItemData (non-empty id).", this);
            return;
        }

        var inventory = playerCollider.GetComponent<Inventory>()
                        ?? playerCollider.GetComponentInParent<Inventory>();

        if (inventory == null)
        {
            Debug.LogWarning($"[WorldItemPickup] No Inventory on {playerCollider.name} or its parents.", this);
            return;
        }

        int added = inventory.AddItem(item, count);
        if (added <= 0)
        {
            Debug.Log($"[WorldItemPickup] Could not add {item.displayName} (inventory full or overweight).");
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
