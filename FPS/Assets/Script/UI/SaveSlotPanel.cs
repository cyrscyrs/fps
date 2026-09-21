using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>选择存档位的两种用途。</summary>
public enum SaveSlotMode
{
    NewGame,    // 开始新游戏：空存档位直接开始，已有存档要先确认覆盖
    Continue    // 继续游戏：只能选有存档的存档位，读档进入游戏
}

/// <summary>
/// 选择存档界面：最多 3 个存档位。
///   - 空存档位显示「无存档」；有存档则显示血量 / 所在关卡 / 存档时间
///   - 开始新游戏：选空存档位 -> 直接建新存档进游戏；选已有存档 -> 弹窗问「是否覆盖该存档」
///   - 继续游戏：选有存档的位 -> 读档进游戏（空位不可点）
/// 挂在主菜单 Canvas 上（和 MainMenuUI 同一个物体）。
/// </summary>
public class SaveSlotPanel : MonoBehaviour
{
    [Serializable]
    public class SlotEntry
    {
        public Button button;
        public Text label;
    }

    [Header("界面")]
    [SerializeField] private GameObject panel;                          // 选择存档界面根物体（默认隐藏）
    [SerializeField] private Text titleText;                            // 顶上的标题
    [SerializeField] private SlotEntry[] slots = new SlotEntry[SaveSystem.SlotCount];
    [SerializeField] private Button backButton;                         // 返回主界面

    [Header("文案")]
    [SerializeField] private string newGameTitle = "选择存档位保存新存档";
    [SerializeField] private string continueTitle = "选择存档继续游戏";
    [SerializeField] private string emptySlotText = "无存档";

    [Header("覆盖确认弹窗")]
    [SerializeField] private GameObject overwriteDialog;                // 弹窗根物体（默认隐藏）
    [SerializeField] private Text overwriteText;
    [SerializeField] private Button yesButton;                          // 是
    [SerializeField] private Button noButton;                           // 否

    [Header("其他")]
    [SerializeField] private MainMenuUI mainMenu;                       // 返回时用它切回主界面
    [SerializeField] private bool logActions = true;

    private SaveSlotMode mode = SaveSlotMode.Continue;
    private int pendingSlot = -1;

    /// <summary>弹窗是不是开着（调试 / 测试用）。</summary>
    public bool IsOverwriteDialogOpen => overwriteDialog != null && overwriteDialog.activeSelf;

    /// <summary>当前是「新游戏」还是「继续游戏」模式。</summary>
    public SaveSlotMode Mode => mode;

    private void Awake()
    {
        UIFonts.Apply(gameObject);

        if (panel != null) panel.SetActive(false);
        if (overwriteDialog != null) overwriteDialog.SetActive(false);

        for (int i = 0; i < slots.Length; i++)
        {
            int index = i;      // 闭包要捕获每轮自己的下标
            if (slots[i] != null && slots[i].button != null)
                slots[i].button.onClick.AddListener(() => OnSlotClicked(index));
        }

        if (backButton != null) backButton.onClick.AddListener(BackToMainMenu);
        if (yesButton != null) yesButton.onClick.AddListener(ConfirmOverwrite);
        if (noButton != null) noButton.onClick.AddListener(CancelOverwrite);
    }

    private void OnDestroy()
    {
        if (backButton != null) backButton.onClick.RemoveListener(BackToMainMenu);
        if (yesButton != null) yesButton.onClick.RemoveListener(ConfirmOverwrite);
        if (noButton != null) noButton.onClick.RemoveListener(CancelOverwrite);
    }

    // ---------- 开关 ----------

    /// <summary>打开选择存档界面。</summary>
    public void Open(SaveSlotMode openMode)
    {
        mode = openMode;
        pendingSlot = -1;

        if (panel != null) panel.SetActive(true);
        if (overwriteDialog != null) overwriteDialog.SetActive(false);
        if (titleText != null) titleText.text = mode == SaveSlotMode.NewGame ? newGameTitle : continueTitle;

        RefreshSlots();

        if (logActions) Debug.Log($"[SaveSlotPanel] 打开选择存档界面（{(mode == SaveSlotMode.NewGame ? "新游戏" : "继续游戏")}）", this);
    }

    /// <summary>只把「选择存档」界面收起来（不影响主界面）。</summary>
    public void Close()
    {
        if (panel != null) panel.SetActive(false);
        if (overwriteDialog != null) overwriteDialog.SetActive(false);
        pendingSlot = -1;
    }

    /// <summary>「返回」按钮：关掉选择存档界面，回到主界面。</summary>
    public void BackToMainMenu()
    {
        Close();

        if (mainMenu != null) mainMenu.ShowMainPanel();
    }

    /// <summary>按存档位内容刷新 3 个按钮上的文字。</summary>
    public void RefreshSlots()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            SlotEntry entry = slots[i];
            if (entry == null) continue;

            bool hasSave = SaveSystem.TryLoadSlot(i, out SaveData data, out _);

            if (entry.label != null)
            {
                entry.label.text = hasSave
                    ? $"存档位 {i + 1}\n{data.ToSlotDescription()}"
                    : $"存档位 {i + 1}\n{emptySlotText}";
            }

            // 「继续游戏」时空存档位不能点
            if (entry.button != null)
                entry.button.interactable = mode == SaveSlotMode.NewGame || hasSave;
        }
    }

    // ---------- 点击存档位 ----------

    private void OnSlotClicked(int slot)
    {
        bool hasSave = SaveSystem.TryLoadSlot(slot, out SaveData data, out _);

        if (mode == SaveSlotMode.Continue)
        {
            if (!hasSave) return;                       // 空存档位（按钮本来就点不动）
            StartGame(slot, data);
            return;
        }

        // ---- 开始新游戏 ----

        if (!hasSave)
        {
            CreateNewSaveAndStart(slot);                // 空存档位：直接建新存档
            return;
        }

        // 已有存档：问要不要覆盖
        pendingSlot = slot;

        if (overwriteText != null)
            overwriteText.text = $"存档位 {slot + 1} 已有存档：\n{data.ToSlotDescription()}\n\n是否覆盖该存档？";

        if (overwriteDialog != null) overwriteDialog.SetActive(true);

        if (logActions) Debug.Log($"[SaveSlotPanel] {slot} 号存档位已有存档，弹窗询问是否覆盖", this);
    }

    /// <summary>弹窗「是」：覆盖该存档位的存档 -> 关弹窗 -> 进游戏。</summary>
    public void ConfirmOverwrite()
    {
        if (pendingSlot < 0) return;

        int slot = pendingSlot;
        pendingSlot = -1;

        if (overwriteDialog != null) overwriteDialog.SetActive(false);

        if (logActions) Debug.Log($"[SaveSlotPanel] 覆盖 {slot} 号存档位", this);

        CreateNewSaveAndStart(slot);
    }

    /// <summary>弹窗「否」：只关弹窗，什么都不改。</summary>
    public void CancelOverwrite()
    {
        if (logActions) Debug.Log($"[SaveSlotPanel] 取消覆盖（{pendingSlot} 号存档位）", this);

        pendingSlot = -1;
        if (overwriteDialog != null) overwriteDialog.SetActive(false);
    }

    // ---------- 进游戏 ----------

    /// <summary>在该存档位创建一份新存档（isNewGame）并进入第一关。</summary>
    private void CreateNewSaveAndStart(int slot)
    {
        var data = new SaveData
        {
            levelIndex = SaveSystem.FirstLevelBuildIndex,
            levelName = SceneNameOf(SaveSystem.FirstLevelBuildIndex),
            health = 0f,
            maxHealth = 0f,
            isNewGame = true
        };

        SaveSystem.SaveSlot(slot, data);
        StartGame(slot, data);
    }

    /// <summary>记录当前存档位并加载关卡。</summary>
    private void StartGame(int slot, SaveData data)
    {
        SaveSystem.SetCurrentSlot(slot);

        int levelIndex = data != null ? data.levelIndex : SaveSystem.FirstLevelBuildIndex;
        if (levelIndex < 0) levelIndex = SaveSystem.FirstLevelBuildIndex;

        Time.timeScale = 1f;

        if (logActions)
            Debug.Log($"[SaveSlotPanel] {slot} 号存档位 -> 进入第 {levelIndex} 关（{SceneNameOf(levelIndex)}）", this);

        SceneManager.LoadScene(levelIndex);
    }

    private static string SceneNameOf(int buildIndex)
    {
        string path = SceneUtility.GetScenePathByBuildIndex(buildIndex);
        return string.IsNullOrEmpty(path) ? string.Empty : System.IO.Path.GetFileNameWithoutExtension(path);
    }
}
