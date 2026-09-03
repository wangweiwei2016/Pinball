using System.Collections.Generic;
using UnityEngine;

namespace DefaultNamespace
{
    /// <summary>
    /// 轨迹回放器：把球切换为运动学体，按预录轨迹逐帧插值移动，
    /// 实现塔塔冒险队式假物理——球看起来在自然弹跳，但落点确定可控。
    /// 回放期间通过近距检测触发撞击器的视觉/音效/加分反馈，
    /// 让“弹珠在挡板和机关之间反复碰撞”的观感得以保留。
    /// </summary>
    public class TrajectoryPlayer : MonoBehaviour
    {
        [Header("引用")]
        public Rigidbody2D ballRb;

        [Header("回放微调")]
        [Tooltip("是否对位置叠加极小随机扰动，避免每次完全一模一样。")]
        public bool jitter = true;

        [Tooltip("位置随机扰动幅度（米）。")]
        public float jitterAmount = 0.01f;

        [Tooltip("撞击器近距反馈的额外判定半径，避免高速掠过漏判。")]
        public float bumperHitPadding = 0.05f;

        private TrajectoryData currentTrajectory;
        private int currentFrameIndex;
        private float playbackTimer;
        private bool isPlaying = false;
        private Vector3 jitterOffset;

        // 撞击器近距反馈缓存
        private Bumper[] cachedBumpers;
        private float[] cachedBumperRadii;
        private bool[] bumperInside;
        private float ballRadius;

        /// <summary>是否正在回放。</summary>
        public bool IsPlaying => isPlaying;

        /// <summary>当前回放轨迹的目标槽（-1 表示无）。</summary>
        public int CurrentTargetSlot => currentTrajectory != null ? currentTrajectory.targetSlotId : -1;

        /// <summary>
        /// 开始回放指定轨迹。球会被切为运动学，位置对齐到第 0 帧。
        /// </summary>
        public void PlayTrajectory(TrajectoryData trajectory)
        {
            if (trajectory == null || trajectory.frames == null || trajectory.frames.Count < 2)
            {
                Debug.LogWarning("[TrajectoryPlayer] 轨迹无效，无法回放。");
                return;
            }

            if (ballRb == null) ballRb = GetComponent<Rigidbody2D>();
            if (ballRb == null) return;

            currentTrajectory = trajectory;
            currentFrameIndex = 0;
            playbackTimer = 0f;
            isPlaying = true;

            // 切为运动学：忽略重力/受力，完全由回放驱动位置
            ballRb.isKinematic = true;
            ballRb.velocity = Vector2.zero;
            ballRb.angularVelocity = 0f;

            TrajectoryFrame f0 = currentTrajectory.frames[0];
            ballRb.position = f0.position;
            // 旋转不入档：2D 圆形弹珠无需还原旋转

            jitterOffset = jitter
                ? new Vector3(Random.Range(-jitterAmount, jitterAmount), Random.Range(-jitterAmount, jitterAmount), 0f)
                : Vector3.zero;

            PrepareBumperCache();
        }

        private void FixedUpdate()
        {
            if (!isPlaying || currentTrajectory == null) return;

            playbackTimer += Time.fixedDeltaTime;

            var frames = currentTrajectory.frames;
            // 时间戳以 0.01s 为单位存储为 ushort，回放时换算回秒
            while (currentFrameIndex < frames.Count - 1 &&
                   frames[currentFrameIndex + 1].timestamp * 0.01f <= playbackTimer)
            {
                currentFrameIndex++;
            }

            if (currentFrameIndex >= frames.Count - 1)
            {
                FinishPlayback();
                return;
            }

            TrajectoryFrame cur = frames[currentFrameIndex];
            TrajectoryFrame next = frames[currentFrameIndex + 1];
            float curTime = cur.timestamp * 0.01f;
            float nextTime = next.timestamp * 0.01f;
            float span = nextTime - curTime;
            float t = span > 0.0001f ? Mathf.Clamp01((playbackTimer - curTime) / span) : 0f;

            Vector2 targetPos = Vector2.Lerp(cur.position, next.position, t);
            if (jitter)
            {
                // 扰动随进度衰减，保证最终落点精确
                targetPos += (Vector2)jitterOffset * (1f - t) * ((float)(frames.Count - currentFrameIndex) / frames.Count);
            }

            ballRb.MovePosition(targetPos);

            TickBumperFeedback();
        }

        /// <summary>
        /// 停止回放，恢复球为动态体（供下次发球或重置使用）。
        /// 幂等：无论当前是否在回放，都复位球状态，防止回放自然结束后球以运动学体悬浮卡住。
        /// </summary>
        public void Stop()
        {
            isPlaying = false;
            currentTrajectory = null;
            if (ballRb != null)
            {
                ballRb.velocity = Vector2.zero;
                ballRb.angularVelocity = 0f;
                ballRb.isKinematic = false;
            }
        }

        private void FinishPlayback()
        {
            // 对齐到末帧
            if (currentTrajectory != null && currentTrajectory.frames.Count > 0)
            {
                TrajectoryFrame last = currentTrajectory.frames[currentTrajectory.frames.Count - 1];
                if (ballRb != null)
                {
                    ballRb.position = last.position;
                }
            }
            isPlaying = false;
            currentTrajectory = null;
            // 恢复动态体：轨迹自然播完（球未入槽）时也必须复位，
            // 否则球会以运动学体悬浮卡住，无法再次发射。
            // （球若恰好入槽，GameManager 会再调用 Stop，幂等无副作用。）
            if (ballRb != null)
            {
                ballRb.velocity = Vector2.zero;
                ballRb.angularVelocity = 0f;
                ballRb.isKinematic = false;
            }
        }

        private void PrepareBumperCache()
        {
            if (cachedBumpers == null || cachedBumpers.Length == 0)
            {
                cachedBumpers = FindObjectsOfType<Bumper>();
            }
            int count = cachedBumpers != null ? cachedBumpers.Length : 0;
            bumperInside = new bool[count];

            // 半径只缓存一次（Bumper 碰撞体/缩放通常不会在回放中变化），
            // 避免每帧对每个 Bumper 调用 GetComponent<Collider2D>
            cachedBumperRadii = new float[count];
            for (int i = 0; i < count; i++)
            {
                cachedBumperRadii[i] = GetBumperRadius(cachedBumpers[i]);
            }

            ballRadius = 0.25f;
            if (ballRb != null)
            {
                var col = ballRb.GetComponent<Collider2D>();
                if (col is CircleCollider2D circle)
                    ballRadius = circle.radius * Mathf.Max(ballRb.transform.lossyScale.x, 0.0001f);
            }
        }

        private void TickBumperFeedback()
        {
            if (cachedBumpers == null) return;
            Vector2 pos = ballRb.position;

            for (int i = 0; i < cachedBumpers.Length; i++)
            {
                Bumper b = cachedBumpers[i];
                if (b == null) continue;

                float threshold = cachedBumperRadii[i] + ballRadius + bumperHitPadding;
                float dist = ((Vector2)b.transform.position - pos).sqrMagnitude;

                if (dist < threshold * threshold)
                {
                    if (!bumperInside[i])
                    {
                        bumperInside[i] = true;
                        b.TriggerHitFeedback();
                    }
                }
                else
                {
                    bumperInside[i] = false;
                }
            }
        }

        private static float GetBumperRadius(Bumper b)
        {
            var col = b.GetComponent<Collider2D>();
            if (col is CircleCollider2D circle)
                return circle.radius * Mathf.Max(b.transform.lossyScale.x, 0.0001f);
            // 退化：用 bounds 估算
            return col != null ? Mathf.Max(col.bounds.extents.x, col.bounds.extents.y) : 0.35f;
        }
    }
}
