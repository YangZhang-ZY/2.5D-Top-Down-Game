using UnityEngine;
using UnityEngine.Events;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

/// <summary>
/// Stand near a built turret or wall and press the upgrade action to spend items and raise level.
/// Requires a trigger <see cref="Collider2D"/> on this object or a child (non-trigger colliders are ignored),
/// unless <see cref="interactionViaBuildSiteOnly"/> is true — then a scene <see cref="BuildSite"/> trigger handles
/// prompts and input for both build and upgrade.
/// Wall: <see cref="Health"/> only (no <see cref="Turret"/>). Turret: <see cref="Turret"/>; add <see cref="Health"/> on the same
/// object for ruin/repair and HP upgrades like walls, and wire <see cref="interactionViaBuildSiteOnly"/> like walls so <see cref="BuildSite"/> drives UI.
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

    [Tooltip("每次升级增加的最大生命（仅当同一物体上同时有 Turret 与 Health 时生效；与围墙分开配置）。")]
    [SerializeField] private int turretMaxHpBonusPerLevel = 15;

    [Header("Wall bonuses")]
    [Tooltip("每次升级增加的最大生命（仅围墙：有 Health、无 Turret）。")]
    [FormerlySerializedAs("maxHpBonusPerLevel")]
    [SerializeField] private int wallMaxHpBonusPerLevel = 25;

    [Header("Costs (index 0 = upgrade from level 1 to 2)")]
    [SerializeField] private int[] costWoodSteps;

    [SerializeField] private int[] costStoneSteps;

    [SerializeField] private int[] costCoinSteps;

    [SerializeField] private ItemData woodItem;

    [SerializeField] private ItemData stoneItem;

    [SerializeField] private ItemData coinItem;

    [Header("BuildSite integration")]
    [Tooltip("勾选后：不再使用本物体上的升级触发区与 Prompt，由场景里同一格 BuildSite 的触发器负责走进区域显示升级消耗与按键。")]
    [SerializeField] private bool interactionViaBuildSiteOnly;

    [Header("Input")]
    [Tooltip("Usually the same Interact or Build action as other buildings. Hosted by BuildSite 时改由 BuildSite 的按键处理，此处可不赋值。")]
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

    /// <summary>已达最高级，不应再显示升级 UI。</summary>
    public bool IsMaxLevel => currentLevel >= maxLevel;

    /// <summary>UI 与升级判定用的物品，与 <see cref="BuildSite"/> 持有量显示一致时需配置相同 ItemData。</summary>
    public ItemData WoodItem => woodItem;

    public ItemData StoneItem => stoneItem;

    public ItemData CoinItem => coinItem;

    /// <summary>升级提示标题用名称（与 Inspector structureName 一致）。</summary>
    public string StructureDisplayName => structureName;

    public bool UsesBuildSiteInteractionOnly => interactionViaBuildSiteOnly;

    private Turret _turret;
    private Health _health;
    /// <summary>True if a <see cref="Turret"/> is present (may still have <see cref="Health"/> for repair/HP bar).</summary>
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

        if (_isTurret && _health == null && debugLog)
            Debug.Log(
                $"[BuildingUpgrade] {name}: Turret has no Health — use BuildSite + Health on this object for repair/upgrade UI like walls.",
                this);

        if (interactionViaBuildSiteOnly)
        {
            _triggerZone = null;
            if (promptRoot != null)
                promptRoot.SetActive(false);
        }
        else
        {
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
    }

    private void OnEnable()
    {
        if (!interactionViaBuildSiteOnly && upgradeAction != null && upgradeAction.action != null)
            upgradeAction.action.Enable();
    }

    private void OnDisable()
    {
        if (!interactionViaBuildSiteOnly && upgradeAction != null && upgradeAction.action != null)
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
        if (interactionViaBuildSiteOnly) return;
        if (!_playerInRange || currentLevel >= maxLevel) return;
        if (upgradeAction == null || upgradeAction.action == null) return;
        if (!upgradeAction.action.WasPressedThisFrame()) return;

        TryUpgrade();
    }

    /// <summary>BuildSite 调用：下一级升级所需资源（已满级则全 0）。</summary>
    public void GetNextUpgradeCosts(out int wood, out int stone, out int coin)
    {
        if (IsMaxLevel)
        {
            wood = stone = coin = 0;
            return;
        }

        int idx = currentLevel - 1;
        wood = GetStepCost(costWoodSteps, idx);
        stone = GetStepCost(costStoneSteps, idx);
        coin = GetStepCost(costCoinSteps, idx);
    }

    /// <summary>BuildSite 在同一按键下代为扣费升级。</summary>
    /// <returns>是否成功升级一级。</returns>
    public bool TryUpgradeWithInventory(Inventory inv, out string failReason)
    {
        failReason = null;
        if (IsMaxLevel)
        {
            failReason = "Already max level.";
            return false;
        }

        if (!ValidateCostConfigForStep(out failReason))
            return false;

        if (inv == null)
        {
            failReason = "No Inventory on player.";
            return false;
        }

        int idx = currentLevel - 1;
        int w = GetStepCost(costWoodSteps, idx);
        int s = GetStepCost(costStoneSteps, idx);
        int c = GetStepCost(costCoinSteps, idx);

        if (!CanAfford(inv, w, s, c, out failReason))
            return false;

        Spend(inv, w, s, c);

        currentLevel++;
        if (_isTurret)
            ApplyTurretLevel();
        if (_health != null)
        {
            int hpBonus = _isTurret ? turretMaxHpBonusPerLevel : wallMaxHpBonusPerLevel;
            if (hpBonus > 0)
                _health.AddMaxHP(hpBonus);
        }

        if (debugLog)
            Debug.Log($"[BuildingUpgrade] {name} upgraded to level {currentLevel}.", this);

        onUpgraded?.Invoke(currentLevel);
        if (!interactionViaBuildSiteOnly)
            RefreshPrompt();
        return true;
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
        if (!TryUpgradeWithInventory(_playerInventory, out string err))
        {
            if (debugLog && !string.IsNullOrEmpty(err))
                Debug.Log($"[BuildingUpgrade] {name}: {err}", this);
        }
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
        if (interactionViaBuildSiteOnly) return;
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
