using UnityEngine;

/// <summary>
/// 球路径控制器：可控假物理碰撞。
/// 核心思路：先确定球落入哪个奖品槽，然后通过在每次撞击时微调反弹方向，
/// 逐步将球引导至目标槽。球看起来仍在自然弹跳，但结果可控。
/// </summary>
public class BallPathController : MonoBehaviour
{
    [Header("奖品槽布局")]
    [Tooltip("各槽中心 X 坐标（由 PinballSetup 自动填充）。")]
    public float[] slotCenterXs = new float[0];

    [Tooltip("奖品槽区域中心 Y（由 PinballSetup 自动填充）。")]
    public float slotAreaCenterY = -5.5f;

    [Header("控制强度")]
    [Tooltip("基础引导强度（0~1），撞击时方向混入目标方向的比例。")]
    public float baseSteerStrength = 0.3f;

    [Tooltip("最大引导强度，越靠近目标槽越强。")]
    public float maxSteerStrength = 0.7f;

    [Tooltip("引导随距离衰减系数。")]
    public float steerFalloff = 1.5f;

    [Tooltip("球下落阶段额外引导强度。")]
    public float descentSteerBoost = 1.5f;

    /// <summary>目标槽索引（-1 = 随机）。</summary>
    public int targetSlotIndex { get; private set; } = -1;

    /// <summary>目标槽的 X 坐标。</summary>
    public float targetX { get; private set; } = 0f;

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
    /// 撞击时调用：计算经引导修正后的反弹方向。
    /// </summary>
    public Vector2 ModifyBounceDirection(Vector2 naturalBounceDir, Vector2 collisionPoint)
    {
        if (targetSlotIndex < 0) return naturalBounceDir;

        Vector2 toTarget = new Vector2(targetX - collisionPoint.x, slotAreaCenterY - collisionPoint.y);
        float dist = toTarget.magnitude;
        if (dist < 0.01f) return naturalBounceDir;

        toTarget.Normalize();

        float t = Mathf.Clamp01(1f - dist / 10f);
        float strength = Mathf.Lerp(baseSteerStrength, maxSteerStrength, t * steerFalloff);

        if (rb != null && rb.velocity.y < 0f)
        {
            strength = Mathf.Min(maxSteerStrength, strength * descentSteerBoost);
        }

        Vector2 result = Vector2.Lerp(naturalBounceDir, toTarget, strength);
        result.Normalize();
        return result;
    }

    /// <summary>
    /// 撞击时调用：计算经引导修正后的反弹速度。
    /// </summary>
    public Vector2 ModifyBounceVelocity(Vector2 naturalVelocity, Vector2 collisionPoint)
    {
        if (targetSlotIndex < 0) return naturalVelocity;

        Vector2 naturalDir = naturalVelocity.normalized;
        float speed = naturalVelocity.magnitude;

        Vector2 modifiedDir = ModifyBounceDirection(naturalDir, collisionPoint);
        return modifiedDir * speed;
    }

    private void FixedUpdate()
    {
        if (targetSlotIndex >= 0 && rb != null && rb.velocity.y < -1f)
        {
            float dist = Mathf.Abs(targetX - rb.position.x);
            if (dist > 0.05f)
            {
                float steerForce = Mathf.Min(3f, dist * 2f);
                Vector2 steerDir = new Vector2(targetX - rb.position.x, 0f).normalized;
                rb.AddForce(steerDir * steerForce, ForceMode2D.Force);
            }
        }
    }
}
