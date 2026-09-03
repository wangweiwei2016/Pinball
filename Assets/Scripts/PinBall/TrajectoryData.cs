using System;
using System.Collections.Generic;
using UnityEngine;

namespace DefaultNamespace
{
    /// <summary>
    /// 轨迹数据 - 使用 ScriptableObject 存储，方便在编辑器中预录与复用。
    /// 用于塔塔冒险队式假物理弹珠台：球按预录轨迹运动学回放，结果确定可控。
    /// </summary>
    [CreateAssetMenu(fileName = "TrajectoryData", menuName = "弹珠台/轨迹数据")]
    public class TrajectoryData : ScriptableObject
    {
        [Header("基本信息")]
        public int targetSlotId;             // 目标（实际落入的）槽位 ID

        [Header("起始条件")]
        public Vector2 startPosition;        // 起始位置（2D）
        public float startSpeed;             // 起始速度大小（仅用于匹配发射力度）

        [Header("轨迹帧数据")]
        [Tooltip("所有帧。每帧仅 10 字节：Vector2 位置(8) + ushort 时间戳(2)。")]
        public List<TrajectoryFrame> frames;

        [Header("特殊撞击器记录")]
        [Tooltip("本轨迹录制过程中，球撞击 SpecialStar 撞击器的次数（0-255）。")]
        public byte starHitCount;

        [Tooltip("本轨迹录制过程中，球撞击 SpecialShield 撞击器的次数（0-255）。")]
        public byte shieldHitCount;

        /// <summary>是否撞过任意一种特殊撞击器。</summary>
        public bool hasAnySpecialHit => starHitCount > 0 || shieldHitCount > 0;

        /// <summary>总时长（秒），由末帧时间戳派生（0.01s 精度），避免冗余存储。</summary>
        public float totalDuration => (frames != null && frames.Count > 0)
            ? frames[frames.Count - 1].timestamp * 0.01f
            : 0f;
    }

    /// <summary>
    /// 单帧数据 - 极简结构以最小化存储。
    /// 仅保留位置(Vector2, 8B) + 时间戳(ushort, 2B) = 10B/帧（原 44B，减 77%）。
    /// 移除 velocity：回放仅靠位置插值，速度不入档。
    /// 移除 rotation：2D 弹珠为均匀圆形，旋转不影响物理/视觉。
    /// 时间戳使用 ushort（0.01s 精度，上限约 655s），覆盖 15s 录制上限绰绰有余。
    /// </summary>
    [Serializable]
    public struct TrajectoryFrame
    {
        public Vector2 position;
        public ushort timestamp;

        public TrajectoryFrame(Vector2 pos, ushort scaledTime)
        {
            position = pos;
            timestamp = scaledTime;
        }
    }
}
