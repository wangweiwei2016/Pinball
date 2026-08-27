using UnityEngine;

/// <summary>
/// 单个条形触发器的逻辑组件
/// </summary>
public class BarTrigger : MonoBehaviour
{
    [Header("生成设置")] [Tooltip("小球经过时生成的小球数量")]
    public int spawnCount = 3;

    private BarTriggerManager manager;
    private float baseY;
    private float phaseOffset;
    private float moveSpeed;
    private float moveRange;
    private GameObject ballPrefab;
    private Vector2 spawnOffset;
    private Vector2 spawnVelocity;
    private float triggerCooldown;
    private float lastTriggerTime = -10f;

    private SpriteRenderer visualRenderer;
    private SpriteRenderer borderRenderer;
    private SpriteRenderer leftBarrierRenderer;
    private SpriteRenderer rightBarrierRenderer;

    private Color normalColor;
    private Color triggeredColor;
    private bool isTriggered = false;
    private float triggerFlashTimer = 0f;
    private float currentOffset = 0f;

    public void Initialize(
        BarTriggerManager manager,
        float baseY,
        float phaseOffset,
        int spawnCount,
        GameObject ballPrefab,
        Vector2 spawnOffset,
        Vector2 spawnVelocity,
        float triggerCooldown,
        Color normalColor,
        Color triggeredColor)
    {
        this.manager = manager;
        this.baseY = baseY;
        this.phaseOffset = phaseOffset;
        this.spawnCount = spawnCount;
        this.ballPrefab = ballPrefab;
        this.spawnOffset = spawnOffset;
        this.spawnVelocity = spawnVelocity;
        this.triggerCooldown = triggerCooldown;
        this.normalColor = normalColor;
        this.triggeredColor = triggeredColor;

        if (manager != null)
        {
            this.moveSpeed = manager.moveSpeed;
            this.moveRange = manager.moveRange;
        }

        currentOffset = Mathf.Sin(phaseOffset) * moveRange * 0.5f;
        UpdatePosition();
    }

    public void SetVisualElements(
        SpriteRenderer visual,
        SpriteRenderer border,
        SpriteRenderer leftBarrier,
        SpriteRenderer rightBarrier)
    {
        visualRenderer = visual;
        borderRenderer = border;
        leftBarrierRenderer = leftBarrier;
        rightBarrierRenderer = rightBarrier;
    }

    public void UpdatePosition()
    {
        Vector3 pos = transform.position;
        pos.x = currentOffset;
        transform.position = pos;
    }

    public void UpdateMovement(float deltaTime)
    {
        currentOffset = Mathf.Sin(Time.time * moveSpeed + phaseOffset) * moveRange;
        UpdatePosition();

        // 处理触发闪烁效果
        if (isTriggered)
        {
            triggerFlashTimer -= deltaTime;
            if (triggerFlashTimer <= 0f)
            {
                isTriggered = false;
                if (visualRenderer != null)
                {
                    visualRenderer.color = normalColor;
                }

                if (borderRenderer != null)
                {
                    borderRenderer.color = Color.white;
                }
            }
        }
    }

    public void OnTriggerEnter()
    {
        HandleTrigger();
    }

    public void OnTriggerStay()
    {
        HandleTrigger();
    }

    private void HandleTrigger()
    {
        if (Time.time - lastTriggerTime < triggerCooldown)
            return;

        lastTriggerTime = Time.time;

        isTriggered = true;
        triggerFlashTimer = 0.2f;
        if (visualRenderer != null)
        {
            visualRenderer.color = triggeredColor;
        }

        if (borderRenderer != null)
        {
            borderRenderer.color = Color.red;
        }

        if (manager != null)
        {
            manager.SpawnBalls(transform.position, spawnCount);
        }
    }
}