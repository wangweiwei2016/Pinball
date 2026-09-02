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
            // 关键优化：立即复制所有需要的数据，避免延迟期间被后续状态变化影响
            // （球可能被回收/重置，velocity 会变 0，所以 startSpeed 必须现在计算）
            var framesCopy = new List<TrajectoryFrame>(recordedFrames);
            Vector2 startPosCopy = startPosition;
            float startSpeedCopy = startVelocity.magnitude;
            byte starCopy = (byte)starHitCount;
            byte shieldCopy = (byte)shieldHitCount;
            string folderCopy = resourceFolder;

            // 推迟到下一帧执行磁盘 I/O，避免阻塞当前物理回调链
            // （OnTriggerEnter2D → GameManager.OnBallEnterSlot → StopAndSave）
            EditorApplication.delayCall += () =>
            {
                SaveAsAssetDeferred(targetSlotId, framesCopy, startPosCopy, startSpeedCopy,
                    starCopy, shieldCopy, folderCopy);
            };
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
        /// <summary>
        /// 延迟执行的保存方法。所有数据通过参数传入，不依赖 recorder 实例状态。
        /// 关键优化：移除 AssetDatabase.SaveAssets() 和 AssetDatabase.Refresh()，
        /// 这两个调用是卡顿主因——CreateAsset 已写入磁盘并更新 AssetDatabase 内部状态，
        /// SaveAssets/Refresh 对新创建的资产是冗余的，且 Refresh 会重新扫描整个 AssetDatabase。
        /// </summary>
        private static void SaveAsAssetDeferred(int targetSlotId, List<TrajectoryFrame> frames,
            Vector2 startPosition, float startSpeed, byte starHitCount, byte shieldHitCount, string folder)
        {
            try
            {
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
                asset.frames = frames;
                asset.startPosition = startPosition;
                asset.startSpeed = startSpeed;
                asset.starHitCount = starHitCount;
                asset.shieldHitCount = shieldHitCount;

                string tag = $"_star{starHitCount}_shield{shieldHitCount}";
                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string path = $"{folder}/slot_{targetSlotId}{tag}_{stamp}.asset";
                path = AssetDatabase.GenerateUniqueAssetPath(path);

                // CreateAsset 已写入磁盘文件并更新 AssetDatabase 内部缓存
                AssetDatabase.CreateAsset(asset, path);
                // 不调用 SaveAssets/Refresh：
                // - SaveAssets 会强制刷新所有未保存资产到磁盘，对刚 CreateAsset 的资产是冗余的
                // - Refresh 会重新扫描整个 AssetDatabase，是最重的操作（可能卡几秒）
                // Unity 编辑器会在 Project 窗口下次刷新时自动显示新资产

                Debug.Log($"[TrajectoryRecorder] 已保存轨迹：{path}（{frames.Count} 帧，槽位 {targetSlotId}，Star×{starHitCount}，Shield×{shieldHitCount}）");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TrajectoryRecorder] 保存失败：{e.Message}");
            }
        }
#endif
    }
}
