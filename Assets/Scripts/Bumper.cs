using UnityEngine;

/// <summary>
/// 弹珠台上的撞击器：球碰到会反弹并加分。
/// 支持可控反弹：isControllable 为 true 时，反弹方向由 BallPathController 决定。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Bumper : MonoBehaviour
{
    [Header("反弹设置")]
    [Tooltip("撞击时给球的速度大小（反弹冲量）。")]
    public float bounceForce = 12f;

    [Tooltip("反弹方向相对法线的偏移，0 = 沿接触法线弹开，-90 ~ 90。")]
    public float bounceAngleOffset = 0f;

    [Tooltip("反弹倍速（特殊撞击器用）。")]
    public float bounceMultiplier = 1f;

    [Header("可控反弹")]
    [Tooltip("是否为可控撞击器：为 true 时反弹方向由 BallPathController 修正。")]
    public bool isControllable = false;

    [Header("得分")]
    public int score = 100;

    [Tooltip("基础分（供 PinballSetup 使用）。")]
    public int baseScore = 10;

    [Header("视觉反馈")]
    [Tooltip("被撞击时缩放的峰值倍数。0 表示不缩放。")]
    public float hitScale = 1.3f;
    public float hitAnimTime = 0.12f;

    [Header("音效")]
    public AudioClip hitClip;

    private AudioSource audioSource;
    private Vector3 originalScale;
    private Coroutine hitRoutine;

    private void Awake()
    {
        var col = GetComponent<Collider2D>();
        if (col.sharedMaterial == null)
        {
            col.sharedMaterial = CreateDefaultBouncyMaterial();
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && hitClip != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        originalScale = transform.localScale;
    }

    /// <summary>
    /// 触发撞击反馈：加分、音效、缩放动画，但不改变球速度。
    /// 供回放（TrajectoryPlayer）在球沿预录轨迹经过撞击器时调用，
    /// 让“弹珠在机关间反复碰撞”的观感得以保留。
    /// </summary>
    public void TriggerHitFeedback()
    {
        int finalScore = Mathf.Max(score, baseScore);
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddScore(finalScore);
        }

        if (hitClip != null && audioSource != null)
        {
            audioSource.PlayOneShot(hitClip);
        }

        if (hitRoutine != null) StopCoroutine(hitRoutine);
        if (hitScale > 0f) hitRoutine = StartCoroutine(HitScaleRoutine());
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        var ball = collision.collider.GetComponent<Ball>();
        if (ball == null) return;

        // 加分、音效、缩放动画（与是否可控无关）
        TriggerHitFeedback();

        // 非可控撞击器：直接设置反弹速度
        if (!isControllable)
        {
            Vector2 normal = (collision.collider.transform.position - transform.position).normalized;
            if (normal.sqrMagnitude < 0.0001f) normal = Vector2.up;

            if (!Mathf.Approximately(bounceAngleOffset, 0f))
            {
                float ang = Mathf.Atan2(normal.y, normal.x) * Mathf.Rad2Deg + bounceAngleOffset;
                normal = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad));
            }

            var ballRb = ball.GetComponent<Rigidbody2D>();
            Vector2 bounceVel = normal * bounceForce * bounceMultiplier;

            // 约束：发射区内速度不受影响
            if (ball.IsInLaunchZone)
            {
                ballRb.velocity = bounceVel;
            }
            else
            {
                // 约束 1：反弹力 ≤ 重力
                bounceVel = ball.CapVelocityToGravity(bounceVel);
                // 约束 2：反弹高度 ≤ 下落起点高度
                bounceVel = ball.CapBounceHeightByDescend(bounceVel, ball.transform.position);
                ballRb.velocity = bounceVel;
            }
        }
        // 可控撞击器：反弹由 Ball.OnCollisionEnter2D 中的路径控制器处理
    }

    private System.Collections.IEnumerator HitScaleRoutine()
    {
        transform.localScale = originalScale * hitScale;
        float t = 0f;
        while (t < hitAnimTime)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(originalScale * hitScale, originalScale, t / hitAnimTime);
            yield return null;
        }
        transform.localScale = originalScale;
        hitRoutine = null;
    }

    private static PhysicsMaterial2D CreateDefaultBouncyMaterial()
    {
        var mat = new PhysicsMaterial2D("BumperBouncy");
        mat.bounciness = 1f;
        mat.friction = 0f;
        return mat;
    }
}
