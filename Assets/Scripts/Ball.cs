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

    private Rigidbody2D rb;
    private Vector2 spawnPosition;
    private BallPathController pathController;
    private TrajectoryPlayer trajectoryPlayer;
    private TrajectoryRecorder trajectoryRecorder;
    private bool inSlot = false;

    /// <summary>球是否处于锁定静止状态。</summary>
    public bool IsLocked => locked;

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

        // rb.velocity 已经是物理引擎处理完碰撞后的自然反弹速度
        Vector2 naturalBounce = rb.velocity;

        Vector2 controlledBounce = pathController.ModifyBounceVelocity(naturalBounce, collisionPoint);

        float separation = 0.05f;
        transform.position = (Vector2)transform.position + contact.normal * separation;

        rb.velocity = controlledBounce;
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
