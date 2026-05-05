using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 属性训练师 UI：消耗背包里的一种 <see cref="ItemData"/>（例如金币道具 Gold），四项各最多升 3 级，每次费用依次为 1、2、3 个该物品。
/// 四个 TMP 仅显示当前等级数字；<see cref="goldText"/> 显示背包中该物品持有数量。
/// <see cref="hpCostText"/> 等四个 TMP 由脚本写入「下次升级」所需 Gold 数量；已满级时显示 <see cref="costTextWhenMaxed"/>。
/// </summary>
public class StatTrainerUI : MonoBehaviour
{
    const int MaxTiersPerStat = 3;

    static readonly int[] CostItemCountByTierIndex = { 1, 2, 3 };

    [Header("目标（可空则自动找 Player）")]
    [SerializeField] PlayerController player;
    [SerializeField] Inventory inventory;

    [Header("货币（背包中的 ItemData，例如 Gold.asset）")]
    [SerializeField] ItemData goldItem;

    [Header("每项每级效果（可自行调）")]
    [Min(1)] public int hpBonusPerLevel = 10;
    [Min(1)] public int inventorySlotsPerLevel = 1;
    public float moveSpeedBonusPerLevel = 0.15f;
    public float attackDamageBonusPerLevel = 0.25f;

    [Header("等级数字（只填 TMP，说明文字自己做在场景里）")]
    public TextMeshProUGUI hpLevelText;
    public TextMeshProUGUI inventorySlotLevelText;
    public TextMeshProUGUI speedLevelText;
    public TextMeshProUGUI attackDamageLevelText;

    [Header("下次升级所需 Gold 数量（脚本写入，与费用表一致）")]
    public TextMeshProUGUI hpCostText;
    public TextMeshProUGUI inventorySlotCostText;
    public TextMeshProUGUI speedCostText;
    public TextMeshProUGUI attackDamageCostText;

    [Tooltip("已满 3 级时，费用 TMP 上显示的内容（例如 — 或 已满）")]
    [SerializeField] string costTextWhenMaxed = "—";

    [Header("持有货币数量（显示背包里 goldItem 的数量）")]
    public TextMeshProUGUI goldText;

    [Header("购买按钮")]
    public Button buyHpButton;
    public Button buyInventorySlotButton;
    public Button buySpeedButton;
    public Button buyAttackDamageButton;

    [Header("背包 UI（可选）")]
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
            Debug.LogWarning("[StatTrainerUI] 请指定 goldItem（背包货币 ItemData）。", this);
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
