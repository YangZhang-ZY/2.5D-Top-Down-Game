using System.Collections;
using UnityEngine;

/// <summary>
/// Boss fight BGM when <see cref="BossController.BossCombatEngaged"/> is true and the player is within range.
/// Fades out when the player leaves <see cref="musicStopDistance"/>; plays again when they come within <see cref="musicResumeDistance"/>.
/// Fades on <see cref="Health.OnDeath"/>.
/// </summary>
[RequireComponent(typeof(AudioSource))]
[DefaultExecutionOrder(1001)]
public class BossBattleMusic : MonoBehaviour
{
    [SerializeField] BossController boss;

    [Tooltip("World point to measure distance from; defaults to boss transform.")]
    [SerializeField] Transform listenOrigin;

    [Tooltip("Long battle track; enable Loop for repeating sections.")]
    [SerializeField] AudioClip battleMusic;

    [Range(0f, 1f)]
    [SerializeField] float volume = 1f;

    [Tooltip("If true, clip restarts when it ends (typical for BGM).")]
    [SerializeField] bool loop = true;

    [Header("Player distance")]
    [Tooltip("Player farther than this: music fades out. Should be >= resume distance.")]
    [Min(0.5f)]
    [SerializeField] float musicStopDistance = 32f;

    [Tooltip("Player closer than this: music can play again after leaving. Slightly smaller than stop distance avoids border flicker.")]
    [Min(0.5f)]
    [SerializeField] float musicResumeDistance = 24f;

    [Tooltip("Fade when leaving range or on boss death; 0 = stop immediately.")]
    [Min(0f)]
    [SerializeField] float fadeOutDuration = 1.5f;

    AudioSource _src;
    Health _health;
    Transform _player;

    float _playedVolume;
    Coroutine _fadeRoutine;

    bool _playerInsideMusicRange;
    bool _combatEngagedLast;

    void Awake()
    {
        _src = GetComponent<AudioSource>();
        _src.playOnAwake = false;
        _src.spatialBlend = 0f;
        _src.loop = loop;

        if (boss == null)
            boss = GetComponentInParent<BossController>();
        if (boss == null)
            boss = GetComponent<BossController>();

        if (listenOrigin == null && boss != null)
            listenOrigin = boss.transform;

        _health = GetComponentInParent<Health>();
        if (_health == null && boss != null)
            _health = boss.GetComponent<Health>();
    }

    void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
            _player = p.transform;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (musicResumeDistance > musicStopDistance)
            musicResumeDistance = musicStopDistance;
    }
#endif

    void OnEnable()
    {
        if (_health != null)
            _health.OnDeath.AddListener(OnBossDeath);
    }

    void OnDisable()
    {
        if (_health != null)
            _health.OnDeath.RemoveListener(OnBossDeath);
        StopFadeRoutine();
    }

    void Update()
    {
        if (boss == null || battleMusic == null || _src == null)
            return;

        if (_health != null && _health.IsDead)
        {
            BeginFadeOut();
            return;
        }

        if (!boss.BossCombatEngaged || _player == null)
        {
            _combatEngagedLast = false;
            BeginFadeOut();
            return;
        }

        float stopSqr = musicStopDistance * musicStopDistance;
        float resumeSqr = musicResumeDistance * musicResumeDistance;
        float sqr = SqrDistanceToPlayer();

        if (boss.BossCombatEngaged && !_combatEngagedLast)
        {
            _playerInsideMusicRange = sqr <= stopSqr;
            _combatEngagedLast = true;
        }
        else
        {
            if (_playerInsideMusicRange)
            {
                if (sqr > stopSqr)
                    _playerInsideMusicRange = false;
            }
            else
            {
                if (sqr < resumeSqr)
                    _playerInsideMusicRange = true;
            }
        }

        bool wantMusic = _playerInsideMusicRange;

        if (wantMusic)
        {
            CancelFadeAndRestoreVolumeIfNeeded();
            if (!_src.isPlaying)
            {
                _src.loop = loop;
                _src.clip = battleMusic;
                _playedVolume = volume;
                _src.volume = _playedVolume;
                _src.Play();
            }
            else if (Mathf.Abs(_src.volume - volume) > 0.001f && _fadeRoutine == null)
                _src.volume = volume;
        }
        else
            BeginFadeOut();
    }

    float SqrDistanceToPlayer()
    {
        if (listenOrigin == null || _player == null)
            return float.MaxValue;
        return (listenOrigin.position - _player.position).sqrMagnitude;
    }

    void OnBossDeath() => BeginFadeOut();

    void BeginFadeOut()
    {
        if (_src == null || battleMusic == null)
            return;
        if (_fadeRoutine != null)
            return;
        if (!_src.isPlaying)
            return;

        if (fadeOutDuration <= 0f)
        {
            _src.Stop();
            return;
        }

        _fadeRoutine = StartCoroutine(CoFadeOut());
    }

    void CancelFadeAndRestoreVolumeIfNeeded()
    {
        if (_fadeRoutine == null)
            return;
        StopCoroutine(_fadeRoutine);
        _fadeRoutine = null;
        _playedVolume = volume;
        _src.volume = _playedVolume;
    }

    void StopFadeRoutine()
    {
        if (_fadeRoutine != null)
        {
            StopCoroutine(_fadeRoutine);
            _fadeRoutine = null;
        }
    }

    IEnumerator CoFadeOut()
    {
        float start = _src.volume;
        float dur = fadeOutDuration;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            _src.volume = Mathf.Lerp(start, 0f, t / dur);
            yield return null;
        }

        _src.Stop();
        _src.volume = volume;
        _fadeRoutine = null;
    }
}
