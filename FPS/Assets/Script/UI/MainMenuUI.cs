using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 主界面：标题「突围」+ 三个按钮（开始新游戏 / 继续游戏 / 结束游戏）。
/// 「继续游戏」只在至少有一个存档时显示（没有存档时它会自动隐藏，
/// 并把「结束游戏」挪到它的位置上，按钮之间不会空一格）。
/// 挂在主菜单的 Canvas 上。
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [Header("界面")]
    [SerializeField] private GameObject mainPanel;        // 主按钮面板
    [SerializeField] private Button newGameButton;        // 开始新游戏
    [SerializeField] private Button continueButton;       // 继续游戏（没存档时隐藏）
    [SerializeField] private Button quitButton;           // 结束游戏
    [SerializeField] private SaveSlotPanel slotPanel;     // 选择存档位界面

    [Header("调试")]
    [SerializeField] private bool logMenu = true;

    private void Awake()
    {
        UIFonts.Apply(gameObject);

        if (newGameButton != null) newGameButton.onClick.AddListener(OnNewGameClicked);
        if (continueButton != null) continueButton.onClick.AddListener(OnContinueClicked);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);
    }

    private void OnDestroy()
    {
        if (newGameButton != null) newGameButton.onClick.RemoveListener(OnNewGameClicked);
        if (continueButton != null) continueButton.onClick.RemoveListener(OnContinueClicked);
        if (quitButton != null) quitButton.onClick.RemoveListener(OnQuitClicked);
    }

    private void Start()
    {
        ShowMainPanel();
    }

    /// <summary>回到主界面（选存档界面点「返回」时也走这里）。</summary>
    public void ShowMainPanel()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (slotPanel != null) slotPanel.Close();
        if (mainPanel != null) mainPanel.SetActive(true);

        // 一个存档都没有就不给「继续游戏」
        bool hasSave = SaveSystem.HasAnySave;

        if (continueButton != null)
        {
            continueButton.gameObject.SetActive(hasSave);

            // 「继续游戏」藏起来时，把「结束游戏」挪上去，按钮之间不留空
            if (!hasSave && quitButton != null)
            {
                RectTransform quitRect = quitButton.transform as RectTransform;
                RectTransform continueRect = continueButton.transform as RectTransform;
                if (quitRect != null && continueRect != null) quitRect.anchoredPosition = continueRect.anchoredPosition;
            }
        }

        if (logMenu) Debug.Log($"[MainMenuUI] 主界面：存档={hasSave}（存档位 {SaveSystem.SlotCount} 个）", this);
    }

    private void OnNewGameClicked()
    {
        if (logMenu) Debug.Log("[MainMenuUI] 开始新游戏 -> 选择存档位", this);

        if (mainPanel != null) mainPanel.SetActive(false);
        if (slotPanel != null) slotPanel.Open(SaveSlotMode.NewGame);
    }

    private void OnContinueClicked()
    {
        if (logMenu) Debug.Log("[MainMenuUI] 继续游戏 -> 选择存档位", this);

        if (mainPanel != null) mainPanel.SetActive(false);
        if (slotPanel != null) slotPanel.Open(SaveSlotMode.Continue);
    }

    private void OnQuitClicked()
    {
        QuitGame();
    }

    /// <summary>结束游戏：编辑器中停止播放，打包后退出程序。</summary>
    public static void QuitGame()
    {
        Debug.Log("[MainMenuUI] 结束游戏");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
