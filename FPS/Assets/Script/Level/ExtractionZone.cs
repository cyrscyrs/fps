using System;
using UnityEngine;

/// <summary>
/// 撤离点：玩家走进绿色光圈里、在里面待够 requiredTime 秒就撤离成功；
/// 中途走出范围会重新计时。
///
/// 范围判定用的是「和玩家的水平距离」，不依赖触发器：所以这个点不需要碰撞体，
/// 既不会挡子弹也不会挡僵尸。预制体 Assets/Prefabs/ExtractionZone.prefab 里
/// 已经配好绿色光圈（LineRenderer）、光柱（圆柱）和绿色点光源。
/// </summary>
[DisallowMultipleComponent]
public class ExtractionZone : MonoBehaviour
{
    [Header("撤离设置")]
    [SerializeField] private float extractRadius = 5f;       // 撤离范围半径（也就是绿色光圈的大小）
    [SerializeField] private float requiredTime = 10f;       // 需要在范围里待够多少秒
    [SerializeField] private string playerTag = "Player";    // 找玩家用的 Tag
    [SerializeField] private float edgePadding = 0.4f;       // 已经进来之后允许超出一点点，避免站在边缘来回重置

    [Header("绿色光圈")]
    [SerializeField] private LineRenderer ring;              // 地面上的绿圈
    [SerializeField] private Color ringColor = new Color(0.16f, 1f, 0.35f, 1f);
    [SerializeField] private int ringSegments = 64;          // 圆有多圆（顶点数）
    [SerializeField] private float ringWidth = 0.25f;        // 线宽（米）
    [SerializeField] private float ringPulseSpeed = 2f;      // 呼吸速度
    [SerializeField] private float ringPulseAmount = 0.25f;  // 呼吸幅度

    [Header("光柱 / 灯光")]
    [SerializeField] private Transform beam;                 // 光柱（圆柱），会跟着半径自动缩放
    [SerializeField] private float beamHeight = 8f;          // 光柱高度（米）
    [SerializeField, Range(0.2f, 1f)] private float beamWidthRatio = 0.5f;   // 光柱粗细（相对半径，太粗会糊住视野）
    [SerializeField] private Light zoneLight;                // 绿色点光源
    [SerializeField] private float lightIntensity = 1f;      // 灯光基础亮度

    [Header("调试")]
    [SerializeField] private bool logProgress = false;       // 打印撤离进度

    /// <summary>玩家撤离成功时触发（静态事件，成功画面 GameSuccessUI 订阅它）。</summary>
    public static event Action<ExtractionZone> PlayerExtracted;

    /// <summary>播放模式开始时清掉订阅，避免关掉 Domain Reload 之后残留。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        PlayerExtracted = null;
    }

    public float ExtractRadius => extractRadius;
    public float RequiredTime => requiredTime;

    /// <summary>玩家已经在范围里待了多久（走出范围会归零）。</summary>
    public float TimeInside { get; private set; }

    /// <summary>玩家现在是否在撤离范围内。</summary>
    public bool PlayerInside { get; private set; }

    /// <summary>这个点是否已经完成过撤离。</summary>
    public bool Extracted { get; private set; }

    public float RemainingTime => Mathf.Max(0f, requiredTime - TimeInside);
    public float Progress01 => requiredTime <= 0f ? 1f : Mathf.Clamp01(TimeInside / requiredTime);

    private Transform player;
    private bool warnedNoPlayer;

    private void Awake()
    {
        BuildRing();
        ApplyRadius();
    }

    private void Start()
    {
        player = FindPlayer();
    }

    private void Update()
    {
        if (Extracted) return;

        if (player == null) player = FindPlayer();

        PlayerInside = IsPlayerInside();

        if (PlayerInside)
        {
            TimeInside += Time.deltaTime;    // 用缩放时间：游戏暂停（GameOver 的 timeScale = 0）时不会白涨

            if (logProgress)
                Debug.Log($"[ExtractionZone] {name} 撤离中 {TimeInside:0.0}/{requiredTime:0.0}s", this);

            if (TimeInside >= requiredTime) Extract();
        }
        else if (TimeInside > 0f)
        {
            TimeInside = 0f;                 // 走开就重新计时
        }

        UpdateVisual();
    }

    /// <summary>玩家现在算不算在范围内：进来之后给一点额外余量，站在边上抖不会动不动重置。</summary>
    private bool IsPlayerInside()
    {
        if (player == null) return false;

        float limit = extractRadius + (PlayerInside ? edgePadding : 0f);

        Vector3 self = transform.position;
        Vector3 target = player.position;
        self.y = 0f;
        target.y = 0f;

        return (self - target).sqrMagnitude <= limit * limit;
    }

    private void Extract()
    {
        if (Extracted) return;

        Extracted = true;
        TimeInside = requiredTime;
        PlayerInside = false;

        Debug.Log($"[ExtractionZone] 玩家在 {name} 撤离成功", this);
        PlayerExtracted?.Invoke(this);
    }

    // ---------- 视觉 ----------

    /// <summary>按半径把绿圈顶点重新排一圈，并做呼吸效果。</summary>
    private void BuildRing()
    {
        if (ring == null) return;

        // 没给材质的话兜底创建一个（不然 LineRenderer 会渲染成品红色）
        if (ring.sharedMaterial == null) ring.sharedMaterial = CreateRingMaterial();

        ring.loop = true;
        ring.useWorldSpace = false;
        ring.positionCount = ringSegments;
        ring.textureMode = LineTextureMode.Stretch;
        ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ring.receiveShadows = false;

        for (int i = 0; i < ringSegments; i++)
        {
            float angle = (i / (float)ringSegments) * Mathf.PI * 2f;
            ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * extractRadius, 0f, Mathf.Sin(angle) * extractRadius));
        }
    }

    private static Material CreateRingMaterial()
    {
        Shader shader = Shader.Find("Legacy Shaders/Particles/Additive");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");

        return shader != null ? new Material(shader) { name = "ExtractionRing (runtime)" } : null;
    }

    private void ApplyRadius()
    {
        if (beam == null) return;

        // 圆柱默认直径 1 米、高 2 米：横向缩放 = 目标直径，纵向缩放 = 高度 / 2
        float diameter = extractRadius * 2f * beamWidthRatio;
        beam.localScale = new Vector3(diameter, beamHeight * 0.5f, diameter);
        beam.localPosition = new Vector3(0f, beamHeight * 0.5f, 0f);
    }

    /// <summary>绿色光圈呼吸 + 玩家进来时变亮一点。</summary>
    private void UpdateVisual()
    {
        float pulse = 1f + ringPulseAmount * Mathf.Sin(Time.time * ringPulseSpeed);
        float boost = PlayerInside ? 1.35f : 1f;

        if (ring != null)
        {
            ring.startWidth = ringWidth * pulse;
            ring.endWidth = ringWidth * pulse;

            Color color = ringColor;
            color.a = Mathf.Clamp01(ringColor.a * (PlayerInside ? 1f : 0.7f) * pulse);
            ring.startColor = color;
            ring.endColor = color;
        }

        if (zoneLight != null)
            zoneLight.intensity = lightIntensity * boost * pulse;
    }

    private Transform FindPlayer()
    {
        GameObject playerObject = null;

        try
        {
            playerObject = GameObject.FindGameObjectWithTag(playerTag);
        }
        catch (UnityException)
        {
            // 标签不存在时 Unity 会抛异常，这里退回按组件查找
        }

        if (playerObject == null)
        {
            PlayerControll controll = FindObjectOfType<PlayerControll>();
            if (controll != null) playerObject = controll.gameObject;
        }

        if (playerObject == null && !warnedNoPlayer)
        {
            warnedNoPlayer = true;
            Debug.LogWarning($"[ExtractionZone] 没有找到玩家（Tag = {playerTag}），这个撤离点不会触发。", this);
        }

        return playerObject != null ? playerObject.transform : null;
    }

    private void OnValidate()
    {
        extractRadius = Mathf.Max(0.5f, extractRadius);
        requiredTime = Mathf.Max(0.1f, requiredTime);
        ringSegments = Mathf.Max(12, ringSegments);

        if (!Application.isPlaying)
        {
            BuildRing();
            ApplyRadius();
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(ringColor.r, ringColor.g, ringColor.b, 0.6f);
        Gizmos.DrawWireSphere(new Vector3(transform.position.x, transform.position.y + 0.05f, transform.position.z), extractRadius);
    }
}
