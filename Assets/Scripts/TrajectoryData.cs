using System.Collections.Generic;
using UnityEngine;

namespace DefaultNamespace
{
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// 轨迹数据 - 使用ScriptableObject存储，方便在编辑器中编辑
    /// </summary>
    [CreateAssetMenu(fileName = "TrajectoryData", menuName = "弹珠台/轨迹数据")]
    public class TrajectoryData : ScriptableObject
    {
        [Header("基本信息")]
        public string id;           // 唯一ID
        public int targetSlotId;              // 目标槽位ID
        public float totalDuration;           // 总时长（秒）
    
        [Header("起始条件")]
        public Vector3 startPosition;         // 起始位置
        public Vector3 startVelocity;         // 起始速度
    
        [Header("轨迹帧数据")]
        public List<TrajectoryFrame> frames;  // 所有帧
    
        [Header("元数据（可选）")]
        public int bounceCount;               // 弹跳次数
        public string difficulty;             // 难度标签
        public float qualityScore;            // 质量评分（用于筛选）
    }

    /// <summary>
    /// 单帧数据 - 使用struct减少内存占用
    /// </summary>
    [System.Serializable]
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