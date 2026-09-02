using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 可被伤害接口。所有有血量的实体（角色、怪物）都实现此接口，
    /// 攻击逻辑只依赖接口，不依赖具体类型，避免 Tag 硬编码问题。
    /// </summary>
    public interface IDamageable
    {
        /// <summary>当前血量。</summary>
        int CurrentHp { get; }

        /// <summary>最大血量。</summary>
        int MaxHp { get; }

        /// <summary>阵营。攻击时只对敌方阵营造成伤害。</summary>
        Team Team { get; }

        /// <summary>是否存活。</summary>
        bool IsAlive { get; }

        /// <summary>受到伤害。返回实际造成的伤害值。</summary>
        int TakeDamage(int damage, GameObject source);

        /// <summary>死亡回调。</summary>
        void Die();
    }

    /// <summary>阵营枚举。玩家方 vs 怪物方。</summary>
    public enum Team
    {
        Player,
        Monster
    }
}
