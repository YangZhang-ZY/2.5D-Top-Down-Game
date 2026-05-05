using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Main menu: load gameplay scene, quit, optional menu BGM.
/// Wire buttons to <see cref="StartGame"/> / <see cref="QuitGame"/>. Assign <see cref="menuMusic"/> for a looping clip.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Tooltip("Scene name must be listed under File → Build Settings → Scenes In Build.")]
    [SerializeField] string gameSceneName = "TestScene";

    [Tooltip("Victory / game over may set timeScale to 0; restore before loading.")]
    [SerializeField] bool resetTimeScaleBeforeLoad = true;

    [Header("Menu music")]
    [Tooltip("Looping BGM for this scene. If empty, no music. Uses or adds AudioSource on this object.")]
    [SerializeField] AudioClip menuMusic;

    [Tooltip("Music volume.")]
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
