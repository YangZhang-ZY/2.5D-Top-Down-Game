using UnityEngine;
using UnityEngine.Events;
using TMPro;
using UnityEngine.InputSystem;

/// <summary>
/// Stand near a built turret or wall and press the upgrade action to spend items and raise level.
/// Requires a trigger <see cref="Collider2D"/> on this object or a child (non-trigger colliders are ignored).
/// Turret: same GameObject must have <see cref="Turret"/>. Wall: same GameObject must have <see cref="Health"/> (no Turret).
/// Cost arrays: length should be <c>maxLevel - 1</c> (one entry per upgrade step). Shorter arrays repeat the last entry.
/// </summary>
public class BuildingUpgrade : MonoBehaviour
{
    [Header("Levels")]
    [Tooltip("Maximum level (level 1 is the initial build).")]
    [Min(2)]
    [SerializeField] private int maxLevel = 3;

    [Tooltip("Starts at 1 for a freshly built structure.")]
    [SerializeField] private int currentLevel = 1;

    [Header("Turret bonuses (per level above 1)")]
    [SerializeField] private float damageBonusPerLevel = 2f;

    [SerializeField] private float rangeBonusPerLevel = 0.4f;

    [SerializeField] private float fireRateBonusPerLevel = 0.25f;

    [Header("Wall bonuses")]
    [Tooltip("Max HP added each time the wall levels up.")]
    [SerializeField] private int maxHpBonusPerLevel = 25;

    [Header("Costs (index 0 = upgrade from level 1 to 2)")]
    [SerializeField] private int[] costWoodSteps;

    [SerializeField] private int[] costStoneSteps;

    [SerializeField] private int[] costCoinSteps;

    [SerializeField] private ItemData woodItem;

    [SerializeField] private ItemData stoneItem;

    [SerializeField] private ItemData coinItem;

    [Header("Input")]
    [Tooltip("Usually the same Interact or Build action as other buildings.")]
    [SerializeField] private InputActionReference upgradeAction;

    [Header("Prompt UI (optional)")]
    [SerializeField] private GameObject promptRoot;

    [Tooltip("Optional: key + structure name + level step.")]
    [SerializeField] private TextMeshProUGUI titleText;

    [Tooltip("Numbers next to wood / stone / coin icons (same order as BuildSite).")]
    [SerializeField] private TextMeshProUGUI woodAmountText;

    [SerializeField] private TextMeshProUGUI stoneAmountText;

    [SerializeField] private TextMeshProUGUI coinAmountText;

    [Tooltip("Optional: hide the whole row when that resource cost is 0.")]
    [SerializeField] private GameObject woodRow;

    [SerializeField] private GameObject stoneRow;

    [SerializeField] private GameObject coinRow;

    [SerializeField] private string structureName = "Structure";

    [Header("Events")]
    [SerializeField] private UnityEvent<int> onUpgraded;

    [Header("Debug")]
    [SerializeField] private bool debugLog;

    public int CurrentLevel => currentLevel;

    public int MaxLevel => maxLevel;

    private Turret _turret;
    private Health _health;
    private bool _isTurret;
    private Collider2D _triggerZone;
    private bool _playerInRange;
    private Inventory _playerInventory;

    private void Awake()
    {
        _turret = GetComponent<Turret>();
        _health = GetComponent<Health>();

        if (_turret != null)
            _isTurret = true;
        else if (_health != null)
            _isTurret = false;
        else
        {
            Debug.LogError($"[BuildingUpgrade] {name} needs Turret and/or Health on the same GameObject.", this);
            enabled = false;
            return;
        }

        _triggerZone = FindTriggerCollider();
        if (_triggerZone == null)
        {
            Debug.LogError($"[BuildingUpgrade] {name}: add a trigger Collider2D (this object or child) for the upgrade zone.", this);
            enabled = false;
            return;
        }

        if (upgradeAction == null || upgradeAction.action == null)
            Debug.LogWarning($"[BuildingUpgrade] {name}: assign an Upgrade Input Action Reference.", this);

        if (promptRoot != null)
            promptRoot.SetActive(false);

        var zone = _triggerZone.gameObject.GetComponent<BuildingUpgradeZone>();
        if (zone == null)
            zone = _triggerZone.gameObject.AddComponent<BuildingUpgradeZone>();
        zone.Init(this);
    }

    private void OnEnable()
    {
        if (upgradeAction != null && upgradeAction.action != null)
            upgradeAction.action.Enable();
    }

    private void OnDisable()
    {
        if (upgradeAction != null && upgradeAction.action != null)
            upgradeAction.action.Disable();
    }

    private void Start()
    {
        currentLevel = Mathf.Clamp(currentLevel, 1, maxLevel);
        if (_isTurret)
            ApplyTurretLevel();
    }

    private void Update()
    {
        if (!_playerInRange || currentLevel >= maxLevel) return;
        if (upgradeAction == null || upgradeAction.action == null) return;
        if (!upgradeAction.action.WasPressedThisFrame()) return;

        TryUpgrade();
    }

    internal void OnZoneEnter(Collider2D other)
    {
        if (!IsPlayer(other)) return;

        _playerInRange = true;
        _playerInventory = other.GetComponent<Inventory>() ?? other.GetComponentInParent<Inventory>();

        if (promptRoot != null)
            promptRoot.SetActive(true);
        RefreshPrompt();
    }

    internal void OnZoneExit(Collider2D other)
    {
        if (!IsPlayer(other)) return;

        _playerInRange = false;
        _playerInventory = null;

        if (promptRoot != null)
            promptRoot.SetActive(false);
    }

    private Collider2D FindTriggerCollider()
    {
        var c = GetComponent<Collider2D>();
        if (c != null && c.isTrigger)
            return c;

        foreach (var col in GetComponentsInChildren<Collider2D>(true))
        {
            if (col.isTrigger)
                return col;
        }

        return null;
    }

    private static bool IsPlayer(Collider2D other) => other.CompareTag("Player");

    /// <summary>Refreshes prompt after changing costs in the Inspector at runtime.</summary>
    public void RefreshPrompt()
    {
        bool hasBinding = HasAnyPromptBinding();
        if (!hasBinding)
            return;

        string bind = upgradeAction != null && upgradeAction.action != null
            ? upgradeAction.action.GetBindingDisplayString()
            : "?";

        if (currentLevel >= maxLevel)
        {
            ApplyResourceRow(woodRow, woodAmountText, 0);
            ApplyResourceRow(stoneRow, stoneAmountText, 0);
            ApplyResourceRow(coinRow, coinAmountText, 0);
            if (titleText != null)
                titleText.text = $"{structureName} — max Lv.{maxLevel}";
            return;
        }

        if (titleText != null)
            titleText.text = $"[{bind}] {structureName}  L{currentLevel}→L{currentLevel + 1}";

        int w = GetStepCost(costWoodSteps, currentLevel - 1);
        int s = GetStepCost(costStoneSteps, currentLevel - 1);
        int c = GetStepCost(costCoinSteps, currentLevel - 1);

        ApplyResourceRow(woodRow, woodAmountText, w);
        ApplyResourceRow(stoneRow, stoneAmountText, s);
        ApplyResourceRow(coinRow, coinAmountText, c);
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

    private void TryUpgrade()
    {
        if (currentLevel >= maxLevel) return;

        if (!ValidateCostConfigForStep(out string err))
        {
            Debug.LogWarning($"[BuildingUpgrade] {name}: {err}", this);
            return;
        }

        if (_playerInventory == null)
        {
            Debug.LogWarning($"[BuildingUpgrade] {name}: no Inventory on player.", this);
            return;
        }

        int idx = currentLevel - 1;
        int w = GetStepCost(costWoodSteps, idx);
        int s = GetStepCost(costStoneSteps, idx);
        int c = GetStepCost(costCoinSteps, idx);

        if (!CanAfford(_playerInventory, w, s, c, out string reason))
        {
            if (debugLog)
                Debug.Log($"[BuildingUpgrade] {name}: cannot upgrade — {reason}", this);
            return;
        }

        Spend(_playerInventory, w, s, c);

        currentLevel++;
        if (_isTurret)
            ApplyTurretLevel();
        else
            _health.AddMaxHP(maxHpBonusPerLevel);

        if (debugLog)
            Debug.Log($"[BuildingUpgrade] {name} upgraded to level {currentLevel}.", this);

        onUpgraded?.Invoke(currentLevel);
        RefreshPrompt();
    }

    private void ApplyTurretLevel()
    {
        _turret.ApplyUpgradeLevel(currentLevel, damageBonusPerLevel, rangeBonusPerLevel, fireRateBonusPerLevel);
    }

    private static int GetStepCost(int[] arr, int stepIndex)
    {
        if (arr == null || arr.Length == 0) return 0;
        int i = Mathf.Clamp(stepIndex, 0, arr.Length - 1);
        return Mathf.Max(0, arr[i]);
    }

    private bool ValidateCostConfigForStep(out string error)
    {
        error = null;
        int idx = currentLevel - 1;
        int w = GetStepCost(costWoodSteps, idx);
        int s = GetStepCost(costStoneSteps, idx);
        int c = GetStepCost(costCoinSteps, idx);

        if (w > 0 && woodItem == null) { error = "Wood cost > 0 but wood ItemData is not assigned."; return false; }
        if (s > 0 && stoneItem == null) { error = "Stone cost > 0 but stone ItemData is not assigned."; return false; }
        if (c > 0 && coinItem == null) { error = "Coin cost > 0 but coin ItemData is not assigned."; return false; }
        return true;
    }

    private bool CanAfford(Inventory inv, int w, int s, int c, out string reason)
    {
        reason = null;
        if (w > 0 && !inv.HasCount(woodItem, w))
        {
            reason = $"Not enough wood (need {w}, have {inv.GetItemCount(woodItem)}).";
            return false;
        }

        if (s > 0 && !inv.HasCount(stoneItem, s))
        {
            reason = $"Not enough stone (need {s}, have {inv.GetItemCount(stoneItem)}).";
            return false;
        }

        if (c > 0 && !inv.HasCount(coinItem, c))
        {
            reason = $"Not enough coin (need {c}, have {inv.GetItemCount(coinItem)}).";
            return false;
        }

        return true;
    }

    private void Spend(Inventory inv, int w, int s, int c)
    {
        if (w > 0) inv.RemoveItem(woodItem, w);
        if (s > 0) inv.RemoveItem(stoneItem, s);
        if (c > 0) inv.RemoveItem(coinItem, c);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        var z = _triggerZone != null ? _triggerZone : FindTriggerCollider();
        if (z == null) return;
        Gizmos.color = new Color(0.4f, 0.9f, 0.4f, 0.25f);
        var b = z.bounds;
        Gizmos.DrawWireCube(b.center, b.size);
    }
#endif
}

/// <summary>Forwards trigger events from the upgrade zone collider (often a child) to <see cref="BuildingUpgrade"/>.</summary>
[DisallowMultipleComponent]
public class BuildingUpgradeZone : MonoBehaviour
{
    private BuildingUpgrade _owner;

    public void Init(BuildingUpgrade owner)
    {
        _owner = owner;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        _owner?.OnZoneEnter(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        _owner?.OnZoneExit(other);
    }
}
