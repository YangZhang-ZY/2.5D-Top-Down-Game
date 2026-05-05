using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 主菜单：加载游戏关卡、退出游戏、播放菜单 BGM。
/// 按钮 OnClick 绑定 <see cref="StartGame"/> / <see cref="QuitGame"/>；
/// 将 <see cref="menuMusic"/> 拖入 Inspector 即可在本场景循环播放。
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Tooltip("需在 File → Build Settings → Scenes In Build 中包含此场景")]
    [SerializeField] string gameSceneName = "TestScene";

    [Tooltip("victory / game over 可能把 timeScale 设为 0，进关卡前恢复")]
    [SerializeField] bool resetTimeScaleBeforeLoad = true;

    [Header("Menu music")]
    [Tooltip("主菜单循环 BGM；留空则不播。运行时会在本物体上挂/用 AudioSource。")]
    [SerializeField] AudioClip menuMusic;

    [Tooltip("音乐音量")]
    [SerializeField] [Range(0f, 1f)] float menuMusicVolume = 0.65f;

    [SerializeField] bool playMenuMusicOnStart = true;

    AudioSource _music;

    void Awake()
    {
        _music = GetComponent<AudioSource>();
        if (_music == null)
            _music = gameObject.AddComponent<AudioSource>();

        _music.playOnAwake = false;
        _music.loop = true;
        _music.spatialBlend = 0f;
        _music.priority = 0;
    }

    void Start()
    {
        if (!playMenuMusicOnStart || menuMusic == null) return;

        _music.clip = menuMusic;
        _music.volume = menuMusicVolume;
        _music.Play();
    }

    public void StartGame()
    {
        if (resetTimeScaleBeforeLoad)
            Time.timeScale = 1f;

        if (string.IsNullOrEmpty(gameSceneName))
        {
            Debug.LogError("[MainMenu] gameSceneName is empty.", this);
            return;
        }

        SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
