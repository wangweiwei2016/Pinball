using UnityEngine;
using DefaultNamespace;

/// <summary>
/// 发球器：按空格连续发射多个小球（上限 maxBalls）。
/// 每次按空格在发射通道底部生成1个新球并向上发射。
/// 球与球之间不碰撞（Layer "Ball" 自碰撞已禁用）。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Launcher : MonoBehaviour
{
    [Header("发射设置")] public KeyCode launchKey = KeyCode.Space;

    [Tooltip("单次发射的最大固定速度。")] public float[] launchSpeedArray = new float[] { 28f, 29f, 30f, 31f, 32f, 33f };

    [Tooltip("发射方向（默认向上）。")] public Vector2 launchDirection = Vector2.up;

    [Header("多球设置")] [Tooltip("同时间最多存在的球数。")]
    public int maxBalls = 15;

    [Header("可控结果")] [Tooltip("指定目标槽索引（-1 = 随机，-2 = 自由弹跳不控制）。")]
    public int targetSlotIndex = -1;

    [Tooltip("强制使用 BallPathController（忽略轨迹回放），调试用。")]
    public bool forcePathController = false;

    [Header("特殊撞击器要求（正常模式过滤轨迹用）")] [Tooltip("只回放撞过 SpecialStar 的轨迹。")]
    public bool requireStar = false;

    [Tooltip("只回放撞过 SpecialShield 的轨迹。")] public bool requireShield = false;

    [Tooltip("只回放撞过任意一种特殊撞击器的轨迹。")] public bool requireAnySpecial = false;

    [Header("球生成参数（由 PinballSetup 注入）")] public Vector2 spawnPosition;
    public float gravityScale = 1.6f;
    public float ballScale = 0.4f;
    public float ballRadius = 0.5f;
    public float ballMaxSpeed = 28f;
    public float ballMinSpeed = 3f;

    [Header("路径控制参数（由 PinballSetup 注入）")] public float[] slotCenterXs;
    public float[] slotCenterYs;
    public float steerTopY = -2f;
    public float steerBottomY = -6.5f;

    private int activeBallCount = 0;
    private Ball waitingBall; // 在发射通道待命的球（不计入 activeBallCount）

    public float intervalTime = 0.2f;
    private float intervalCountdown = 0.2f;
    //是否是连续发射
    private bool isContinuousLaunch = false;
    //是否连续发射达到上限
    private bool isContinuousMax = false;

    private void Update()
    {
        if (Input.GetKey(launchKey))
        {
            if (intervalCountdown > 0f)
            {
                intervalCountdown -= Time.deltaTime;
                if (intervalCountdown < 0f)
                {
                    intervalCountdown = intervalTime;
                    TryLaunchOneBall();
                }
            }
        }
        else if (Input.GetKeyDown(launchKey))
        {
            TryLaunchOneBall();
        }
    }

    /// <summary>当前场上活跃球数（球入槽时由 GameManager 调用减1）。</summary>
    public int ActiveBallCount => activeBallCount;

    /// <summary>
    /// 生成初始待命球（在发射通道底部锁定，等待发射）。
    /// 由 GameManager.Start() 在游戏开始时调用。
    /// </summary>
    public void SpawnInitialBall()
    {
        if (waitingBall != null) return;

        GameObject ballGo = CreateBallGameObject();
        var ball = ballGo.GetComponent<Ball>();
        ball.LockAndReset();
        waitingBall = ball;
        Debug.Log("[Launcher] 初始球已生成（待命）");
    }

    private void NotifyBallAdded()
    {
        if (activeBallCount > 0)
        {
            isContinuousLaunch = true;
        }
        activeBallCount++;
        if (activeBallCount >= maxBalls)
        {
            isContinuousMax = true;
        }
    }

    /// <summary>球入槽时调用，活跃球数 -1。</summary>
    public void NotifyBallRemoved()
    {
        if (activeBallCount > 0)
        {
            activeBallCount--;
            if (0 == activeBallCount)
            {
                isContinuousLaunch = false;
                isContinuousMax = false;
                SpawnWaitingBallIfNeeded();
            }
        }
    }

    private void TryLaunchOneBall()
    {
        if (isContinuousMax)
        {
            Debug.Log($"[Launcher] 已达上限 {maxBalls} 球，本次空格不发射");
            return;
        }

        // 使用待命球，或创建新球
        GameObject ballGo;
        if (waitingBall != null)
        {
            ballGo = waitingBall.gameObject;
            waitingBall = null;
        }
        else
        {
            ballGo = CreateBallGameObject();
        }

        float speed = launchSpeedArray[Random.Range(0, launchSpeedArray.Length)];
        Vector2 startVel = launchDirection.normalized * speed;

        var rb = ballGo.GetComponent<Rigidbody2D>();
        var pathController = ballGo.GetComponent<BallPathController>();
        var trajectoryPlayer = ballGo.GetComponent<TrajectoryPlayer>();
        var recorder = ballGo.GetComponent<TrajectoryRecorder>();

        Vector2 startPos = rb.position;

        // ---- 模式 1：编辑器预录模式 ----
        if (recorder != null && recorder.enableRecording)
        {
            Debug.Log("[Launcher] 模式1：编辑器预录");
            rb.isKinematic = false;
            ApplyTargetToPathController(pathController);
            ballGo.GetComponent<Ball>().Unlock();
            rb.velocity = startVel;
            recorder.StartRecording();
            NotifyLaunched();
            NotifyBallAdded();
            SpawnWaitingBallIfNeeded();
            return;
        }

        // ---- 模式 2：强制 BallPathController ----
        if (forcePathController)
        {
            Debug.Log("[Launcher] 模式2：强制 BallPathController");
            rb.isKinematic = false;
            if (pathController != null) pathController.steeringEnabled = true;
            ApplyTargetToPathController(pathController);
            ballGo.GetComponent<Ball>().Unlock();
            rb.velocity = startVel;
            NotifyLaunched();
            NotifyBallAdded();
            SpawnWaitingBallIfNeeded();
            return;
        }

        // ---- 模式 3：运行时回放优先（带特殊撞击器过滤） ----
        if (trajectoryPlayer != null && TrajectoryLibrary.Instance != null)
        {
            int slot = ResolveTargetSlot(pathController);
            if (slot >= 0)
            {
                TrajectoryData match = TrajectoryLibrary.Instance.FindBestMatch(
                    slot, startPos, speed, requireStar, requireShield, requireAnySpecial);

                if (match != null)
                {
                    Debug.Log($"[Launcher] 模式3：轨迹回放 (槽{slot}, Star={match.hitSpecialStar}, Shield={match.hitSpecialShield})");
                    if (pathController != null)
                    {
                        pathController.ClearTarget();
                        pathController.steeringEnabled = false;
                    }

                    ballGo.GetComponent<Ball>().Unlock();
                    rb.isKinematic = true;
                    rb.velocity = Vector2.zero;
                    trajectoryPlayer.PlayTrajectory(match);
                    NotifyLaunched();
                    NotifyBallAdded();
                    SpawnWaitingBallIfNeeded();
                    return;
                }

                Debug.Log("[Launcher] 模式3：无匹配轨迹，回退到 BallPathController");
            }
        }

        // ---- 模式 4：后备——真实物理 + 实时引导 ----
        Debug.Log("[Launcher] 模式4：BallPathController 实时引导");
        rb.isKinematic = false;
        if (pathController != null) pathController.steeringEnabled = true;
        ApplyTargetToPathController(pathController);
        ballGo.GetComponent<Ball>().Unlock();
        rb.velocity = startVel;
        NotifyLaunched();
        NotifyBallAdded();
        SpawnWaitingBallIfNeeded();
    }

    /// <summary>发射后自动补充一个新的待命球（如果未达上限）。</summary>
    private void SpawnWaitingBallIfNeeded()
    {
        if (activeBallCount == 0 && waitingBall == null)
        {
            GameObject ballGo = CreateBallGameObject();
            var ball = ballGo.GetComponent<Ball>();
            ball.LockAndReset();
            waitingBall = ball;
        }
    }

    /// <summary>动态创建一个球 GameObject（带所有必要组件）。</summary>
    private GameObject CreateBallGameObject()
    {
        var ballGo = new GameObject("Ball_" + activeBallCount);
        ballGo.transform.SetParent(transform.parent, false);
        ballGo.transform.position = spawnPosition;
        ballGo.transform.localScale = new Vector3(ballScale, ballScale, 1f);
        ballGo.layer = LayerMask.NameToLayer("Ball");

        var ballRb = ballGo.AddComponent<Rigidbody2D>();
        ballRb.gravityScale = gravityScale;
        ballRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        ballRb.interpolation = RigidbodyInterpolation2D.Interpolate;
        ballRb.freezeRotation = true;

        var ballCol = ballGo.AddComponent<CircleCollider2D>();
        ballCol.radius = ballRadius;
        ballCol.sharedMaterial = CreateBouncyMaterial();

        var ballSprite = ballGo.AddComponent<SpriteRenderer>();
        ballSprite.sprite = MakeCircleSprite(new Color(1f, 0.95f, 0.85f));
        ballSprite.sortingOrder = 3;

        var ball = ballGo.AddComponent<Ball>();
        ball.maxSpeed = ballMaxSpeed;
        ball.minSpeed = ballMinSpeed;

        // 路径控制器
        var pathController = ballGo.AddComponent<BallPathController>();
        if (slotCenterXs != null) pathController.slotCenterXs = slotCenterXs;
        if (slotCenterYs != null) pathController.slotCenterYs = slotCenterYs;
        pathController.steerTopY = steerTopY;
        pathController.steerBottomY = steerBottomY;

        // 轨迹回放器 + 录制器
        var trajectoryPlayer = ballGo.AddComponent<TrajectoryPlayer>();
        trajectoryPlayer.ballRb = ballRb;
        var trajectoryRecorder = ballGo.AddComponent<TrajectoryRecorder>();
        trajectoryRecorder.ballRb = ballRb;

        return ballGo;
    }

    private PhysicsMaterial2D CreateBouncyMaterial()
    {
        var mat = new PhysicsMaterial2D("BouncyMat");
        mat.bounciness = 0.85f;
        mat.friction = 0.02f;
        return mat;
    }

    private Sprite MakeCircleSprite(Color color)
    {
        int size = 100;
        var tex = new Texture2D(size, size);
        var center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f - 1;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                Color c = color;
                c.a = Mathf.Clamp01(radius - dist) > 0f ? 1f : 0f;
                tex.SetPixel(x, y, c);
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
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