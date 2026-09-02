using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 攻击视觉体：普攻投射物 / 技能 AOE 效果。
    /// 负责：从攻击者飞到目标位置 → 命中后查找 IDamageable 扣血 → 自毁。
    /// 所有伤害仍走 IDamageable.TakeDamage 接口，不引入平行伤害分发。
    /// </summary>
    public class AttackVisual : MonoBehaviour
    {
        [Header("伤害")]
        [Tooltip("伤害值。")]
        public int damage = 10;

        [Tooltip("攻击者（用于阵营过滤，不对友军造成伤害）。")]
        public EntityBase attacker;

        [Header("移动")]
        [Tooltip("飞行速度（单位/秒）。")]
        public float flySpeed = 10f;

        [Tooltip("目标格子（到达后在此格子找目标）。")]
        public GridCell targetCell;

        [Header("AOE（技能用）")]
        [Tooltip("是否为 AOE 技能效果。true 时对范围内所有敌方造成伤害。")]
        public bool isAoe = false;

        [Tooltip("AOE 半径（切比雪夫距离，格子数）。")]
        public int aoeRadius = 2;

        [Tooltip("AOE 视觉停留时长（秒）。")]
        public float aoeDisplayDuration = 0.3f;

        [Header("命中视觉")]
        [Tooltip("命中后特效颜色（闪一下）。")]
        public Color hitFlashColor = Color.yellow;

        // 已处理标记，避免 AOE 多次命中同一实体
        private System.Collections.Generic.HashSet<EntityBase> hitTargets;

        /// <summary>
        /// 初始化普攻投射物：从 attacker 飞向 targetCell，到达后对 targetCell 的 occupant 扣血。
        /// </summary>
        public void InitBasicAttack(EntityBase attacker, GridCell targetCell, int damage, float speed)
        {
            this.attacker = attacker;
            this.targetCell = targetCell;
            this.damage = damage;
            this.flySpeed = speed;
            this.isAoe = false;
            this.hitTargets = new System.Collections.Generic.HashSet<EntityBase>();

            Vector2 startPos = attacker != null ? (Vector2)attacker.transform.position : Vector2.zero;
            Vector2 endPos = targetCell != null ? targetCell.center : startPos;
            StartCoroutine(FlyAndHit(startPos, endPos));
        }

        /// <summary>
        /// 初始化 AOE 技能效果：在 targetCell 位置显示 AOE，然后对范围内所有敌方扣血。
        /// </summary>
        public void InitAoeAttack(EntityBase attacker, GridCell centerCell, int damage, int radius, float displayDuration)
        {
            this.attacker = attacker;
            this.targetCell = centerCell;
            this.damage = damage;
            this.isAoe = true;
            this.aoeRadius = radius;
            this.aoeDisplayDuration = displayDuration;
            this.hitTargets = new System.Collections.Generic.HashSet<EntityBase>();

            // AOE 直接放在中心位置，停留一段时间后扣血并消失
            transform.position = centerCell != null ? centerCell.center : (Vector2)attacker.transform.position;
            StartCoroutine(AoeHitAndDie());
        }

        /// <summary>普攻飞行协程：从起点飞到终点，到达后扣血。</summary>
        private System.Collections.IEnumerator FlyAndHit(Vector2 from, Vector2 to)
        {
            transform.position = from;
            float distance = Vector2.Distance(from, to);
            float duration = flySpeed > 0f ? distance / flySpeed : 0.01f;
            float t = 0f;

            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                transform.position = Vector2.Lerp(from, to, k);
                yield return null;
            }

            // 到达目标位置 → 扣血
            DealBasicHit();
            Destroy(gameObject);
        }

        /// <summary>AOE 停留协程：显示一段时间后扣血并消失。</summary>
        private System.Collections.IEnumerator AoeHitAndDie()
        {
            // 先显示视觉效果
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 0.6f);

            yield return new WaitForSeconds(aoeDisplayDuration * 0.5f);

            // 扣血
            DealAoeHit();

            yield return new WaitForSeconds(aoeDisplayDuration * 0.5f);
            Destroy(gameObject);
        }

        /// <summary>普攻命中：查找 targetCell 上的敌方实体并扣血。</summary>
        private void DealBasicHit()
        {
            if (targetCell == null || attacker == null) return;

            var target = targetCell.occupant;
            if (target != null && target.IsAlive && target.Team != attacker.Team)
            {
                target.TakeDamage(damage, gameObject);
                hitTargets.Add(target);
                FlashHit();
            }
        }

        /// <summary>AOE 命中：查找范围内所有敌方实体并扣血。</summary>
        private void DealAoeHit()
        {
            if (targetCell == null || attacker == null) return;
            var grid = attacker.gridSystem;
            if (grid == null) return;

            for (int dr = -aoeRadius; dr <= aoeRadius; dr++)
            {
                for (int dc = -aoeRadius; dc <= aoeRadius; dc++)
                {
                    var cell = grid.GetCell(targetCell.col + dc, targetCell.row + dr);
                    if (cell == null || cell.IsEmpty) continue;

                    var target = cell.occupant;
                    if (target == null || !target.IsAlive) continue;
                    if (target.Team == attacker.Team) continue;
                    if (hitTargets.Contains(target)) continue;

                    target.TakeDamage(damage, gameObject);
                    hitTargets.Add(target);
                }
            }
            FlashHit();
        }

        /// <summary>命中闪白效果。</summary>
        private void FlashHit()
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                var orig = sr.color;
                sr.color = hitFlashColor;
                StartCoroutine(FlashRestore(orig, 0.1f));
            }
        }

        private System.Collections.IEnumerator FlashRestore(Color orig, float delay)
        {
            yield return new WaitForSeconds(delay);
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = orig;
        }
    }
}
