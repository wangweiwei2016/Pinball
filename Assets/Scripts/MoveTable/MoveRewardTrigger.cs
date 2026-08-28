using System.Collections;
using UnityEngine;

namespace DefaultNamespace.MoveTable
{
    /// <summary>
    /// 移动奖励触发器：从右向左匀速移动，到达左边界后延迟重置到右侧
    /// </summary>
    public class MoveRewardTrigger : MonoBehaviour
    {
        [Header("移动设置（匀速）")] [Tooltip("移动速度（单位/秒）")]
        public float moveSpeed = 2f;

        [Tooltip("移动范围")] public float moveRange = 10f;

        [Header("重置设置")] [Tooltip("到达左边界后延迟多久才从右侧重新出现")]
        public float resetDelay = 1.5f;

        [Tooltip("重置时是否播放闪烁效果")] public bool enableResetFlash = true;

        [Header("奖励设置")] public int maxScore = 5;

        private BarTriggerManager manager;
        private SpriteRenderer spriteRenderer;
        private BoxCollider2D boxCollider;
        private Collider2D triggerCollider;

        // ★ 关键：触发器之间的固定水平间距
        private float horizontalSpacing;
        private int triggerIndex;
        private float yPosition;

        // 移动状态
        private float currentX;
        private float leftBoundary;
        private float rightBoundary;
        private float halfWidth;

        // ★ 重置延迟状态
        private bool isWaitingToReset = false;
        private float resetTimer = 0f;
        private bool isVisible = true;

        // 其他状态
        private int currentScore;
        private Color normalColor = new Color(0.2f, 0.6f, 1f, 0.7f);
        private Color hitColor = new Color(1f, 0.8f, 0f, 0.9f);
        private Color destroyColor = new Color(1f, 0.2f, 0.2f, 0.9f);
        private bool isFlashing = false;
        private float flashTimer = 0f;
        private float flashDuration = 0.15f;

        // ★ 重置时的发光效果
        private float glowIntensity = 0f;
        private bool isGlowing = false;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            boxCollider = GetComponent<BoxCollider2D>();
            triggerCollider = GetComponent<Collider2D>();

            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = CreateSquareSprite();
                spriteRenderer.color = normalColor;
                spriteRenderer.sortingOrder = 10;
            }

            if (boxCollider == null)
            {
                boxCollider = gameObject.AddComponent<BoxCollider2D>();
                boxCollider.size = new Vector2(4f, 0.5f);
                boxCollider.isTrigger = true;
            }

            spriteRenderer.size = boxCollider.size;
            currentScore = maxScore;
            UpdateVisual();
        }

        public void Initialize(
            BarTriggerManager manager,
            int triggerIndex,
            float yPosition,
            float startX,
            float horizontalSpacing,
            float moveSpeed,
            float moveRange,
            int maxScore = 5,
            float resetDelay = 1.5f
        )
        {
            this.manager = manager;
            this.triggerIndex = triggerIndex;
            this.yPosition = yPosition;
            this.horizontalSpacing = horizontalSpacing;
            this.moveSpeed = moveSpeed;
            this.moveRange = moveRange;
            this.maxScore = maxScore;
            this.resetDelay = resetDelay;

            currentScore = maxScore;

            // 起始位置 = 基础起始位置 + 索引 × 间距
            currentX = startX + triggerIndex * horizontalSpacing;

            // 计算边界
            halfWidth = boxCollider.size.x / 2f;
            leftBoundary = -moveRange / 2f - halfWidth;
            rightBoundary = moveRange / 2f + halfWidth;

            // 初始可见
            isVisible = true;
            isWaitingToReset = false;
            SetVisibility(true);

            UpdatePosition();
            UpdateVisual();
        }

        public void UpdatePosition()
        {
            Vector3 pos = transform.position;
            pos.x = currentX;
            pos.y = yPosition;
            transform.position = pos;
        }

        /// <summary>
        /// 更新移动（每帧调用）
        /// </summary>
        public void UpdateMovement(float deltaTime)
        {
            // ★ 如果正在等待重置，不移动
            // if (isWaitingToReset)
            // {
            //     // 倒计时
            //     resetTimer -= deltaTime;
            //
            //     // ★ 延迟结束后，从右侧重新出现
            //     if (resetTimer <= 0f)
            //     {
            //         ResetToRight();
            //     }
            //
            //     return;
            // }

            // 从右向左匀速移动
            currentX -= moveSpeed * deltaTime;

            // ★ 检查是否超出左边界
            if (currentX < leftBoundary)
            {
                // ★ 进入等待重置状态
                ResetToRight();
            }

            UpdatePosition();

            // 处理闪烁
            if (isFlashing)
            {
                flashTimer -= deltaTime;
                if (flashTimer <= 0f)
                {
                    isFlashing = false;
                    if (spriteRenderer != null && isVisible)
                    {
                        spriteRenderer.color = normalColor;
                    }
                }
            }

            // ★ 处理重置时的发光效果
            if (isGlowing)
            {
                glowIntensity -= deltaTime * 2f;
                if (glowIntensity <= 0f)
                {
                    isGlowing = false;
                    if (spriteRenderer != null && isVisible)
                    {
                        spriteRenderer.color = normalColor;
                    }
                }
                else if (spriteRenderer != null && isVisible)
                {
                    // 发光效果：颜色变亮
                    Color glowColor = Color.Lerp(normalColor, Color.white, glowIntensity);
                    spriteRenderer.color = glowColor;
                }
            }
        }

        /// <summary>
        /// ★ 开始延迟重置
        /// </summary>
        private void StartResetDelay()
        {
            isWaitingToReset = true;
            resetTimer = resetDelay;

            // 隐藏触发器
            SetVisibility(false);

            // 可以在这里添加"消失"特效
            OnDisappear();
        }

        /// <summary>
        /// ★ 重置到右侧
        /// </summary>
        private void ResetToRight()
        {
            // ★ 重置位置到右侧，保持与第一个触发器的固定间距
            currentX = rightBoundary + 3f;

            // 更新位置
            UpdatePosition();
        }

        /// <summary>
        /// 设置可见性
        /// </summary>
        private void SetVisibility(bool visible)
        {
            isVisible = visible;

            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = visible;
            }

            if (triggerCollider != null)
            {
                triggerCollider.enabled = visible;
            }
        }

        /// <summary>
        /// 消失时的回调
        /// </summary>
        private void OnDisappear()
        {
            // 可以在这里添加粒子特效或音效
            // 例如：播放消失动画
            Debug.Log($"触发器 {triggerIndex} 消失，将在 {resetDelay} 秒后重新出现");
        }

        /// <summary>
        /// 重置到右侧时的回调
        /// </summary>
        private void OnResetToRight()
        {
            if (spriteRenderer != null && enableResetFlash)
            {
                // ★ 发光效果
                isGlowing = true;
                glowIntensity = 1f;
                spriteRenderer.color = new Color(0.5f, 1f, 0.5f, 0.9f);

                // 延迟恢复颜色
                Invoke(nameof(ResetColor), 0.3f);
            }
        }

        private void ResetColor()
        {
            if (spriteRenderer != null && !isFlashing && !isGlowing && isVisible)
            {
                spriteRenderer.color = normalColor;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // ★ 如果正在等待重置或不可见，不处理碰撞
            if (isWaitingToReset || !isVisible)
                return;

            if (!other.CompareTag("Ball") && other.GetComponent<Ball>() == null)
                return;

            OnBallHit(other.gameObject);
        }

        private void OnBallHit(GameObject ball)
        {
            FlashVisual();
            currentScore--;
            UpdateVisual();

            if (ball != null)
            {
                Destroy(ball);
            }

            if (currentScore <= 0)
            {
                DestroyVisual();
                SetVisibility(false);
                //Destroy(gameObject, 0.2f);
            }
        }

        private void FlashVisual()
        {
            if (spriteRenderer == null || !isVisible) return;

            isFlashing = true;
            flashTimer = flashDuration;
            spriteRenderer.color = hitColor;
        }

        private void UpdateVisual()
        {
            if (spriteRenderer != null && isVisible)
            {
                float progress = (float)currentScore / maxScore;
                if (progress > 0.6f)
                    spriteRenderer.color = normalColor;
                else if (progress > 0.3f)
                    spriteRenderer.color = Color.Lerp(normalColor, hitColor, 0.5f);
                else
                    spriteRenderer.color = Color.Lerp(normalColor, destroyColor, 0.7f);
            }
        }

        private void DestroyVisual()
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = destroyColor;
                transform.localScale = Vector3.one * 1.2f;
            }
        }

        private Sprite CreateSquareSprite()
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        public void SetSize(float width, float height)
        {
            if (boxCollider != null)
                boxCollider.size = new Vector2(width, height);
            if (spriteRenderer != null)
                spriteRenderer.size = new Vector2(width, height);

            halfWidth = width / 2f;
            leftBoundary = -moveRange / 2f - halfWidth;
            rightBoundary = moveRange / 2f + halfWidth;
        }

        public int GetRemainingScore() => currentScore;
        public void ForceTrigger() => OnBallHit(null);

        /// <summary>
        /// 手动重置（外部调用）
        /// </summary>
        public void ManualReset()
        {
            if (!isWaitingToReset)
            {
                StartResetDelay();
            }
        }
    }
}