using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 怪物配置。运行时作为模板创建 Monster 实例。
    /// 在编辑器中 Create → Battle → Monster Data 创建。
    /// </summary>
    [CreateAssetMenu(fileName = "MonsterData", menuName = "Battle/Monster Data")]
    public class MonsterData : ScriptableObject
    {
        [Header("显示")]
        public string displayName = "怪物";
        public Sprite sprite;
        public Color bodyColor = new Color(1f, 0.4f, 0.4f);

        [Header("基础属性")]
        public int maxHp = 60;
        public int attack = 8;
        public int attackRange = 1;        // 格子数
        public float attackInterval = 1.2f;// 秒

        [Header("移动")]
        [Tooltip("每格移动耗时（秒）。")]
        public float moveInterval = 1.5f;
    }
}
