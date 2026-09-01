using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DefaultNamespace
{
    /// <summary>
    /// 轨迹录制器：在编辑器 Play 模式下用真实物理 + BallPathController 跑一遍弹珠路径，
    /// 逐帧记录位置/速度/旋转，球入槽时保存为 ScriptableObject 资源，
    /// 供运行时 TrajectoryPlayer 回放，实现塔塔冒险队式假物理弹珠。
    /// 录制开关由 GameManager 全局统一管理（F6 切换）。
    /// </summary>
    public class TrajectoryRecorder : MonoBehaviour
    {
        [Header("录制设置")]
        [Tooltip("单条轨迹最大录制时长（秒），超时自动停止保存。")]
        public float recordDuration = 15f;

        [Tooltip("录制保存目录（相对 Assets 的资源路径），Resources.LoadAll 会从此加载。")]
        public string resourceFolder = "Assets/Resources/Trajectories";

        [Header("引用")]
        public Rigidbody2D ballRb;

        private readonly List<TrajectoryFrame> recordedFrames = new List<TrajectoryFrame>();
        private float timer = 0f;
        private bool isRecording = false;
        private Vector2 startPosition;
        private Vector3 startVelocity;  // 仅用于 .magnitude，保持 Vector3

        // 特殊撞击器碰撞次数
        private int starHitCount = 0;
        private int shieldHitCount = 0;

        /// <summary>是否正在录制。</summary>
        public bool IsRecording => isRecording;

        /// <summary>全局录制开关（从 GameManager 读取）。</summary>
        public bool enableRecording
        {
            get
            {
                if (GameManager.Instance != null)
                    return GameManager.Instance.EnableRecording;
                return false;
            }
        }

        /// <summary>
        /// 开始录制。由 Launcher 在录制模式发球时调用。
        /// </summary>
        public void StartRecording()
        {
            if (ballRb == null)
            {
                ballRb = GetComponent<Rigidbody2D>();
                if (ballRb == null) return;
            }

            recordedFrames.Clear();
            timer = 0f;
            isRecording = true;
            starHitCount = 0;
            shieldHitCount = 0;

            // 确保真实物理开启
            ballRb.isKinematic = false;

            startPosition = ballRb.position;
            startVelocity = ballRb.velocity;

            // 立即写入第 0 帧
            CaptureFrame();
        }

        /// <summary>
        /// 通知球碰到了特殊撞击器（按 GameObject 名字判定）。
        /// 由 Ball.OnCollisionEnter2D 在录制期间调用。
        /// </summary>
        public void NotifySpecialHit(string bumperName)
        {
            if (!isRecording) return;

            if (bumperName != null && bumperName.Contains(Const.NAME_KEY_STAR))
            {
                starHitCount++;
            }
            else if (bumperName != null && bumperName.Contains(Const.NAME_KEY_SHIELD))
            {
                shieldHitCount++;
            }
        }

        public int StarHitCount => starHitCount;
        public int ShieldHitCount => shieldHitCount;

        private void FixedUpdate()
        {
            if (!isRecording) return;

            timer += Time.fixedDeltaTime;
            if (timer > recordDuration)
            {
                StopAndSave(-1); // 超时：未入槽，标记为无效槽
                return;
            }

            CaptureFrame();
        }

        private void CaptureFrame()
        {
            // 时间戳量化为 0.01s 单位的 ushort（上限 ~655s，覆盖 15s 录制上限）
            ushort scaledTime = (ushort)Mathf.Clamp(Mathf.RoundToInt(timer * 100f), 0, ushort.MaxValue);
            recordedFrames.Add(new TrajectoryFrame(ballRb.position, scaledTime));
        }

        /// <summary>
        /// 停止录制并以指定目标槽位保存为资源。由 GameManager 在球入槽时调用。
        /// </summary>
        public void StopAndSave(int targetSlotId)
        {
            if (!isRecording) return;

            isRecording = false;
            CaptureFrame(); // 补一帧终态

            if (recordedFrames.Count < 2)
            {
                Debug.LogWarning("[TrajectoryRecorder] 帧数过少，未保存。");
                return;
            }

#if UNITY_EDITOR
            SaveAsAsset(targetSlotId);
#else
            Debug.Log("[TrajectoryRecorder] 运行时不可保存资源，已忽略。");
#endif
        }

        /// <summary>停止录制但不保存（用于重置等场景）。</summary>
        public void Cancel()
        {
            isRecording = false;
        }

#if UNITY_EDITOR
        private void SaveAsAsset(int targetSlotId)
        {
            string folder = resourceFolder;
            if (!AssetDatabase.IsValidFolder(folder))
            {
                // 逐级创建 Assets/Resources/Trajectories
                string[] parts = folder.Split('/');
                string current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(current, parts[i]);
                    current = next;
                }
            }

            var asset = ScriptableObject.CreateInstance<TrajectoryData>();
            asset.targetSlotId = targetSlotId;
            asset.frames = new List<TrajectoryFrame>(recordedFrames);
            asset.startPosition = startPosition;
            asset.startSpeed = startVelocity.magnitude;
            asset.starHitCount = (byte)starHitCount;
            asset.shieldHitCount = (byte)shieldHitCount;

            string tag = "";
            if (starHitCount > 0) tag += $"_star{starHitCount}";
            if (shieldHitCount > 0) tag += $"_shield{shieldHitCount}";
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string path = $"{folder}/slot_{targetSlotId}{tag}_{stamp}.asset";
            path = AssetDatabase.GenerateUniqueAssetPath(path);

            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[TrajectoryRecorder] 已保存轨迹：{path}（{recordedFrames.Count} 帧，槽位 {targetSlotId}，Star×{starHitCount}，Shield×{shieldHitCount}）");
        }
#endif
    }
}
