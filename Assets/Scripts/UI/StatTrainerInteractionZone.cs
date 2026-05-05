using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 走进触发区按交互键打开 <see cref="StatTrainerUI"/>（可选同时打开背包）。
/// 与商人类似：需要 Collider2D IsTrigger、Input System 的 Interact/Build 等 Action Reference。
/// </summary>
public class StatTrainerInteractionZone : MonoBehaviour
{
    public StatTrainerUI trainerUI;
    public InventoryUI inventoryUI;

    [Tooltip("例如 Player/Interact 或 Player/Build")]
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
