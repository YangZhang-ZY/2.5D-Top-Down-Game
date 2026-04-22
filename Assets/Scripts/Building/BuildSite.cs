using UnityEngine;
using UnityEngine.Events;
using TMPro;
using UnityEngine.InputSystem;

/// <summary>
/// Build slot: player enters trigger, sees cost, presses Build to spawn the result and spend items from <see cref="Inventory"/>.
/// Requires a Collider2D set as trigger; player uses tag Player.
/// The player side must have a Rigidbody2D (dynamic or kinematic) or trigger callbacks may not fire.
/// Site visual: optional child shown before build (stakes, outline); hide after build. Do not use the same asset reference as builtPrefab.
/// Build input: create an Input Action Reference from InputSystem_Actions → Player → Build and assign it to Build Action.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class BuildSite : MonoBehaviour
{
    [Header("Build result")]
    [Tooltip("Prefab spawned at confirm (wall, turret, etc.). Keep the prefab root enabled in the project or instances may spawn disabled.")]
    [SerializeField] private GameObject builtPrefab;

    [Tooltip("Leave empty to use this object's transform.")]
    [SerializeField] private Transform spawnPoint;

    [Tooltip("Optional placeholder shown before build (e.g. stakes). Must be a child in this scene; do not assign the built prefab or a project asset or you may disable the wrong asset.")]
    [SerializeField] private GameObject siteVisual;

    [Header("Ghost preview")]
    [Tooltip("Shown while player is in range. If empty, builtPrefab is cloned with colliders off and alpha applied.")]
    [SerializeField] private GameObject ghostPreviewPrefab;

    [Tooltip("Ghost sprite alpha (0–1).")]
    [SerializeField] [Range(0.05f, 1f)] private float ghostAlpha = 0.45f;

    [Tooltip("Sorting order offset so the ghost draws under the real build.")]
    [SerializeField] private int ghostSortingOrderOffset;

    [Header("Cost (spent from entering player's inventory)")]
    [Tooltip("Amount consumed; if zero you can leave the matching ItemData empty.")]
    [SerializeField] private int costWood;

    [SerializeField] private int costStone;

    [SerializeField] private int costCoin;

    [Tooltip("ItemData for wood (required when costWood > 0).")]
    [SerializeField] private ItemData woodItem;

    [Tooltip("ItemData for stone (required when costStone > 0).")]
    [SerializeField] private ItemData stoneItem;

    [Tooltip("ItemData for coin/currency (required when costCoin > 0).")]
    [SerializeField] private ItemData coinItem;

    [Header("Input (Input System)")]
    [Tooltip("Input Action Reference for Player/Build.")]
    [SerializeField] private InputActionReference buildAction;

    [SerializeField] private string buildingName = "Building";

    [Header("Prompt UI (optional)")]
    [Tooltip("Shown while player is in range. Put your Canvas + 3 icons here; icons are static in the prefab.")]
    [SerializeField] private GameObject promptRoot;

    [Tooltip("Optional one line: key + build name. Icons and counts are separate.")]
    [SerializeField] private TextMeshProUGUI titleText;

    [Tooltip("Numbers only — place next to wood icon (order: wood, stone, coin).")]
    [SerializeField] private TextMeshProUGUI woodAmountText;

    [SerializeField] private TextMeshProUGUI stoneAmountText;

    [SerializeField] private TextMeshProUGUI coinAmountText;

    [Tooltip("Optional: entire row (icon + text) hidden when that cost is 0.")]
    [SerializeField] private GameObject woodRow;

    [SerializeField] private GameObject stoneRow;

    [SerializeField] private GameObject coinRow;

    [Header("Events")]
    [SerializeField] private UnityEvent onBuilt;

    [Tooltip("Invoked when the player cannot afford the cost (hook up SFX or floating text).")]
    [SerializeField] private UnityEvent onBuildFailedInsufficientResources;

    [Header("Debug")]
    [SerializeField] private bool debugLog;

    /// <summary>True after a successful build this session.</summary>
    public bool IsBuilt { get; private set; }

    private Collider2D _trigger;
    private bool _playerInRange;
    private Inventory _playerInventory;
    private GameObject _ghostInstance;

    private void Awake()
    {
        _trigger = GetComponent<Collider2D>();
        if (_trigger != null && !_trigger.isTrigger)
            Debug.LogWarning($"[BuildSite] Collider2D on {name} should be a trigger.", this);

        if (promptRoot != null)
            promptRoot.SetActive(false);

        if (buildAction == null || buildAction.action == null)
            Debug.LogWarning(
                $"[BuildSite] {name}: assign Build Action (InputActionReference from InputSystem_Actions → Player → Build).",
                this);
    }

    private void OnEnable()
    {
        if (buildAction != null && buildAction.action != null)
            buildAction.action.Enable();
    }

    private void OnDisable()
    {
        HideGhostPreview();
        if (buildAction != null && buildAction.action != null)
            buildAction.action.Disable();
    }

    private void Update()
    {
        if (!_playerInRange || IsBuilt) return;

        if (WasBuildPressedThisFrame())
            TryBuild();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayer(other) || IsBuilt) return;

        _playerInRange = true;
        _playerInventory = other.GetComponent<Inventory>() ?? other.GetComponentInParent<Inventory>();
        if (debugLog)
            Debug.Log($"[BuildSite] Player entered {name}", this);

        if (promptRoot != null)
            promptRoot.SetActive(true);

        RefreshPromptText();
        ShowGhostPreview();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsPlayer(other)) return;

        _playerInRange = false;
        _playerInventory = null;
        if (debugLog)
            Debug.Log($"[BuildSite] Player left {name}", this);

        if (promptRoot != null)
            promptRoot.SetActive(false);

        HideGhostPreview();
    }

    private bool WasBuildPressedThisFrame()
    {
        if (buildAction == null || buildAction.action == null)
            return false;

        return buildAction.action.WasPressedThisFrame();
    }

    private string GetBuildBindingDisplayString()
    {
        if (buildAction == null || buildAction.action == null)
            return "Build";

        return buildAction.action.GetBindingDisplayString();
    }

    /// <summary>Refresh prompt after changing cost or name.</summary>
    public void RefreshPromptText()
    {
        bool hasBinding = HasAnyPromptBinding();
        if (!hasBinding)
            return;

        if (titleText != null)
            titleText.text = $"[{GetBuildBindingDisplayString()}] {buildingName}";

        ApplyResourceRow(woodRow, woodAmountText, costWood);
        ApplyResourceRow(stoneRow, stoneAmountText, costStone);
        ApplyResourceRow(coinRow, coinAmountText, costCoin);
    }

    private bool HasAnyPromptBinding()
    {
        return promptRoot != null || titleText != null || woodAmountText != null || stoneAmountText != null ||
               coinAmountText != null;
    }

    private static void ApplyResourceRow(GameObject row, TextMeshProUGUI amountText, int amount)
    {
        if (row != null)
            row.SetActive(amount > 0);
        if (amountText != null)
            amountText.text = amount.ToString();
    }

    private void TryBuild()
    {
        if (IsBuilt) return;

        if (builtPrefab == null)
        {
            Debug.LogWarning($"[BuildSite] {name}: assign builtPrefab.", this);
            return;
        }

        if (!ValidateCostConfiguration(out string configError))
        {
            Debug.LogWarning($"[BuildSite] {name}: {configError}", this);
            return;
        }

        if (_playerInventory == null)
        {
            Debug.LogWarning($"[BuildSite] {name}: no Inventory on player (add Inventory to Player or a parent).", this);
            onBuildFailedInsufficientResources?.Invoke();
            return;
        }

        if (!CanAfford(_playerInventory, out string affordReason))
        {
            Debug.LogWarning($"[BuildSite] {name}: cannot build — {affordReason}", this);
            onBuildFailedInsufficientResources?.Invoke();
            return;
        }

        SpendCost(_playerInventory);

        if (debugLog)
            Debug.Log($"[BuildSite] Built {name} (cost deducted).", this);

        HideGhostPreview();

        var pos = spawnPoint != null ? spawnPoint.position : transform.position;
        var rot = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

        var built = Instantiate(builtPrefab, pos, rot);
        if (!built.activeSelf)
            built.SetActive(true);

        IsBuilt = true;
        _playerInRange = false;
        _playerInventory = null;

        if (promptRoot != null)
            promptRoot.SetActive(false);

        // Do not SetActive(false) on the same reference as builtPrefab or on assets outside this scene, or prefab templates break.
        if (siteVisual != null)
        {
            if (ReferenceEquals(siteVisual, builtPrefab))
            {
                Debug.LogWarning(
                    $"[BuildSite] {name}: site visual is the same reference as builtPrefab; skipped hide to avoid disabling the template.",
                    this);
            }
            else if (!siteVisual.scene.IsValid() || siteVisual.scene != gameObject.scene)
            {
                Debug.LogWarning(
                    $"[BuildSite] {name}: site visual is not in this scene; skipped SetActive(false).",
                    this);
            }
            else
                siteVisual.SetActive(false);
        }

        if (_trigger != null)
            _trigger.enabled = false;

        onBuilt?.Invoke();
    }

    private bool ValidateCostConfiguration(out string error)
    {
        error = null;
        if (costWood > 0 && woodItem == null)
        {
            error = "costWood > 0 but woodItem (ItemData) is not assigned.";
            return false;
        }

        if (costStone > 0 && stoneItem == null)
        {
            error = "costStone > 0 but stoneItem (ItemData) is not assigned.";
            return false;
        }

        if (costCoin > 0 && coinItem == null)
        {
            error = "costCoin > 0 but coinItem (ItemData) is not assigned.";
            return false;
        }

        return true;
    }

    private bool CanAfford(Inventory inv, out string reason)
    {
        reason = null;
        if (costWood > 0 && !inv.HasCount(woodItem, costWood))
        {
            reason = $"Not enough wood (need {costWood}, have {inv.GetItemCount(woodItem)}).";
            return false;
        }

        if (costStone > 0 && !inv.HasCount(stoneItem, costStone))
        {
            reason = $"Not enough stone (need {costStone}, have {inv.GetItemCount(stoneItem)}).";
            return false;
        }

        if (costCoin > 0 && !inv.HasCount(coinItem, costCoin))
        {
            reason = $"Not enough coin (need {costCoin}, have {inv.GetItemCount(coinItem)}).";
            return false;
        }

        return true;
    }

    private void SpendCost(Inventory inv)
    {
        if (costWood > 0)
            inv.RemoveItem(woodItem, costWood);
        if (costStone > 0)
            inv.RemoveItem(stoneItem, costStone);
        if (costCoin > 0)
            inv.RemoveItem(coinItem, costCoin);
    }

    private static bool IsPlayer(Collider2D other)
    {
        return other.CompareTag("Player");
    }

    private void ShowGhostPreview()
    {
        if (IsBuilt || _ghostInstance != null) return;

        var template = ghostPreviewPrefab != null ? ghostPreviewPrefab : builtPrefab;
        if (template == null) return;

        var pos = spawnPoint != null ? spawnPoint.position : transform.position;
        var rot = spawnPoint != null ? spawnPoint.rotation : transform.rotation;
        _ghostInstance = Instantiate(template, pos, rot);
        _ghostInstance.name = template.name + "_Ghost";

        if (!_ghostInstance.activeSelf)
            _ghostInstance.SetActive(true);

        ApplyGhostPreviewSettings(_ghostInstance);
    }

    private void HideGhostPreview()
    {
        if (_ghostInstance == null) return;

        Destroy(_ghostInstance);
        _ghostInstance = null;
    }

    private void ApplyGhostPreviewSettings(GameObject root)
    {
        foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            var c = sr.color;
            c.a = ghostAlpha;
            sr.color = c;
            sr.sortingOrder += ghostSortingOrderOffset;
        }

        foreach (var col in root.GetComponentsInChildren<Collider2D>(true))
            col.enabled = false;

        foreach (var rb in root.GetComponentsInChildren<Rigidbody2D>(true))
        {
            rb.simulated = false;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        foreach (var health in root.GetComponentsInChildren<Health>(true))
            health.enabled = false;
    }
}
