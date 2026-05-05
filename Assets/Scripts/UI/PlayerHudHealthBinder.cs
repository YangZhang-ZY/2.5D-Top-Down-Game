using UnityEngine;

/// <summary>
/// Binds the sibling <see cref="HealthBar"/> on this object to the player's <see cref="Health"/> (Tag &quot;Player&quot;).
/// Use for screen HUDs when the bar is not parented under the player.
/// Does not override <see cref="HealthBar.health"/> if already assigned in the Inspector.
/// </summary>
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(HealthBar))]
public class PlayerHudHealthBinder : MonoBehaviour
{
    [Tooltip("Optional player Health; if empty, finds by Tag Player at runtime.")]
    [SerializeField] Health playerHealthOverride;

    void Awake()
    {
        var bar = GetComponent<HealthBar>();
        if (bar == null) return;
        if (bar.health != null) return;

        if (playerHealthOverride != null)
            bar.health = playerHealthOverride;
        else
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
                bar.health = p.GetComponent<Health>();
        }

        if (bar.health == null)
            Debug.LogWarning("[PlayerHudHealthBinder] Player Health not found: assign Player tag or playerHealthOverride.", this);
    }
}
