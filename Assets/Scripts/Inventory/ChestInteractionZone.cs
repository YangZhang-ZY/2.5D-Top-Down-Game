using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Chest interaction: opens/closes <b>two</b> <see cref="InventoryUI"/> panels together (player bag + chest copy),
/// with drag-and-drop moving items between the player <see cref="Inventory"/> and the chest <see cref="Inventory"/>.
///
/// Setup: on the chest object add <see cref="Inventory"/> (capacity/maxWeight), this script, and a trigger Collider2D.
/// UI: assign both <see cref="InventoryUI"/> references in the Inspector, or leave empty to use <see cref="InventoryUiRegistry"/> in the scene (recommended for built chest prefabs).
/// Opening binds <see cref="InventoryUI.BindStorageView"/> to the <strong>current</strong> chest (multiple chests share one chest panel).
/// </summary>
public class ChestInteractionZone : MonoBehaviour
{
    [Header("Chest data")]
    [SerializeField] Inventory chestInventory;

    [Header("Two UI panels")]
    [Tooltip("Player-bag InventoryUI (player Inventory fields all reference the player Inventory).")]
    [SerializeField] InventoryUI playerBagInventoryUI;

    [Tooltip("Chest-only InventoryUI (both Inventory references point at this chest's Inventory).")]
    [SerializeField] InventoryUI chestInventoryUI;

    [Tooltip("e.g. Player/Interact or Build (E).")]
    [SerializeField] InputActionReference interactAction;

    [Header("Filter")]
    [SerializeField] string playerTag = "Player";

    [Header("Leave zone")]
    [Tooltip("When leaving the trigger, close both panels if the chest UI is still open.")]
    [SerializeField] bool closePanelsWhenPlayerLeaves = true;

    bool _playerInRange;

    void Awake()
    {
        if (chestInventory == null)
            chestInventory = GetComponent<Inventory>() ?? GetComponentInChildren<Inventory>();

        if (chestInventory == null)
            Debug.LogWarning("[ChestInteractionZone] Add an Inventory component on the chest.", this);
        if (interactAction == null || interactAction.action == null)
            Debug.LogWarning("[ChestInteractionZone] Assign an interact Input Action.", this);
    }

    void Start()
    {
        var reg = InventoryUiRegistry.Instance;
        if (reg != null)
        {
            if (playerBagInventoryUI == null)
                playerBagInventoryUI = reg.PlayerBagInventoryUI;
            if (chestInventoryUI == null)
                chestInventoryUI = reg.ChestStorageInventoryUI;
        }

        if (playerBagInventoryUI == null)
            Debug.LogWarning("[ChestInteractionZone] Assign player bag InventoryUI or add InventoryUiRegistry to the scene.", this);
        if (chestInventoryUI == null)
            Debug.LogWarning("[ChestInteractionZone] Assign chest InventoryUI or add InventoryUiRegistry to the scene.", this);
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

        bool chestOpen = chestInventoryUI != null && chestInventoryUI.gameObject.activeSelf;
        if (chestOpen)
        {
            playerBagInventoryUI?.Close();
            chestInventoryUI.Close();
        }
        else
        {
            playerBagInventoryUI?.OpenPlayerBag();
            if (chestInventoryUI != null)
            {
                chestInventoryUI.BindStorageView(chestInventory);
                chestInventoryUI.Open();
            }
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (string.IsNullOrEmpty(playerTag) || other.CompareTag(playerTag))
            _playerInRange = true;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (string.IsNullOrEmpty(playerTag) || other.CompareTag(playerTag))
        {
            _playerInRange = false;
            if (!closePanelsWhenPlayerLeaves) return;
            if (chestInventoryUI != null && chestInventoryUI.gameObject.activeSelf)
            {
                playerBagInventoryUI?.Close();
                chestInventoryUI.Close();
            }
        }
    }
}
