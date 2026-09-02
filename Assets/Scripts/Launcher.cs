using System;
using UnityEngine;
using DefaultNamespace;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

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

    [Header("纯物理模式（独立按键切换）")]
    [Tooltip("开启后弹珠完全走物理引擎，不应用路径引导、不使用轨迹回放。")]
    public bool purePhysicsMode = true;

    [Tooltip("切换纯物理模式的按键（默认 F7）。")]
    public KeyCode purePhysicsToggleKey = KeyCode.F7;

    [FormerlySerializedAs("minStarHits")] [Header("特殊撞击器要求（正常模式过滤轨迹用）")] [Tooltip("要求轨迹撞过 SpecialStar的次数 指定次数。0 = 不要求。")]
    public int tarStarHits = 0;

    [FormerlySerializedAs("minShieldHits")] [Tooltip("要求轨迹撞过 SpecialShield的次数 指定次数。0 = 不要求。")]
    public int tarShieldHits = 0;

    [Header("球生成参数（由 PinballSetup 注入）")] public Vector2 spawnPosition;
    public float gravityScale = 1.6f;
    public float ballScale = 0.4f;
    public float ballRadius = 0.5f;
    public float ballMaxSpeed = 28f;
    public float ballMinSpeed = 0.1f;//3f;

    [Header("路径控制参数（由 PinballSetup 注入）")] public float[] slotCenterXs;
    public float[] slotCenterYs;
    public float freePlayTopY = 0f;
    public float midGuideTopY = -2.5f;
    public float strongGuideY = -5f;

    [Header("发射区参数（由 PinballSetup 注入）")]
    [Tooltip("发射通道左墙 x 坐标。")]
    public float launchZoneLeftX = 3f;
    [Tooltip("发射通道顶部 y 坐标（导流板高度）。")]
    public float launchZoneTopY = 5.3f;

    private int activeBallCount = 0;
    private Ball waitingBall; // 在发射通道待命的球（不计入 activeBallCount）

    private float continuousCountDown = 0f;
    public float intervalTime = 0.2f;

    private float intervalCountdown = 0.2f;

    //是否是连续发射
    private bool isContinuousLaunch = false;

    //是否连续发射达到上限
    private bool isContinuousMax = false;
    private ObjectPool<GameObject> objectPool;

    private void Start()
    {
        // 池容量与最大同时存在的球数一致，避免池满后回收的球引用被丢弃（泄漏）
        objectPool = new ObjectPool<GameObject>(CreateBallGameObject, Mathf.Max(1, maxBalls));
        Collider2D collider = GetComponent<Collider2D>();
        if (null != collider)
        {
            collider.isTrigger = false;
        }
    }

    private void Update()
    {
        // 切换纯物理模式
        if (Input.GetKeyDown(purePhysicsToggleKey))
        {
            purePhysicsMode = !purePhysicsMode;
            Debug.Log($"[Launcher] 纯物理模式 {(purePhysicsMode ? "开启" : "关闭")} — 按 {purePhysicsToggleKey} 切换");
        }

        if (!isContinuousLaunch && Input.GetKey(launchKey))
        {
            continuousCountDown += Time.deltaTime;
            if (continuousCountDown > intervalTime)
            {
                isContinuousLaunch = true;
                continuousCountDown = 0f;
            }
        }

        if (Input.GetKeyUp(launchKey))
        {
            continuousCountDown = 0f;
        }

        // 连续发射模式
        if (isContinuousLaunch && Input.GetKey(launchKey))
        {
            HandleContinuousLaunch();
            return; // 防止进入单次发射检查
        }

        // 单次发射模式
        if (!isContinuousLaunch && Input.GetKeyDown(launchKey))
        {
            TryLaunchOneBall();
        }
    }

    private void HandleContinuousLaunch()
    {
        // 初始状态立即发射
        if (intervalCountdown <= 0f)
        {
            if (TryLaunchOneBall())
            {
                intervalCountdown = intervalTime;
            }

            return;
        }

        // 倒计时递减
        intervalCountdown -= Time.deltaTime;
        if (intervalCountdown <= 0f)
        {
            if (TryLaunchOneBall())
            {
                intervalCountdown = intervalTime;
            }
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

        GameObject ballGo = GetBallGameObjectFromPool();
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

    public void Recycle(Ball ball)
    {
        ball.DisableSelf();
        objectPool.Free(ball.gameObject);
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

    private bool TryLaunchOneBall()
    {
        if (isContinuousMax)
        {
            Debug.Log($"[Launcher] 已达上限 {maxBalls} 球，本次空格不发射");
            return false;
        }

        Debug.Log($"[Launcher] TryLaunchOneBall(targetSlotIndex={targetSlotIndex}, tarStarHits={tarStarHits}, tarShieldHits={tarShieldHits})");

        // 使用待命球，或创建新球
        GameObject ballGo;
        if (waitingBall != null)
        {
            ballGo = waitingBall.gameObject;
            waitingBall = null;
        }
        else
        {
            ballGo = GetBallGameObjectFromPool();
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
            rb.isKinematic = false;
            if (purePhysicsMode)
            {
                // 纯物理录制：关闭路径引导，让球完全走物理引擎，
                // 但仍然录制轨迹数据（供回放使用）。
                Debug.Log("[Launcher] 模式1：编辑器预录（纯物理）");
                if (pathController != null)
                {
                    pathController.ClearTarget();
                    pathController.steeringEnabled = false;
                }
            }
            else
            {
                Debug.Log("[Launcher] 模式1：编辑器预录（带引导）");
                ApplyTargetToPathController(pathController);
            }
            ballGo.GetComponent<Ball>().Unlock();
            rb.velocity = startVel;
            recorder.StartRecording();
            NotifyLaunched();
            NotifyBallAdded();
            SpawnWaitingBallIfNeeded();
            return true;
        }

        // ---- 模式 5：纯物理（按键 F7 切换） ----
        // 完全走物理引擎：不应用路径引导、不使用轨迹回放，
        // 只保留 Ball 层的发射区豁免和能量约束（反弹力≤重力、反弹高度≤下落起点）。
        if (purePhysicsMode)
        {
            Debug.Log("[Launcher] 模式5：纯物理引擎");
            rb.isKinematic = false;
            if (pathController != null)
            {
                pathController.ClearTarget();
                pathController.steeringEnabled = false;
            }
            ballGo.GetComponent<Ball>().Unlock();
            rb.velocity = startVel;
            NotifyLaunched();
            NotifyBallAdded();
            SpawnWaitingBallIfNeeded();
            return true;
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
            return true;
        }

        // ---- 模式 3：运行时回放优先（带特殊撞击器过滤） ----
        if (trajectoryPlayer != null && TrajectoryLibrary.Instance != null)
        {
            int slot = ResolveTargetSlot(pathController);
            if (slot >= 0)
            {
                TrajectoryData match = TrajectoryLibrary.Instance.FindBestMatch(
                    slot, startPos, speed, tarStarHits, tarShieldHits);

                if (match != null)
                {
                    Debug.Log($"[Launcher] 模式3：轨迹回放 (槽{slot}, Star×{match.starHitCount}, Shield×{match.shieldHitCount})");
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
                    return true;
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
        return true;
    }

    /// <summary>发射后自动补充一个新的待命球（如果未达上限）。</summary>
    private void SpawnWaitingBallIfNeeded()
    {
        if (activeBallCount == 0 && waitingBall == null)
        {
            GameObject ballGo = GetBallGameObjectFromPool();
            var ball = ballGo.GetComponent<Ball>();
            ball.LockAndReset();
            waitingBall = ball;
        }
    }

    private GameObject GetBallGameObjectFromPool()
    {
        GameObject ballGo = objectPool.Allocate();
        ballGo.GetComponent<Ball>().EnableSelf();
        return ballGo;
    }

    /// <summary>动态创建一个球 GameObject（带所有必要组件）。</summary>
    private GameObject CreateBallGameObject()
    {
        var ballGo = new GameObject("Ball_" + activeBallCount);
        ballGo.transform.SetParent(transform.parent, false);
        ballGo.transform.position = spawnPosition;
        ballGo.transform.localScale = new Vector3(ballScale, ballScale, 1f);
        ballGo.layer = LayerMask.NameToLayer(Const.LAYER_BALL);

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

        // 路径控制器
        var pathController = ballGo.AddComponent<BallPathController>();
        if (slotCenterXs != null) pathController.slotCenterXs = slotCenterXs;
        if (slotCenterYs != null) pathController.slotCenterYs = slotCenterYs;
        pathController.freePlayTopY = freePlayTopY;
        pathController.midGuideTopY = midGuideTopY;
        pathController.strongGuideY = strongGuideY;

        // 轨迹回放器 + 录制器
        var trajectoryPlayer = ballGo.AddComponent<TrajectoryPlayer>();
        trajectoryPlayer.ballRb = ballRb;
        var trajectoryRecorder = ballGo.AddComponent<TrajectoryRecorder>();
        trajectoryRecorder.ballRb = ballRb;

        var ball = ballGo.AddComponent<Ball>();
        ball.maxSpeed = ballMaxSpeed;
        ball.minSpeed = ballMinSpeed;
        ball.launchZoneLeftX = launchZoneLeftX;
        ball.launchZoneTopY = launchZoneTopY;

        return ballGo;
    }

    private PhysicsMaterial2D CreateBouncyMaterial()
    {
        var mat = new PhysicsMaterial2D("BouncyMat");
        mat.bounciness = 0.5f; //0.9f;
        mat.friction = 0.05f;
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