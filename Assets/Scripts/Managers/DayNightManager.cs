using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Day/night cycle: day → dusk → night → day.
///
/// Setup:
/// 1. Add to a manager GameObject.
/// 2. Assign the scene Global Light 2D.
/// 3. Tune durations, colours, and intensities in the Inspector.
/// 4. Other systems subscribe via DayNightManager.Instance events.
/// </summary>
public class DayNightManager : MonoBehaviour
{
    public static DayNightManager Instance { get; private set; }

    public enum GamePhase { Day, Dusk, Night }

    [Header("Lighting")]
    [Tooltip("Scene Global Light 2D.")]
    public Light2D globalLight;

    [Header("Day")]
    [Tooltip("Day length in seconds.")]
    public float dayDuration = 60f;
    [Tooltip("Day light intensity.")]
    public float dayIntensity = 1f;
    [Tooltip("Day light colour.")]
    public Color dayColor = Color.white;

    [Header("Dusk")]
    [Tooltip("Dusk length in seconds (warning phase).")]
    public float duskDuration = 30f;
    [Tooltip("Dusk light intensity.")]
    public float duskIntensity = 0.75f;
    [Tooltip("Dusk light colour (e.g. sunset orange).")]
    public Color duskColor = new Color(1f, 0.45f, 0.1f);

    [Header("Night")]
    [Tooltip("Night length in seconds.")]
    public float nightDuration = 60f;
    [Tooltip("Night light intensity.")]
    public float nightIntensity = 0.25f;
    [Tooltip("Night light colour.")]
    public Color nightColor = new Color(0.2f, 0.2f, 0.5f);

    [Header("Blood moon")]
    [Tooltip("Blood moon every N days (default 7).")]
    public int bloodMoonInterval = 7;
    [Tooltip("Blood moon light colour.")]
    public Color bloodMoonColor = new Color(0.6f, 0.05f, 0.05f);

    [Header("Transitions")]
    [Tooltip("Seconds to lerp light when a phase starts.")]
    public float transitionDuration = 3f;

    /// <summary>Invoked at day start (resource refill, etc.).</summary>
    public event Action OnDayStart;

    /// <summary>Invoked at dusk (warning UI, SFX).</summary>
    public event Action OnDuskStart;

    /// <summary>Invoked at normal night start (enemy waves).</summary>
    public event Action OnNightStart;

    /// <summary>Invoked when a blood moon night begins.</summary>
    public event Action OnBloodMoonStart;

    /// <summary>Current phase.</summary>
    public GamePhase CurrentPhase { get; private set; } = GamePhase.Day;

    /// <summary>True during day (not dusk).</summary>
    public bool IsDay => CurrentPhase == GamePhase.Day;

    /// <summary>True during dusk.</summary>
    public bool IsDusk => CurrentPhase == GamePhase.Dusk;

    /// <summary>True during night.</summary>
    public bool IsNight => CurrentPhase == GamePhase.Night;

    /// <summary>True if this night is a blood moon.</summary>
    public bool IsBloodMoon { get; private set; }

    /// <summary>1-based day counter.</summary>
    public int DayCount { get; private set; } = 1;

    /// <summary>Seconds left in the current phase.</summary>
    public float TimeRemaining { get; private set; }

    private float _transitionTimer;
    private bool _isTransitioning;
    private Color _transitionFromColor;
    private float _transitionFromIntensity;
    private Color _transitionToColor;
    private float _transitionToIntensity;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        CurrentPhase = GamePhase.Day;
        TimeRemaining = dayDuration;
        ApplyLightImmediate(dayColor, dayIntensity);
    }

    private void Update()
    {
        TickTimer();
        TickTransition();
    }

    private void TickTimer()
    {
        TimeRemaining -= Time.deltaTime;
        if (TimeRemaining > 0f) return;

        switch (CurrentPhase)
        {
            case GamePhase.Day:   EnterDusk();  break;
            case GamePhase.Dusk:  EnterNight(); break;
            case GamePhase.Night: EnterDay();   break;
        }
    }

    private void EnterDay()
    {
        CurrentPhase = GamePhase.Day;
        IsBloodMoon = false;
        DayCount++;
        TimeRemaining = dayDuration;
        BeginTransition(dayColor, dayIntensity);
        OnDayStart?.Invoke();
        Debug.Log($"[DayNightManager] Day {DayCount} — day started.");
    }

    private void EnterDusk()
    {
        CurrentPhase = GamePhase.Dusk;
        TimeRemaining = duskDuration;
        BeginTransition(duskColor, duskIntensity);
        OnDuskStart?.Invoke();
        Debug.Log($"[DayNightManager] Day {DayCount} — dusk ({duskDuration}s).");
    }

    private void EnterNight()
    {
        CurrentPhase = GamePhase.Night;
        TimeRemaining = nightDuration;

        bool isBloodMoon = (DayCount % bloodMoonInterval == 0);
        IsBloodMoon = isBloodMoon;

        if (isBloodMoon)
        {
            BeginTransition(bloodMoonColor, nightIntensity);
            OnBloodMoonStart?.Invoke();
            Debug.Log($"[DayNightManager] Day {DayCount} — blood moon.");
        }
        else
        {
            BeginTransition(nightColor, nightIntensity);
            OnNightStart?.Invoke();
            Debug.Log($"[DayNightManager] Day {DayCount} — night started.");
        }
    }

    private void BeginTransition(Color toColor, float toIntensity)
    {
        if (globalLight == null) return;
        _transitionFromColor = globalLight.color;
        _transitionFromIntensity = globalLight.intensity;
        _transitionToColor = toColor;
        _transitionToIntensity = toIntensity;
        _transitionTimer = 0f;
        _isTransitioning = true;
    }

    private void TickTransition()
    {
        if (!_isTransitioning || globalLight == null) return;

        _transitionTimer += Time.deltaTime;
        float t = Mathf.Clamp01(_transitionTimer / transitionDuration);
        globalLight.color = Color.Lerp(_transitionFromColor, _transitionToColor, t);
        globalLight.intensity = Mathf.Lerp(_transitionFromIntensity, _transitionToIntensity, t);

        if (t >= 1f)
            _isTransitioning = false;
    }

    private void ApplyLightImmediate(Color color, float intensity)
    {
        if (globalLight == null) return;
        globalLight.color = color;
        globalLight.intensity = intensity;
    }

    /// <summary>
    /// Phase progress 0 = start, 1 = end. Useful for a day/night HUD bar.
    /// </summary>
    public float GetPhaseProgress()
    {
        float total = CurrentPhase switch
        {
            GamePhase.Day  => dayDuration,
            GamePhase.Dusk => duskDuration,
            _              => nightDuration,
        };
        return 1f - Mathf.Clamp01(TimeRemaining / total);
    }

    /// <summary>Debug: force the current phase to end.</summary>
    [ContextMenu("Debug: Skip current phase")]
    public void DebugSkipPhase()
    {
        TimeRemaining = 0f;
    }
}
