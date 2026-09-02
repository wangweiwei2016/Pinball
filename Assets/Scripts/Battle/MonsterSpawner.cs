using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 怪物生成器。按 Wave 配置定时在顶部随机列生成怪物。
    /// Wave 结束后自动进入下一波，所有 Wave 打完通知 BattleManager 胜利。
    /// </summary>
    public class MonsterSpawner : MonoBehaviour
    {
        [Header("引用")]
        public GridSystem gridSystem;

        [Header("生成配置")]
        [Tooltip("所有 Wave 配置。每个 Wave 有多个怪物批次。")]
        public List<WaveConfig> waves = new List<WaveConfig>();

        [Tooltip("Wave 之间的间隔（秒）。")]
        public float waveInterval = 3f;

        [Tooltip("怪物预制体（运行时创建 Monster 组件 + SpriteRenderer）。")]
        public GameObject monsterPrefab;

        // 运行时状态
        private int currentWaveIndex;
        private bool waveActive;
        private float spawnTimer;
        private int monstersSpawnedInWave;

        /// <summary>当前是否有 Wave 在生成中或场上仍有存活怪物。</summary>
        public bool IsWaveCleared => !waveActive && !HasAliveMonsters();

        private void Start()
        {
            if (gridSystem == null)
                gridSystem = FindObjectOfType<GridSystem>();
            StartNextWave();
        }

        /// <summary>
        /// 开始下一波。由 Start 或 BattleManager 在波间间隔后调用。
        /// </summary>
        public void StartNextWave()
        {
            if (currentWaveIndex >= waves.Count)
            {
                BattleManager.Instance?.OnAllWavesCleared();
                return;
            }

            waveActive = true;
            monstersSpawnedInWave = 0;
            spawnTimer = 0f;
            Debug.Log($"[MonsterSpawner] Wave {currentWaveIndex + 1}/{waves.Count} 开始");
        }

        private void Update()
        {
            if (!waveActive) return;
            if (currentWaveIndex >= waves.Count) return;

            WaveConfig wave = waves[currentWaveIndex];
            if (monstersSpawnedInWave >= wave.totalCount)
            {
                // 当前 Wave 的怪物已全部生成完毕
                waveActive = false;
                StartCoroutine(WaitAndNextWave(waveInterval));
                return;
            }

            spawnTimer += Time.deltaTime;
            if (spawnTimer >= wave.spawnInterval)
            {
                spawnTimer = 0f;
                SpawnMonster(wave);
            }
        }

        private IEnumerator WaitAndNextWave(float interval)
        {
            // 等场上所有怪物都死了才进入下一波
            yield return new WaitUntil(() => !HasAliveMonsters());
            BattleManager.Instance?.OnWaveCleared(currentWaveIndex);
            currentWaveIndex++;
            if (currentWaveIndex < waves.Count)
            {
                yield return new WaitForSeconds(interval);
                StartNextWave();
            }
            else
            {
                BattleManager.Instance?.OnAllWavesCleared();
            }
        }

        private void SpawnMonster(WaveConfig wave)
        {
            // 随机选一个空列（优先选有空位的列）
            int col = Random.Range(0, gridSystem.columns);
            GridCell spawnCell = gridSystem.GetCell(col, gridSystem.SpawnRow);
            if (spawnCell == null || !spawnCell.IsEmpty)
            {
                // 该列被占用，尝试其他列
                for (int c = 0; c < gridSystem.columns; c++)
                {
                    spawnCell = gridSystem.GetCell(c, gridSystem.SpawnRow);
                    if (spawnCell != null && spawnCell.IsEmpty) { col = c; break; }
                }
            }
            if (spawnCell == null || !spawnCell.IsEmpty) return; // 所有列都被占了

            // 从 Wave 的怪物池中随机选一个配置
            var monsterData = wave.monsters[Random.Range(0, wave.monsters.Count)];

            // 创建怪物
            GameObject go;
            if (monsterPrefab != null)
            {
                go = Instantiate(monsterPrefab);
            }
            else
            {
                go = new GameObject("Monster_" + monsterData.displayName);
                go.AddComponent<SpriteRenderer>();
            }

            var monster = go.GetComponent<Monster>();
            if (monster == null) monster = go.AddComponent<Monster>();
            monster.gridSystem = gridSystem;
            monster.ApplyData(monsterData);
            monster.PlaceOnCell(spawnCell);

            monstersSpawnedInWave++;
        }

        /// <summary>检查场上是否有存活的怪物。</summary>
        public bool HasAliveMonsters()
        {
            var monsters = FindObjectsOfType<Monster>();
            foreach (var m in monsters)
                if (m.IsAlive) return true;
            return false;
        }
    }

    /// <summary>
    /// 单个 Wave 配置。
    /// </summary>
    [System.Serializable]
    public class WaveConfig
    {
        [Tooltip("Wave 名称（调试用）。")]
        public string waveName = "Wave 1";

        [Tooltip("此 Wave 要生成的怪物总数。")]
        public int totalCount = 5;

        [Tooltip("每只怪物的生成间隔（秒）。")]
        public float spawnInterval = 2f;

        [Tooltip("此 Wave 可用的怪物配置池（随机选取）。")]
        public List<MonsterData> monsters = new List<MonsterData>();
    }
}
