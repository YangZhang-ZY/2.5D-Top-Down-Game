using UnityEngine;

/// <summary>
/// Random target inside <see cref="roamRadius"/> around spawn; after arriving, waits <see cref="roamIdleDuration"/> then picks another (optional loop).
/// 2D XY + <see cref="Rigidbody2D"/>, same idea as the player.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Sheep : MonoBehaviour
{
    [Header("Roam")]
    [Tooltip("Radius (meters) around spawn; targets are random in that disk.")]
    [Min(0.1f)]
    public float roamRadius = 10f;

    [Tooltip("Seconds to idle at the current point before the next wander; only when Repeat Roam is on.")]
    [Min(0f)]
    public float roamIdleDuration = 2f;

    [Tooltip("If true: after idling, wander again. If false: stop forever after the first arrival.")]
    public bool repeatRoam = true;

    [Tooltip("Move speed (world units per second).")]
    [Min(0f)]
    public float moveSpeed = 2f;

    [Tooltip("Treat as arrived when closer than this distance to the target.")]
    [Min(0.01f)]
    public float stopDistance = 0.15f;

    [Header("Optional")]
    [Tooltip("If set, drives an Animator float (e.g. Speed) from movement speed.")]
    public Animator animator;

    [Tooltip("Animator float parameter name — must match the Controller exactly (case-sensitive). Empty = do not write.")]
    public string animatorSpeedParam = "";

    [Tooltip("If true, flip localScale.x by horizontal movement (face left/right).")]
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

    /// <summary>Call after swapping Animator / controller to re-resolve <see cref="animatorSpeedParam"/>.</summary>
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
