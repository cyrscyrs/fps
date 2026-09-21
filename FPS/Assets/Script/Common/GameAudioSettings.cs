using UnityEngine;

/// <summary>
/// 音效音量设置（0~1），用 PlayerPrefs 存，重启游戏也保留。
/// 目前管两路：僵尸叫声、开火枪声。
/// 音量变化时会广播 Changed；玩家在暂停界面拖滑条时，正在试听的声音会立刻跟着变。
/// </summary>
public static class GameAudioSettings
{
    public const string ZombieVolumeKey = "audio_zombie_volume";
    public const string WeaponVolumeKey = "audio_weapon_volume";

    /// <summary>僵尸叫声音量 0~1（默认 1）。</summary>
    public static float ZombieVolume { get; private set; } = 1f;

    /// <summary>开火枪声音量 0~1（默认 1）。</summary>
    public static float WeaponVolume { get; private set; } = 1f;

    /// <summary>音量变化时触发（暂停界面拖滑条会一直触发）。</summary>
    public static event System.Action Changed;

    /// <summary>进入播放模式时从 PlayerPrefs 读一次。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Load()
    {
        ZombieVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(ZombieVolumeKey, 1f));
        WeaponVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(WeaponVolumeKey, 1f));
    }

    /// <summary>设置僵尸叫声音量（拖滑条时调）。</summary>
    public static void SetZombieVolume(float value)
    {
        ZombieVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(ZombieVolumeKey, ZombieVolume);   // 只写内存，Flush() 时才落盘
        Changed?.Invoke();
    }

    /// <summary>设置开火枪声音量。</summary>
    public static void SetWeaponVolume(float value)
    {
        WeaponVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(WeaponVolumeKey, WeaponVolume);
        Changed?.Invoke();
    }

    /// <summary>把设置真正写进磁盘（退出暂停界面 / 返回主界面时调一次就够）。</summary>
    public static void Flush()
    {
        PlayerPrefs.Save();
    }
}
