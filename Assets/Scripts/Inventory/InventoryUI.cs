using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 背包面板：格子、重量、排序/关闭；拖拽见 <see cref="InventorySlotUI"/>。
///
/// <b>双面板箱子方案（推荐）：</b>复制一份玩家用的 Inventory Panel，「玩家面板」的 <see cref="playerInventory"/> + <see cref="inventory"/> 都拖玩家身上的
/// <see cref="Inventory"/>；「箱子面板」可拖场景里某箱子做测试，或<strong>留空</strong>并由 <see cref="BindStorageView"/> 在打开时绑定当前箱子（配合 <see cref="InventoryUiRegistry"/> + 建造的箱子）。
/// <see cref="ChestInteractionZone"/> 引用两个面板的 <see cref="InventoryUI"/>（或通过 Registry 自动填写），按交互键会同时打开/关闭两个面板，物品可在两面板格子间拖拽搬运。
///
/// <b>单面板切换（旧）：</b>仍可用 <see cref="OpenExternalStorage"/> 等在一只面板上切换视图。
/// </summary>
public class InventoryUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("玩家背包（用于 I 键、商人、关闭面板后恢复）。")]
    public Inventory playerInventory;

    [Tooltip("当前正在显示的仓库；运行时会在玩家与箱子之间切换，Inspector 里请与 playerInventory 填成同一个以兼容旧场景。")]
    public Inventory inventory;

    [Tooltip("Prefab for one slot (InventorySlotUI on root).")]
    public GameObject slotUIPrefab;

    [Tooltip("Parent for slot instances (e.g. SlotGrid with Grid Layout Group).")]
    public Transform slotGridParent;

    [Tooltip("Displays current / max weight.")]
    public TextMeshProUGUI weightText;

    [Header("Gold (optional)")]
    [Tooltip("若赋值：goldText 显示玩家背包里该道具的数量（与商人/建造的「钱」一致）；与 wallet 二选一即可。")]
    [SerializeField] ItemData currencyItem;

    [Tooltip("Player wallet; leave empty to search on the same object / parents as player inventory.")]
    public PlayerWallet wallet;

    [Tooltip("TMP for coin count (e.g. next to an icon).")]
    public TextMeshProUGUI goldText;

    [Header("外部储存 UI（可选）")]
    [Tooltip("面板标题：随当前视图切换文案。")]
    public TextMeshProUGUI panelTitleText;

    [SerializeField] string playerInventoryTitle = "Inventory";
    [SerializeField] string storageTitle = "Chest";

    [Tooltip("仅在打开某一外部储存时显示：切回玩家背包视图。")]
    public Button showPlayerInventoryButton;

    [Tooltip("仅在打开某一外部储存时显示：切到该储存视图。")]
    public Button showStorageButton;

    [Tooltip("查看箱子时：把选中格子整堆能拿多少拿多少到玩家背包。")]
    public Button withdrawSelectionButton;

    [Tooltip("查看玩家背包且当前有外部储存会话时：把选中格子物品尽量存入外部储存。")]
    public Button depositSelectionButton;

    [Header("Buttons")]
    [Tooltip("Invokes Inventory.Sort.")]
    public Button sortButton;

    [Tooltip("Closes the inventory panel (calls Close). 在场景里把按钮拖到这里即可。")]
    public Button closeButton;

    Inventory _externalStorage;

    private List<InventorySlotUI> _slotUIs = new List<InventorySlotUI>();
    bool _inputBlockRequested;

    private int _selectedSlotIndex = -1;

    /// <summary>当前显示的 <see cref="Inventory"/>（玩家或箱子）。</summary>
    public Inventory CurrentViewInventory => inventory;

    /// <summary>当前交互的外部储存；无则为 null。</summary>
    public Inventory ActiveExternalStorage => _externalStorage;

    /// <summary>是否正在显示某外部储存视图。</summary>
    public bool IsViewingExternalStorage => _externalStorage != null && inventory == _externalStorage;

    public int SelectedSlotIndex => _selectedSlotIndex;

    private void Start()
    {
        if (playerInventory == null)
            playerInventory = inventory;
        if (inventory == null)
            inventory = playerInventory;

        if (currencyItem == null)
        {
            if (wallet == null && playerInventory != null)
                wallet = playerInventory.GetComponent<PlayerWallet>() ?? playerInventory.GetComponentInParent<PlayerWallet>();

            if (wallet != null)
                wallet.OnGoldChanged.AddListener(OnWalletGoldChanged);
        }
        else if (playerInventory != null)
            playerInventory.OnInventoryChanged.AddListener(OnPlayerBagCurrencyChanged);

        if (sortButton != null)
            sortButton.onClick.AddListener(OnSortClicked);

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        if (showPlayerInventoryButton != null)
            showPlayerInventoryButton.onClick.AddListener(ShowPlayerBag);
        if (showStorageButton != null)
            showStorageButton.onClick.AddListener(ShowExternalStorageView);
        if (withdrawSelectionButton != null)
            withdrawSelectionButton.onClick.AddListener(TryWithdrawSelectedToPlayer);
        if (depositSelectionButton != null)
            depositSelectionButton.onClick.AddListener(TryDepositSelectedToStorage);

        RefreshGoldDisplay();

        if (inventory != null)
        {
            inventory.OnInventoryChanged.AddListener(RefreshAll);
            inventory.StartCoroutine(BootstrapSlotUIsWhenInventoryReady());
        }

        UpdateStorageChrome();
        gameObject.SetActive(false);
    }

    IEnumerator BootstrapSlotUIsWhenInventoryReady()
    {
        int guard = 0;
        const int maxFrames = 120;
        while (inventory != null && inventory.Slots.Count < inventory.capacity && guard < maxFrames)
        {
            guard++;
            yield return null;
        }

        EnsureSlotWidgetsMatchInventory();
        RefreshAll();
    }

    private void OnDestroy()
    {
        if (sortButton != null)
            sortButton.onClick.RemoveListener(OnSortClicked);
        if (closeButton != null)
            closeButton.onClick.RemoveListener(Close);
        if (showPlayerInventoryButton != null)
            showPlayerInventoryButton.onClick.RemoveListener(ShowPlayerBag);
        if (showStorageButton != null)
            showStorageButton.onClick.RemoveListener(ShowExternalStorageView);
        if (withdrawSelectionButton != null)
            withdrawSelectionButton.onClick.RemoveListener(TryWithdrawSelectedToPlayer);
        if (depositSelectionButton != null)
            depositSelectionButton.onClick.RemoveListener(TryDepositSelectedToStorage);

        if (currencyItem == null && wallet != null)
            wallet.OnGoldChanged.RemoveListener(OnWalletGoldChanged);
        if (currencyItem != null && playerInventory != null)
            playerInventory.OnInventoryChanged.RemoveListener(OnPlayerBagCurrencyChanged);
        if (inventory != null)
            inventory.OnInventoryChanged.RemoveListener(RefreshAll);
        ReleaseInputBlockIfNeeded();
    }

    private void OnDisable()
    {
        ReleaseInputBlockIfNeeded();
    }

    /// <summary>
    /// 唯一「箱子 UI」在打开任意世界箱子前调用：绑定该箱子的 <see cref="Inventory"/> 并刷新格子（建造生成、多箱子共用一面板时必用）。
    /// </summary>
    public void BindStorageView(Inventory storage)
    {
        if (storage == null) return;

        if (inventory != null)
            inventory.OnInventoryChanged.RemoveListener(RefreshAll);

        _externalStorage = null;
        playerInventory = storage;
        inventory = storage;

        if (inventory != null)
            inventory.OnInventoryChanged.AddListener(RefreshAll);

        ClearSelection();
        EnsureSlotWidgetsMatchInventory();
        UpdateStorageChrome();
        RefreshAll();
    }

    /// <summary>打开玩家背包视图并显示面板（I 键、商人、训练师等请用这个）。</summary>
    public void OpenPlayerBag()
    {
        SwitchViewTo(playerInventory);
        Open();
    }

    /// <summary>绑定某一外部 <see cref="Inventory"/>（箱子）并显示；若面板已开则只切换视图。</summary>
    public void OpenExternalStorage(Inventory storage)
    {
        if (storage == null)
        {
            Debug.LogWarning("[InventoryUI] OpenExternalStorage: storage is null.", this);
            return;
        }

        _externalStorage = storage;
        SwitchViewTo(storage);
        if (!gameObject.activeSelf)
            Open();
        else
            RefreshAll();
    }

    /// <summary>在有外部储存会话时切换到玩家背包格子（不关闭面板）。</summary>
    public void ShowPlayerBag()
    {
        SwitchViewTo(playerInventory);
    }

    /// <summary>在有外部储存会话时切换到储存格子。</summary>
    public void ShowExternalStorageView()
    {
        if (_externalStorage == null) return;
        SwitchViewTo(_externalStorage);
    }

    void SwitchViewTo(Inventory newTarget)
    {
        if (newTarget == null) return;

        if (inventory == newTarget)
        {
            UpdateStorageChrome();
            RefreshAll();
            return;
        }

        if (inventory != null)
            inventory.OnInventoryChanged.RemoveListener(RefreshAll);

        inventory = newTarget;
        ClearSelection();

        if (inventory != null)
            inventory.OnInventoryChanged.AddListener(RefreshAll);

        EnsureSlotWidgetsMatchInventory();
        UpdateStorageChrome();
        RefreshAll();
    }

    void UpdateStorageChrome()
    {
        if (panelTitleText != null)
        {
            bool storage = IsViewingExternalStorage;
            panelTitleText.text = storage ? storageTitle : playerInventoryTitle;
        }

        bool session = _externalStorage != null;
        if (showPlayerInventoryButton != null)
        {
            showPlayerInventoryButton.gameObject.SetActive(session);
            showPlayerInventoryButton.interactable = session && inventory != playerInventory;
        }

        if (showStorageButton != null)
        {
            showStorageButton.gameObject.SetActive(session);
            showStorageButton.interactable = session && inventory != _externalStorage;
        }

        if (withdrawSelectionButton != null)
        {
            withdrawSelectionButton.gameObject.SetActive(session);
            withdrawSelectionButton.interactable = session && IsViewingExternalStorage;
        }

        if (depositSelectionButton != null)
        {
            depositSelectionButton.gameObject.SetActive(session);
            depositSelectionButton.interactable = session && playerInventory != null && inventory == playerInventory;
        }
    }

    /// <summary>从当前查看的储存中，将选中格物品尽量转移到玩家背包。</summary>
    public void TryWithdrawSelectedToPlayer()
    {
        if (playerInventory == null || inventory == null || inventory == playerInventory) return;
        int idx = SelectedSlotIndex;
        if (idx < 0) return;
        var slot = inventory.Slots[idx];
        if (slot.IsEmpty) return;

        int added = playerInventory.AddItem(slot.item, slot.count);
        if (added > 0)
        {
            inventory.RemoveItem(idx, added);
            ClearSelection();
        }
    }

    /// <summary>在查看玩家背包且存在外部储存会话时，将选中格物品尽量存入储存。</summary>
    public void TryDepositSelectedToStorage()
    {
        if (playerInventory == null || _externalStorage == null || inventory != playerInventory) return;
        int idx = SelectedSlotIndex;
        if (idx < 0) return;
        var slot = playerInventory.Slots[idx];
        if (slot.IsEmpty) return;

        int added = _externalStorage.AddItem(slot.item, slot.count);
        if (added > 0)
            playerInventory.RemoveItem(idx, added);

        ClearSelection();
        RefreshAll();
    }

    public bool IsViewingInventory(Inventory inv) => gameObject.activeSelf && inv != null && inventory == inv;

    /// <summary>与 <see cref="Inventory.Slots"/> 数量对齐：多删少建。</summary>
    void EnsureSlotWidgetsMatchInventory()
    {
        if (inventory == null || slotUIPrefab == null || slotGridParent == null) return;

        while (_slotUIs.Count > inventory.Slots.Count)
        {
            int last = _slotUIs.Count - 1;
            var ui = _slotUIs[last];
            _slotUIs.RemoveAt(last);
            if (ui != null)
                Destroy(ui.gameObject);
        }

        while (_slotUIs.Count < inventory.Slots.Count)
        {
            int idx = _slotUIs.Count;
            var go = Instantiate(slotUIPrefab, slotGridParent);
            var slotUI = go.GetComponent<InventorySlotUI>();
            if (slotUI == null) break;
            slotUI.Bind(inventory, idx, this);
            _slotUIs.Add(slotUI);
        }
    }

    /// <summary>进范围、建成、升级后刷新 UI。外部（如 StatTrainer 扣费）也可调用。</summary>
    public void RefreshAll()
    {
        EnsureSlotWidgetsMatchInventory();

        if (inventory != null)
        {
            if (_selectedSlotIndex >= inventory.Slots.Count)
                _selectedSlotIndex = -1;
            else if (_selectedSlotIndex >= 0 && inventory.Slots[_selectedSlotIndex].IsEmpty)
                _selectedSlotIndex = -1;
        }

        foreach (var slotUI in _slotUIs)
            slotUI.Refresh();

        ApplySelectionVisuals();

        if (weightText != null && inventory != null)
        {
            weightText.text = $"Weight: {inventory.CurrentWeight:F1}/{inventory.maxWeight}";
            if (inventory.IsOverweight)
                weightText.color = Color.red;
            else
                weightText.color = Color.white;
        }

        RefreshGoldDisplay();
        UpdateStorageChrome();
    }

    void OnWalletGoldChanged(int _) => RefreshGoldDisplay();

    void OnPlayerBagCurrencyChanged() => RefreshGoldDisplay();

    void RefreshGoldDisplay()
    {
        if (goldText == null)
            return;
        if (currencyItem != null && currencyItem.IsValid && playerInventory != null)
        {
            goldText.text = playerInventory.GetItemCount(currencyItem).ToString();
            return;
        }
        if (wallet == null)
        {
            goldText.text = string.Empty;
            return;
        }

        goldText.text = wallet.Gold.ToString();
    }

    private void OnSortClicked()
    {
        inventory?.Sort();
    }

    public void SelectSlot(int index)
    {
        if (inventory == null) return;
        if (index < 0 || index >= inventory.Slots.Count) return;
        _selectedSlotIndex = index;
        ApplySelectionVisuals();
    }

    public void ClearSelection()
    {
        _selectedSlotIndex = -1;
        ApplySelectionVisuals();
    }

    /// <summary>拖拽等操作后清除所有 <see cref="InventoryUI"/> 的选中状态（含未激活对象）。</summary>
    public static void ClearSelectionEverywhere()
    {
        foreach (var ui in Object.FindObjectsByType<InventoryUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (ui != null)
                ui.ClearSelection();
        }
    }

    void ApplySelectionVisuals()
    {
        for (int i = 0; i < _slotUIs.Count; i++)
            _slotUIs[i].SetSelected(i == _selectedSlotIndex);
    }

    /// <summary>保持当前视图，仅显示面板（一般请用 <see cref="OpenPlayerBag"/>）。</summary>
    public void Open()
    {
        gameObject.SetActive(true);
        RequestInputBlockIfNeeded();
        RefreshAll();
    }

    /// <summary>关闭面板并结束外部储存会话，监听始终回到玩家背包。</summary>
    public void Close()
    {
        if (inventory != null)
            inventory.OnInventoryChanged.RemoveListener(RefreshAll);

        _externalStorage = null;

        if (playerInventory != null)
        {
            inventory = playerInventory;
            inventory.OnInventoryChanged.AddListener(RefreshAll);
        }

        ClearSelection();
        EnsureSlotWidgetsMatchInventory();
        UpdateStorageChrome();

        ReleaseInputBlockIfNeeded();
        gameObject.SetActive(false);
    }

    public void Toggle()
    {
        if (gameObject.activeSelf)
            Close();
        else
            OpenPlayerBag();
    }

    void RequestInputBlockIfNeeded()
    {
        if (_inputBlockRequested) return;
        PlayerInputBlocker.Request(this);
        _inputBlockRequested = true;
    }

    void ReleaseInputBlockIfNeeded()
    {
        if (!_inputBlockRequested) return;
        PlayerInputBlocker.Release(this);
        _inputBlockRequested = false;
    }
}
