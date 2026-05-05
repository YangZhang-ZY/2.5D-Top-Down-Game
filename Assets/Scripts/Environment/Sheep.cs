using UnityEngine;

/// <summary>
/// 在出生点周围 <see cref="roamRadius"/> 内随机选点移动；到达后停留 <see cref="roamIdleDuration"/> 再选下一点（可关循环）。
/// 适用于 2D XY + <see cref="Rigidbody2D"/>（与玩家一致）。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Sheep : MonoBehaviour
{
    [Header("Roam")]
    [Tooltip("活动范围：相对出生点的半径（米），目标点在圆盘内随机。")]
    [Min(0.1f)]
    public float roamRadius = 10f;

    [Tooltip("到达当前目标后停顿多久（秒）再去下一个随机点；仅在 Repeat Roam 开启时有效。")]
    [Min(0f)]
    public float roamIdleDuration = 2f;

    [Tooltip("为 true：停顿结束后继续在下一点漫游；为 false：第一次到达目标后永远停下。")]
    public bool repeatRoam = true;

    [Tooltip("移动速度（单位/秒）。")]
    [Min(0f)]
    public float moveSpeed = 2f;

    [Tooltip("距目标小于此距离时视为到达。")]
    [Min(0.01f)]
    public float stopDistance = 0.15f;

    [Header("Optional")]
    [Tooltip("非空时根据速度写入 Animator Float（如 Speed）。")]
    public Animator animator;

    [Tooltip("非空时根据速度写入 Animator Float；**须与 Animator 窗口里参数名完全一致**（区分大小写）。留空则不写 Animator。")]
    public string animatorSpeedParam = "";

    [Tooltip("为 true：移动时按水平速度翻转 localScale.x（面向左右）。")]
    public bool flipScaleXByVelocity = true;

    Rigidbody2D _rb;
    Vector2 _spawn;
    Vector2 _target;
    enum Phase { Moving, Idle }
    Phase _phase;
    float _idleTimer;
    float _facingSign = 1f;

    enum SpeedParamCache { Unchecked, Valid, Missing }
    SpeedParamCache _speedParamCache = SpeedParamCache.Unchecked;
    int _speedParamHash;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        if (animator == null)
            animator = GetComponent<Animator>();
    }

    void OnEnable()
    {
        _speedParamCache = SpeedParamCache.Unchecked;
    }

    void Start()
    {
        _spawn = _rb.position;
        BeginMoveToNewTarget();
    }

    void BeginMoveToNewTarget()
    {
        _target = _spawn + Random.insideUnitCircle * roamRadius;
        _phase = Phase.Moving;
        _idleTimer = 0f;
    }

    void FixedUpdate()
    {
        if (_phase == Phase.Idle)
        {
            StopMotion();
            PushAnimatorSpeed(0f);
            if (!repeatRoam)
                return;

            _idleTimer -= Time.fixedDeltaTime;
            if (_idleTimer <= 0f)
                BeginMoveToNewTarget();
            return;
        }

        // Moving
        Vector2 pos = _rb.position;
        Vector2 delta = _target - pos;
        if (delta.sqrMagnitude <= stopDistance * stopDistance)
        {
            StopMotion();
            PushAnimatorSpeed(0f);
            if (repeatRoam)
            {
                _phase = Phase.Idle;
                _idleTimer = roamIdleDuration;
                if (_idleTimer <= 0f)
                    BeginMoveToNewTarget();
            }
            else
                _phase = Phase.Idle;
            return;
        }

        Vector2 dir = delta.normalized;
        if (_rb.bodyType == RigidbodyType2D.Kinematic)
            _rb.MovePosition(pos + dir * (moveSpeed * Time.fixedDeltaTime));
        else
            _rb.linearVelocity = dir * moveSpeed;

        if (dir.x * dir.x > 1e-6f)
            _facingSign = dir.x >= 0f ? 1f : -1f;

        PushAnimatorSpeed(moveSpeed);
        ApplyFacingScale();
    }

    void StopMotion()
    {
        if (_rb.bodyType == RigidbodyType2D.Dynamic)
            _rb.linearVelocity = Vector2.zero;
    }

    void PushAnimatorSpeed(float speed)
    {
        if (animator == null || string.IsNullOrEmpty(animatorSpeedParam)) return;

        if (_speedParamCache == SpeedParamCache.Unchecked)
            CacheSpeedParamIfPresent();

        if (_speedParamCache != SpeedParamCache.Valid)
            return;

        animator.SetFloat(_speedParamHash, speed);
    }

    void CacheSpeedParamIfPresent()
    {
        _speedParamCache = SpeedParamCache.Missing;
        if (animator == null || string.IsNullOrEmpty(animatorSpeedParam)) return;

        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Float && p.name == animatorSpeedParam)
            {
                _speedParamHash = Animator.StringToHash(animatorSpeedParam);
                _speedParamCache = SpeedParamCache.Valid;
                return;
            }
        }
    }

    /// <summary>更换 Animator / Controller 后可调用，重新解析 <see cref="animatorSpeedParam"/>。</summary>
    public void InvalidateAnimatorSpeedParameterCache()
    {
        _speedParamCache = SpeedParamCache.Unchecked;
    }

    void ApplyFacingScale()
    {
        if (!flipScaleXByVelocity || _phase != Phase.Moving) return;
        Vector3 s = transform.localScale;
        float ax = Mathf.Abs(s.x);
        if (ax < 1e-4f) ax = 1f;
        s.x = ax * _facingSign;
        transform.localScale = s;
    }
}
