namespace DefaultNamespace
{
    using System.Collections.Generic;
    using System.IO;
    using UnityEngine;

    /// <summary>
    /// 轨迹库 - 管理所有已录制的轨迹数据
    /// 可以挂载到场景中的GameObject上，作为单例使用
    /// </summary>
    public class TrajectoryLibrary : MonoBehaviour
    {
        // 单例
        public static TrajectoryLibrary Instance { get; private set; }

        // 所有轨迹的索引：key = 目标槽位ID，value = 该槽位的所有轨迹列表
        private Dictionary<int, List<TrajectoryData>> trajectoryIndex = new Dictionary<int, List<TrajectoryData>>();

        // 是否已加载完成
        private bool isLoaded = false;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void Start()
        {
            LoadAllTrajectories();
        }

        /// <summary>
        /// 加载所有轨迹（从Resources文件夹或StreamingAssets）
        /// </summary>
        public void LoadAllTrajectories()
        {
            if (isLoaded) return;

            trajectoryIndex.Clear();

            // 方式1：从Resources文件夹加载（推荐，打包时自动压缩）
            TrajectoryData[] resourcesTrajectories = Resources.LoadAll<TrajectoryData>("Trajectories");
            foreach (var traj in resourcesTrajectories)
            {
                AddTrajectoryToIndex(traj);
            }

            // 方式2：从StreamingAssets加载JSON文件（适合热更新）
            // 在移动端需要异步加载，这里简化处理
            string path = Path.Combine(Application.streamingAssetsPath, "Trajectories");
            if (Directory.Exists(path))
            {
                string[] jsonFiles = Directory.GetFiles(path, "*.json");
                foreach (string filePath in jsonFiles)
                {
                    string json = File.ReadAllText(filePath);
                    TrajectoryData traj = JsonUtility.FromJson<TrajectoryData>(json);
                    if (traj != null)
                    {
                        AddTrajectoryToIndex(traj);
                    }
                }
            }

            isLoaded = true;
            Debug.Log($"轨迹库加载完成，共 {GetTotalTrajectoryCount()} 条轨迹，覆盖 {trajectoryIndex.Count} 个槽位");
        }

        /// <summary>
        /// 将轨迹添加到索引中
        /// </summary>
        private void AddTrajectoryToIndex(TrajectoryData traj)
        {
            if (traj == null || traj.frames == null || traj.frames.Count == 0) return;

            if (!trajectoryIndex.ContainsKey(traj.targetSlotId))
            {
                trajectoryIndex[traj.targetSlotId] = new List<TrajectoryData>();
            }

            trajectoryIndex[traj.targetSlotId].Add(traj);
        }

        /// <summary>
        /// 获取某个槽位的所有轨迹
        /// </summary>
        public List<TrajectoryData> GetTrajectoriesBySlot(int slotId)
        {
            if (!isLoaded) LoadAllTrajectories();

            if (trajectoryIndex.TryGetValue(slotId, out List<TrajectoryData> list))
            {
                return list;
            }

            return new List<TrajectoryData>(); // 返回空列表，不要返回null
        }

        /// <summary>
        /// 随机获取某个槽位的一条轨迹
        /// </summary>
        public TrajectoryData GetRandomTrajectoryBySlot(int slotId)
        {
            List<TrajectoryData> list = GetTrajectoriesBySlot(slotId);
            if (list == null || list.Count == 0) return null;
            return list[Random.Range(0, list.Count)];
        }

        /// <summary>
        /// 随机返回一个存在轨迹的槽位 ID。
        /// maxSlotExclusive 为有效槽位数上限（用于约束在合法槽范围内）；传 -1 表示不约束。
        /// 没有任何轨迹时返回 -1。
        /// </summary>
        public int GetRandomSlotWithTrajectory(int maxSlotExclusive = -1)
        {
            if (!isLoaded) LoadAllTrajectories();

            var keys = new List<int>();
            foreach (var kvp in trajectoryIndex)
            {
                if (kvp.Value.Count > 0 && (maxSlotExclusive < 0 || kvp.Key < maxSlotExclusive))
                {
                    keys.Add(kvp.Key);
                }
            }
            if (keys.Count == 0) return -1;
            return keys[Random.Range(0, keys.Count)];
        }

        /// <summary>
        /// 根据起始位置和力度，匹配最合适的轨迹
        /// </summary>
        public TrajectoryData FindBestMatch(int targetSlotId, Vector3 launchPos, float launchForce)
        {
            List<TrajectoryData> candidates = GetTrajectoriesBySlot(targetSlotId);
            if (candidates == null || candidates.Count == 0) return null;

            float bestScore = float.MaxValue;
            TrajectoryData best = null;

            foreach (var traj in candidates)
            {
                // 起始位置差异（只比较X和Z，忽略Y，因为发射点在同一平面）
                Vector3 posDiff = traj.startPosition - launchPos;
                posDiff.y = 0;
                float posScore = posDiff.magnitude;

                // 起始速度差异（力度）
                float forceScore = Mathf.Abs(traj.startVelocity.magnitude - launchForce);

                // 综合评分（权重可调整）
                float score = posScore * 0.5f + forceScore * 0.5f;

                if (score < bestScore)
                {
                    bestScore = score;
                    best = traj;
                }
            }

            return best;
        }

        /// <summary>
        /// 获取总共的轨迹数量
        /// </summary>
        public int GetTotalTrajectoryCount()
        {
            int count = 0;
            foreach (var kvp in trajectoryIndex)
            {
                count += kvp.Value.Count;
            }

            return count;
        }

        /// <summary>
        /// 打印统计信息（用于调试）
        /// </summary>
        public void PrintStatistics()
        {
            Debug.Log("=== 轨迹库统计 ===");
            foreach (var kvp in trajectoryIndex)
            {
                Debug.Log($"槽位 {kvp.Key}: {kvp.Value.Count} 条轨迹");
            }

            Debug.Log($"总计: {GetTotalTrajectoryCount()} 条轨迹");
        }
    }
}