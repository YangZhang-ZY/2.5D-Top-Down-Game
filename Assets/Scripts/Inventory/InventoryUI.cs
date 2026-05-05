using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Inventory panel: slot grid, weight, sort/close; drag-and-drop in <see cref="InventorySlotUI"/>.
///
/// <b>Dual-panel chest (recommended):</b> duplicate the player's Inventory Panel. On the player panel, set
/// <see cref="playerInventory"/> and <see cref="inventory"/> to the player's <see cref="Inventory"/>.
/// On the chest panel, assign a test chest in the scene or leave references empty and use <see cref="BindStorageView"/> when opening
/// (with <see cref="InventoryUiRegistry"/> and built chests). <see cref="ChestInteractionZone"/> references both <see cref="InventoryUI"/>s;
/// interact opens/closes both; items can be dragged between panels.
///
/// <b>Single-panel (legacy):</b> use <see cref="OpenExternalStorage"/> etc. to switch views on one UI.
/// </summary>
public class InventoryUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Player bag (I key, merchant, restore after close).")]
    public Inventory playerInventory;

    [Tooltip("Currently displayed inventory; switches between player and chest at runtime. Match playerInventory in Inspector for old scenes.")]
    public Inventory inventory;

    [Tooltip("Prefab for one slot (InventorySlotUI on root).")]
    public GameObject slotUIPrefab;

    [Tooltip("Parent for slot instances (e.g. SlotGrid with Grid Layout Group).")]
    public Transform slotGridParent;

    [Tooltip("Displays current / max weight.")]
    public TextMeshProUGUI weightText;

    [Header("Gold (optional)")]
    [Tooltip("If set, goldText shows this item's count in the player's bag (same currency as merchant/build). Use either this or wallet.")]
    [SerializeField] ItemData currencyItem;

    [Tooltip("Player wallet; leave empty to search on the same object / parents as player inventory.")]
    public PlayerWallet wallet;

    [Tooltip("TMP for coin count (e.g. next to an icon).")]
    public TextMeshProUGUI goldText;

    [Header("External storage UI (optional)")]
    [Tooltip("Panel title text; switches with the active view.")]
    public TextMeshProUGUI panelTitleText;

    [SerializeField] string playerInventoryTitle = "Inventory";
    [SerializeField] string storageTitle = "Chest";

    [Tooltip("Shown only with external storage open: switch to player bag view.")]
    public Button showPlayerInventoryButton;

    [Tooltip("Shown only with external storage open: switch to chest view.")]
    public Button showStorageButton;

    [Tooltip("While viewing chest: move selected stack to player bag as much as possible.")]
    public Button withdrawSelectionButton;

    [Tooltip("While viewing player bag with an external session: deposit selected stack into storage.")]
    public Button depositSelectionButton;

    [Header("Buttons")]
    [Tooltip("Invokes Inventory.Sort.")]
    public Button sortButton;

    [Tooltip("Closes the inventory panel (calls Close). Wire the scene button here.")]
    public Button closeButton;

    Inventory _externalStorage;

    private List<InventorySlotUI> _slotUIs = new List<InventorySlotUI>();
    bool _inputBlockRequested;

    private int _selectedSlotIndex = -1;

    /// <summary>Inventory currently shown (player or chest).</summary>
    public Inventory CurrentViewInventory => inventory;

    /// <summary>Active external storage session, or null.</summary>
    public Inventory ActiveExternalStorage => _externalStorage;

    /// <summary>True when viewing external storage inventory.</summary>
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
    /// Single chest UI: call before opening any world chest to bind that <see cref="Inventory"/> (built chests, one panel for many chests).
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

    /// <summary>Opens player bag view and shows the panel (I key, merchant, trainer, etc.).</summary>
    public void OpenPlayerBag()
    {
        SwitchViewTo(playerInventory);
        Open();
    }

    /// <summary>Binds an external <see cref="Inventory"/> (chest) and shows it; if already open, only switches view.</summary>
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

    /// <summary>With an external session active, switch to player bag without closing.</summary>
    public void ShowPlayerBag()
    {
        SwitchViewTo(playerInventory);
    }

    /// <summary>With an external session active, switch to storage view.</summary>
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

    /// <summary>Move as much of the selected stack as possible from the viewed storage into the player bag.</summary>
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

    /// <summary>While viewing player bag with storage session: deposit selected stack into storage.</summary>
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

    /// <summary>Match slot UI count to <see cref="Inventory.Slots"/> — add or remove widgets.</summary>
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

    /// <summary>Refresh after range events, builds, upgrades. External callers (e.g. StatTrainer spend) may call this.</summary>
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

    /// <summary>Clear selection on every <see cref="InventoryUI"/> (including inactive).</summary>
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

    /// <summary>Show panel with current view (prefer <see cref="OpenPlayerBag"/>).</summary>
    public void Open()
    {
        gameObject.SetActive(true);
        RequestInputBlockIfNeeded();
        RefreshAll();
    }

    /// <summary>Close panel and end external storage; listeners return to player bag.</summary>
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
