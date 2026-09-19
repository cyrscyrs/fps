using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 游戏失败画面：玩家血量归零时显示（默认隐藏），画面上有「重新开始」按钮。
/// 挂在 Canvas 上（保持 Canvas 一直是激活的），panel 指向那个默认隐藏的失败面板。
/// </summary>
public class GameOverUI : MonoBehaviour
{
    [Header("数据来源")]
    [SerializeField] private Health playerHealth;      // 留空自动找 Tag 为 Player 的对象

    [Header("界面")]
    [SerializeField] private GameObject panel;         // 失败画面根物体（默认 SetActive(false)）
    [SerializeField] private Button restartButton;     // 「重新开始」按钮

    [Header("其他")]
    [SerializeField] private bool freezeGame = true;   // 失败时暂停游戏（Time.timeScale = 0）
    [SerializeField] private KeyCode restartKey = KeyCode.R;   // 也可以按 R 重开

    [Header("关卡")]
    [SerializeField] private bool restartFromFirstLevel = true;   // 失败重开：回到第一关重新开始
    [SerializeField] private int firstLevelBuildIndex = 0;        // 第一关在 Build Settings 里的下标

    private bool isShown;

    private void Awake()
    {
        UIFonts.Apply(gameObject);

        if (panel != null) panel.SetActive(false);

        // Inspector 里已经连过按钮就不再重复添加，避免一次点击触发两次重开
        if (restartButton != null && restartButton.onClick.GetPersistentEventCount() == 0)
            restartButton.onClick.AddListener(Restart);
    }

    private void Start()
    {
        if (playerHealth == null) playerHealth = FindPlayerHealth();

        if (playerHealth != null) playerHealth.Died += OnPlayerDied;
        else Debug.LogWarning("[GameOverUI] 没有找到玩家的 Health，游戏失败画面不会触发。", this);
    }

    private void OnDestroy()
    {
        if (playerHealth != null) playerHealth.Died -= OnPlayerDied;
        if (restartButton != null) restartButton.onClick.RemoveListener(Restart);
    }

    private void Update()
    {
        if (isShown && Input.GetKeyDown(restartKey)) Restart();
    }

    private void OnPlayerDied()
    {
        Show();
    }

    /// <summary>显示失败画面。</summary>
    public void Show()
    {
        if (isShown) return;
        isShown = true;

        if (panel != null) panel.SetActive(true);

        if (freezeGame) Time.timeScale = 0f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log("[GameOverUI] 游戏失败", this);
    }

    /// <summary>「重新开始」按钮：恢复时间流速并重新加载当前场景。</summary>
    public void Restart()
    {
        Time.timeScale = 1f;

        int sceneCount = SceneManager.sceneCountInBuildSettings;

        // 失败之后从第一关重新来：不管现在卡在第几关，都回 Build Settings 里的第一关
        if (restartFromFirstLevel && sceneCount > 0)
        {
            int index = Mathf.Clamp(firstLevelBuildIndex, 0, sceneCount - 1);
            Debug.Log($"[GameOverUI] 重新开始：回到第一关（Build Settings 下标 {index}）", this);
            SceneManager.LoadScene(index);
            return;
        }

        Scene scene = SceneManager.GetActiveScene();

        if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
        else SceneManager.LoadScene(scene.name);          // 场景没加进 Build Settings 时的兜底
    }

    private static Health FindPlayerHealth()
    {
        GameObject player = null;

        try { player = GameObject.FindGameObjectWithTag("Player"); }
        catch (UnityException) { /* 没有这个 Tag 就忽略 */ }

        if (player == null)
        {
            PlayerControll controll = FindObjectOfType<PlayerControll>();
            if (controll != null) player = controll.gameObject;
        }

        return player != null ? player.GetComponentInChildren<Health>() : null;
    }
}
