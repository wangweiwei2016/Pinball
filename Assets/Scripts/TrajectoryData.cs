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
        public string id;                    // 唯一 ID
        public int targetSlotId;             // 目标（实际落入的）槽位 ID
        public float totalDuration;         // 总时长（秒）

        [Header("起始条件")]
        public Vector3 startPosition;        // 起始位置（2D：z=0）
        public Vector3 startVelocity;       // 起始速度（2D：z=0）

        [Header("轨迹帧数据")]
        public List<TrajectoryFrame> frames; // 所有帧

        [Header("元数据（可选）")]
        public int bounceCount;              // 弹跳次数
        public string difficulty;           // 难度标签
        public float qualityScore;          // 质量评分（用于筛选）

        [Header("特殊撞击器记录")]
        [Tooltip("本轨迹录制过程中，球是否撞击过 SpecialStar 撞击器。")]
        public bool hitSpecialStar;

        [Tooltip("本轨迹录制过程中，球是否撞击过 SpecialShield 撞击器。")]
        public bool hitSpecialShield;
    }

    /// <summary>
    /// 单帧数据 - 使用 struct 减少内存占用。
    /// 2D 游戏：position/velocity 的 z 分量为 0，rotation 仅绕 Z 轴。
    /// </summary>
    [Serializable]
    public struct TrajectoryFrame
    {
        public Vector3 position;
        public Vector3 velocity;
        public Quaternion rotation;
        public float timestamp;

        public TrajectoryFrame(Vector3 pos, Vector3 vel, Quaternion rot, float time)
        {
            position = pos;
            velocity = vel;
            rotation = rot;
            timestamp = time;
        }
    }
}
