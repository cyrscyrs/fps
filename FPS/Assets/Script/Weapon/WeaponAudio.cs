using UnityEngine;

/// <summary>
/// 开火音效：玩家每扣一次扳机（Weapon 里生成子弹那一瞬间）放一次 single-gun-shot-sound。
/// 用 PlayOneShot 播放，所以连发时上一声不会被掐断，而是叠着响
/// （子弹间隔 0.1 秒、音效 0.38 秒，最多同时叠 4 声）。
/// 挂在和 Weapon 同一个物体上（玩家预制体的根物体 Player）。
/// </summary>
[DisallowMultipleComponent]
public class WeaponAudio : MonoBehaviour
{
    [Header("音效")]
    [SerializeField] private AudioClip shotSound;      // Assets/Audios/single-gun-shot-sound.mp3
    [SerializeField] private AudioSource shotSource;   // 留空 = 自动用自己身上的 AudioSource（没有就建一个）

    [Header("随机化")]
    [SerializeField] private Vector2 volumeRange = new Vector2(0.85f, 1f);   // 每枪音量稍微随机，听起来不那么机械
    [SerializeField] private Vector2 pitchRange = new Vector2(0.96f, 1.04f); // 音调随机

    [Header("调试")]
    [SerializeField] private bool logShots = false;

    /// <summary>一共开过多少枪（调试 / 测试用）。</summary>
    public int ShotCount { get; private set; }

    /// <summary>最近一次播放的枪声音效。</summary>
    public AudioClip LastClip { get; private set; }

    /// <summary>现在有没有枪声在响。</summary>
    public bool IsPlaying => Source != null && Source.isPlaying;

    /// <summary>枪声用的 AudioSource（自动补齐，不用手动挂）。</summary>
    private AudioSource Source
    {
        get
        {
            if (shotSource == null)
            {
                shotSource = GetComponent<AudioSource>();
                if (shotSource == null) shotSource = gameObject.AddComponent<AudioSource>();
            }

            shotSource.playOnAwake = false;
            return shotSource;
        }
    }

    private void Awake()
    {
        shotSource = Source;   // 提前准备好 AudioSource
    }

    /// <summary>开一枪：放一声枪响（可以叠加）。Weapon 每次生成子弹后调用。</summary>
    public void PlayShot()
    {
        if (shotSound == null)
        {
            if (logShots) Debug.LogWarning("[WeaponAudio] 没有设置枪声音效。", this);
            return;
        }

        AudioSource source = Source;
        float volume = Random.Range(volumeRange.x, volumeRange.y) * GameAudioSettings.WeaponVolume;   // 再乘上暂停界面里的音量设置

        source.pitch = Random.Range(pitchRange.x, pitchRange.y);
        source.PlayOneShot(shotSound, volume);

        LastClip = shotSound;
        ShotCount++;

        if (logShots) Debug.Log($"[WeaponAudio] 第 {ShotCount} 声枪响 {shotSound.name}（音量 {volume:0.00}）", this);
    }
}
