using UnityEngine;
using TMPro;

/// <summary>
/// 游戏胜利：可调暂停、显示 Victory UI。
/// 在 Boss 的 <see cref="Health.OnDeath"/> 上绑定 <see cref="TriggerVictory"/>，或与 <see cref="BossController.triggerVictoryOnDeath"/> 自动联动。
/// </summary>
public class VictoryManager : MonoBehaviour
{
    public static VictoryManager Instance { get; private set; }

    [Header("Pause")]
    [SerializeField] bool pauseTimeScale = true;

    [Header("UI")]
    [SerializeField] GameObject victoryPanel;

    [SerializeField] string victoryMessage = "Victory";

    [SerializeField] TextMeshProUGUI victoryTextTMP;

    bool _triggered;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (victoryPanel != null)
            victoryPanel.SetActive(false);
    }

    /// <summary>供 Inspector：Boss Health.OnDeath（无参）。</summary>
    public void TriggerVictory()
    {
        if (_triggered) return;
        _triggered = true;

        if (pauseTimeScale)
            Time.timeScale = 0f;

        if (victoryPanel != null)
            victoryPanel.SetActive(true);

        if (victoryTextTMP != null)
            victoryTextTMP.text = victoryMessage;
    }

    public static void ResetTimeScale()
    {
        Time.timeScale = 1f;
    }
}
