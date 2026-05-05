using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Player enters merchant trigger zone, presses interact action (e.g. E) to open both inventory and merchant UI.
/// Attach to merchant trigger object with Collider2D set to IsTrigger.
///
/// Day/night: when <see cref="onlyPresentDuringDay"/> is true, subscribes to <see cref="DayNightManager"/>;
/// merchant is shown only during day (hidden from dusk onward). Uses collider/renderer/animator/canvas toggles
/// so this script keeps running (do not deactivate the GameObject that hosts this component if it must receive events).
/// </summary>
public class MerchantInteractionZone : MonoBehaviour
{
    [Header("References")]
    public MerchantUI merchantUI;
    public InventoryUI inventoryUI;

    [Tooltip("Input action reference for interaction key, e.g. Player/Build (E).")]
    [SerializeField] InputActionReference interactAction;

    [Header("Filter")]
    [Tooltip("Only objects with this tag can interact.")]
    [SerializeField] string playerTag = "Player";

    [Header("Day / night")]
    [Tooltip("Hide merchant when dusk starts; show again on day start. Requires DayNightManager in the scene.")]
    [SerializeField] bool onlyPresentDuringDay = true;

    [Tooltip("Root whose colliders, renderers, animators and canvases are toggled. Defaults to this GameObject.")]
    [SerializeField] GameObject presenceRoot;

    bool _playerInRange;
    bool _dayNightHandlersRegistered;

    void Awake()
    {
        if (interactAction == null || interactAction.action == null)
            Debug.LogWarning("[MerchantInteractionZone] Assign interactAction (InputSystem action bound to E).", this);
    }

    void Start()
    {
        ApplyMerchantPresenceForCurrentPhase();
        RegisterDayNightPresenceHandlers();
    }

    void OnEnable()
    {
        if (interactAction != null && interactAction.action != null)
            interactAction.action.Enable();
        RegisterDayNightPresenceHandlers();
    }

    void OnDisable()
    {
        UnregisterDayNightPresenceHandlers();
        if (interactAction != null && interactAction.action != null)
            interactAction.action.Disable();
    }

    void OnDestroy()
    {
        UnregisterDayNightPresenceHandlers();
    }

    void RegisterDayNightPresenceHandlers()
    {
        if (!onlyPresentDuringDay || _dayNightHandlersRegistered) return;
        var d = DayNightManager.Instance;
        if (d == null) return;
        d.OnDayStart += OnMerchantDayShown;
        d.OnDuskStart += OnMerchantHiddenUntilDay;
        _dayNightHandlersRegistered = true;
    }

    void UnregisterDayNightPresenceHandlers()
    {
        if (!_dayNightHandlersRegistered) return;
        var d = DayNightManager.Instance;
        if (d != null)
        {
            d.OnDayStart -= OnMerchantDayShown;
            d.OnDuskStart -= OnMerchantHiddenUntilDay;
        }
        _dayNightHandlersRegistered = false;
    }

    void OnMerchantDayShown() => SetMerchantWorldPresence(true);
    void OnMerchantHiddenUntilDay() => SetMerchantWorldPresence(false);

    void ApplyMerchantPresenceForCurrentPhase()
    {
        if (!onlyPresentDuringDay)
        {
            SetMerchantWorldPresence(true);
            return;
        }

        var d = DayNightManager.Instance;
        if (d == null)
        {
            SetMerchantWorldPresence(true);
            return;
        }

        SetMerchantWorldPresence(d.IsDay);
    }

    void SetMerchantWorldPresence(bool visible)
    {
        var root = presenceRoot != null ? presenceRoot : gameObject;

        if (onlyPresentDuringDay && !visible)
        {
            _playerInRange = false;
            if (merchantUI != null && merchantUI.gameObject.activeSelf)
                merchantUI.Close();
        }

        if (!onlyPresentDuringDay)
            return;

        foreach (var c in root.GetComponentsInChildren<Collider2D>(true))
            c.enabled = visible;
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            r.enabled = visible;
        foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
            canvas.enabled = visible;
        foreach (var anim in root.GetComponentsInChildren<Animator>(true))
            anim.enabled = visible;
    }

    void Update()
    {
        if (!_playerInRange) return;
        if (interactAction == null || interactAction.action == null) return;
        if (!interactAction.action.WasPressedThisFrame()) return;

        if (onlyPresentDuringDay && DayNightManager.Instance != null && !DayNightManager.Instance.IsDay)
            return;

        if (inventoryUI != null)
            inventoryUI.OpenPlayerBag();
        if (merchantUI != null)
            merchantUI.Open();
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
