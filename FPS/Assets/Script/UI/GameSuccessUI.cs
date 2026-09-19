using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 游戏成功画面：玩家在撤离点待够时间、成功撤离时弹出（默认隐藏）。
/// 画面下方最多两个按钮，右边那个位置按关卡自动换：
///   还有下一关 -> 「进入下一关」
///   已经是最后一关 -> 「重新开始」（回到第一关重新玩）
/// 左边固定是「再次游玩」（重开本关）。挂在 Canvas 上，panel 指向默认隐藏的成功面板。
/// </summary>
public class GameSuccessUI : MonoBehaviour
{
    [Header("界面")]
    [SerializeField] private GameObject panel;                  // 成功画面根物体（默认 SetActive(false)）
    [SerializeField] private Button playAgainButton;            // 「再次游玩」：重新开始本关
    [SerializeField] private Button nextLevelButton;            // 「进入下一关」：有下一关时才显示
    [SerializeField] private Button restartFromStartButton;     // 「重新开始」：回到第一关，最后一关才显示

    [Header("关卡顺序")]
    [SerializeField] private bool autoDetectNextLevel = true;   // 用当前场景在 Build Settings 里的下一个场景
    [SerializeField] private int nextLevelBuildIndex = -1;      // 关掉自动检测时手动指定（-1 = 自动）
    [SerializeField] private int firstLevelBuildIndex = 0;      // 「重新开始」回到第几关（默认 Build Settings 第 0 关）

    [Header("其他")]
    [SerializeField] private bool freezeGame = true;      // 成功时暂停游戏（Time.timeScale = 0），免得在结算界面里被咬死
    [SerializeField] private KeyCode playAgainKey = KeyCode.R;   // 也可以按 R 重开本关

    private bool isShown;

    /// <summary>下一关在 Build Settings 里的下标；没有下一关就是 -1。</summary>
    public int NextLevelIndex { get; private set; } = -1;

    /// <summary>当前关卡后面还有没有关卡。</summary>
    public bool HasNextLevel => NextLevelIndex >= 0;

    /// <summary>当前关卡是不是最后一关（没有下一关）。</summary>
    public bool IsLastLevel => NextLevelIndex < 0;

    private void Awake()
    {
        UIFonts.Apply(gameObject);

        if (panel != null) panel.SetActive(false);

        // Inspector 里已经连过按钮就不再重复添加，避免一次点击触发两次
        if (playAgainButton != null && playAgainButton.onClick.GetPersistentEventCount() == 0)
            playAgainButton.onClick.AddListener(PlayAgain);

        if (nextLevelButton != null && nextLevelButton.onClick.GetPersistentEventCount() == 0)
            nextLevelButton.onClick.AddListener(GoToNextLevel);

        if (restartFromStartButton != null && restartFromStartButton.onClick.GetPersistentEventCount() == 0)
            restartFromStartButton.onClick.AddListener(RestartFromFirstLevel);

        SetupLevelButtons();
    }

    private void OnEnable()
    {
        ExtractionZone.PlayerExtracted += OnPlayerExtracted;
    }

    private void OnDisable()
    {
        ExtractionZone.PlayerExtracted -= OnPlayerExtracted;
    }

    private void OnDestroy()
    {
        if (playAgainButton != null) playAgainButton.onClick.RemoveListener(PlayAgain);
        if (nextLevelButton != null) nextLevelButton.onClick.RemoveListener(GoToNextLevel);
        if (restartFromStartButton != null) restartFromStartButton.onClick.RemoveListener(RestartFromFirstLevel);
    }

    private void Update()
    {
        if (isShown && Input.GetKeyDown(playAgainKey)) PlayAgain();
    }

    private void OnPlayerExtracted(ExtractionZone zone)
    {
        Show();
    }

    /// <summary>显示成功画面。</summary>
    public void Show()
    {
        if (isShown) return;
        isShown = true;

        if (panel != null) panel.SetActive(true);

        if (freezeGame) Time.timeScale = 0f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log("[GameSuccessUI] 撤离成功，显示成功画面"
            + (HasNextLevel ? "（有下一关）" : "（最后一关，可重新开始）"), this);
    }

    /// <summary>「再次游玩」按钮：恢复时间流速并重新加载本关。</summary>
    public void PlayAgain()
    {
        Time.timeScale = 1f;

        Scene scene = SceneManager.GetActiveScene();

        if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
        else SceneManager.LoadScene(scene.name);      // 场景没加进 Build Settings 时的兜底
    }

    /// <summary>「进入下一关」按钮：加载 Build Settings 里的下一关。</summary>
    public void GoToNextLevel()
    {
        if (!HasNextLevel)
        {
            Debug.LogWarning("[GameSuccessUI] 没有下一关了，忽略这次点击。", this);
            return;
        }

        Time.timeScale = 1f;
        Debug.Log($"[GameSuccessUI] 进入下一关（Build Settings 下标 {NextLevelIndex}）", this);
        SceneManager.LoadScene(NextLevelIndex);
    }

    /// <summary>「重新开始」按钮（最后一关显示）：回到第一关重新开始玩。</summary>
    public void RestartFromFirstLevel()
    {
        Time.timeScale = 1f;

        int sceneCount = SceneManager.sceneCountInBuildSettings;
        if (sceneCount <= 0)
        {
            Debug.LogWarning("[GameSuccessUI] Build Settings 里没有场景，没法回到第一关。", this);
            return;
        }

        int index = Mathf.Clamp(firstLevelBuildIndex, 0, sceneCount - 1);
        Debug.Log($"[GameSuccessUI] 重新开始：回到第一关（Build Settings 下标 {index}）", this);
        SceneManager.LoadScene(index);
    }

    /// <summary>
    /// 决定成功画面下方右边那个按钮显示哪一个：
    /// 有下一关显示「进入下一关」；最后一关显示「重新开始」（回第一关）；
    /// 两个都没有（比如场景没加进 Build Settings）就把「再次游玩」摆回正中间。
    /// </summary>
    private void SetupLevelButtons()
    {
        NextLevelIndex = FindNextLevelIndex();

        bool showNextLevel = HasNextLevel;
        bool showRestart = IsLastLevel && SceneManager.sceneCountInBuildSettings > 0;

        if (nextLevelButton != null) nextLevelButton.gameObject.SetActive(showNextLevel);
        if (restartFromStartButton != null) restartFromStartButton.gameObject.SetActive(showRestart);

        if (!showNextLevel && !showRestart && playAgainButton != null)
        {
            RectTransform rect = playAgainButton.transform as RectTransform;
            if (rect != null) rect.anchoredPosition = new Vector2(0f, rect.anchoredPosition.y);
        }
    }

    private int FindNextLevelIndex()
    {
        int sceneCount = SceneManager.sceneCountInBuildSettings;
        if (sceneCount <= 1) return -1;

        if (!autoDetectNextLevel)
            return (nextLevelBuildIndex >= 0 && nextLevelBuildIndex < sceneCount) ? nextLevelBuildIndex : -1;

        int current = SceneManager.GetActiveScene().buildIndex;
        if (current < 0 || current + 1 >= sceneCount) return -1;    // 不在 Build Settings 里 / 已经是最后一关

        return current + 1;
    }
}
