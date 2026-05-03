using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 主菜单：加载游戏关卡。按钮 OnClick 绑定 <see cref="StartGame"/>。
/// 新建场景 MainMenu，拖一个空物体挂上本脚本；<see cref="gameSceneName"/> 填 Build Settings 里已有场景名（如 TestScene）。
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Tooltip("需在 File → Build Settings → Scenes In Build 中包含此场景")]
    [SerializeField] string gameSceneName = "TestScene";

    [Tooltip(" victory / game over 可能把 timeScale 设为 0，进关卡前恢复")]
    [SerializeField] bool resetTimeScaleBeforeLoad = true;

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
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
