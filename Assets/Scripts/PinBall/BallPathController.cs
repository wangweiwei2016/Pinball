using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 球路径控制器：可控假物理碰撞。
/// 分阶段引导策略：
///   - 上半部分（y > freePlayTopY）：完全自由弹跳，不干预
///   - 中段（midGuideTopY ~ freePlayTopY）：轻微角度偏转 + 小幅水平微调
///   - 下段（slotY 附近）：强引导，直接设置水平速度向目标靠拢
/// 这样球在上半场看起来自然弹跳，下半场才逐渐偏向目标。
/// </summary>
public class BallPathController : MonoBehaviour
{
    [Header("奖品槽布局")] [Tooltip("各槽中心 X 坐标（由 PinballSetup 自动填充）。")]
    public float[] slotCenterXs = new float[0];

    [Tooltip("奖品槽区域中心 Y（由 PinballSetup 自动填充）。")]
    public float[] slotCenterYs = new float[0];

    [Header("引导区域（分阶段）")] [Tooltip("自由弹跳区上界：球 Y 高于此值时不引导（上半部分完全自然）。")]
    public float freePlayTopY = 0f;

    [Tooltip("中段引导区上界：球 Y 在 [midGuideTopY, freePlayTopY] 之间时轻微引导。")]
    public float midGuideTopY = -2.5f;

    [Tooltip("强引导区下界：球 Y 低于此值时进入强引导阶段（直逼目标）。")]
    public float strongGuideY = -5f;

    [Header("中段引导强度（轻微偏转）")] [Tooltip("中段反弹方向最大偏转角（度）。")]
    public float midMaxAngle = 12f;

    [Tooltip("中段 FixedUpdate 水平微调最大速度。")] public float midSteerSpeed = 3f;

    [Tooltip("中段插值因子。")] public float midLerpFactor = 0.08f;

    [Header("强引导强度（直逼目标）")] [Tooltip("强引导阶段反弹方向最大偏转角（度）。")]
    public float strongMaxAngle = 40f;

    [Tooltip("强引导阶段水平速度上限。")] public float strongSteerSpeed = 10f;

    [Tooltip("强引导插值因子（越大越快到位）。")] public float strongLerpFactor = 0.2f;

    [Header("通用")] [Tooltip("是否启用实时引导。回放期间会被关闭。")]
    public bool steeringEnabled = true;

    /// <summary>目标槽索引（-1 = 随机）。</summary>
    public int targetSlotIndex { get; private set; } = -1;

    /// <summary>目标槽的 X 坐标。</summary>
    public float targetX { get; private set; } = 0f;

    /// <summary>目标槽的 Y 坐标。</summary>
    public float targetY { get; private set; } = 0f;

    private Ball ball;
    private Rigidbody2D rb;

    /// <summary>最近一次撞墙后的自然反弹速度大小，作为强引导作用力的上限。</summary>
    private float lastBounceSpeed = 0f;

    private void Awake()
    {
        ball = GetComponent<Ball>();
        rb = GetComponent<Rigidbody2D>();
    }

    /// <summary>设置目标槽。传 -1 则随机选一个。</summary>
    public void SetTargetSlot(int index)
    {
        if (slotCenterXs == null || slotCenterXs.Length == 0) return;

        if (index < 0 || index >= slotCenterXs.Length)
        {
            index = Random.Range(0, slotCenterXs.Length);
        }

        targetSlotIndex = index;
        targetX = GetSlotCenterX(index);
        targetY = GetSlotCenterY(index);
    }

    /// <summary>清除目标槽（球自由弹跳）。</summary>
    public void ClearTarget()
    {
        targetSlotIndex = -1;
    }

    public float GetSlotCenterX(int index)
    {
        if (slotCenterXs != null && index >= 0 && index < slotCenterXs.Length)
            return slotCenterXs[index];
        return 0f;
    }

    public float GetSlotCenterY(int index)
    {
        if (slotCenterYs != null && index >= 0 && index < slotCenterYs.Length)
            return slotCenterYs[index];
        return 0f;
    }

    /// <summary>判断当前处于哪个引导阶段：0=自由，1=中段，2=强引导。</summary>
    public int GetGuideStage(float y)
    {
        if (y > freePlayTopY) return 0;
        if (y > strongGuideY) return 1;
        return 2;
    }

    /// <summary>
    /// 撞击时调用：计算经引导修正后的反弹方向。
    /// 自由阶段保持自然反弹，中段轻微偏转，强引导阶段大幅偏转向目标。
    /// </summary>
    public Vector2 ModifyBounceDirection(Vector2 naturalBounceDir, Vector2 collisionPoint)
    {
        if (targetSlotIndex < 0) return naturalBounceDir;

        int stage = GetGuideStage(collisionPoint.y);
        if (stage == 0) return naturalBounceDir; // 自由阶段不干预

        Vector2 toTarget = new Vector2(targetX - collisionPoint.x, targetY - collisionPoint.y);
        float dist = toTarget.magnitude;
        if (dist < 0.01f) return naturalBounceDir;

        float naturalAngle = Mathf.Atan2(naturalBounceDir.y, naturalBounceDir.x);
        float targetAngle = Mathf.Atan2(toTarget.y, toTarget.x);
        float angleDiff = Mathf.DeltaAngle(naturalAngle * Mathf.Rad2Deg, targetAngle * Mathf.Rad2Deg);

        // 根据阶段和距离计算最大偏转角
        float maxAngleDeg;
        if (stage == 1)
        {
            // 中段：距离越近偏转越大
            float t = Mathf.Clamp01(1f - dist / 8f);
            maxAngleDeg = Mathf.Lerp(midMaxAngle * 0.3f, midMaxAngle, t);
        }
        else
        {
            // 强引导：距离越近偏转越大，且整体更强
            float t = Mathf.Clamp01(1f - dist / 5f);
            maxAngleDeg = Mathf.Lerp(strongMaxAngle * 0.5f, strongMaxAngle, t);
        }

        // 下落阶段额外增强
        if (rb != null && rb.velocity.y < 0f && stage == 2)
        {
            maxAngleDeg = Mathf.Min(60f, maxAngleDeg * 1.3f);
        }

        float actualOffset = Mathf.Clamp(angleDiff, -maxAngleDeg, maxAngleDeg);
        float resultAngle = naturalAngle + actualOffset * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(resultAngle), Mathf.Sin(resultAngle));
    }

    /// <summary>撞击时调用：计算经引导修正后的反弹速度。</summary>
    public Vector2 ModifyBounceVelocity(Vector2 naturalVelocity, Vector2 collisionPoint)
    {
        if (!steeringEnabled || targetSlotIndex < 0) return naturalVelocity;

        // 约束：球在右侧发射区内时不干预反弹（速度不受影响）
        if (ball != null && ball.IsInLaunchZone) return naturalVelocity;

        // 缓存反弹速度大小，作为强引导区域作用力的上限
        lastBounceSpeed = naturalVelocity.magnitude;

        Vector2 naturalDir = naturalVelocity.normalized;
        float speed = naturalVelocity.magnitude;

        Vector2 modifiedDir = ModifyBounceDirection(naturalDir, collisionPoint);
        return modifiedDir * speed;
    }

    private void FixedUpdate()
    {
        if (!steeringEnabled) return;
        if (targetSlotIndex < 0 || rb == null) return;
        if (null != ball && ball.IsLocked) return;

        // 约束：球在右侧发射区内时不引导（速度不受影响）
        if (ball != null && ball.IsInLaunchZone) return;

        float y = rb.position.y;
        int stage = GetGuideStage(y);
        if (stage == 0) return; // 自由阶段不干预

        float dist = targetX - rb.position.x;
        if (Mathf.Abs(dist) < 0.05f) return;

        // 上升阶段力度减半（避免把球往上推的观感）
        bool isDescending = rb.velocity.y < 0f;
        float verticalFactor = isDescending ? 1f : 0.5f;

        if (stage == 1)
        {
            // 中段：轻微水平微调
            float strength = Mathf.Clamp01(1f - Mathf.Abs(dist) / 8f);
            float targetVX = Mathf.Sign(dist) * midSteerSpeed * strength * verticalFactor;
            float newVX = Mathf.Lerp(rb.velocity.x, targetVX, midLerpFactor);
            rb.velocity = new Vector2(newVX, rb.velocity.y);
        }
        else
        {
            // 强引导：直接拉向目标
            // 关键约束：引导作用力（目标水平速度）不得超过最近一次撞墙的反弹力大小，
            // 否则球会被"凭空加速"破坏自然弹跳观感。
            float bounceSpeed = lastBounceSpeed > 0.01f
                ? lastBounceSpeed
                : rb.velocity.magnitude; // 退化：尚未碰撞时用当前速度
            float maxSteerSpeed = Mathf.Min(strongSteerSpeed, bounceSpeed);

            float strength = Mathf.Clamp01(1f - Mathf.Abs(dist) / 4f);
            float targetVX = Mathf.Sign(dist) * maxSteerSpeed * strength * verticalFactor;
            float newVX = Mathf.Lerp(rb.velocity.x, targetVX, strongLerpFactor);
            rb.velocity = new Vector2(newVX, rb.velocity.y);
        }
    }
}