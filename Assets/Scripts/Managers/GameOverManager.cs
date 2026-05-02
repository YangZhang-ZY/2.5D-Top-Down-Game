using UnityEngine;
using TMPro;

/// <summary>
/// Game over flow: optional time freeze and UI display.
/// Bind <see cref="TriggerGameOver"/> to <see cref="Health.OnDeath"/> on the crystal (no parameters).
/// </summary>
public class GameOverManager : MonoBehaviour
{
    public static GameOverManager Instance { get; private set; }

    [Header("Pause")]
    [Tooltip("When true, sets Time.timeScale = 0 (UI still updates).")]
    [SerializeField] private bool pauseTimeScale = true;

    [Header("UI")]
    [Tooltip("Hidden by default; enabled when game over triggers.")]
    [SerializeField] private GameObject gameOverPanel;

    [SerializeField] private string gameOverMessage = "Game Over";

    [SerializeField] private TextMeshProUGUI gameOverTextTMP;

    bool _triggered;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }

    /// <summary>Inspector-friendly hook for Health.OnDeath (no parameters).</summary>
    public void TriggerGameOver()
    {
        if (_triggered) return;
        _triggered = true;

        if (pauseTimeScale)
            Time.timeScale = 0f;

        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);

        if (gameOverTextTMP != null)
            gameOverTextTMP.text = gameOverMessage;
    }

    /// <summary>Call before reloading the scene or returning to menu.</summary>
    public static void ResetTimeScale()
    {
        Time.timeScale = 1f;
    }
}

// Future: play game-over BGM via AudioSource with ignoreTimeScale = true so audio still plays at timeScale 0.
