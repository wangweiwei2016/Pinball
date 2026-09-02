using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 战斗实体基类。角色和怪物共用，避免重复逻辑。
    /// 包含：血量、阵营、攻击范围、当前格子位置、死亡处理。
    /// 统一通过 IDamageable 接口处理伤害，通过 GridSystem.GetAttackTarget 查找目标，
    /// 避免不同子类用不同查找逻辑导致的断链问题。
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class EntityBase : MonoBehaviour, IDamageable
    {
        [Header("基础属性")]
        [Tooltip("阵营（Player 或 Monster）。")]
        public Team team;

        [Tooltip("初始最大血量。")]
        public int maxHp = 100;

        [Tooltip("攻击力。")]
        public int attack = 10;

        [Tooltip("攻击范围（切比雪夫距离，格子数）。")]
        public int attackRange = 1;

        [Tooltip("攻击间隔（秒）。")]
        public float attackInterval = 1f;

        [Tooltip("移动速度（格子/秒）。角色通常为 0，怪物为正数。")]
        public float moveSpeed = 0f;

        [Header("引用")]
        [Tooltip("所属格子系统。运行时由 GridSystem 注入或查找。")]
        public GridSystem gridSystem;

        [Tooltip("当前占用的格子。")]
        public GridCell currentCell;

        // 运行时状态
        private int _currentHp;
        private float attackCooldown;
        private SpriteRenderer spriteRenderer;
        private Color originalColor;
        private float hitFlashTimer;

        // 属性接口
        public int CurrentHp => _currentHp;
        public int MaxHp => maxHp;
        public Team Team => team;
        public bool IsAlive => _currentHp > 0;

        protected virtual void Awake()
        {
            _currentHp = maxHp;
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null) originalColor = spriteRenderer.color;
        }

        protected virtual void Update()
        {
            // 攻击冷却
            if (attackCooldown > 0f) attackCooldown -= Time.deltaTime;

            // 受击闪白
            if (hitFlashTimer > 0f)
            {
                hitFlashTimer -= Time.deltaTime;
                if (hitFlashTimer <= 0f && spriteRenderer != null)
                    spriteRenderer.color = originalColor;
            }
        }

        /// <summary>
        /// 查找攻击范围内的敌方目标。
        /// 所有子类（Character/Monster）统一走这个入口，避免不同状态用不同查找逻辑。
        /// </summary>
        public EntityBase GetAttackTarget()
        {
            if (gridSystem == null || currentCell == null) return null;
            return gridSystem.GetAttackTarget(currentCell, team, attackRange);
        }

        /// <summary>
        /// 执行一次普攻。找目标 → 生成攻击视觉 → 命中后扣血。冷却自动处理。
        /// 返回是否成功发起攻击（冷却中或无目标返回 false）。
        /// </summary>
        public bool TryAttack()
        {
            if (attackCooldown > 0f) return false;
            var target = GetAttackTarget();
            if (target == null) return false;

            SpawnBasicAttackVisual(target);
            attackCooldown = attackInterval;
            return true;
        }

        /// <summary>
        /// 生成普攻投射物。从自身飞向 target 所在格子，到达后扣血。
        /// </summary>
        protected void SpawnBasicAttackVisual(EntityBase target)
        {
            if (target == null) return;
            var go = new GameObject("Attack_" + team + "_" + target.gameObject.name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = MakeAttackSprite();
            sr.color = team == Team.Player ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 0.3f, 0.3f);
            sr.sortingOrder = 10;
            var visual = go.AddComponent<AttackVisual>();
            visual.InitBasicAttack(this, target.currentCell, attack, 12f);
        }

        /// <summary>
        /// 生成 AOE 技能效果。在指定格子位置显示视觉，然后对范围内所有敌方扣血。
        /// 由 Character.UseSkill 调用。
        /// </summary>
        protected void SpawnAoeAttackVisual(GridCell centerCell, int damage, int radius, float displayDuration)
        {
            if (centerCell == null) return;
            var go = new GameObject("AoeSkill_" + team);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = MakeAoeSprite();
            sr.color = team == Team.Player ? new Color(0.5f, 0.8f, 1f, 0.6f) : new Color(1f, 0.4f, 0.4f, 0.6f);
            sr.sortingOrder = 5;
            // AOE 视觉大小 = (radius * 2 + 1) 格
            float size = (radius * 2f + 1f) * (gridSystem != null ? gridSystem.cellSize : 1.5f);
            go.transform.localScale = new Vector3(size, size, 1f);
            var visual = go.AddComponent<AttackVisual>();
            visual.InitAoeAttack(this, centerCell, damage, radius, displayDuration);
        }

        /// <summary>生成攻击投射物用的简单圆点纹理。</summary>
        private static Sprite MakeAttackSprite()
        {
            int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            var center = size * 0.5f;
            float radius = size * 0.4f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                    tex.SetPixel(x, y, d <= radius ? Color.white : new Color(0, 0, 0, 0));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>生成 AOE 技能用的简单圆形纹理。</summary>
        private static Sprite MakeAoeSprite()
        {
            int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            var center = size * 0.5f;
            float radius = size * 0.45f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                    float alpha = d <= radius ? 0.5f : 0f;
                    tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>受到伤害。</summary>
        public int TakeDamage(int damage, GameObject source)
        {
            if (!IsAlive) return 0;
            _currentHp = Mathf.Max(0, _currentHp - damage);

            // 受击闪白
            hitFlashTimer = 0.1f;
            if (spriteRenderer != null) spriteRenderer.color = Color.white;

            if (_currentHp <= 0) Die();
            return damage;
        }

        /// <summary>死亡处理。子类可 override 加特效。</summary>
        public virtual void Die()
        {
            if (currentCell != null && gridSystem != null)
                gridSystem.Release(currentCell);
            Destroy(gameObject);
        }

        /// <summary>
        /// 将实体放置到指定格子。更新 world position 并占用格子。
        /// </summary>
        public void PlaceOnCell(GridCell cell)
        {
            if (gridSystem != null && currentCell != null)
                gridSystem.Release(currentCell);

            currentCell = cell;
            if (gridSystem != null && cell != null)
                gridSystem.Occupy(this, cell);

            if (cell != null)
                transform.position = cell.center;
        }

        /// <summary>
        /// 移动到相邻格子（由怪物子类调用）。
        /// 返回是否成功移动（前方有阻挡返回 false）。
        /// </summary>
        public bool MoveToCell(GridCell targetCell)
        {
            if (targetCell == null) return false;
            if (!targetCell.IsEmpty) return false; // 路径被阻挡

            if (gridSystem != null && currentCell != null)
                gridSystem.Release(currentCell);

            currentCell = targetCell;
            if (gridSystem != null)
                gridSystem.Occupy(this, targetCell);

            // 平滑移动到目标格子中心
            StartCoroutine(SmoothMove(targetCell.center, moveSpeed));
            return true;
        }

        private System.Collections.IEnumerator SmoothMove(Vector2 target, float speed)
        {
            float t = 0f;
            Vector2 from = transform.position;
            float distance = Vector2.Distance(from, target);
            float duration = speed > 0f ? distance / speed : 0.01f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                transform.position = Vector2.Lerp(from, target, k);
                yield return null;
            }
            transform.position = target;
        }

        protected void SetHp(int curHp)
        {
            _currentHp = curHp;
        }
    }
}
