using System.Collections.Generic;
using UnityEngine;

namespace DefaultNamespace
{
    public class TrajectoryRecorder : MonoBehaviour
    {
        public Rigidbody ballRb;
        public int targetSlotId;          // 这个轨迹的目标落点
        public float recordDuration = 10f;
    
        private List<TrajectoryFrame> recordedFrames = new List<TrajectoryFrame>();
        private float timer = 0f;
        private bool isRecording = false;
    
        void Start()
        {
            // 在编辑器模式下，点击Play后自动开始录制
            StartRecording();
        }
    
        void FixedUpdate()
        {
            if (!isRecording) return;
        
            timer += Time.fixedDeltaTime;
            if (timer > recordDuration)
            {
                StopRecording();
                return;
            }
        
            // 每帧记录状态
            TrajectoryFrame frame = new TrajectoryFrame
            {
                position = ballRb.position,
                velocity = ballRb.velocity,
                rotation = ballRb.rotation,
                timestamp = timer
            };
            recordedFrames.Add(frame);
        }
    
        public void StartRecording()
        {
            recordedFrames.Clear();
            timer = 0f;
            isRecording = true;
            ballRb.isKinematic = false; // 确保物理开启
        }
    
        public void StopRecording()
        {
            isRecording = false;
            SaveTrajectoryToAsset();
        }
    
        void SaveTrajectoryToAsset()
        {
            TrajectoryData data = new TrajectoryData
            {
                id = System.Guid.NewGuid().ToString(),
                targetSlotId = targetSlotId,
                frames = recordedFrames,
                totalDuration = timer,
                startPosition = ballRb.position,
                startVelocity = ballRb.velocity
            };
        
            // 保存为ScriptableObject或JSON文件
            string json = JsonUtility.ToJson(data);
            System.IO.File.WriteAllText(
                Application.dataPath + $"/Trajectories/slot_{targetSlotId}_{System.DateTime.Now.Ticks}.json", 
                json
            );
        
            Debug.Log($"轨迹已保存，共 {recordedFrames.Count} 帧，目标槽位: {targetSlotId}");
        }
    }
}