using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Player enters merchant trigger zone, presses interact action (e.g. E) to open both inventory and merchant UI.
/// Attach to merchant trigger object with Collider2D set to IsTrigger.
/// </summary>
public class MerchantInteractionZone : MonoBehaviour
{
    [Header("References")]
    public MerchantUI merchantUI;
    public InventoryUI inventoryUI;

    [Tooltip("Input action reference for interaction key, e.g. Player/Build (E).")]
    [SerializeField] InputActionReference interactAction;

    [Header("Filter")]
    [Tooltip("Only objects with this tag can interact.")]
    [SerializeField] string playerTag = "Player";

    bool _playerInRange;

    void Awake()
    {
        if (interactAction == null || interactAction.action == null)
            Debug.LogWarning("[MerchantInteractionZone] Assign interactAction (InputSystem action bound to E).", this);
    }

    void OnEnable()
    {
        if (interactAction != null && interactAction.action != null)
            interactAction.action.Enable();
    }

    void OnDisable()
    {
        if (interactAction != null && interactAction.action != null)
            interactAction.action.Disable();
    }

    void Update()
    {
        if (!_playerInRange) return;
        if (interactAction == null || interactAction.action == null) return;
        if (!interactAction.action.WasPressedThisFrame()) return;

        if (inventoryUI != null)
            inventoryUI.Open();
        if (merchantUI != null)
            merchantUI.Open();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (string.IsNullOrEmpty(playerTag) || other.CompareTag(playerTag))
            _playerInRange = true;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (string.IsNullOrEmpty(playerTag) || other.CompareTag(playerTag))
            _playerInRange = false;
    }
}
