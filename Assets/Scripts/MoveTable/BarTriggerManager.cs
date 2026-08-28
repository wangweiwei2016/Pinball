using System.Collections.Generic;
using DefaultNamespace.MoveTable;
using UnityEngine;

public class BarTriggerManager : MonoBehaviour
{
    [Header("触发器设置")] [Tooltip("条形触发器的宽度（水平方向）")]
    public float barWidth = 4f;

    [Tooltip("条形触发器的高度（垂直方向）")] public float barHeight = 0.5f;

    [Tooltip("三个触发器之间的垂直间距")] public float verticalSpacing = 2.5f;

    [Tooltip("触发器的移动速度")] public float moveSpeed = 2f;

    [Tooltip("触发器的左右移动范围（从初始位置偏移）")] public float moveRange = 3f;

    [Header("显示体设置")] [Tooltip("显示体的颜色")] public Color visualColor = new Color(0.2f, 0.6f, 1f, 0.5f);

    [Tooltip("触发时的闪烁颜色")] public Color triggeredColor = new Color(1f, 0.3f, 0.3f, 0.7f);

    [Tooltip("显示体的边框颜色")] public Color borderColor = new Color(1f, 1f, 1f, 0.8f);

    [Tooltip("边框宽度")] public float borderWidth = 0.05f;

    [Header("挡板设置")] [Tooltip("挡板的宽度（厚度）")]
    public float barrierWidth = 0.25f;

    [Tooltip("挡板的高度（与触发器高度匹配）")] public float barrierHeight = 0.7f;

    [Tooltip("挡板的颜色")] public Color barrierColor = new Color(0.8f, 0.4f, 0.1f, 1f);

    [Tooltip("挡板的碰撞反弹系数")] public float barrierBounciness = 0.8f;

    [Tooltip("挡板的摩擦力")] public float barrierFriction = 0.3f;

    [Tooltip("生成的小球预制体")] public GameObject ballPrefab;

    [Tooltip("生成位置偏移（相对于触发器中心）")] public Vector2 spawnOffset = new Vector2(0, -0.5f);

    [Tooltip("生成的小球初始速度")] public Vector2 spawnInitialVelocity = new Vector2(0, -3f);

    [Tooltip("生成间隔（防止同一触发器短时间内多次触发）")] public float triggerCooldown = 0.5f;

    [Header("奖励触发器设置")] [Tooltip("奖励触发器的宽度（水平方向）")]
    public float rewardWidth = 2f;

    [Tooltip("奖励触发器的高度（垂直方向）")] public float rewardHeight = 1f;

    [Tooltip("奖励触发器的移动速度")] public float rewardMoveSpeed = 2.5f;

    [Tooltip("奖励触发器的左右移动范围（从初始位置偏移）")] public float rewardMoveRange = 5f;

    // 存储三个触发器的数据
    private List<BarTrigger> triggers = new List<BarTrigger>();
    private List<MoveRewardTrigger> rewardTriggers = new List<MoveRewardTrigger>();
    private Transform triggerParent;

    [Header("发射设置")] public KeyCode launchKey = KeyCode.Space;

    private void Start()
    {
        triggerParent = new GameObject("BarTriggers").transform;
        triggerParent.SetParent(transform);
        CreateEnv();
        CreateBarTriggers();
        CreateRewardTriggers();
    }

    private void Update()
    {
        foreach (var trigger in triggers)
        {
            trigger.UpdateMovement(Time.deltaTime);
        }

        foreach (var trigger in rewardTriggers)
        {
            trigger.UpdateMovement(Time.deltaTime);
        }

        if (Input.GetKeyDown(launchKey))
        {
            TryLaunchOneBall();
        }
    }

    private void CreateEnv()
    {
        //创建边框
        CreateGameBroad("leftBroad", new Vector2(-3, 0), 0.5f, 10f, Color.black);
        CreateGameBroad("rightBroad", new Vector2(3, 0), 0.5f, 10f, Color.black);
        CreateGameBroad("bottomBroad", new Vector2(0, -5f), 6f, 0.5f, Color.black);

        //创建底部触发器
        CreateTargetTriggers();
    }

    private void CreateBarTriggers()
    {
        for (int i = 0; i < 3; i++)
        {
            float yPos = (i - 1) * verticalSpacing;
            float phaseOffset = i * 1.2f;

            // 创建触发器容器
            GameObject triggerGO = new GameObject($"BarTrigger_{i}");
            triggerGO.transform.SetParent(triggerParent);
            triggerGO.transform.position = new Vector3(0, yPos, 0);

            // 添加触发器逻辑组件
            BarTrigger trigger = triggerGO.AddComponent<BarTrigger>();

            // 创建显示体和挡板
            CreateVisualElements(triggerGO, trigger);

            int spawnCount = i == 0 ? 3 : i == 1 ? 4 : 5;
            // 初始化触发器逻辑
            trigger.Initialize(
                this,
                yPos,
                phaseOffset,
                spawnCount,
                ballPrefab,
                spawnOffset,
                spawnInitialVelocity,
                triggerCooldown,
                visualColor,
                triggeredColor
            );

            // 设置显示体引用
            trigger.SetVisualElements(
                triggerGO.transform.Find("Visual")?.GetComponent<SpriteRenderer>(),
                triggerGO.transform.Find("VisualBorder")?.GetComponent<SpriteRenderer>(),
                triggerGO.transform.Find("BarrierLeft")?.GetComponent<SpriteRenderer>(),
                triggerGO.transform.Find("BarrierRight")?.GetComponent<SpriteRenderer>()
            );

            triggers.Add(trigger);
        }
    }

    private void CreateRewardTriggers()
    {
        int triggerCount = 6;
        float horizontalSpacing = 1.5f;
        float startX = -2f;

        for (int i = 0; i < triggerCount; i++)
        {
            float yPos = -4f;

            GameObject triggerGO = new GameObject($"MoveRewardTrigger_{i}");
            triggerGO.transform.SetParent(triggerParent);

            MoveRewardTrigger trigger = triggerGO.AddComponent<MoveRewardTrigger>();
            trigger.SetSize(rewardWidth, rewardHeight);
            trigger.Initialize(
                manager: this,
                triggerIndex: i,
                yPosition: yPos,
                startX: startX,
                horizontalSpacing: horizontalSpacing,
                moveSpeed: rewardMoveSpeed,
                moveRange: rewardMoveRange,
                maxScore: Random.Range(3, 10),
                resetDelay: 1.5f // ★ 延迟重置时间
            );

            rewardTriggers.Add(trigger);
        }
    }

    /// <summary>
    /// 创建可视化元素：显示体（与触发器一样大）+ 边框 + 左右挡板
    /// </summary>
    private void CreateVisualElements(GameObject parent, BarTrigger trigger)
    {
        // --- 1. 中间显示体（与触发器完全一样大）---
        GameObject visualGO = new GameObject("Visual");
        visualGO.transform.SetParent(parent.transform);
        visualGO.transform.localPosition = Vector3.zero;

        SpriteRenderer visualRenderer = visualGO.AddComponent<SpriteRenderer>();
        visualRenderer.sprite = CreateSquareSprite(barWidth, barHeight);
        visualRenderer.color = visualColor;
        visualRenderer.sortingOrder = 10;
        // **关键：显示体大小 = 触发器大小**
        visualRenderer.size = new Vector2(rewardWidth, rewardHeight);

        // --- 2. 显示体边框（可选）---
        GameObject borderGO = new GameObject("VisualBorder");
        borderGO.transform.SetParent(parent.transform);
        borderGO.transform.localPosition = Vector3.zero;

        SpriteRenderer borderRenderer = borderGO.AddComponent<SpriteRenderer>();
        borderRenderer.sprite = CreateBorderSprite();
        borderRenderer.color = borderColor;
        borderRenderer.sortingOrder = 11;
        borderRenderer.size = new Vector2(barWidth + borderWidth * 2, barHeight + borderWidth * 2);

        // --- 3. 触发器碰撞体（与显示体完全重合）---
        BoxCollider2D triggerCollider = visualGO.AddComponent<BoxCollider2D>();
        triggerCollider.size = new Vector2(barWidth, barHeight);
        triggerCollider.isTrigger = true;

        // 碰撞事件转发
        TriggerCollisionForwarder forwarder = visualGO.AddComponent<TriggerCollisionForwarder>();
        forwarder.SetOwner(trigger);

        // --- 4. 左侧挡板（紧贴显示体左边缘）---
        CreateBarrier(parent, "BarrierLeft",
            new Vector2(-barWidth / 2 - barrierWidth / 2, 0),
            barrierWidth, barrierHeight,
            barrierColor);

        // --- 5. 右侧挡板（紧贴显示体右边缘）---
        CreateBarrier(parent, "BarrierRight",
            new Vector2(barWidth / 2 + barrierWidth / 2, 0),
            barrierWidth, barrierHeight,
            barrierColor);
    }

    /// <summary>
    /// 创建单个挡板
    /// </summary>
    private void CreateBarrier(GameObject parent, string name, Vector2 localPos, float width, float height, Color color)
    {
        GameObject barrierGO = new GameObject(name);
        barrierGO.transform.SetParent(parent.transform);
        barrierGO.transform.localPosition = localPos;

        // 视觉
        SpriteRenderer renderer = barrierGO.AddComponent<SpriteRenderer>();
        renderer.sprite = CreateSquareSprite(width, height);
        renderer.color = color;
        renderer.sortingOrder = 12;
        renderer.size = new Vector2(width, height);

        // 物理碰撞体（非触发器）
        BoxCollider2D collider = barrierGO.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(width, height);
        collider.isTrigger = false;

        // 物理材质（反弹效果）
        PhysicsMaterial2D physMat = new PhysicsMaterial2D("BarrierMaterial");
        physMat.bounciness = barrierBounciness;
        physMat.friction = barrierFriction;
        collider.sharedMaterial = physMat;

        // 刚体（Kinematic，跟随父物体移动）
        Rigidbody2D rb = barrierGO.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        // 标记
        //barrierGO.tag = "Barrier";

        // 跟随组件
        BarrierComponent barrierComp = barrierGO.AddComponent<BarrierComponent>();
        barrierComp.SetOwner(barrierGO.transform.parent);
    }

    private void CreateGameBroad(string name, Vector2 localPos, float width, float height, Color color)
    {
        GameObject barrierGO = new GameObject(name);
        barrierGO.transform.SetParent(transform);
        barrierGO.transform.localPosition = localPos;

        // 视觉
        SpriteRenderer renderer = barrierGO.AddComponent<SpriteRenderer>();
        renderer.sprite = CreateSquareSprite(width, height);
        renderer.color = color;
        renderer.sortingOrder = 12;
        renderer.size = new Vector2(width, height);

        // 物理碰撞体（非触发器）
        BoxCollider2D collider = barrierGO.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(width, height);
        collider.isTrigger = false;

        // 物理材质（反弹效果）
        PhysicsMaterial2D physMat = new PhysicsMaterial2D("BarrierMaterial");
        physMat.bounciness = barrierBounciness;
        physMat.friction = barrierFriction;
        collider.sharedMaterial = physMat;

        // 刚体（Kinematic，跟随父物体移动）
        Rigidbody2D rb = barrierGO.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    private void CreateTargetTriggers()
    {
        GameObject triggerGO = new GameObject("TargetTrigger");
        triggerGO.transform.SetParent(triggerParent);
        triggerGO.transform.position = new Vector3(0, -5, 0);

        BoxCollider2D triggerCollider = triggerGO.AddComponent<BoxCollider2D>();
        triggerCollider.size = new Vector2(5, 1);
        triggerCollider.isTrigger = true;

        triggerGO.AddComponent<TargetTrigger>();
    }

    private Sprite CreateSquareSprite(float width, float height)
    {
        Texture2D texture = new Texture2D(100, 100);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 1f);
    }

    /// <summary>
    /// 创建边框Sprite（空心矩形）
    /// </summary>
    private Sprite CreateBorderSprite()
    {
        // 创建一个带有边框的纹理
        int size = 16;
        Texture2D texture = new Texture2D(size, size);
        Color transparent = new Color(0, 0, 0, 0);
        Color white = Color.white;

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                // 边框像素（边缘2像素）
                bool isBorder = x < 2 || x >= size - 2 || y < 2 || y >= size - 2;
                texture.SetPixel(x, y, isBorder ? white : transparent);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    public void SpawnBalls(Vector2 triggerPosition, int curSpawnCount)
    {
        if (ballPrefab == null)
        {
            Debug.LogWarning("Ball prefab is not assigned!");
            return;
        }

        Vector2 spawnPos = triggerPosition + spawnOffset;

        for (int i = 0; i < curSpawnCount; i++)
        {
            float offsetX = (i - (curSpawnCount - 1) / 2f) * 0.3f;
            Vector2 finalPos = spawnPos + new Vector2(offsetX, 0);

            GameObject newBall = Instantiate(ballPrefab, finalPos, Quaternion.identity);

            Rigidbody2D rb = newBall.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                float spread = 0.3f;
                Vector2 velocity = spawnInitialVelocity + new Vector2(
                    Random.Range(-spread, spread),
                    Random.Range(-spread * 0.5f, spread * 0.5f)
                );
                rb.velocity = velocity;
            }
        }
    }

    private void TryLaunchOneBall()
    {
        SpawnBalls(new Vector2(0, 4), 1);
    }
}