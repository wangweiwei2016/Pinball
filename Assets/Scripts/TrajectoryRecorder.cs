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
    /// 运行时（正式包）不会录制，仅回放。
    /// </summary>
    public class TrajectoryRecorder : MonoBehaviour
    {
        [Header("录制设置")]
        [Tooltip("启用录制（仅编辑器用）。勾选后每次发球都会录制并保存为资源；正式运行时请关闭。")]
        public bool enableRecording = false;

        [Tooltip("运行时切换录制开关的按键（编辑器 Play 模式下预录轨迹用）。")]
        public KeyCode toggleKey = KeyCode.F6;

        [Tooltip("单条轨迹最大录制时长（秒），超时自动停止保存。")]
        public float recordDuration = 15f;

        [Tooltip("录制保存目录（相对 Assets 的资源路径），Resources.LoadAll 会从此加载。")]
        public string resourceFolder = "Assets/Resources/Trajectories";

        [Header("引用")]
        public Rigidbody2D ballRb;

        private readonly List<TrajectoryFrame> recordedFrames = new List<TrajectoryFrame>();
        private float timer = 0f;
        private bool isRecording = false;
        private Vector3 startPosition;
        private Vector3 startVelocity;

        /// <summary>是否正在录制。</summary>
        public bool IsRecording => isRecording;

#if UNITY_EDITOR
        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                enableRecording = !enableRecording;
                Debug.Log($"[TrajectoryRecorder] 录制模式：{(enableRecording ? "开" : "关")}（{toggleKey} 切换）");
            }
        }
#endif

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

            // 确保真实物理开启
            ballRb.isKinematic = false;

            startPosition = ballRb.position;
            startVelocity = ballRb.velocity;

            // 立即写入第 0 帧
            CaptureFrame();
        }

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
            recordedFrames.Add(new TrajectoryFrame(
                ballRb.position,
                ballRb.velocity,
                Quaternion.Euler(0f, 0f, ballRb.rotation),
                timer
            ));
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
            asset.id = Guid.NewGuid().ToString("N");
            asset.targetSlotId = targetSlotId;
            asset.frames = new List<TrajectoryFrame>(recordedFrames);
            asset.totalDuration = timer;
            asset.startPosition = startPosition;
            asset.startVelocity = startVelocity;

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string path = $"{folder}/slot_{targetSlotId}_{stamp}.asset";
            path = AssetDatabase.GenerateUniqueAssetPath(path);

            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[TrajectoryRecorder] 已保存轨迹：{path}（{recordedFrames.Count} 帧，槽位 {targetSlotId}）");
        }
#endif
    }
}
