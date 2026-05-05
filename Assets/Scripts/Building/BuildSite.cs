using UnityEngine;
using UnityEngine.Events;
using TMPro;
using UnityEngine.InputSystem;

/// <summary>
/// 建造点：玩家进入触发区显示消耗与虚影，按键建造；建成之后<strong>同一触发区</strong>继续显示
/// <see cref="BuildingUpgrade"/> 的升级消耗（建筑 prefab 上需勾选 Interaction Via Build Site Only），满级后不再显示面板。
/// 建筑耐久归零则按修理逻辑重新开放建造。
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

    [Header("Repair (after built Health reaches 0)")]
    [Tooltip("修复时各资源消耗 = ceil(对应建造消耗 × 本系数)。0 = 免费修复。")]
    [SerializeField] [Range(0f, 1f)] private float repairCostMultiplier = 0.5f;

    [Tooltip("提示里修复行动的前缀（如「修复」）。")]
    [SerializeField] private string repairPromptPrefix = "修复";

    [Header("Input (Input System)")]
    [Tooltip("Input Action Reference for Player/Build.")]
    [SerializeField] private InputActionReference buildAction;

    [SerializeField] private string buildingName = "Building";

    [Header("Prompt UI (optional — wire your panel like BuildingUpgrade)")]
    [Tooltip("Root panel shown while player is in range.")]
    [SerializeField] private GameObject promptRoot;

    [Tooltip("Optional: key binding + building name (or repair prefix + name).")]
    [SerializeField] private TextMeshProUGUI titleText;

    [Tooltip("Required amounts next to wood / stone / coin icons (same order as BuildingUpgrade).")]
    [SerializeField] private TextMeshProUGUI woodAmountText;

    [SerializeField] private TextMeshProUGUI stoneAmountText;

    [SerializeField] private TextMeshProUGUI coinAmountText;

    [Tooltip("Optional: current inventory counts for each resource. Leave empty to only show required amounts above.")]
    [SerializeField] private TextMeshProUGUI woodOwnedAmountText;

    [SerializeField] private TextMeshProUGUI stoneOwnedAmountText;

    [SerializeField] private TextMeshProUGUI coinOwnedAmountText;

    [Tooltip("Optional: hide the whole row when that resource cost is 0.")]
    [SerializeField] private GameObject woodRow;

    [SerializeField] private GameObject stoneRow;

    [SerializeField] private GameObject coinRow;

    [Header("Status display (optional)")]
    [Tooltip("建成且带 BuildingUpgrade：显示「前缀 + 等级」；已满级为「前缀 + Max 文案」。")]
    [SerializeField] private TextMeshProUGUI currentLevelStatusText;

    [Tooltip("等级数字前的文字，如 \"Level \"（末尾可带空格）。")]
    [SerializeField] private string levelStatusPrefix = "Level ";

    [Tooltip("已满级时接在前缀后的文案，如 Max。")]
    [SerializeField] private string maxLevelDisplayString = "Max";

    [Tooltip("建成且带 Health：显示「前缀 + 当前/最大」。")]
    [SerializeField] private TextMeshProUGUI builtStructureHealthText;

    [Tooltip("生命值前的文字，如 \"Health \"（末尾可带空格）。")]
    [SerializeField] private string healthStatusPrefix = "Health ";

    [Tooltip("当前 HP 与最大 HP 之间的分隔符。")]
    [SerializeField] private string healthHpSeparator = "/";

    [Header("Events")]
    [SerializeField] private UnityEvent onBuilt;

    [Tooltip("Invoked when the player cannot afford the cost (hook up SFX or floating text).")]
    [SerializeField] private UnityEvent onBuildFailedInsufficientResources;

    [Header("Debug")]
    [SerializeField] private bool debugLog;

    [Header("Scene 视图调试（仅编辑器）")]
    [Tooltip("勾选后在 Scene 视图绘制「建成后」的大致包围盒，便于对齐 Spawn Point；调好位置后请取消勾选。运行时不会绘制。")]
    [SerializeField] private bool showEditorGhostInScene;

    [Tooltip("勾选后仅当选中本物体时绘制，避免场景里多个点一起画满屏。")]
    [SerializeField] private bool editorGhostOnlyWhenSelected = true;

    [SerializeField] private Color editorGhostWireColor = new Color(0.2f, 1f, 0.45f, 0.95f);

    [SerializeField] private Color editorGhostFillColor = new Color(0.2f, 1f, 0.45f, 0.08f);

    [Tooltip("无法从 Prefab 估出包围盒时用的立方体尺寸（世界单位，XY 为主）。")]
    [SerializeField] private Vector3 editorGhostFallbackSize = new Vector3(2f, 2f, 0.1f);

#if UNITY_EDITOR
    GameObject _editorGhostCachedTemplate;
    Vector3 _editorGhostCachedPos;
    Quaternion _editorGhostCachedRot;
    Bounds _editorGhostCachedBounds;
    bool _editorGhostCacheValid;
#endif

    /// <summary>当前是否有存活中的已造建筑（根实例仍存在）。</summary>
    public bool IsBuilt => _builtInstance != null;

    /// <summary>是否至少成功建造过一次（含修复），用于区分首次建造与灾后修复。</summary>
    public bool HasBuiltAtLeastOnce => _hasBuiltAtLeastOnce;

    /// <summary>建成实例上的升级组件（可选）；须由 prefab 勾选 <see cref="BuildingUpgrade.UsesBuildSiteInteractionOnly"/>。</summary>
    public BuildingUpgrade HostedUpgrade => _hostedUpgrade;

    private Collider2D _trigger;
    private bool _playerInRange;
    private Inventory _playerInventory;
    private GameObject _ghostInstance;
    private GameObject _builtInstance;
    private Health _builtHealth;
    private bool _hasBuiltAtLeastOnce;
    private BuildingUpgrade _hostedUpgrade;

    /// <summary>进入触发区时那块玩家 Collider（用于补测离开，避免用根物体坐标误判）。</summary>
    Collider2D _playerZoneCollider;

    private void Awake()
    {
        _trigger = GetComponent<Collider2D>();
        if (_trigger != null && !_trigger.isTrigger)
            Debug.LogWarning($"[BuildSite] Collider2D on {name} should be a trigger.", this);

        HidePromptUiOnly();

        if (HasAnyPromptBinding() && promptRoot == null)
            Debug.LogWarning(
                $"[BuildSite] {name}: Prompt Root 未赋值时，请把整块提示面板拖到 Prompt Root，或在场景里关掉面板默认显示；否则进游戏就会看到数字。",
                this);

        if (buildAction == null || buildAction.action == null)
            Debug.LogWarning(
                $"[BuildSite] {name}: assign Build Action (InputActionReference from InputSystem_Actions → Player → Build).",
                this);
    }

    private void Start()
    {
        if (!_playerInRange)
            HidePromptUiOnly();
    }

    private void OnEnable()
    {
        if (buildAction != null && buildAction.action != null)
            buildAction.action.Enable();
    }

    private void OnDisable()
    {
        UnsubscribeBuiltHealth();
        HandlePlayerLeftTriggerZone();
        HidePromptUiOnly();
        if (buildAction != null && buildAction.action != null)
            buildAction.action.Disable();
    }

    private void Update()
    {
        if (_playerInRange && _trigger != null && _playerZoneCollider != null)
        {
            if (!_trigger.bounds.Intersects(_playerZoneCollider.bounds))
                HandlePlayerLeftTriggerZone();
        }

        if (!_playerInRange || buildAction == null || buildAction.action == null) return;
        if (!buildAction.action.WasPressedThisFrame()) return;

        if (!IsBuilt)
        {
            TryBuild();
            return;
        }

        if (_hostedUpgrade != null && !_hostedUpgrade.IsMaxLevel)
        {
            if (_hostedUpgrade.TryUpgradeWithInventory(_playerInventory, out string upgradeErr))
            {
                if (_hostedUpgrade.IsMaxLevel)
                {
                    if (HasStatusDisplayConfigured())
                        RefreshPromptText();
                    else
                        HidePromptUiOnly();
                }
                else
                    RefreshPromptText();
            }
            else if (!string.IsNullOrEmpty(upgradeErr))
                Debug.LogWarning($"[BuildSite] {name}: 无法升级 — {upgradeErr}", this);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayer(other)) return;

        _playerInRange = true;
        _playerZoneCollider = other;
        _playerInventory = other.GetComponent<Inventory>() ?? other.GetComponentInParent<Inventory>();
        if (debugLog)
            Debug.Log($"[BuildSite] Player entered {name}", this);

        ApplyPromptForCurrentState();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsPlayer(other)) return;

        HandlePlayerLeftTriggerZone();
    }

    void HandlePlayerLeftTriggerZone()
    {
        if (!_playerInRange)
            return;

        _playerInRange = false;
        _playerInventory = null;
        _playerZoneCollider = null;
        if (debugLog)
            Debug.Log($"[BuildSite] Player left {name}", this);

        HidePromptUiOnly();
    }

    /// <summary>只关面板/虚影，不重置「在范围内」状态（例如满级仍站在点里）。</summary>
    void HidePromptUiOnly()
    {
        HideGhostPreview();

        if (promptRoot != null)
            promptRoot.SetActive(false);
        else
            SetOrphanPromptPiecesVisible(false);
    }

    /// <summary>父物体 Prompt Root 打开后，恢复标题/持有量等（曾被无 Root 模式单独 SetActive(false) 的子节点否则会一直不显示）。</summary>
    void PreparePromptRootContentVisible()
    {
        if (titleText != null)
            titleText.gameObject.SetActive(true);
        if (woodOwnedAmountText != null)
            woodOwnedAmountText.gameObject.SetActive(true);
        if (stoneOwnedAmountText != null)
            stoneOwnedAmountText.gameObject.SetActive(true);
        if (coinOwnedAmountText != null)
            coinOwnedAmountText.gameObject.SetActive(true);
        if (currentLevelStatusText != null)
            currentLevelStatusText.gameObject.SetActive(true);
        if (builtStructureHealthText != null)
            builtStructureHealthText.gameObject.SetActive(true);
    }

    /// <summary>未拖 Prompt Root 时，直接关掉各 TMP / Row 节点，避免默认激活。</summary>
    void SetOrphanPromptPiecesVisible(bool visible)
    {
        if (titleText != null)
            titleText.gameObject.SetActive(visible);
        if (woodRow != null)
            woodRow.SetActive(visible);
        if (stoneRow != null)
            stoneRow.SetActive(visible);
        if (coinRow != null)
            coinRow.SetActive(visible);
        if (woodAmountText != null && woodRow == null)
            woodAmountText.gameObject.SetActive(visible);
        if (stoneAmountText != null && stoneRow == null)
            stoneAmountText.gameObject.SetActive(visible);
        if (coinAmountText != null && coinRow == null)
            coinAmountText.gameObject.SetActive(visible);
        if (woodOwnedAmountText != null)
            woodOwnedAmountText.gameObject.SetActive(visible);
        if (stoneOwnedAmountText != null)
            stoneOwnedAmountText.gameObject.SetActive(visible);
        if (coinOwnedAmountText != null)
            coinOwnedAmountText.gameObject.SetActive(visible);
        if (currentLevelStatusText != null)
            currentLevelStatusText.gameObject.SetActive(visible);
        if (builtStructureHealthText != null)
            builtStructureHealthText.gameObject.SetActive(visible);

        if (!visible)
            ClearOwnedAmountLabels();
    }

    /// <summary>进范围、建成、升级后刷新 UI 与虚影。</summary>
    void ApplyPromptForCurrentState()
    {
        if (!ShouldShowInteractionPrompt(out bool showGhost))
        {
            HidePromptUiOnly();
            return;
        }

        if (promptRoot != null)
            promptRoot.SetActive(true);
        else
            SetOrphanPromptPiecesVisible(true);

        PreparePromptRootContentVisible();

        RefreshPromptText();
        if (showGhost)
            ShowGhostPreview();
        else
            HideGhostPreview();
    }

    /// <summary>未建成/维修：显示；已建：可升级、或配置了等级/生命显示（含满级看 Max）、或仅生命条。</summary>
    bool ShouldShowInteractionPrompt(out bool showGhostPreview)
    {
        showGhostPreview = false;
        if (!IsBuilt)
        {
            showGhostPreview = true;
            return true;
        }

        if (_hostedUpgrade != null && !_hostedUpgrade.IsMaxLevel)
            return true;

        if (HasStatusDisplayConfigured())
            return true;

        return false;
    }

    bool HasStatusDisplayConfigured() =>
        currentLevelStatusText != null || builtStructureHealthText != null;

    private string GetBuildBindingDisplayString()
    {
        if (buildAction == null || buildAction.action == null)
            return "Build";

        return buildAction.action.GetBindingDisplayString();
    }

    /// <summary>刷新建造/修复/升级提示（同一套 Prompt 字段）。</summary>
    public void RefreshPromptText()
    {
        bool hasBinding = HasAnyPromptBinding();
        if (!hasBinding)
            return;

        if (IsBuilt && _hostedUpgrade != null && !_hostedUpgrade.IsMaxLevel)
        {
            if (titleText != null)
            {
                titleText.text =
                    $"[{GetBuildBindingDisplayString()}] {_hostedUpgrade.StructureDisplayName} " +
                    $"L{_hostedUpgrade.CurrentLevel}→L{_hostedUpgrade.CurrentLevel + 1}";
            }

            _hostedUpgrade.GetNextUpgradeCosts(out int uw, out int us, out int uc);
            ApplyResourceRow(woodRow, woodAmountText, uw);
            ApplyResourceRow(stoneRow, stoneAmountText, us);
            ApplyResourceRow(coinRow, coinAmountText, uc);

            ApplyOwnedAmountText(woodOwnedAmountText, _hostedUpgrade.WoodItem, uw);
            ApplyOwnedAmountText(stoneOwnedAmountText, _hostedUpgrade.StoneItem, us);
            ApplyOwnedAmountText(coinOwnedAmountText, _hostedUpgrade.CoinItem, uc);
        }
        else if (IsBuilt)
        {
            if (titleText != null)
                titleText.text = string.Empty;
            ApplyResourceRow(woodRow, woodAmountText, 0);
            ApplyResourceRow(stoneRow, stoneAmountText, 0);
            ApplyResourceRow(coinRow, coinAmountText, 0);
            ClearOwnedAmountLabels();
        }
        else
        {
            bool repairMode = IsRepairPromptMode();
            if (titleText != null)
            {
                string action = repairMode ? $"{repairPromptPrefix} {buildingName}" : buildingName;
                titleText.text = $"[{GetBuildBindingDisplayString()}] {action}";
            }

            GetCostForNextInteraction(out int w, out int s, out int c);
            ApplyResourceRow(woodRow, woodAmountText, w);
            ApplyResourceRow(stoneRow, stoneAmountText, s);
            ApplyResourceRow(coinRow, coinAmountText, c);

            ApplyOwnedAmountText(woodOwnedAmountText, woodItem, w);
            ApplyOwnedAmountText(stoneOwnedAmountText, stoneItem, s);
            ApplyOwnedAmountText(coinOwnedAmountText, coinItem, c);
        }

        RefreshStatusLines();
    }

    /// <summary>等级与生命（与其它 Prompt 文本独立，便于面板单独摆位）。受击/治疗时也会刷新。</summary>
    void RefreshStatusLines()
    {
        if (currentLevelStatusText != null)
        {
            if (!IsBuilt || _hostedUpgrade == null)
                currentLevelStatusText.text = string.Empty;
            else if (_hostedUpgrade.IsMaxLevel)
                currentLevelStatusText.text = levelStatusPrefix + maxLevelDisplayString;
            else
                currentLevelStatusText.text = levelStatusPrefix + _hostedUpgrade.CurrentLevel.ToString();
        }

        if (builtStructureHealthText != null)
        {
            if (!IsBuilt || _builtHealth == null || _builtHealth.IsDead)
                builtStructureHealthText.text = string.Empty;
            else
            {
                builtStructureHealthText.text =
                    $"{healthStatusPrefix}{_builtHealth.CurrentHP}{healthHpSeparator}{_builtHealth.maxHP}";
            }
        }
    }

    void ClearOwnedAmountLabels()
    {
        if (woodOwnedAmountText != null) woodOwnedAmountText.text = string.Empty;
        if (stoneOwnedAmountText != null) stoneOwnedAmountText.text = string.Empty;
        if (coinOwnedAmountText != null) coinOwnedAmountText.text = string.Empty;
    }

    /// <summary>Show current stacks when player in range; clears to em dash when unknown.</summary>
    void ApplyOwnedAmountText(TextMeshProUGUI label, ItemData item, int needAmount)
    {
        if (label == null) return;
        if (needAmount <= 0)
        {
            label.text = string.Empty;
            return;
        }

        if (_playerInventory != null && item != null)
            label.text = _playerInventory.GetItemCount(item).ToString();
        else
            label.text = "—";
    }

    private bool HasAnyPromptBinding()
    {
        return promptRoot != null || titleText != null || woodAmountText != null || stoneAmountText != null ||
               coinAmountText != null || woodOwnedAmountText != null || stoneOwnedAmountText != null ||
               coinOwnedAmountText != null || currentLevelStatusText != null ||
               builtStructureHealthText != null;
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

        GetCostForNextInteraction(out int needWood, out int needStone, out int needCoin);
        bool placingRepair = IsRepairPromptMode();
        if (!CanAfford(_playerInventory, needWood, needStone, needCoin, out string affordReason))
        {
            Debug.LogWarning($"[BuildSite] {name}: cannot build — {affordReason}", this);
            onBuildFailedInsufficientResources?.Invoke();
            return;
        }

        SpendCost(_playerInventory, needWood, needStone, needCoin);

        if (debugLog)
            Debug.Log(
                $"[BuildSite] {(placingRepair ? "Repaired" : "Built")} {name} (cost deducted).",
                this);

        HideGhostPreview();

        var pos = spawnPoint != null ? spawnPoint.position : transform.position;
        var rot = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

        var built = Instantiate(builtPrefab, pos, rot);
        if (!built.activeSelf)
            built.SetActive(true);

        _builtInstance = built;
        _hasBuiltAtLeastOnce = true;
        TrySubscribeBuiltHealth(built);
        CacheHostedUpgrade(built);

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
            _trigger.enabled = true;

        onBuilt?.Invoke();

        if (_playerInRange)
            ApplyPromptForCurrentState();
    }

    void CacheHostedUpgrade(GameObject builtRoot)
    {
        _hostedUpgrade = builtRoot.GetComponentInChildren<BuildingUpgrade>(true);
        if (_hostedUpgrade == null)
        {
            if (debugLog)
                Debug.Log(
                    $"[BuildSite] {name}: built prefab has no BuildingUpgrade — only construction/repair UI will show.",
                    this);
            return;
        }

        if (!_hostedUpgrade.UsesBuildSiteInteractionOnly)
            Debug.LogWarning(
                $"[BuildSite] {name}: BuildingUpgrade on prefab should enable Interaction Via Build Site Only " +
                $"to avoid a second upgrade trigger. Prompt/input are still driven by this BuildSite.",
                this);

        WarnIfUpgradeCostsMissingItems();
    }

    void WarnIfUpgradeCostsMissingItems()
    {
        if (_hostedUpgrade == null) return;
        _hostedUpgrade.GetNextUpgradeCosts(out int w, out int s, out int c);
        if (w > 0 && _hostedUpgrade.WoodItem == null)
            Debug.LogWarning(
                $"[BuildSite] {name}: 升级需要木材但 BuildingUpgrade 未赋值 Wood Item，按键升级会失败。",
                _hostedUpgrade);
        if (s > 0 && _hostedUpgrade.StoneItem == null)
            Debug.LogWarning(
                $"[BuildSite] {name}: 升级需要石头但 BuildingUpgrade 未赋值 Stone Item，按键升级会失败。",
                _hostedUpgrade);
        if (c > 0 && _hostedUpgrade.CoinItem == null)
            Debug.LogWarning(
                $"[BuildSite] {name}: 升级需要金币但 BuildingUpgrade 未赋值 Coin Item，按键升级会失败。",
                _hostedUpgrade);
    }

    private bool ValidateCostConfiguration(out string error)
    {
        error = null;
        GetCostForNextInteraction(out int w, out int s, out int c);
        if (w > 0 && woodItem == null)
        {
            error = "wood cost > 0 but woodItem (ItemData) is not assigned.";
            return false;
        }

        if (s > 0 && stoneItem == null)
        {
            error = "stone cost > 0 but stoneItem (ItemData) is not assigned.";
            return false;
        }

        if (c > 0 && coinItem == null)
        {
            error = "coin cost > 0 but coinItem (ItemData) is not assigned.";
            return false;
        }

        return true;
    }

    private bool CanAfford(Inventory inv, int wood, int stone, int coin, out string reason)
    {
        reason = null;
        if (wood > 0 && !inv.HasCount(woodItem, wood))
        {
            reason = $"Not enough wood (need {wood}, have {inv.GetItemCount(woodItem)}).";
            return false;
        }

        if (stone > 0 && !inv.HasCount(stoneItem, stone))
        {
            reason = $"Not enough stone (need {stone}, have {inv.GetItemCount(stoneItem)}).";
            return false;
        }

        if (coin > 0 && !inv.HasCount(coinItem, coin))
        {
            reason = $"Not enough coin (need {coin}, have {inv.GetItemCount(coinItem)}).";
            return false;
        }

        return true;
    }

    private void SpendCost(Inventory inv, int wood, int stone, int coin)
    {
        if (wood > 0)
            inv.RemoveItem(woodItem, wood);
        if (stone > 0)
            inv.RemoveItem(stoneItem, stone);
        if (coin > 0)
            inv.RemoveItem(coinItem, coin);
    }

    bool IsRepairPromptMode() => !IsBuilt && _hasBuiltAtLeastOnce;

    void GetCostForNextInteraction(out int wood, out int stone, out int coin)
    {
        if (IsRepairPromptMode())
        {
            wood = ScaleRepairCost(costWood);
            stone = ScaleRepairCost(costStone);
            coin = ScaleRepairCost(costCoin);
        }
        else
        {
            wood = costWood;
            stone = costStone;
            coin = costCoin;
        }
    }

    int ScaleRepairCost(int buildCost)
    {
        if (buildCost <= 0 || repairCostMultiplier <= 0f)
            return 0;
        return Mathf.Max(0, Mathf.CeilToInt(buildCost * repairCostMultiplier));
    }

    void TrySubscribeBuiltHealth(GameObject built)
    {
        UnsubscribeBuiltHealth();
        _builtHealth = built.GetComponentInChildren<Health>(true);
        if (_builtHealth == null)
        {
            if (debugLog)
                Debug.Log(
                    $"[BuildSite] {name}: built prefab has no Health — ruin/repair flow will not run.",
                    this);
            return;
        }

        _builtHealth.OnDeath.AddListener(OnBuiltStructureDied);
        _builtHealth.OnDamaged.AddListener(OnBuiltHealthChangedForPromptUi);
        _builtHealth.OnHealed.AddListener(OnBuiltHealthChangedForPromptUiNoArgs);
    }

    void UnsubscribeBuiltHealth()
    {
        if (_builtHealth == null) return;
        _builtHealth.OnDeath.RemoveListener(OnBuiltStructureDied);
        _builtHealth.OnDamaged.RemoveListener(OnBuiltHealthChangedForPromptUi);
        _builtHealth.OnHealed.RemoveListener(OnBuiltHealthChangedForPromptUiNoArgs);
        _builtHealth = null;
    }

    void OnBuiltHealthChangedForPromptUi(float _) => RefreshStatusLinesIfInRange();

    void OnBuiltHealthChangedForPromptUiNoArgs() => RefreshStatusLinesIfInRange();

    void RefreshStatusLinesIfInRange()
    {
        if (_playerInRange)
            RefreshStatusLines();
    }

    void OnBuiltStructureDied()
    {
        UnsubscribeBuiltHealth();
        _hostedUpgrade = null;

        var instance = _builtInstance;
        _builtInstance = null;

        if (instance != null && instance.GetComponentInChildren<Destructible>() == null)
            Destroy(instance);

        if (_trigger != null)
            _trigger.enabled = true;

        if (siteVisual != null
            && siteVisual.scene.IsValid()
            && siteVisual.scene == gameObject.scene
            && !ReferenceEquals(siteVisual, builtPrefab))
            siteVisual.SetActive(true);

        if (debugLog)
            Debug.Log($"[BuildSite] {name}: structure destroyed — site open for repair.", this);

        if (_playerInRange)
            ApplyPromptForCurrentState();
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

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!showEditorGhostInScene || UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (builtPrefab == null && ghostPreviewPrefab == null)
            return;
        if (editorGhostOnlyWhenSelected && !IsThisGameObjectInEditorSelection())
            return;

        GameObject template = ghostPreviewPrefab != null ? ghostPreviewPrefab : builtPrefab;
        Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;
        Quaternion rot = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

        if (!EditorGhostCacheMatches(template, pos, rot))
        {
            _editorGhostCacheValid = TryComputeWorldBoundsByInstantiatingTemplate(
                template, pos, rot, out _editorGhostCachedBounds);
            _editorGhostCachedTemplate = template;
            _editorGhostCachedPos = pos;
            _editorGhostCachedRot = rot;
        }

        Bounds drawBounds = _editorGhostCacheValid
            ? _editorGhostCachedBounds
            : new Bounds(pos, editorGhostFallbackSize);

        Gizmos.color = editorGhostFillColor;
        Gizmos.DrawCube(drawBounds.center, drawBounds.size);
        Gizmos.color = editorGhostWireColor;
        Gizmos.DrawWireCube(drawBounds.center, drawBounds.size);

        UnityEditor.Handles.color = editorGhostWireColor;
        UnityEditor.Handles.Label(
            pos + Vector3.up * 0.2f,
            string.IsNullOrEmpty(buildingName) ? "BuildSite ghost (= runtime)" : $"BuildSite: {buildingName} (= runtime)");
    }

    bool EditorGhostCacheMatches(GameObject template, Vector3 pos, Quaternion rot)
    {
        if (!_editorGhostCacheValid || _editorGhostCachedTemplate != template)
            return false;
        if ((_editorGhostCachedPos - pos).sqrMagnitude > 1e-10f)
            return false;
        return Quaternion.Angle(_editorGhostCachedRot, rot) < 0.01f;
    }

    bool IsThisGameObjectInEditorSelection()
    {
        UnityEngine.Object[] objs = UnityEditor.Selection.objects;
        for (int i = 0; i < objs.Length; i++)
        {
            if (objs[i] == gameObject)
                return true;
        }

        return false;
    }

    /// <summary>与运行时 <see cref="Instantiate"/> 相同摆放，再合并 Bounds，保证 Scene 虚影对齐。</summary>
    static bool TryComputeWorldBoundsByInstantiatingTemplate(
        GameObject prefab,
        Vector3 worldPos,
        Quaternion worldRot,
        out Bounds worldBounds)
    {
        worldBounds = default;
        if (prefab == null)
            return false;

        GameObject instance = null;
        try
        {
            instance = UnityEditor.PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
                return false;

            instance.hideFlags = HideFlags.HideAndDontSave;
            instance.transform.SetPositionAndRotation(worldPos, worldRot);

            worldBounds = GetTotalWorldBoundsForEditorInstance(instance);
            return worldBounds.size.sqrMagnitude > 1e-8f;
        }
        finally
        {
            if (instance != null)
                UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    static Bounds GetTotalWorldBoundsForEditorInstance(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);
            return b;
        }

        var cols = root.GetComponentsInChildren<Collider2D>(true);
        if (cols.Length > 0)
        {
            Bounds b = cols[0].bounds;
            for (int i = 1; i < cols.Length; i++)
                b.Encapsulate(cols[i].bounds);
            return b;
        }

        return new Bounds(root.transform.position, Vector3.one * 0.25f);
    }
#endif
}
