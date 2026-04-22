using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Player gold. Attach to the player (or a persistent game manager).
/// Hook <see cref="OnGoldChanged"/> to TMP text for the HUD / shop.
/// </summary>
public class PlayerWallet : MonoBehaviour
{
    [SerializeField] int startingGold;

    public int Gold { get; private set; }

    [Tooltip("Invoked with new total after AddGold / TrySpend.")]
    public UnityEvent<int> OnGoldChanged = new UnityEvent<int>();

    void Awake()
    {
        Gold = Mathf.Max(0, startingGold);
    }

    void Start()
    {
        OnGoldChanged?.Invoke(Gold);
    }

    public void AddGold(int amount)
    {
        if (amount <= 0) return;
        Gold += amount;
        OnGoldChanged?.Invoke(Gold);
    }

    /// <returns>False if not enough gold; wallet unchanged.</returns>
    public bool TrySpend(int amount)
    {
        if (amount <= 0) return true;
        if (Gold < amount) return false;
        Gold -= amount;
        OnGoldChanged?.Invoke(Gold);
        return true;
    }
}
