using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 撤离进度提示：玩家站在撤离点里时，屏幕上方显示「撤离中… x.x / 10 秒」和一条进度条；
/// 走出去（计时重置）提示就隐藏。
/// 挂在提示的根物体上，把进度条填充和文字拖进来即可。
/// </summary>
public class ExtractionProgressUI : MonoBehaviour
{
    [Header("界面")]
    [SerializeField] private GameObject panel;        // 提示根物体（默认隐藏）
    [SerializeField] private Image fillImage;         // 进度条填充（Image Type = Filled, Horizontal, Origin Left）
    [SerializeField] private Text progressText;       // 进度文字

    [Header("文案")]
    [SerializeField] private string textFormat = "撤离中… {0:0.0} / {1:0} 秒";

    private ExtractionZone[] zones;

    private void Awake()
    {
        UIFonts.Apply(gameObject);

        if (panel != null) panel.SetActive(false);
    }

    private void Start()
    {
        RefreshZones();
    }

    private void Update()
    {
        ExtractionZone zone = FindActiveZone();

        if (zone == null)
        {
            if (panel != null && panel.activeSelf) panel.SetActive(false);
            return;
        }

        if (panel != null && !panel.activeSelf) panel.SetActive(true);

        if (fillImage != null) fillImage.fillAmount = zone.Progress01;

        if (progressText != null)
        {
            progressText.text = string.Format(textFormat, zone.TimeInside, zone.RequiredTime);
        }
    }

    /// <summary>当前正在撤离的那个点（同时只有一个玩家，取第一个在范围里的）。</summary>
    private ExtractionZone FindActiveZone()
    {
        if (zones == null || zones.Length == 0) RefreshZones();
        if (zones == null) return null;

        for (int i = 0; i < zones.Length; i++)
        {
            if (zones[i] != null && zones[i].PlayerInside) return zones[i];
        }

        return null;
    }

    private void RefreshZones()
    {
        zones = FindObjectsOfType<ExtractionZone>();
    }
}
