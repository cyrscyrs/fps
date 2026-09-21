using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 存档读写：每个存档位一个 JSON 文件，放在 Application.persistentDataPath/save_slot0.json ~ save_slot2.json。
///
/// 三个要点（和单存档时一样）：
/// 1) 带版本号：以后加字段可以把老存档升级上来；
/// 2) 原子写入：先写 .tmp，再重命名覆盖；覆盖前把旧存档留一份 .bak，中途崩溃最多丢一次自动保存；
/// 3) 读取时先校验（版本、关卡、血量），坏了自动回退到 .bak。
///
/// 另外：v1 时代的单存档文件 save.json 会在第一次运行时自动搬到 0 号存档位。
/// </summary>
public static class SaveSystem
{
    /// <summary>当前存档格式版本。加字段/改结构就 +1。</summary>
    public const int CurrentVersion = 2;

    /// <summary>存档位数量（主菜单里最多显示这么多个）。</summary>
    public const int SlotCount = 3;

    /// <summary>主菜单场景在 Build Settings 里的下标（存档不会指向它）。</summary>
    public const int MenuSceneBuildIndex = 0;

    /// <summary>第一关在 Build Settings 里的下标（开新游戏时从这一关开始）。</summary>
    public const int FirstLevelBuildIndex = 1;

    /// <summary>当前正在玩的存档位；-1 = 还没定（比如直接从关卡场景开始游戏）。</summary>
    public static int CurrentSlot { get; private set; } = -1;

    /// <summary>存档目录（排查问题时用）。</summary>
    public static string Folder => Application.persistentDataPath;

    // ---------- 路径 ----------

    public static string SlotPath(int slot) => Path.Combine(Folder, $"save_slot{slot}.json");
    public static string SlotBackupPath(int slot) => SlotPath(slot) + ".bak";
    public static string SlotTempPath(int slot) => SlotPath(slot) + ".tmp";

    /// <summary>v1 的单存档文件（老版本用的），第一次运行会迁移到 0 号存档位。</summary>
    public static string LegacyPath => Path.Combine(Folder, "save.json");

    private static bool migrationChecked;
    private static int _legacyMigratedTo = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSessionState()
    {
        migrationChecked = false;
        _legacyMigratedTo = -1;
        CurrentSlot = -1;
    }

    // ---------- 存档位 ----------

    /// <summary>指定当前在玩哪一个存档位（主菜单点了「开始新游戏 / 继续游戏」时调）。</summary>
    public static void SetCurrentSlot(int slot)
    {
        if (slot < 0 || slot >= SlotCount) return;
        CurrentSlot = slot;
    }

    /// <summary>
    /// 当前存档位。没指定过（比如直接在关卡场景里按 Play）就用「最近保存过的那个」，
    /// 一个存档都没有则用 0 号位。
    /// </summary>
    public static int ResolveCurrentSlot()
    {
        if (CurrentSlot >= 0 && CurrentSlot < SlotCount) return CurrentSlot;

        int newest = -1;
        DateTime newestTime = DateTime.MinValue;

        for (int i = 0; i < SlotCount; i++)
        {
            if (!TryLoadSlot(i, out SaveData data, out _)) continue;

            DateTime time = data.SavedAtLocal;
            if (newest < 0 || time > newestTime)
            {
                newest = i;
                newestTime = time;
            }
        }

        CurrentSlot = newest >= 0 ? newest : 0;
        return CurrentSlot;
    }

    /// <summary>任意一个存档位里有没有存档（主菜单用：都没有就隐藏「继续游戏」）。</summary>
    public static bool HasAnySave
    {
        get
        {
            MigrateLegacySaveOnce();
            for (int i = 0; i < SlotCount; i++)
                if (HasSaveInSlot(i)) return true;

            return false;
        }
    }

    /// <summary>某个存档位有没有存档（主文件或备份存在都算）。</summary>
    public static bool HasSaveInSlot(int slot)
    {
        if (!IsValidSlot(slot)) return false;
        return File.Exists(SlotPath(slot)) || File.Exists(SlotBackupPath(slot));
    }

    // ---------- 写 ----------

    /// <summary>
    /// 原子写入某个存档位：临时文件 -> 备份旧档 -> 重命名覆盖。
    /// 写失败会打 error 并返回 false，不会抛异常打断游戏。
    /// </summary>
    public static bool SaveSlot(int slot, SaveData data)
    {
        if (!IsValidSlot(slot) || data == null) return false;

        data.version = CurrentVersion;
        data.slotIndex = slot;
        data.savedAtUtc = DateTime.UtcNow.ToString("o");

        string path = SlotPath(slot);
        string backup = SlotBackupPath(slot);
        string temp = SlotTempPath(slot);

        try
        {
            if (!Directory.Exists(Folder)) Directory.CreateDirectory(Folder);

            File.WriteAllText(temp, JsonUtility.ToJson(data, true), new UTF8Encoding(false));

            // 覆盖前留一份 .bak：Windows 上「重命名覆盖」不保证原子，靠这份兜底
            if (File.Exists(path))
            {
                if (File.Exists(backup)) File.Delete(backup);
                File.Copy(path, backup, true);
                File.Delete(path);
            }

            File.Move(temp, path);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] 写 {slot} 号存档位失败：{e.Message}\n路径：{path}");
            return false;
        }
    }

    /// <summary>删掉某个存档位（含备份/临时文件）。</summary>
    public static void DeleteSlot(int slot)
    {
        if (!IsValidSlot(slot)) return;

        try
        {
            if (File.Exists(SlotPath(slot))) File.Delete(SlotPath(slot));
            if (File.Exists(SlotBackupPath(slot))) File.Delete(SlotBackupPath(slot));
            if (File.Exists(SlotTempPath(slot))) File.Delete(SlotTempPath(slot));
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] 删 {slot} 号存档位失败：{e.Message}");
        }
    }

    /// <summary>删掉全部存档位（做「清空所有存档」之类的功能时用）。</summary>
    public static void DeleteAllSaves()
    {
        for (int i = 0; i < SlotCount; i++) DeleteSlot(i);
    }

    // ---------- 读 ----------

    /// <summary>
    /// 读某个存档位：主存档读不了（不存在 / 解析失败 / 校验不过）会自动回退到 .bak。
    /// 返回 true 时 data 一定是校验过的可用数据。
    /// </summary>
    public static bool TryLoadSlot(int slot, out SaveData data, out string error)
    {
        data = null;
        error = null;

        if (!IsValidSlot(slot))
        {
            error = $"存档位下标越界：{slot}";
            return false;
        }

        MigrateLegacySaveOnce();

        if (TryLoadFile(slot, SlotPath(slot), out data, out error)) return true;

        if (!File.Exists(SlotBackupPath(slot))) return false;

        string primaryError = error;
        if (TryLoadFile(slot, SlotBackupPath(slot), out data, out string backupError))
        {
            Debug.LogWarning($"[SaveSystem] {slot} 号存档位读取失败（{primaryError}），已用备份继续。");
            error = null;
            return true;
        }

        error = $"{primaryError}；备份也读不了：{backupError}";
        data = null;
        return false;
    }

    private static bool TryLoadFile(int slot, string path, out SaveData data, out string error)
    {
        data = null;
        error = null;

        if (!File.Exists(path))
        {
            error = "存档不存在";
            return false;
        }

        string json;
        try
        {
            json = File.ReadAllText(path, Encoding.UTF8);
        }
        catch (Exception e)
        {
            error = $"读文件失败：{e.Message}";
            return false;
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "存档是空文件";
            return false;
        }

        SaveData parsed;
        try
        {
            parsed = JsonUtility.FromJson<SaveData>(json);
        }
        catch (Exception e)
        {
            error = $"解析 JSON 失败：{e.Message}";
            return false;
        }

        if (parsed == null)
        {
            error = "解析出来是空的";
            return false;
        }

        if (!UpgradeSave(parsed, slot, out error)) return false;
        if (!Validate(parsed, out error)) return false;

        data = parsed;
        return true;
    }

    // ---------- 兼容旧调用（自动用 CurrentSlot） ----------

    /// <summary>当前存档位里有没有存档。</summary>
    public static bool HasSave => HasSaveInSlot(ResolveCurrentSlot());

    /// <summary>把数据写进当前存档位。</summary>
    public static bool Save(SaveData data) => SaveSlot(ResolveCurrentSlot(), data);

    /// <summary>读当前存档位。</summary>
    public static bool TryLoad(out SaveData data, out string error) => TryLoadSlot(ResolveCurrentSlot(), out data, out error);

    /// <summary>删掉当前存档位。</summary>
    public static void Delete() => DeleteSlot(ResolveCurrentSlot());

    // ---------- 版本 / 校验 ----------

    /// <summary>
    /// 版本处理：比当前版本新就拒绝；旧的就一路升级上来。
    /// v1 -> v2：加了「存档位」和「新存档」两个字段，老存档按普通存档处理。
    /// </summary>
    private static bool UpgradeSave(SaveData data, int slot, out string error)
    {
        error = null;

        if (data.version > CurrentVersion)
        {
            error = $"存档版本(v{data.version})比当前游戏(v{CurrentVersion})新，拒绝读取";
            return false;
        }

        if (data.version <= 0) data.version = 1;      // 没有版本号的历史存档当成 v1

        if (data.version == 1)
        {
            data.slotIndex = slot;
            data.isNewGame = false;
            data.version = 2;
        }

        data.slotIndex = slot;
        data.version = CurrentVersion;
        return true;
    }

    /// <summary>读取后的校验：关卡能不能定位到、数字合不合理。</summary>
    private static bool Validate(SaveData data, out string error)
    {
        error = null;

        // 关卡：优先按场景名重新定位（以后在 Build Settings 里插了主菜单/改了顺序也不会读错关）
        int byName = IndexOfSceneNamed(data.levelName);
        if (byName >= 0) data.levelIndex = byName;

        int sceneCount = SceneManager.sceneCountInBuildSettings;
        if (data.levelIndex < 0 || (sceneCount > 0 && data.levelIndex >= sceneCount))
        {
            error = $"关卡下标越界：{data.levelIndex}（Build Settings 里共 {sceneCount} 关）";
            return false;
        }

        if (sceneCount > 0 && data.levelIndex == MenuSceneBuildIndex && byName < 0)
        {
            error = "存档指向了主菜单场景";
            return false;
        }

        if (data.isNewGame) return true;      // 新存档还没进过关，血量留 0 是正常的

        if (float.IsNaN(data.health) || float.IsInfinity(data.health) ||
            float.IsNaN(data.maxHealth) || float.IsInfinity(data.maxHealth))
        {
            error = "血量是 NaN / Infinity";
            return false;
        }

        if (data.maxHealth <= 0f) data.maxHealth = 100f;

        if (data.health <= 0f)
        {
            error = "存档里的血量是 0（这次进度已经结束）";
            return false;
        }

        data.health = Mathf.Clamp(data.health, 1f, data.maxHealth);
        return true;
    }

    private static int IndexOfSceneNamed(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return -1;

        int count = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < count; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            if (string.IsNullOrEmpty(path)) continue;

            if (string.Equals(Path.GetFileNameWithoutExtension(path), sceneName, StringComparison.Ordinal))
                return i;
        }

        return -1;
    }

    private static bool IsValidSlot(int slot) => slot >= 0 && slot < SlotCount;

    /// <summary>把 v1 时代留下的单存档文件搬成 0 号存档位（一次运行只做一次）。</summary>
    private static void MigrateLegacySaveOnce()
    {
        if (migrationChecked) return;
        migrationChecked = true;

        if (!File.Exists(LegacyPath)) return;

        for (int i = 0; i < SlotCount; i++)
        {
            if (File.Exists(SlotPath(i)))
            {
                // 已经有存档位了，老文件直接忽略（不删，留着以防万一）
                Debug.Log("[SaveSystem] 已经有多存档位存档，忽略旧的 save.json");
                return;
            }
        }

        try
        {
            if (TryLoadFile(0, LegacyPath, out SaveData legacy, out string error))
            {
                legacy.slotIndex = 0;
                SaveSlot(0, legacy);
                _legacyMigratedTo = 0;
                Debug.Log($"[SaveSystem] 把旧的单存档迁移到 0 号存档位：{legacy}");
            }
            else
            {
                Debug.LogWarning($"[SaveSystem] 旧存档读不出来（{error}），忽略。");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SaveSystem] 迁移旧存档失败：{e.Message}");
        }
    }

    /// <summary>旧存档被迁移到了哪个存档位（没人用过返回 -1，调试用）。</summary>
    public static int LegacyMigratedSlot => _legacyMigratedTo;
}
