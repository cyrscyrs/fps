using System;
using System.Globalization;
using UnityEngine;

/// <summary>
/// 一个存档位里的存档内容：只放「数据」，不放 Unity 对象 / 场景节点（读档时重新构造出来）。
/// 以后要是加字段（比如背包、任务进度），把 SaveSystem.CurrentVersion 加一，
/// 并在 SaveSystem.UpgradeSave 里补一条升级规则，老存档才不会读不了。
/// </summary>
[Serializable]
public class SaveData
{
    /// <summary>存档格式版本号。读取时先看它：比当前版本新就拒绝读，比当前版本旧就升级。</summary>
    public int version = SaveSystem.CurrentVersion;

    /// <summary>存在哪个存档位（0 ~ SaveSystem.SlotCount-1，-1 = 没记录）。</summary>
    public int slotIndex = -1;

    // ---------- 玩家在哪 ----------

    /// <summary>玩家所在的关卡：Build Settings 里的下标。</summary>
    public int levelIndex;

    /// <summary>关卡场景名（读档时用它重新定位关卡，关卡顺序变了也不会读错）。</summary>
    public string levelName;

    // ---------- 玩家状态 ----------

    /// <summary>玩家当前生命值。</summary>
    public float health;

    /// <summary>玩家的最大生命值。</summary>
    public float maxHealth;

    /// <summary>true = 刚在主菜单里创建、还没进过任何关卡的新存档（血量按关卡默认满血给）。</summary>
    public bool isNewGame;

    // ---------- 元数据 ----------

    /// <summary>写入时间（UTC / ISO 8601）。</summary>
    public string savedAtUtc;

    public float HealthPercent => maxHealth > 0f ? Mathf.Clamp01(health / maxHealth) : 0f;

    /// <summary>第几关（给人看的 1 开始编号）。</summary>
    public int LevelNumber => Mathf.Max(1, levelIndex - SaveSystem.FirstLevelBuildIndex + 1);

    /// <summary>存档时间（本地时间），解析不出来就返回 DateTime.MinValue。</summary>
    public DateTime SavedAtLocal
    {
        get
        {
            if (string.IsNullOrEmpty(savedAtUtc)) return DateTime.MinValue;

            return DateTime.TryParse(savedAtUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out DateTime parsed)
                ? parsed.ToLocalTime()
                : DateTime.MinValue;
        }
    }

    /// <summary>存档位列表里显示的一行信息：血量 / 关卡 / 时间。</summary>
    public string ToSlotDescription()
    {
        if (isNewGame) return "新存档 · 还没开始";

        DateTime time = SavedAtLocal;
        string timeText = time == DateTime.MinValue ? "时间未知" : time.ToString("yyyy-MM-dd HH:mm");

        return $"第 {LevelNumber} 关 · 血量 {health:0}/{maxHealth:0}\n{timeText}";
    }

    public override string ToString()
    {
        return $"槽{slotIndex} v{version} 第{levelIndex}关({levelName}) 血量 {health:0.#}/{maxHealth:0.#}"
            + (isNewGame ? " [新存档]" : "") + $" 于 {savedAtUtc}";
    }
}
