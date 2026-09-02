using System.Collections.Generic;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 战斗流程管理器。单例。
    /// 负责：
    /// - 战斗状态（准备/进行中/胜利/失败）
    /// - 角色放置（战斗开始前可配置）
    /// - 胜负判定（怪物到达底部 → 失败；所有 Wave 打完 → 胜利）
    /// - 统计数据（击杀数、波数）
    /// </summary>
    public class BattleManager : MonoBehaviour
    {
        public static BattleManager Instance { get; private set; }

        public enum BattleState { Idle, Ready, Fighting, Victory, Defeat }

        [Header("引用")]
        public GridSystem gridSystem;
        public MonsterSpawner spawner;

        [Header("初始角色放置（战斗开始前）")]
        [Tooltip("要放置的角色配置列表，按顺序放到 PlayerRow 的格子上。")]
        public List<CharacterData> initialCharacters = new List<CharacterData>();

        [Header("状态")]
        public BattleState state = BattleState.Idle;

        // 统计
        public int monstersKilled;
        public int currentWave;

        // 事件
        public System.Action<BattleState> OnStateChanged;
        public System.Action<Monster> OnMonsterKilled;
        public System.Action<int> OnWaveClearedAction;

        private void Awake()
        {
            Application.runInBackground = true; //unity窗口失去焦点时，仍可以正常运行
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            if (gridSystem == null) gridSystem = FindObjectOfType<GridSystem>();
            if (spawner == null) spawner = FindObjectOfType<MonsterSpawner>();

            if (gridSystem != null)
            {
                gridSystem.Build();
                state = BattleState.Ready;
                OnStateChanged?.Invoke(state);
            }
        }

        /// <summary>
        /// 战斗开始：放置初始角色，通知 Spawner 开始生成。
        /// </summary>
        public void StartBattle()
        {
            if (state != BattleState.Ready && state != BattleState.Idle) return;

            // 放置初始角色
            PlaceInitialCharacters();

            state = BattleState.Fighting;
            OnStateChanged?.Invoke(state);
            Debug.Log("[BattleManager] 战斗开始！");
        }

        /// <summary>
        /// 放置一个角色到 PlayerRow 的指定列。返回是否成功。
        /// </summary>
        public bool PlaceCharacter(CharacterData data, int col)
        {
            if (gridSystem == null || data == null) return false;
            if (state != BattleState.Ready)
            {
                Debug.LogWarning("[BattleManager] 只能在 Ready 状态下放置角色。");
                return false;
            }

            var cell = gridSystem.GetCell(col, gridSystem.PlayerRow);
            if (cell == null || !cell.IsEmpty) return false;

            var go = new GameObject("Char_" + data.displayName);
            go.AddComponent<SpriteRenderer>();
            var character = go.AddComponent<Character>();
            character.gridSystem = gridSystem;
            character.ApplyData(data);
            character.PlaceOnCell(cell);
            return true;
        }

        /// <summary>放置列表中配置的初始角色。</summary>
        private void PlaceInitialCharacters()
        {
            if (gridSystem == null) return;
            int row = gridSystem.PlayerRow;
            int col = 0;
            foreach (var data in initialCharacters)
            {
                var cell = gridSystem.GetCell(col, row);
                if (cell != null && cell.IsEmpty)
                {
                    PlaceCharacter(data, col);
                }
                col++;
            }
        }

        // ============ 怪物回调 ============

        /// <summary>Monster 被击败时调用。</summary>
        public void OnMonsterDefeated(Monster monster)
        {
            monstersKilled++;
            OnMonsterKilled?.Invoke(monster);
        }

        /// <summary>Monster 到达最底部 → 失败。</summary>
        public void OnMonsterReachBottom(Monster monster)
        {
            string showName = monster.data != null ? monster.data.displayName : "";
            Debug.Log($"[BattleManager] 怪物 {showName} 突破防线！");
            EndBattle(false);
        }

        /// <summary>Wave 清空。</summary>
        public void OnWaveCleared(int waveIndex)
        {
            currentWave = waveIndex + 1;
            OnWaveClearedAction?.Invoke(currentWave);
            Debug.Log($"[BattleManager] Wave {currentWave} 清空！");
        }

        /// <summary>所有 Wave 打完 → 胜利。</summary>
        public void OnAllWavesCleared()
        {
            EndBattle(true);
        }

        /// <summary>结束战斗。</summary>
        private void EndBattle(bool victory)
        {
            state = victory ? BattleState.Victory : BattleState.Defeat;
            OnStateChanged?.Invoke(state);
            Debug.Log($"[BattleManager] 战斗结束！{(victory ? "胜利" : "失败")} — 击杀数={monstersKilled}");
        }

        /// <summary>重置战斗（清空所有实体，回到 Ready 状态）。</summary>
        public void ResetBattle()
        {
            // 清怪物
            foreach (var m in FindObjectsOfType<Monster>()) Destroy(m.gameObject);
            // 清角色
            foreach (var c in FindObjectsOfType<Character>()) Destroy(c.gameObject);

            monstersKilled = 0;
            currentWave = 0;
            state = BattleState.Ready;

            // 重置格子占用
            if (gridSystem != null)
            {
                // 触发重建以清空所有占位
                gridSystem.Build();
            }

            OnStateChanged?.Invoke(state);
        }
    }
}
