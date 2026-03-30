using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 昼夜循环管理器。
/// 流程：白天 → 黄昏（警告阶段） → 夜晚 → 白天 → …
/// 
/// 使用步骤：
/// 1. 挂到场景中的 GameManager 空物体上
/// 2. 将场景中的 Global Light 2D 拖到 globalLight 槽位
/// 3. 在 Inspector 中配置各阶段时长、颜色和亮度
/// 4. 其他脚本通过 DayNightManager.Instance.OnXxxStart 事件订阅
/// </summary>
public class DayNightManager : MonoBehaviour
{
    public static DayNightManager Instance { get; private set; }

    // ==================== 阶段枚举 ====================

    public enum GamePhase { Day, Dusk, Night }

    // ==================== Inspector 设置 ====================

    [Header("光照引用")]
    [Tooltip("场景中的 Global Light 2D")]
    public Light2D globalLight;

    [Header("白天设置")]
    [Tooltip("白天持续时间（秒）")]
    public float dayDuration = 60f;
    [Tooltip("白天光照强度")]
    public float dayIntensity = 1f;
    [Tooltip("白天光照颜色")]
    public Color dayColor = Color.white;

    [Header("黄昏设置")]
    [Tooltip("黄昏持续时间（秒），起警示作用")]
    public float duskDuration = 30f;
    [Tooltip("黄昏光照强度")]
    public float duskIntensity = 0.75f;
    [Tooltip("黄昏光照颜色（橙红夕阳色）")]
    public Color duskColor = new Color(1f, 0.45f, 0.1f);

    [Header("夜晚设置")]
    [Tooltip("夜晚持续时间（秒）")]
    public float nightDuration = 60f;
    [Tooltip("夜晚光照强度")]
    public float nightIntensity = 0.25f;
    [Tooltip("夜晚光照颜色")]
    public Color nightColor = new Color(0.2f, 0.2f, 0.5f);

    [Header("血月设置")]
    [Tooltip("每隔多少天触发一次血月（默认 7）")]
    public int bloodMoonInterval = 7;
    [Tooltip("血月光照颜色")]
    public Color bloodMoonColor = new Color(0.6f, 0.05f, 0.05f);

    [Header("过渡设置")]
    [Tooltip("阶段切换时光照过渡时长（秒）")]
    public float transitionDuration = 3f;

    // ==================== 事件 ====================

    /// <summary>白天开始（资源刷新等可订阅）</summary>
    public event Action OnDayStart;

    /// <summary>黄昏开始（警告 UI、音效等可订阅）</summary>
    public event Action OnDuskStart;

    /// <summary>夜晚开始（刷怪器可订阅）</summary>
    public event Action OnNightStart;

    /// <summary>血月开始（特殊刷怪逻辑可订阅）</summary>
    public event Action OnBloodMoonStart;

    // ==================== 只读状态属性 ====================

    /// <summary>当前阶段</summary>
    public GamePhase CurrentPhase { get; private set; } = GamePhase.Day;

    /// <summary>当前是否白天（不含黄昏）</summary>
    public bool IsDay => CurrentPhase == GamePhase.Day;

    /// <summary>当前是否黄昏</summary>
    public bool IsDusk => CurrentPhase == GamePhase.Dusk;

    /// <summary>当前是否夜晚</summary>
    public bool IsNight => CurrentPhase == GamePhase.Night;

    /// <summary>当前是否血月夜</summary>
    public bool IsBloodMoon { get; private set; }

    /// <summary>当前天数（从第 1 天开始）</summary>
    public int DayCount { get; private set; } = 1;

    /// <summary>当前阶段剩余时间（秒）</summary>
    public float TimeRemaining { get; private set; }

    // ==================== 私有变量 ====================

    private float _transitionTimer;
    private bool _isTransitioning;
    private Color _transitionFromColor;
    private float _transitionFromIntensity;
    private Color _transitionToColor;
    private float _transitionToIntensity;

    // ==================== Unity 生命周期 ====================

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

    // ==================== 阶段切换逻辑 ====================

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
        Debug.Log($"[DayNightManager] 第 {DayCount} 天 — 白天开始");
    }

    private void EnterDusk()
    {
        CurrentPhase = GamePhase.Dusk;
        TimeRemaining = duskDuration;
        BeginTransition(duskColor, duskIntensity);
        OnDuskStart?.Invoke();
        Debug.Log($"[DayNightManager] 第 {DayCount} 天 — 黄昏，快回家！（剩 {duskDuration}s）");
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
            Debug.Log($"[DayNightManager] 第 {DayCount} 天 — 血月之夜！");
        }
        else
        {
            BeginTransition(nightColor, nightIntensity);
            OnNightStart?.Invoke();
            Debug.Log($"[DayNightManager] 第 {DayCount} 天 — 夜晚开始");
        }
    }

    // ==================== 光照过渡 ====================

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

    // ==================== 工具方法 ====================

    /// <summary>
    /// 返回当前阶段进度（0 = 刚开始，1 = 快结束）。
    /// 可用于 HUD 昼夜进度条。
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

    /// <summary>强制跳过当前阶段（测试用）</summary>
    [ContextMenu("Debug: 跳过当前阶段")]
    public void DebugSkipPhase()
    {
        TimeRemaining = 0f;
    }
}
