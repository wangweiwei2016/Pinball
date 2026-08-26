using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 球路径控制器：可控假物理碰撞。
/// 核心思路：球在上半部分自由弹跳（自然物理反弹），
/// 只有当球下降到目标槽附近区域时，才对反弹方向做轻微角度偏转，
/// 让球看起来仍在自然弹跳，但最终落入指定槽。
/// </summary>
public class BallPathController : MonoBehaviour
{
    [Header("奖品槽布局")]
    [Tooltip("各槽中心 X 坐标（由 PinballSetup 自动填充）。")]
    public float[] slotCenterXs = new float[0];

    [Tooltip("奖品槽区域中心 Y（由 PinballSetup 自动填充）。")]
    public float[] slotCenterYs = new float[0];

    [Header("引导区域")]
    [Tooltip("引导介入的最高 Y 阈值（球 Y 低于此值时才开始引导）。")]
    public float steerTopY = -2f;

    [Tooltip("引导介入的最低 Y 阈值（球 Y 高于此值时停止引导，避免过度偏转）。")]
    public float steerBottomY = -6.5f;

    [Header("控制强度")]
    [Tooltip("基础最大偏转角（度），碰撞时从自然反弹方向向目标方向偏转的最大角度。")]
    public float baseMaxAngle = 8f;

    [Tooltip("靠近目标槽时的最大偏转角（度）。")]
    public float maxAngle = 25f;

    [Tooltip("引导随距离衰减系数。")]
    public float steerFalloff = 1.5f;

    [Tooltip("球下落阶段额外引导强度。")]
    public float descentSteerBoost = 1.5f;

    [Tooltip("水平引导最大速度（单位/秒），用于 FixedUpdate 微调。")]
    public float maxSteerSpeed = 4f;

    [Tooltip("FixedUpdate 引导插值因子（越小越柔和）。")]
    public float steerLerpFactor = 0.04f;

    [Tooltip("是否启用实时引导。回放期间会被关闭，让球完全按预录轨迹运动。")]
    public bool steeringEnabled = true;

    /// <summary>目标槽索引（-1 = 随机）。</summary>
    public int targetSlotIndex { get; private set; } = -1;

    /// <summary>目标槽的 X 坐标。</summary>
    public float targetX { get; private set; } = 0f;

    /// <summary>目标槽的 Y 坐标。</summary>
    public float targetY { get; private set; } = 0f;

    private Ball ball;
    private Rigidbody2D rb;

    private void Awake()
    {
        ball = GetComponent<Ball>();
        rb = GetComponent<Rigidbody2D>();
    }

    /// <summary>
    /// 设置目标槽。传 -1 则随机选一个。
    /// </summary>
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

    /// <summary>
    /// 清除目标槽（球自由弹跳）。
    /// </summary>
    public void ClearTarget()
    {
        targetSlotIndex = -1;
    }

    /// <summary>
    /// 根据槽索引获取槽中心 X 坐标。
    /// </summary>
    public float GetSlotCenterX(int index)
    {
        if (slotCenterXs != null && index >= 0 && index < slotCenterXs.Length)
        {
            return slotCenterXs[index];
        }
        return 0f;
    }

    /// <summary>
    /// 根据槽索引获取槽中心 Y 坐标。
    /// </summary>
    public float GetSlotCenterY(int index)
    {
        if (slotCenterYs != null && index >= 0 && index < slotCenterYs.Length)
        {
            return slotCenterYs[index];
        }
        return 0f;
    }

    /// <summary>
    /// 球是否处于引导介入区域内。
    /// </summary>
    public bool IsInSteeringZone(float y)
    {
        return y <= steerTopY && y >= steerBottomY;
    }

    /// <summary>
    /// 撞击时调用：计算经引导修正后的反弹方向（角度偏移法，更自然）。
    /// 只在球处于引导区域内时才介入，否则保持自然反弹。
    /// </summary>
    public Vector2 ModifyBounceDirection(Vector2 naturalBounceDir, Vector2 collisionPoint)
    {
        if (targetSlotIndex < 0) return naturalBounceDir;
        if (!IsInSteeringZone(collisionPoint.y)) return naturalBounceDir;

        Vector2 toTarget = new Vector2(targetX - collisionPoint.x, targetY - collisionPoint.y);
        float dist = toTarget.magnitude;
        if (dist < 0.01f) return naturalBounceDir;

        // 计算自然反弹方向与目标方向之间的角度差
        float naturalAngle = Mathf.Atan2(naturalBounceDir.y, naturalBounceDir.x);
        float targetAngle = Mathf.Atan2(toTarget.y, toTarget.x);
        float angleDiff = Mathf.DeltaAngle(naturalAngle * Mathf.Rad2Deg, targetAngle * Mathf.Rad2Deg);

        // 根据距离计算最大允许偏转角
        float t = Mathf.Clamp01(1f - dist / 10f);
        float maxAngleDeg = Mathf.Lerp(baseMaxAngle, maxAngle, t * steerFalloff);

        // 下落阶段额外增强
        if (rb != null && rb.velocity.y < 0f)
        {
            maxAngleDeg = Mathf.Min(maxAngle * 1.5f, maxAngleDeg * descentSteerBoost);
        }

        // 限制实际偏转不超过最大角度
        float actualOffset = Mathf.Clamp(angleDiff, -maxAngleDeg, maxAngleDeg);

        float resultAngle = naturalAngle + actualOffset * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(resultAngle), Mathf.Sin(resultAngle));
    }

    /// <summary>
    /// 撞击时调用：计算经引导修正后的反弹速度。
    /// </summary>
    public Vector2 ModifyBounceVelocity(Vector2 naturalVelocity, Vector2 collisionPoint)
    {
        if (!steeringEnabled || targetSlotIndex < 0) return naturalVelocity;

        Vector2 naturalDir = naturalVelocity.normalized;
        float speed = naturalVelocity.magnitude;

        Vector2 modifiedDir = ModifyBounceDirection(naturalDir, collisionPoint);
        return modifiedDir * speed;
    }

    private void FixedUpdate()
    {
        if (!steeringEnabled) return;
        if (targetSlotIndex < 0 || rb == null) return;

        // 只有球进入引导区域且处于下降阶段时才介入
        if (!IsInSteeringZone(rb.position.y)) return;
        if (rb.velocity.y >= 0f) return;

        float dist = targetX - rb.position.x;
        if (Mathf.Abs(dist) < 0.05f) return;

        float strength = Mathf.Clamp01(1f - Mathf.Abs(dist) / 10f);
        strength = Mathf.Lerp(baseMaxAngle / maxAngle, 1f, strength * steerFalloff);

        float targetVX = Mathf.Sign(dist) * maxSteerSpeed * strength;
        float newVX = Mathf.Lerp(rb.velocity.x, targetVX, steerLerpFactor);

        rb.velocity = new Vector2(newVX, rb.velocity.y);
    }
}
