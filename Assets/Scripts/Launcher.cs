using UnityEngine;

/// <summary>
/// 发球器：球放在发射通道底部，按一次发射键即以固定速度把球向上弹射。
/// 支持可控结果：可指定球落入哪个奖品槽（通过 BallPathController）。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Launcher : MonoBehaviour
{
    [Header("发射设置")]
    public KeyCode launchKey = KeyCode.Space;

    [Tooltip("单次发射的固定速度。")]
    public float launchSpeed = 28f;

    [Tooltip("发射方向（默认向上）。")]
    public Vector2 launchDirection = Vector2.up;

    [Header("可控结果")]
    [Tooltip("指定目标槽索引（-1 = 随机，-2 = 自由弹跳不控制）。")]
    public int targetSlotIndex = -1;

    private Ball ballInChannel;

    private void Update()
    {
        if (Input.GetKeyDown(launchKey) && ballInChannel != null)
        {
            Launch();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var ball = other.GetComponent<Ball>();
        if (ball != null) ballInChannel = ball;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (ballInChannel != null) return;
        var ball = other.GetComponent<Ball>();
        if (ball != null) ballInChannel = ball;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponent<Ball>() == ballInChannel)
        {
            ballInChannel = null;
        }
    }

    private void Launch()
    {
        var pathController = ballInChannel.GetComponent<BallPathController>();
        if (pathController != null)
        {
            if (targetSlotIndex == -2)
            {
                pathController.ClearTarget();
            }
            else
            {
                pathController.SetTargetSlot(targetSlotIndex);
            }
        }

        ballInChannel.Unlock();
        var rb = ballInChannel.GetComponent<Rigidbody2D>();
        rb.velocity = launchDirection.normalized * launchSpeed;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnBallLaunched();
        }

        ballInChannel = null;
    }
}
