using System.Collections.Generic;
using UnityEngine;

namespace DefaultNamespace
{
    public class TrajectoryPlayer : MonoBehaviour
    {
        public Rigidbody ballRb;
        public TrajectoryLibrary trajectoryLibrary; // 引用所有已加载的轨迹数据

        private TrajectoryData currentTrajectory;
        private int currentFrameIndex;
        private float playbackTimer;
        private bool isPlaying = false;
        private Vector3 randomOffsetSeed;
        private Vector3 currentVelocity;
        
        // 根据目标槽位选择轨迹
        public bool PlayTrajectoryForSlot(int targetSlotId, Vector3 currentLaunchPos, float launchForce)
        {
            // 从轨迹库中筛选符合条件的轨迹
            List<TrajectoryData> candidates = trajectoryLibrary.GetTrajectoriesBySlot(targetSlotId);

            if (candidates.Count == 0)
            {
                Debug.LogError($"没有找到槽位 {targetSlotId} 的轨迹！");
                return false;
            }

            // 选择最匹配当前发射参数的轨迹（距离和力度最接近的）
            TrajectoryData bestMatch = FindBestMatch(candidates, currentLaunchPos, launchForce);

            if (bestMatch == null)
            {
                bestMatch = candidates[Random.Range(0, candidates.Count)]; // 保底随机选
            }

            // 开始回放
            StartPlayback(bestMatch);
            return true;
        }

        TrajectoryData FindBestMatch(List<TrajectoryData> candidates, Vector3 launchPos, float launchForce)
        {
            float bestScore = float.MaxValue;
            TrajectoryData best = null;

            foreach (var traj in candidates)
            {
                // 计算起始位置差异
                float posDiff = Vector3.Distance(traj.startPosition, launchPos);
                // 计算起始速度差异（力度）
                float forceDiff = Mathf.Abs(traj.startVelocity.magnitude - launchForce);

                // 综合评分（权重可调）
                float score = posDiff * 0.3f + forceDiff * 0.7f;

                if (score < bestScore)
                {
                    bestScore = score;
                    best = traj;
                }
            }

            return best;
        }

        void StartPlayback(TrajectoryData trajectory)
        {
            currentTrajectory = trajectory;
            currentFrameIndex = 0;
            playbackTimer = 0f;
            isPlaying = true;

            // 关闭物理模拟，变成纯运动学
            ballRb.isKinematic = true;
            ballRb.useGravity = false;

            // 设置初始位置为轨迹起点（需要对齐发射位置）
            ballRb.position = currentTrajectory.frames[0].position;
            ballRb.rotation = currentTrajectory.frames[0].rotation;

            // 添加一个非常微小的随机偏移种子，让每次看起来略有不同
            randomOffsetSeed = new Vector3(
                Random.Range(-0.01f, 0.01f),
                0,
                Random.Range(-0.01f, 0.01f)
            );
        }

        void FixedUpdate()
        {
            if (!isPlaying || currentTrajectory == null) return;

            playbackTimer += Time.fixedDeltaTime;

            // 根据时间戳找到当前应该在哪一帧
            while (currentFrameIndex < currentTrajectory.frames.Count - 1 &&
                   currentTrajectory.frames[currentFrameIndex + 1].timestamp <= playbackTimer)
            {
                currentFrameIndex++;
            }

            if (currentFrameIndex >= currentTrajectory.frames.Count - 1)
            {
                // 播放结束
                FinishPlayback();
                return;
            }

            // 插值计算当前帧的位置和旋转
            TrajectoryFrame current = currentTrajectory.frames[currentFrameIndex];
            TrajectoryFrame next = currentTrajectory.frames[currentFrameIndex + 1];
            float t = (playbackTimer - current.timestamp) / (next.timestamp - current.timestamp);

            // 位置插值（加微小随机偏移，让轨迹看起来不100%重复）
            Vector3 targetPos = Vector3.Lerp(current.position, next.position, t);
            targetPos += randomOffsetSeed * (1f - t); // 偏移随进度逐渐消失，确保最终落点精确

            // 旋转插值
            Quaternion targetRot = Quaternion.Slerp(current.rotation, next.rotation, t);

            // 应用位置和旋转
            ballRb.MovePosition(targetPos);
            ballRb.MoveRotation(targetRot);

            // 速度用于碰撞特效和音效（虽然不参与物理，但可用于表现）
            currentVelocity = Vector3.Lerp(current.velocity, next.velocity, t);
        }

        void FinishPlayback()
        {
            isPlaying = false;

            // 确保精确落在目标位置
            Vector3 finalPos = currentTrajectory.frames[currentTrajectory.frames.Count - 1].position;
            ballRb.position = finalPos;

            // 触发落点事件（显示奖励、播放特效等）
            //OnBallLanded(currentTrajectory.targetSlotId);
        }
    }
}