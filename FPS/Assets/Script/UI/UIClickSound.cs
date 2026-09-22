using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI 点击音：挂在 Canvas 上，Awake 时自动给「这个 Canvas 下所有按钮」（含默认隐藏的面板里的）
/// 挂上点击音，鼠标点到就播一次 Assets/Audios/UI-click-sound。
///
/// 用独立的 2D 音源播放，并且 ignoreListenerPause = true，
/// 所以暂停界面（AudioListener.pause = true）里点按钮也听得见。
/// </summary>
[DisallowMultipleComponent]
public class UIClickSound : MonoBehaviour
{
    [Header("音效")]
    [SerializeField] private AudioClip clickSound;        // Assets/Audios/UI-click-sound.mp3
    [SerializeField] private AudioSource clickSource;     // 留空 = 自动用子物体上的 / 新建一个
    [SerializeField, Range(0f, 1f)] private float volume = 0.9f;

    [Header("随机化")]
    [SerializeField] private Vector2 pitchRange = new Vector2(0.97f, 1.03f);

    [Header("调试")]
    [SerializeField] private bool logClicks = false;

    /// <summary>挂上点击音的按钮数量（调试 / 测试用）。</summary>
    public int HookedButtonCount { get; private set; }

    /// <summary>一共点响了多少次（调试 / 测试用）。</summary>
    public int ClickCount { get; private set; }

    /// <summary>最近一次播放的音效。</summary>
    public AudioClip LastClip { get; private set; }

    private AudioSource Source
    {
        get
        {
            if (clickSource == null)
            {
                clickSource = GetComponentInChildren<AudioSource>(true);
                if (clickSource == null)
                {
                    var go = new GameObject("UIClickAudio", typeof(AudioSource));
                    go.transform.SetParent(transform, false);
                    clickSource = go.GetComponent<AudioSource>();
                }
            }

            clickSource.playOnAwake = false;
            clickSource.loop = false;
            clickSource.spatialBlend = 0f;                 // 2D：UI 音不该有距离衰减
            clickSource.ignoreListenerPause = true;        // 暂停时也响
            clickSource.priority = 32;                     // 数值越小优先级越高，UI 音别被挤掉
            return clickSource;
        }
    }

    private void Awake()
    {
        clickSource = Source;
        HookButtons();

        if (logClicks)
            Debug.Log($"[UIClickSound] {name} 已给 {HookedButtonCount} 个按钮挂上点击音（音效={(clickSound != null ? clickSound.name : "未设置")}）", this);
    }

    /// <summary>给这个 Canvas 下所有按钮（含未激活的）挂点击音；重复调用不会重复挂。</summary>
    public void HookButtons()
    {
        var buttons = GetComponentsInChildren<Button>(true);
        HookedButtonCount = 0;

        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null) continue;

            button.onClick.RemoveListener(PlayClick);   // 防止重复挂
            button.onClick.AddListener(PlayClick);
            HookedButtonCount++;
        }
    }

    /// <summary>播一次点击音（所有 UI 按钮的统一回调）。</summary>
    public void PlayClick()
    {
        if (clickSound == null)
        {
            if (logClicks) Debug.LogWarning("[UIClickSound] 没有设置点击音效。", this);
            return;
        }

        AudioSource source = Source;
        source.pitch = Random.Range(pitchRange.x, pitchRange.y);
        source.PlayOneShot(clickSound, volume);

        LastClip = clickSound;
        ClickCount++;

        if (logClicks) Debug.Log($"[UIClickSound] 第 {ClickCount} 声点击音", this);
    }
}
