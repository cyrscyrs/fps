using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 存档流程控制：在合适的时机存/读档，具体的文件读写交给 SaveSystem。
/// 挂在 HUDCanvas 预制体上（三个关卡都用这个预制体），所以三关通用。
///
/// 什么时候存：
///   - 进入关卡时记一笔（当前关卡 + 当前血量）
///   - 每隔 autosaveInterval 秒自动存一次
///   - 退出游戏 / 切到后台时
///   - 成功撤离时改成「下一关 + 当前血量」（最后一关撤离 = 通关，直接清档）
/// 什么时候清：
///   - 玩家死亡（这一局结束了，下次从第一关重新开始）
///
/// 什么时候读：
///   - 本次启动第一次进关卡：存档记录的是别的关卡 -> 直接加载到那一关继续玩
///   - 进入存档记录的那一关时，把血量恢复成存档里的值
/// </summary>
public class GameSaveController : MonoBehaviour
{
    [Header("存档")]
    [SerializeField] private bool resumeOnGameStart = true;    // 启动游戏时有存档就继续上次的关卡（编辑器里按 Play 也会这样）
    [SerializeField] private bool restoreHealth = true;        // 进入存档记录的那一关时恢复当时的血量
    [SerializeField] private float autosaveInterval = 0f;     // 每隔多少秒自动存一次（0 = 关掉自动保存）
    [SerializeField] private bool logSave = true;              // 存/读/清档时打日志

    /// <summary>场景里的存档控制器（调试用，可以 GameSaveController.Instance.SaveNow()）。</summary>
    public static GameSaveController Instance { get; private set; }

    private Health playerHealth;
    private float autosaveTimer;
    private bool levelFinished;    // 已经撤离或死亡，不再写当前关卡的进度
    private bool playerDied;

    // 「继续上次进度」每次启动游戏只处理一次
    private static bool resumeChecked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSessionState()
    {
        resumeChecked = false;
        Instance = null;
    }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        playerHealth = FindPlayerHealth();

        if (playerHealth != null) playerHealth.Died += OnPlayerDied;
        else Debug.LogWarning("[GameSaveController] 没找到玩家的 Health，存档功能不可用。", this);

        ExtractionZone.PlayerExtracted += OnPlayerExtracted;

        // 有存档且记录的是别的关卡：直接切过去，剩下的事由那一关的控制器接手
        if (TryResumeSavedLevel()) return;

        RestoreHealthIfSameLevel();

        // 进关卡先记一笔，这样「在哪一关」永远是最新的
        SaveNow();
    }

    private void OnDestroy()
    {
        if (playerHealth != null) playerHealth.Died -= OnPlayerDied;
        ExtractionZone.PlayerExtracted -= OnPlayerExtracted;

        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (playerHealth == null || levelFinished || autosaveInterval <= 0f) return;

        autosaveTimer += Time.deltaTime;              // 用受暂停影响的时间：暂停界面（timeScale=0）里不会偷偷存盘
        if (autosaveTimer < autosaveInterval) return;

        autosaveTimer = 0f;
        SaveNow();
    }

    private void OnApplicationQuit()
    {
        SaveNow();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus) SaveNow();                   // 手机切后台
    }

    // ---------- 存档 / 读档 ----------

    /// <summary>立刻把「当前关卡 + 当前血量」写进存档。</summary>
    public void SaveNow()
    {
        if (playerHealth == null) return;
        if (levelFinished) return;                    // 已经撤离/死亡，别用旧数据覆盖进度

        Scene scene = SceneManager.GetActiveScene();

        SaveSystem.Save(new SaveData
        {
            levelIndex = scene.buildIndex,
            levelName = scene.name,
            health = playerHealth.CurrentHealth,
            maxHealth = playerHealth.MaxHealth
        });

        if (logSave)
            Debug.Log($"[GameSaveController] 已存档：第 {scene.buildIndex} 关（{scene.name}），血量 {playerHealth.CurrentHealth:0.#}/{playerHealth.MaxHealth:0.#}", this);
    }

    /// <summary>手动清档（做「新游戏」按钮时可以用）。</summary>
    public void DeleteSave()
    {
        SaveSystem.Delete();
        if (logSave) Debug.Log("[GameSaveController] 已清空存档。", this);
    }

    /// <summary>启动游戏时：存档记录的是别的关卡就直接加载过去。返回 true 表示场景已经在切换了。</summary>
    private bool TryResumeSavedLevel()
    {
        if (resumeChecked) return false;
        resumeChecked = true;

        if (!resumeOnGameStart) return false;

        if (!SaveSystem.TryLoad(out SaveData data, out string error))
        {
            if (logSave && !string.IsNullOrEmpty(error))
                Debug.Log($"[GameSaveController] 没有可用存档（{error}），从头开始。", this);
            return false;
        }

        int current = SceneManager.GetActiveScene().buildIndex;
        if (data.levelIndex == current) return false;      // 存档就是这一关，不用切

        if (logSave) Debug.Log($"[GameSaveController] 找到存档：{data} -> 继续这一关", this);

        Time.timeScale = 1f;
        SceneManager.LoadScene(data.levelIndex);
        return true;
    }

    /// <summary>存档记录的就是当前这一关时，把血量恢复成存档里的值。</summary>
    private void RestoreHealthIfSameLevel()
    {
        if (!restoreHealth || playerHealth == null) return;

        if (!SaveSystem.TryLoad(out SaveData data, out _)) return;

        int current = SceneManager.GetActiveScene().buildIndex;

        if (data.levelIndex != current)
        {
            if (logSave) Debug.Log($"[GameSaveController] 存档在第 {data.levelIndex} 关，这一关（第 {current} 关）按满血开始。", this);
            return;
        }

        // 刚在主菜单里创建的新存档：按关卡默认满血开始
        if (data.isNewGame)
        {
            if (logSave) Debug.Log("[GameSaveController] 这是新存档，按满血开始。", this);
            return;
        }

        playerHealth.SetMaxHealth(data.maxHealth, false);
        playerHealth.SetHealth(data.health);

        if (logSave) Debug.Log($"[GameSaveController] 恢复存档血量：{data.health:0.#}/{data.maxHealth:0.#}", this);
    }

    // ---------- 事件 ----------

    /// <summary>撤离成功：把进度记到下一关，并带上当前血量；最后一关则是通关，清档。</summary>
    private void OnPlayerExtracted(ExtractionZone zone)
    {
        levelFinished = true;

        Scene scene = SceneManager.GetActiveScene();
        int next = scene.buildIndex + 1;
        bool hasNext = scene.buildIndex >= 0 && next < SceneManager.sceneCountInBuildSettings;

        if (!hasNext)
        {
            SaveSystem.Delete();
            if (logSave) Debug.Log("[GameSaveController] 最后一关撤离成功 = 通关，清空存档。", this);
            return;
        }

        if (playerHealth == null) return;

        SaveSystem.Save(new SaveData
        {
            levelIndex = next,
            levelName = SceneNameOf(next),
            health = playerHealth.CurrentHealth,
            maxHealth = playerHealth.MaxHealth
        });

        if (logSave)
            Debug.Log($"[GameSaveController] 撤离成功：进度存到第 {next} 关（{SceneNameOf(next)}），血量 {playerHealth.CurrentHealth:0.#}", this);
    }

    /// <summary>玩家死亡：清档，下次从第一关重新开始。</summary>
    private void OnPlayerDied()
    {
        if (playerDied) return;
        playerDied = true;
        levelFinished = true;

        SaveSystem.Delete();
        if (logSave) Debug.Log("[GameSaveController] 玩家死亡，清空存档（下次从第一关重新开始）。", this);
    }

    // ---------- 工具 ----------

    private static string SceneNameOf(int buildIndex)
    {
        string path = SceneUtility.GetScenePathByBuildIndex(buildIndex);
        return string.IsNullOrEmpty(path) ? string.Empty : Path.GetFileNameWithoutExtension(path);
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
