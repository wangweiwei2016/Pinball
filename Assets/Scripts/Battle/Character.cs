using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 玩家角色（塔塔）。放置在底部格子，自动攻击范围内的怪物。
    /// 继承 EntityBase，额外有技能系统。
    /// </summary>
    public class Character : EntityBase
    {
        [Header("角色配置")]
        public CharacterData data;

        [Header("技能")]
        [Tooltip("技能伤害倍率。0 表示无技能。")]
        public float skillMultiplier = 2.5f;
        [Tooltip("技能冷却（秒）。")]
        public float skillCooldown = 8f;
        private float skillTimer;

        /// <summary>技能是否就绪。</summary>
        public bool SkillReady => skillTimer <= 0f;

        protected override void Awake()
        {
            base.Awake();
            team = Team.Player;
            moveSpeed = 0f; // 角色不移动，固定在格子上
            ApplyData(data);
        }

        protected override void Update()
        {
            base.Update();
            if (skillTimer > 0f) skillTimer -= Time.deltaTime;

            // 技能就绪时自动释放（有目标就放）
            if (SkillReady && skillMultiplier > 0f)
            {
                var target = GetAttackTarget();
                if (target != null)
                {
                    UseSkill(target);
                }
            }
            // 否则走普攻
            else if (!SkillReady)
            {
                TryAttack();
            }
        }

        /// <summary>
        /// 释放技能：生成 AOE 视觉，显示后对范围内所有敌方造成伤害。
        /// 冷却重置。
        /// </summary>
        public void UseSkill(EntityBase primaryTarget)
        {
            if (skillMultiplier <= 0f) return;
            if (gridSystem == null || currentCell == null) return;

            int skillRange = 1;//Mathf.Max(attackRange + 1, 2);
            float skillDamage = attack * skillMultiplier;

            // 以主要目标所在格子为 AOE 中心，若目标已死则用自身位置
            GridCell aoeCenter = primaryTarget != null && primaryTarget.currentCell != null
                ? primaryTarget.currentCell
                : currentCell;

            SpawnAoeAttackVisual(aoeCenter, Mathf.RoundToInt(skillDamage), skillRange, 0.4f);

            skillTimer = skillCooldown;
            string showName = data != null ? data.skillName : "";
            Debug.Log($"[Character] 技能释放！{showName} 伤害={skillDamage}");
        }

        /// <summary>从 CharacterData 配置加载属性。</summary>
        public void ApplyData(CharacterData cfg)
        {
            if (cfg == null) return;
            data = cfg;
            maxHp = cfg.maxHp;
            attack = cfg.attack;
            attackRange = cfg.attackRange;
            attackInterval = cfg.attackInterval;
            skillMultiplier = cfg.skillMultiplier;
            skillCooldown = cfg.skillCooldown;
            SetHp(maxHp);

            var sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                if (cfg.sprite != null) sr.sprite = cfg.sprite;
                sr.color = cfg.bodyColor;
            }
        }

        /// <summary>角色死亡回调。</summary>
        public override void Die()
        {
            string showName = data != null ? data.displayName : gameObject.name;
            Debug.Log($"[Character] {showName} 阵亡");
            base.Die();
        }
    }
}
