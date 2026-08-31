using UnityEngine;
using DefaultNamespace;

/// <summary>
/// 弹珠：处理碰撞反弹、速度限制、假物理可控反弹、奖品槽判定。
/// 要求同一 GameObject 上挂载 Rigidbody2D 与 CircleCollider2D。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class Ball : MonoBehaviour
{
    [Header("速度限制")]
    [Tooltip("球的最大速度（单位/秒），防止过快穿透。")]
    public float maxSpeed = 28f;

    [Tooltip("球的最小速度，避免球停滞。")]
    public float minSpeed = 3f;

    [Header("球路修正")]
    [Tooltip("水平速度接近 0 时给球的微小水平推力，防止一直垂直弹跳。")]
    public float nudgeHorizontalForce = 0.3f;

    [Header("初始状态")]
    [Tooltip("是否处于锁定（静止）状态，等待发球器释放。")]
    [SerializeField] private bool locked = true;

    [Header("发射区参数（由 PinballSetup 注入）")]
    [Tooltip("发射通道左墙 x 坐标，球 x 大于此值且 y 低于顶部时视为在发射区内。")]
    public float launchZoneLeftX = 3f;

    [Tooltip("发射通道顶部 y 坐标（导流板高度），球 y 低于此值且 x 在通道内时视为在发射区内。")]
    public float launchZoneTopY = 5.3f;

    private Rigidbody2D rb;
    private Vector2 spawnPosition;
    private BallPathController pathController;
    private TrajectoryPlayer trajectoryPlayer;
    private TrajectoryRecorder trajectoryRecorder;
    private bool inSlot = false;

    // 下落起点跟踪：用于约束反弹高度不得超过下落起点
    private float yDescendStart = float.MinValue;
    private float prevVelocityY = 0f;

    /// <summary>球是否处于锁定静止状态。</summary>
    public bool IsLocked => locked;

    /// <summary>球是否还在右侧发射区内（速度不受约束、不被引导）。</summary>
    public bool IsInLaunchZone
    {
        get
        {
            if (rb == null) return false;
            return rb.position.x > launchZoneLeftX && rb.position.y < launchZoneTopY;
        }
    }

    /// <summary>释放球，使其进入正常物理运动状态。</summary>
    public void Unlock()
    {
        locked = false;
    }

    /// <summary>重新锁定球，清零速度并复位到生成点。</summary>
    public void LockAndReset()
    {
        locked = true;
        inSlot = false;
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
        transform.position = spawnPosition;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.freezeRotation = true;
        spawnPosition = transform.position;
        pathController = GetComponent<BallPathController>();
        trajectoryPlayer = GetComponent<TrajectoryPlayer>();
        trajectoryRecorder = GetComponent<TrajectoryRecorder>();
    }

    private void FixedUpdate()
    {
        if (locked)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
            return;
        }
        // 回放期间由 TrajectoryPlayer 驱动位置，跳过真实物理修正
        if (trajectoryPlayer != null && trajectoryPlayer.IsPlaying) return;
        ClampSpeed();
        PreventStall();

        // 检测开始下落：速度 y 从 >=0 变为 <0 时记录起点高度，
        // 用于约束反弹高度不得超过下落起点（能量守恒式约束）。
        if (prevVelocityY >= 0f && rb.velocity.y < 0f)
        {
            yDescendStart = rb.position.y;
        }
        prevVelocityY = rb.velocity.y;
    }

    private void ClampSpeed()
    {
        Vector2 v = rb.velocity;
        float sqr = v.sqrMagnitude;
        if (sqr > maxSpeed * maxSpeed)
        {
            rb.velocity = v.normalized * maxSpeed;
        }
        else if (sqr > 0f && sqr < minSpeed * minSpeed)
        {
            rb.velocity = v.normalized * minSpeed;
        }
    }

    private void PreventStall()
    {
        if (pathController != null && pathController.targetSlotIndex >= 0) return;
        if (Mathf.Abs(rb.velocity.x) < 0.05f)
        {
            float dir = Random.value < 0.5f ? -1f : 1f;
            rb.AddForce(new Vector2(dir * nudgeHorizontalForce, 0f), ForceMode2D.Impulse);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (locked) return;
        // 回放期间球为运动学体，不参与真实碰撞反弹
        if (trajectoryPlayer != null && trajectoryPlayer.IsPlaying) return;

        bool hasPathControl = pathController != null && pathController.steeringEnabled && pathController.targetSlotIndex >= 0;

        var bumper = collision.collider.GetComponent<Bumper>();

        if (bumper != null)
        {
            // 撞击器碰撞
            if (bumper.isControllable && hasPathControl)
            {
                // 可控撞击器：路径控制器介入反弹方向
                ApplyControlledBounce(collision, collision.collider.transform.position);
            }
            // 非可控撞击器：Bumper.OnCollisionEnter2D 已处理反弹速度
            // 此处不再干预

            // 录制模式下通知特殊撞击器碰撞
            if (trajectoryRecorder != null && trajectoryRecorder.IsRecording && collision.collider.CompareTag(Const.TAG_SPBUMPER))
            {
                trajectoryRecorder.NotifySpecialHit(collision.collider.name);
            }
        }
        else if (hasPathControl)
        {
            // 墙体等非撞击器碰撞：也应用路径控制
            ApplyControlledBounce(collision, collision.collider.transform.position);
        }
    }

    private void ApplyControlledBounce(Collision2D collision, Vector2 colliderPos)
    {
        ContactPoint2D contact = collision.GetContact(0);
        Vector2 collisionPoint = contact.point;

        float separation = 0.05f;
        transform.position = (Vector2)transform.position + contact.normal * separation;

        // 约束：发射区内速度不受影响，用自然反弹直接返回
        if (IsInLaunchZone)
        {
            return;
        }

        // rb.velocity 已经是物理引擎处理完碰撞后的自然反弹速度
        Vector2 naturalBounce = rb.velocity;

        // 约束 1：反弹力 ≤ 重力（在送入路径控制器前封顶，
        // 这样路径控制器缓存的 lastBounceSpeed 也是封顶后的值，
        // 强引导封顶会自动传递）
        naturalBounce = CapVelocityToGravity(naturalBounce);

        // 约束 2：反弹高度 ≤ 下落起点高度（仅当球处于下落状态时生效）
        naturalBounce = CapBounceHeightByDescend(naturalBounce, collisionPoint);

        Vector2 controlledBounce = pathController.ModifyBounceVelocity(naturalBounce, collisionPoint);

        rb.velocity = controlledBounce;
    }

    /// <summary>
    /// 将速度大小封顶到重力加速度大小（约束：弹珠反弹力不得大于重力）。
    /// 重力 = |Physics2D.gravity| × |rb.gravityScale|；若重力为 0 则不封顶。
    /// 发射区内不封顶（球还未进入主游戏区，发射冲力不受约束）。
    /// </summary>
    public Vector2 CapVelocityToGravity(Vector2 vel)
    {
        // 发射区内速度不受影响
        if (IsInLaunchZone) return vel;

        if (rb == null) return vel;
        float gravityMag = Physics2D.gravity.magnitude * Mathf.Abs(rb.gravityScale);
        if (gravityMag < 0.01f) return vel;
        float speed = vel.magnitude;
        if (speed > gravityMag)
        {
            return vel.normalized * gravityMag;
        }
        return vel;
    }

    /// <summary>
    /// 约束：反弹后能达到的高度不得超过开始下落时的高度。
    /// 即反弹点 y + v²/(2g) ≤ yDescendStart，等价于 v ≤ sqrt(2g × 高度差)。
    /// 仅当球处于下落状态（有有效的 yDescendStart）且反弹点低于下落起点时生效。
    /// </summary>
    public Vector2 CapBounceHeightByDescend(Vector2 bounceVel, Vector2 bouncePoint)
    {
        if (yDescendStart == float.MinValue) return bounceVel; // 未下落过
        if (rb == null) return bounceVel;
        float g = Physics2D.gravity.magnitude * Mathf.Abs(rb.gravityScale);
        if (g < 0.01f) return bounceVel;

        float heightDiff = yDescendStart - bouncePoint.y;
        if (heightDiff <= 0f) return bounceVel; // 反弹点已高于下落起点，不限制

        // 反弹后能达到的高度 = v²/(2g)，限制不超过 heightDiff
        float maxSpeed = Mathf.Sqrt(2f * g * heightDiff);
        float speed = bounceVel.magnitude;
        if (speed > maxSpeed)
        {
            return bounceVel.normalized * maxSpeed;
        }
        return bounceVel;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (inSlot) return;

        if (other.CompareTag(Const.TAG_SLOT) || other.name.StartsWith("SlotTrigger"))
        {
            inSlot = true;
            if (GameManager.Instance != null)
            {
                int slotIndex = ExtractSlotIndex(other.name);
                GameManager.Instance.OnBallEnterSlot(slotIndex, this);
            }
        }
    }

    private int ExtractSlotIndex(string name)
    {
        // 从 "SlotTrigger0" 提取 0
        for (int i = 0; i < name.Length; i++)
        {
            if (char.IsDigit(name[i]))
            {
                string num = name.Substring(i);
                return int.TryParse(num, out int idx) ? idx : -1;
            }
        }
        return -1;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, GetComponent<CircleCollider2D>() != null
            ? GetComponent<CircleCollider2D>().radius : 0.25f);
    }

    public void EnableSelf()
    {
        transform.position = spawnPosition;
        gameObject.SetActive(true);
    }

    public void DisableSelf()
    {
        LockAndReset();
        gameObject.SetActive(false);
        transform.position = Vector2.one * 1000;
    }
}
