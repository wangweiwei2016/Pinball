using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 怪物。从顶部出生，沿所在列向下移动，遇到角色阻挡时停止并攻击。
    /// 到达最底部（FailRow）时通知 BattleManager 判定失败。
    /// </summary>
    public class Monster : EntityBase
    {
        [Header("怪物配置")]
        public MonsterData data;

        [Header("移动")]
        [Tooltip("每格移动耗时（秒）。")]
        public float moveInterval = 1.5f;
        private float moveTimer;
        private bool isMoving;

        /// <summary>是否被阻挡（路径上有角色）。</summary>
        public bool IsBlocked { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            team = Team.Monster;
            moveSpeed = cellSizeToWorldMoveSpeed();
            ApplyData(data);
        }

        /// <summary>
        /// 根据格子系统的 cellSize 把 moveInterval（秒/格）
        /// 转换为 moveSpeed（格子/秒）。
        /// </summary>
        private float cellSizeToWorldMoveSpeed()
        {
            // SmoothMove 用格子中心距离 / moveSpeed，
            // 所以 moveSpeed = cellSize / moveInterval
            if (gridSystem == null) return 1f;
            return gridSystem.cellSize / Mathf.Max(0.01f, moveInterval);
        }

        protected override void Update()
        {
            base.Update();
            if (!IsAlive) return;

            // 如果正在平滑移动，等移动完成
            if (isMoving)
            {
                // SmoothMove 协程结束后 currentCell 已更新
                return;
            }

            // 1. 优先攻击：攻击范围内有敌方就停下攻击
            var target = GetAttackTarget();
            if (target != null)
            {
                IsBlocked = true;
                TryAttack();
                return;
            }

            // 2. 没有攻击目标，继续向下移动
            IsBlocked = false;
            moveTimer += Time.deltaTime;
            if (moveTimer >= moveInterval)
            {
                moveTimer = 0f;
                MoveDown();
            }
        }

        /// <summary>尝试向下移动一格。</summary>
        private void MoveDown()
        {
            if (gridSystem == null || currentCell == null) return;

            var nextCell = gridSystem.GetCellBelow(currentCell.col, currentCell.row);

            // 已到达 FailRow 或越界 → 失败
            if (nextCell == null || nextCell.row <= gridSystem.FailRow)
            {
                // 到达最底部：触发失败事件
                BattleManager.Instance?.OnMonsterReachBottom(this);
                return;
            }

            // 路径被阻挡（前方格子有角色）
            if (!nextCell.IsEmpty)
            {
                IsBlocked = true;
                return;
            }

            // 移动
            bool moved = MoveToCell(nextCell);
            if (moved)
            {
                isMoving = true;
                // SmoothMove 完成后需要重置 isMoving
                // 用协程的 onComplete 不方便，这里用简单延时
                StartCoroutine(MarkMoveDone(moveInterval));
            }
        }

        private System.Collections.IEnumerator MarkMoveDone(float delay)
        {
            yield return new WaitForSeconds(delay);
            isMoving = false;
        }

        /// <summary>从 MonsterData 配置加载属性。</summary>
        public void ApplyData(MonsterData cfg)
        {
            if (cfg == null) return;
            data = cfg;
            maxHp = cfg.maxHp;
            attack = cfg.attack;
            attackRange = cfg.attackRange;
            attackInterval = cfg.attackInterval;
            moveInterval = cfg.moveInterval;
            SetHp(maxHp);

            var sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                if (cfg.sprite != null) sr.sprite = cfg.sprite;
                sr.color = cfg.bodyColor;
            }
        }

        public override void Die()
        {
            string showName = data != null ? data.displayName : gameObject.name;
            Debug.Log($"[Monster] {showName} 被击败");
            BattleManager.Instance?.OnMonsterDefeated(this);
            base.Die();
        }
    }
}
