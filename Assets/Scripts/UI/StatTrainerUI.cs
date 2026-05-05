using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Stat trainer UI: spends one <see cref="ItemData"/> from the bag (e.g. gold). Four stats cap at 3 tiers; cost is 1, 2, then 3 of that item per tier.
/// Four TMPs show tier numbers only; <see cref="goldText"/> shows how many of that item the player holds.
/// <see cref="hpCostText"/> and the other cost TMPs show the next tier's item cost; at max tier shows <see cref="costTextWhenMaxed"/>.
/// </summary>
public class StatTrainerUI : MonoBehaviour
{
    const int MaxTiersPerStat = 3;

    static readonly int[] CostItemCountByTierIndex = { 1, 2, 3 };

    [Header("Targets (optional; finds Player if empty)")]
    [SerializeField] PlayerController player;
    [SerializeField] Inventory inventory;

    [Header("Currency (ItemData in bag, e.g. Gold.asset)")]
    [SerializeField] ItemData goldItem;

    [Header("Per-tier bonuses (tune as needed)")]
    [Min(1)] public int hpBonusPerLevel = 10;
    [Min(1)] public int inventorySlotsPerLevel = 1;
    public float moveSpeedBonusPerLevel = 0.15f;
    public float attackDamageBonusPerLevel = 0.25f;

    [Header("Tier numbers (TMP only; put labels in the scene)")]
    public TextMeshProUGUI hpLevelText;
    public TextMeshProUGUI inventorySlotLevelText;
    public TextMeshProUGUI speedLevelText;
    public TextMeshProUGUI attackDamageLevelText;

    [Header("Next upgrade cost in gold items (written by script)")]
    public TextMeshProUGUI hpCostText;
    public TextMeshProUGUI inventorySlotCostText;
    public TextMeshProUGUI speedCostText;
    public TextMeshProUGUI attackDamageCostText;

    [Tooltip("Shown on cost TMPs when tier 3 is reached (e.g. — or Max).")]
    [SerializeField] string costTextWhenMaxed = "—";

    [Header("Currency held (count of goldItem in bag)")]
    public TextMeshProUGUI goldText;

    [Header("Purchase buttons")]
    public Button buyHpButton;
    public Button buyInventorySlotButton;
    public Button buySpeedButton;
    public Button buyAttackDamageButton;

    [Header("Inventory UI (optional)")]
    [SerializeField] InventoryUI inventoryUI;
    [SerializeField] bool openInventoryWhenOpen = true;
    [SerializeField] bool closeInventoryWhenClose = true;

    [Header("UI")]
    [SerializeField] Button closeButton;

    [Header("Events")]
    [SerializeField] UnityEvent onPurchaseSucceeded;

    int _hpLevel;
    int _slotLevel;
    int _speedLevel;
    int _attackLevel;
    bool _inputBlocked;

    void Awake()
    {
        if (buyHpButton != null) buyHpButton.onClick.AddListener(TryBuyHp);
        if (buyInventorySlotButton != null) buyInventorySlotButton.onClick.AddListener(TryBuySlot);
        if (buySpeedButton != null) buySpeedButton.onClick.AddListener(TryBuySpeed);
        if (buyAttackDamageButton != null) buyAttackDamageButton.onClick.AddListener(TryBuyAttack);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        gameObject.SetActive(false);
    }

    void OnEnable()
    {
        ResolveTargets();
        if (inventory != null)
            inventory.OnInventoryChanged.AddListener(OnInventoryChanged);
        RefreshUi();
    }

    void OnDisable()
    {
        if (inventory != null)
            inventory.OnInventoryChanged.RemoveListener(OnInventoryChanged);
        ReleaseInputBlock();
    }

    void OnDestroy()
    {
        ReleaseInputBlock();
    }

    void OnInventoryChanged() => RefreshUi();

    void ResolveTargets()
    {
        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.GetComponent<PlayerController>();
        }

        if (inventory == null && player != null)
            inventory = player.GetComponent<Inventory>() ?? player.GetComponentInChildren<Inventory>();
    }

    public void Open()
    {
        ResolveTargets();
        gameObject.SetActive(true);
        RequestInputBlock();
        if (openInventoryWhenOpen && inventoryUI != null)
            inventoryUI.OpenPlayerBag();
        RefreshUi();
    }

    public void Close()
    {
        ReleaseInputBlock();
        gameObject.SetActive(false);
        if (closeInventoryWhenClose && inventoryUI != null)
            inventoryUI.Close();
    }

    void RequestInputBlock()
    {
        if (_inputBlocked) return;
        PlayerInputBlocker.Request(this);
        _inputBlocked = true;
    }

    void ReleaseInputBlock()
    {
        if (!_inputBlocked) return;
        PlayerInputBlocker.Release(this);
        _inputBlocked = false;
    }

    public void TryBuyHp() => TryPurchase(ref _hpLevel, ApplyHp);

    public void TryBuySlot() => TryPurchase(ref _slotLevel, ApplySlot);

    public void TryBuySpeed() => TryPurchase(ref _speedLevel, ApplySpeed);

    public void TryBuyAttack() => TryPurchase(ref _attackLevel, ApplyAttack);

    void TryPurchase(ref int level, Action apply)
    {
        ResolveTargets();
        if (player == null || inventory == null) return;
        if (goldItem == null || !goldItem.IsValid)
        {
            Debug.LogWarning("[StatTrainerUI] Assign goldItem (currency ItemData in bag).", this);
            return;
        }

        if (level >= MaxTiersPerStat) return;
        int cost = CostItemCountByTierIndex[level];
        if (!inventory.HasCount(goldItem, cost)) return;

        inventory.RemoveItem(goldItem, cost);
        apply.Invoke();
        level++;
        onPurchaseSucceeded?.Invoke();
        if (inventoryUI != null)
            inventoryUI.RefreshAll();
        RefreshUi();
    }

    void ApplyHp() => player.ApplyTrainerBonusMaxHp(hpBonusPerLevel);

    void ApplySlot() => inventory.ExpandCapacity(inventorySlotsPerLevel);

    void ApplySpeed() => player.ApplyTrainerBonusMoveSpeed(moveSpeedBonusPerLevel);

    void ApplyAttack() => player.ApplyTrainerBonusAttackDamageAllSteps(attackDamageBonusPerLevel);

    void RefreshUi()
    {
        ResolveTargets();

        SetLevelText(hpLevelText, _hpLevel);
        SetLevelText(inventorySlotLevelText, _slotLevel);
        SetLevelText(speedLevelText, _speedLevel);
        SetLevelText(attackDamageLevelText, _attackLevel);

        SetNextGoldCostText(hpCostText, _hpLevel);
        SetNextGoldCostText(inventorySlotCostText, _slotLevel);
        SetNextGoldCostText(speedCostText, _speedLevel);
        SetNextGoldCostText(attackDamageCostText, _attackLevel);

        if (goldText != null)
        {
            if (inventory != null && goldItem != null && goldItem.IsValid)
                goldText.text = inventory.GetItemCount(goldItem).ToString();
            else
                goldText.text = string.Empty;
        }

        if (buyHpButton != null)
            buyHpButton.interactable = CanBuy(_hpLevel);
        if (buyInventorySlotButton != null)
            buyInventorySlotButton.interactable = inventory != null && CanBuy(_slotLevel);
        if (buySpeedButton != null)
            buySpeedButton.interactable = CanBuy(_speedLevel);
        if (buyAttackDamageButton != null)
            buyAttackDamageButton.interactable = CanBuy(_attackLevel);
    }

    static void SetLevelText(TextMeshProUGUI tmp, int level)
    {
        if (tmp != null)
            tmp.text = level.ToString();
    }

    void SetNextGoldCostText(TextMeshProUGUI tmp, int currentLevel)
    {
        if (tmp == null) return;
        if (currentLevel >= MaxTiersPerStat)
        {
            tmp.text = costTextWhenMaxed;
            return;
        }

        int cost = CostItemCountByTierIndex[currentLevel];
        tmp.text = cost.ToString();
    }

    bool CanBuy(int currentLevel)
    {
        if (player == null || inventory == null) return false;
        if (goldItem == null || !goldItem.IsValid) return false;
        if (currentLevel >= MaxTiersPerStat) return false;
        int cost = CostItemCountByTierIndex[currentLevel];
        return inventory.HasCount(goldItem, cost);
    }
}
