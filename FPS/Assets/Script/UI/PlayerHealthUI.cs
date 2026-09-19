using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家血条 UI：屏幕左下角血条 + 中间显示「当前血量 / 最大血量」。
/// 颜色按血量比例走一条渐变：满血绿色 -> 半血橙黄 -> 空血红。
/// 挂在血条根物体上，把血条图片和文字拖进来即可。
/// </summary>
public class PlayerHealthUI : MonoBehaviour
{
    [Header("数据来源")]
    [SerializeField] private Health target;                 // 留空则自动找 Tag 为 Player 的对象

    [Header("显示")]
    [SerializeField] private Image fillImage;               // 血条填充（Image Type = Filled, Horizontal, Origin Left）
    [SerializeField] private Text healthText;               // 中间的文字：当前/最大
    [SerializeField] private string textFormat = "{0} / {1}";

    [Header("颜色")]
    [SerializeField] private bool useHealthColor = true;     // 关掉就用填充图片自身的颜色
    [SerializeField] private Gradient healthGradient;        // 从左到右：0 = 空血(红)，1 = 满血(绿)

    private void Awake()
    {
        UIFonts.Apply(gameObject);   // 换成支持中文的系统字体

        if (healthGradient == null) healthGradient = CreateDefaultGradient();
    }

    private void Start()
    {
        if (target == null) target = FindPlayerHealth();
        if (target != null)
        {
            target.Damaged += OnDamaged;
            target.Died += OnDied;
        }

        Refresh();
    }

    private void OnDestroy()
    {
        if (target != null)
        {
            target.Damaged -= OnDamaged;
            target.Died -= OnDied;
        }
    }

    private void OnDamaged(float damage, GameObject attacker)
    {
        Refresh();
    }

    private void OnDied()
    {
        Refresh();
    }

    /// <summary>刷新血条长度、颜色和文字（也可以外部手动调用）。</summary>
    public void Refresh()
    {
        if (target == null) return;

        float percent = target.HealthPercent;

        if (fillImage != null)
        {
            fillImage.fillAmount = percent;                        // 长度按比例

            if (useHealthColor && healthGradient != null && fillImage.sprite != null)
                fillImage.color = healthGradient.Evaluate(percent); // 颜色按比例
        }

        if (healthText != null)
        {
            int current = Mathf.CeilToInt(target.CurrentHealth);
            int max = Mathf.CeilToInt(target.MaxHealth);
            healthText.text = string.Format(textFormat, current, max);
        }
    }

    /// <summary>默认渐变：空血红 -> 半血橙黄 -> 满血绿。</summary>
    public static Gradient CreateDefaultGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.90f, 0.13f, 0.13f), 0f),    // 0% 红
                new GradientColorKey(new Color(0.95f, 0.62f, 0.10f), 0.5f),  // 50% 橙黄
                new GradientColorKey(new Color(0.22f, 0.85f, 0.28f), 1f)     // 100% 绿
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            });
        return gradient;
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
