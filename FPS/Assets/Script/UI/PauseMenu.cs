using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 暂停界面：按 ESC 暂停 / 继续。
///   - 两条音量滑条：僵尸叫声、开火枪声（拖动时有试听音，设置存 PlayerPrefs）
///   - 「返回主界面」按钮：先弹窗问「是否返回主界面」；
///     选「否」只关弹窗、继续暂停；选「是」不动存档直接回主界面。
/// 挂在 HUDCanvas 预制体上（三个关卡都有）。
/// </summary>
public class PauseMenu : MonoBehaviour
{
    [Header("界面")]
    [SerializeField] private GameObject pausePanel;        // 暂停界面根物体（默认隐藏）
    [SerializeField] private GameObject confirmDialog;     // 返回主界面的确认弹窗（默认隐藏）
    [SerializeField] private Text confirmText;
    [SerializeField] private Button backToMenuButton;
    [SerializeField] private Button yesButton;             // 弹窗：是
    [SerializeField] private Button noButton;              // 弹窗：否

    [Header("音量")]
    [SerializeField] private Slider zombieVolumeSlider;
    [SerializeField] private Slider weaponVolumeSlider;
    [SerializeField] private AudioSource previewSource;    // 试听用的 2D 音源（暂停时也能出声）
    [SerializeField] private AudioClip zombiePreviewClip;
    [SerializeField] private AudioClip weaponPreviewClip;
    [SerializeField] private float previewInterval = 0.3f; // 拖动时最快多久试听一次

    [Header("其他")]
    [SerializeField] private KeyCode pauseKey = KeyCode.Escape;
    [SerializeField] private string confirmMessage = "是否返回主界面？";
    [SerializeField] private bool logPause = true;

    /// <summary>现在是不是暂停中。</summary>
    public bool IsPaused { get; private set; }

    /// <summary>「是否返回主界面」弹窗是不是开着。</summary>
    public bool IsConfirmDialogOpen => confirmDialog != null && confirmDialog.activeSelf;

    private GameOverUI gameOverUI;
    private GameSuccessUI gameSuccessUI;

    private PlayerControll playerControll;
    private Weapon weapon;
    private Health playerHealth;

    private float nextPreviewTime;

    private void Awake()
    {
        UIFonts.Apply(gameObject);

        if (pausePanel != null) pausePanel.SetActive(false);
        if (confirmDialog != null) confirmDialog.SetActive(false);

        // 试听音源要无视 AudioListener.pause，暂停时才听得见
        if (previewSource != null)
        {
            previewSource.ignoreListenerPause = true;
            previewSource.playOnAwake = false;
            previewSource.loop = false;
        }

        gameOverUI = GetComponent<GameOverUI>();
        gameSuccessUI = GetComponent<GameSuccessUI>();

        if (backToMenuButton != null) backToMenuButton.onClick.AddListener(OnBackToMenuClicked);
        if (yesButton != null) yesButton.onClick.AddListener(OnConfirmYes);
        if (noButton != null) noButton.onClick.AddListener(OnConfirmNo);

        SetupSlider(zombieVolumeSlider, GameAudioSettings.ZombieVolume, OnZombieVolumeChanged);
        SetupSlider(weaponVolumeSlider, GameAudioSettings.WeaponVolume, OnWeaponVolumeChanged);
    }

    private void OnDestroy()
    {
        if (backToMenuButton != null) backToMenuButton.onClick.RemoveListener(OnBackToMenuClicked);
        if (yesButton != null) yesButton.onClick.RemoveListener(OnConfirmYes);
        if (noButton != null) noButton.onClick.RemoveListener(OnConfirmNo);

        if (zombieVolumeSlider != null) zombieVolumeSlider.onValueChanged.RemoveListener(OnZombieVolumeChanged);
        if (weaponVolumeSlider != null) weaponVolumeSlider.onValueChanged.RemoveListener(OnWeaponVolumeChanged);

        RestoreTimeAndAudio();   // 切场景 / 被销毁时别把暂停状态留下
    }

    private void Update()
    {
        HandlePauseKey(Input.GetKeyDown(pauseKey));
    }

    /// <summary>
    /// 处理一次「按了暂停键」：ESC 的优先级是先关确认弹窗、再切换暂停/继续；
    /// 失败/成功结算界面开着时不允许暂停。
    /// </summary>
    public void HandlePauseKey(bool pressed)
    {
        if (!pressed) return;

        if (IsConfirmDialogOpen)
        {
            HideConfirm();
            return;
        }

        if (IsPaused)
        {
            Resume();
            return;
        }

        if (CanPause()) Pause();
    }

    /// <summary>游戏失败 / 撤离成功的结算界面开着时，不允许再开暂停界面。</summary>
    private bool CanPause()
    {
        if (gameOverUI != null && gameOverUI.IsShown) return false;
        if (gameSuccessUI != null && gameSuccessUI.IsShown) return false;

        return true;
    }

    // ---------- 暂停 / 继续 ----------

    /// <summary>进入暂停。</summary>
    public void Pause()
    {
        if (IsPaused) return;
        IsPaused = true;

        if (pausePanel != null) pausePanel.SetActive(true);
        if (confirmDialog != null) confirmDialog.SetActive(false);

        SyncSliders();

        Time.timeScale = 0f;
        AudioListener.pause = true;                  // 游戏里的声音一起停住
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SetPlayerControlEnabled(false);

        if (logPause) Debug.Log("[PauseMenu] 暂停", this);
    }

    /// <summary>继续游戏。</summary>
    public void Resume()
    {
        if (!IsPaused) return;
        IsPaused = false;

        if (pausePanel != null) pausePanel.SetActive(false);
        if (confirmDialog != null) confirmDialog.SetActive(false);

        RestoreTimeAndAudio();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        SetPlayerControlEnabled(true);

        GameAudioSettings.Flush();                   // 音量设置落盘

        if (logPause) Debug.Log("[PauseMenu] 继续游戏", this);
    }

    private void RestoreTimeAndAudio()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    // ---------- 返回主界面 ----------

    /// <summary>「返回主界面」按钮：弹窗确认。</summary>
    public void OnBackToMenuClicked()
    {
        if (confirmText != null) confirmText.text = confirmMessage;
        if (confirmDialog != null) confirmDialog.SetActive(true);

        if (logPause) Debug.Log("[PauseMenu] 询问是否返回主界面", this);
    }

    /// <summary>弹窗「否」：只关弹窗，继续暂停。</summary>
    public void OnConfirmNo()
    {
        HideConfirm();

        if (logPause) Debug.Log("[PauseMenu] 取消返回主界面", this);
    }

    /// <summary>弹窗「是」：不修改存档，直接回主界面。</summary>
    public void OnConfirmYes()
    {
        if (logPause) Debug.Log("[PauseMenu] 返回主界面（不修改存档）", this);

        HideConfirm();

        IsPaused = false;
        RestoreTimeAndAudio();
        GameAudioSettings.Flush();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 这里只切场景：不写存档、不删存档，进度保持原样
        SceneManager.LoadScene(SaveSystem.MenuSceneBuildIndex);
    }

    private void HideConfirm()
    {
        if (confirmDialog != null) confirmDialog.SetActive(false);
    }

    // ---------- 音量滑条 ----------

    private void SetupSlider(Slider slider, float value, UnityEngine.Events.UnityAction<float> handler)
    {
        if (slider == null) return;

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.SetValueWithoutNotify(value);
        slider.onValueChanged.AddListener(handler);
    }

    private void SyncSliders()
    {
        if (zombieVolumeSlider != null) zombieVolumeSlider.SetValueWithoutNotify(GameAudioSettings.ZombieVolume);
        if (weaponVolumeSlider != null) weaponVolumeSlider.SetValueWithoutNotify(GameAudioSettings.WeaponVolume);
    }

    private void OnZombieVolumeChanged(float value)
    {
        GameAudioSettings.SetZombieVolume(value);
        PlayPreview(zombiePreviewClip, value);
    }

    private void OnWeaponVolumeChanged(float value)
    {
        GameAudioSettings.SetWeaponVolume(value);
        PlayPreview(weaponPreviewClip, value);
    }

    /// <summary>拖动滑条时试听一下当前音量（暂停状态也能听到）。</summary>
    private void PlayPreview(AudioClip clip, float volume)
    {
        if (previewSource == null || clip == null) return;
        if (Time.unscaledTime < nextPreviewTime) return;      // 拖动时别每帧重放

        nextPreviewTime = Time.unscaledTime + Mathf.Max(0.05f, previewInterval);

        previewSource.Stop();
        previewSource.clip = clip;
        previewSource.volume = Mathf.Clamp01(volume);
        previewSource.Play();
    }

    // ---------- 暂停时锁住玩家操作 ----------

    private void SetPlayerControlEnabled(bool enable)
    {
        FindPlayer();

        bool canControl = enable && (playerHealth == null || !playerHealth.IsDead);   // 死了就别把操作开回来

        if (playerControll != null) playerControll.enabled = canControl;
        if (weapon != null) weapon.enabled = canControl;
    }

    private void FindPlayer()
    {
        if (playerControll != null && weapon != null && playerHealth != null) return;

        GameObject playerObject = null;

        try { playerObject = GameObject.FindGameObjectWithTag("Player"); }
        catch (UnityException) { /* 没有这个 Tag 就忽略 */ }

        if (playerObject == null)
        {
            PlayerControll controll = FindObjectOfType<PlayerControll>();
            if (controll != null) playerObject = controll.gameObject;
        }

        if (playerObject == null) return;

        playerControll = playerObject.GetComponent<PlayerControll>();
        weapon = playerObject.GetComponent<Weapon>();
        playerHealth = playerObject.GetComponent<Health>();
    }
}
