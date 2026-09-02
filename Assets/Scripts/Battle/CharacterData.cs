using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 角色配置。运行时作为模板创建 Character 实例。
    /// 在编辑器中 Create → Battle → Character Data 创建。
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterData", menuName = "Battle/Character Data")]
    public class CharacterData : ScriptableObject
    {
        [Header("显示")]
        public string displayName = "角色";
        public Sprite sprite;
        public Color bodyColor = new Color(0.4f, 0.7f, 1f);

        [Header("基础属性")]
        public int maxHp = 100;
        public int attack = 15;
        public int attackRange = 2;       // 格子数
        public float attackInterval = 1f; // 秒

        [Header("技能")]
        [Tooltip("技能伤害倍率（相对 attack）。0 表示无技能。")]
        public float skillMultiplier = 2.5f;
        [Tooltip("技能冷却（秒）。")]
        public float skillCooldown = 8f;
        [Tooltip("技能名称（调试用）。")]
        public string skillName = "AOE";
    }
}
