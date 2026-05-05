using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Enter trigger and press interact to open <see cref="StatTrainerUI"/> (optionally opens the bag too).
/// Same pattern as merchants: trigger Collider2D, Input System Interact/Build Action Reference.
/// </summary>
public class StatTrainerInteractionZone : MonoBehaviour
{
    public StatTrainerUI trainerUI;
    public InventoryUI inventoryUI;

    [Tooltip("e.g. Player/Interact or Player/Build")]
    [SerializeField] InputActionReference interactAction;

    [SerializeField] string playerTag = "Player";

    bool _playerInRange;

    void Awake()
    {
        if (interactAction == null || interactAction.action == null)
            Debug.LogWarning("[StatTrainerInteractionZone] Assign interactAction.", this);
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
            inventoryUI.OpenPlayerBag();
        if (trainerUI != null)
            trainerUI.Open();
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
