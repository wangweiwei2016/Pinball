using UnityEngine;
using DefaultNamespace;

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
    public int targetSlotIndex = 0;//-1;

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
        var rb = ballInChannel.GetComponent<Rigidbody2D>();
        var pathController = ballInChannel.GetComponent<BallPathController>();
        var trajectoryPlayer = ballInChannel.GetComponent<TrajectoryPlayer>();
        var recorder = ballInChannel.GetComponent<TrajectoryRecorder>();

        Vector2 startPos = rb.position;
        Vector2 startVel = launchDirection.normalized * launchSpeed;

        // ---- 模式 1：编辑器预录模式 ----
        // 用真实物理 + BallPathController 跑一遍，逐帧记录，入槽时保存为资源
        if (recorder != null && recorder.enableRecording)
        {
            rb.isKinematic = false;
            ApplyTargetToPathController(pathController);
            ballInChannel.Unlock();
            rb.velocity = startVel;
            recorder.StartRecording();
            NotifyLaunched();
            ballInChannel = null;
            return;
        }

        // ---- 模式 2：运行时回放优先 ----
        // 从轨迹库中按目标槽 + 发射参数匹配预录轨迹，命中则运动学回放
        if (trajectoryPlayer != null && TrajectoryLibrary.Instance != null)
        {
            int slot = ResolveTargetSlot(pathController);
            if (slot >= 0)
            {
                TrajectoryData match = TrajectoryLibrary.Instance.FindBestMatch(slot, startPos, launchSpeed);
                if (match != null)
                {
                    Debug.Log($"FindBestMatch,success,slot={slot}, startPos={startPos}, launchSpeed={launchSpeed}");
                    // 关掉实时引导，球完全按预录轨迹运动
                    if (pathController != null)
                    {
                        pathController.ClearTarget();
                        pathController.steeringEnabled = false;
                    }
                    ballInChannel.Unlock();
                    rb.isKinematic = true;
                    rb.velocity = Vector2.zero;
                    trajectoryPlayer.PlayTrajectory(match);
                    NotifyLaunched();
                    ballInChannel = null;
                    return;
                }
            }
        }

        Debug.Log($"FindBestMatch,fail");
        // ---- 模式 3：后备——真实物理 + 实时引导 ----
        rb.isKinematic = false;
        if (pathController != null) pathController.steeringEnabled = true;
        ApplyTargetToPathController(pathController);
        ballInChannel.Unlock();
        rb.velocity = startVel;
        NotifyLaunched();
        ballInChannel = null;
    }

    private void ApplyTargetToPathController(BallPathController pathController)
    {
        if (pathController == null) return;
        if (targetSlotIndex == -2)
        {
            pathController.ClearTarget();
        }
        else
        {
            pathController.SetTargetSlot(targetSlotIndex);
        }
    }

    /// <summary>
    /// 把 targetSlotIndex 解析为具体槽位：-2/-1 时随机选一个有轨迹的槽。
    /// </summary>
    private int ResolveTargetSlot(BallPathController pathController)
    {
        if (targetSlotIndex >= 0) return targetSlotIndex;

        int maxSlots = pathController != null && pathController.slotCenterXs != null
            ? pathController.slotCenterXs.Length
            : -1;
        return TrajectoryLibrary.Instance.GetRandomSlotWithTrajectory(maxSlots);
    }

    private void NotifyLaunched()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnBallLaunched();
        }
    }
}
