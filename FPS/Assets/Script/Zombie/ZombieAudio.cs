using UnityEngine;

/// <summary>
/// 僵尸叫声：三种声音共用一个 AudioSource，切换时先把僵尸身上正在播的音效全部停掉，再放新的。
///   - Idle：放 zombie-idle-sound，之后每隔一段时间（只要还在 Idle）再叫一次
///   - 发现玩家（进入 Walk / Attack）：换成 zombie-battle-sound
///   - 死亡：放一次 zombie-dying-sound
/// 挂在僵尸预制体根物体上，Zombie 会在状态切换时自动通知它（不用手动连线）。
/// </summary>
[DisallowMultipleComponent]
public class ZombieAudio : MonoBehaviour
{
    public enum ZombieSound { None, Idle, Battle, Dying }

    [Header("音效文件")]
    [SerializeField] private AudioClip idleSound;        // Assets/Audios/zombie-idle-sound.mp3
    [SerializeField] private AudioClip battleSound;      // Assets/Audios/zombie-battle-sound.mp3
    [SerializeField] private AudioClip dyingSound;       // Assets/Audios/zombie-dying-sound.mp3
    [SerializeField] private AudioSource voiceSource;    // 留空 = 自动用自己身上的 AudioSource

    [Header("Idle 叫声重复")]
    [SerializeField] private float idleInterval = 10f;       // 每隔多少秒再叫一次（音效本身 8.1 秒，别设得比它还短太多）
    [SerializeField] private float idleIntervalRandom = 3f;  // 上下随机浮动，免得一群僵尸同时叫

    [Header("战斗叫声")]
    [SerializeField] private bool loopBattleSound = true;    // 追击过程中循环播战斗叫声

    [Header("随机化")]
    [SerializeField] private Vector2 volumeRange = new Vector2(0.75f, 1f);
    [SerializeField] private Vector2 pitchRange = new Vector2(0.94f, 1.06f);

    [Header("调试")]
    [SerializeField] private bool logSound = false;

    /// <summary>当前应该处于哪种叫声状态（调试 / 测试用）。</summary>
    public ZombieSound CurrentSound { get; private set; } = ZombieSound.None;

    /// <summary>最近一次真正播出去的音效（调试 / 测试用）。</summary>
    public AudioClip LastClip { get; private set; }

    /// <summary>下一次 Idle 叫声的时间点（Time.time）。</summary>
    public float NextIdleTime { get; private set; }

    /// <summary>这具僵尸一共叫过多少次（调试 / 测试用，能看出 Idle 有没有按间隔重复）。</summary>
    public int PlayCount { get; private set; }

    /// <summary>叫声用的 AudioSource 是不是正在播。</summary>
    public bool IsVoicePlaying => Voice != null && Voice.isPlaying;

    /// <summary>叫声用的 AudioSource（自动补齐，不用在 Inspector 里手动挂）。</summary>
    private AudioSource Voice
    {
        get
        {
            if (voiceSource == null)
            {
                voiceSource = GetComponent<AudioSource>();
                if (voiceSource == null) voiceSource = gameObject.AddComponent<AudioSource>();
            }

            voiceSource.playOnAwake = false;
            voiceSource.loop = false;
            return voiceSource;
        }
    }

    private void Awake()
    {
        voiceSource = Voice;   // 提前把 AudioSource 准备好

        if (logSound)
            Debug.Log($"[ZombieAudio] {name} 就绪：idle={NameOf(idleSound)} battle={NameOf(battleSound)} dying={NameOf(dyingSound)}", this);
    }

    private void OnDisable()
    {
        // 被对象池收起来 / 物体关掉时，声音一起停
        StopAllSounds();
        CurrentSound = ZombieSound.None;
    }

    private void Update()
    {
        if (CurrentSound != ZombieSound.Idle) return;

        // 还在 Idle，到点了就再叫一声
        if (Time.time < NextIdleTime) return;

        PlayClip(idleSound, false);
        ScheduleNextIdle();
    }

    /// <summary>状态机切换时由 Zombie.NotifyStateEntered 调用。</summary>
    public void OnStateEntered(ZombieState state)
    {
        if (state is ZombieDeathState) { PlayDying(); return; }
        if (state is ZombieIdleState) { PlayIdle(); return; }
        if (state is ZombieWalkState || state is ZombieAttackState) { PlayBattle(); return; }
    }

    // ---------- 三种叫声 ----------

    /// <summary>Idle：停掉其它音效，放一声 idle 叫声，之后每隔一段时间（仍在 Idle）再叫。</summary>
    public void PlayIdle()
    {
        StopAllSounds();

        CurrentSound = ZombieSound.Idle;
        PlayClip(idleSound, false);
        ScheduleNextIdle();
    }

    /// <summary>发现玩家：停掉其它音效，换成战斗叫声。</summary>
    public void PlayBattle()
    {
        StopAllSounds();

        CurrentSound = ZombieSound.Battle;
        PlayClip(battleSound, loopBattleSound);
    }

    /// <summary>死亡：停掉其它音效，放一次死亡叫声。</summary>
    public void PlayDying()
    {
        StopAllSounds();

        CurrentSound = ZombieSound.Dying;
        PlayClip(dyingSound, false);
    }

    /// <summary>停掉这个僵尸身上所有正在播的音效（含子物体上的）。</summary>
    public void StopAllSounds()
    {
        var all = GetComponentsInChildren<AudioSource>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null) continue;
            all[i].Stop();
        }
    }

    // ---------- 内部 ----------

    private void PlayClip(AudioClip clip, bool loop)
    {
        if (clip == null) return;

        AudioSource source = Voice;
        source.clip = clip;
        source.loop = loop;
        source.volume = Random.Range(volumeRange.x, volumeRange.y) * GameAudioSettings.ZombieVolume;   // 再乘上暂停界面里的音量设置
        source.pitch = Random.Range(pitchRange.x, pitchRange.y);
        source.Play();

        LastClip = clip;
        PlayCount++;

        if (logSound) Debug.Log($"[ZombieAudio] {name} 播放 {clip.name}（循环={loop}）", this);
    }

    private void ScheduleNextIdle()
    {
        float offset = Random.Range(-idleIntervalRandom, idleIntervalRandom);
        NextIdleTime = Time.time + Mathf.Max(1f, idleInterval + offset);
    }

    private static string NameOf(AudioClip clip)
    {
        return clip != null ? clip.name : "(未设置)";
    }
}
