using System.Collections.Generic;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 一键生成战斗场景。挂在空物体上，运行时自动创建：
    /// - GridSystem（格子系统）
    /// - 背景网格可视化
    /// - BattleManager（战斗流程）
    /// - MonsterSpawner（怪物生成器）+ 默认 Wave 配置
    /// - 预设 CharacterData / MonsterData 资源（若无则自动创建）
    ///
    /// 用法：新建空 GameObject 挂本脚本 → 运行 → 按 B 键开始战斗。
    /// </summary>
    public class BattleSetup : MonoBehaviour
    {
        [Header("格子布局（竖屏优化 1080×2340）")]
        [Tooltip("列数。竖屏宽度有限，5 列刚好。")]
        public int columns = 5;
        [Tooltip("行数。竖屏高度优势，14 行让怪物有足够长的下落路径。")]
        public int rows = 14;
        [Tooltip("格子大小（单位：米）。2.0 在 orthoSize=15 时清晰可见。")]
        public float cellSize = 2.0f;
        [Tooltip("战斗区中心点。Camera 会自动调整到能容纳整个战斗区。")]
        public Vector2 gridCenter = Vector2.zero;

        [Header("Camera")]
        [Tooltip("是否自动配置 Camera 的正交尺寸和位置以适配竖屏战斗区。")]
        public bool autoSetupCamera = true;
        [Tooltip("Camera 底部留白（给角色放置区和 UI 留空间）。")]
        public float bottomPadding = 2f;
        [Tooltip("Camera 顶部留白（给怪物出生区留空间）。")]
        public float topPadding = 2f;
        [Tooltip("Camera 两侧留白。")]
        public float sidePadding = 1.5f;

        [Header("背景颜色")]
        public Color bgColor = new Color(0.1f, 0.1f, 0.2f);
        public Color gridLineColor = new Color(0.3f, 0.3f, 0.4f, 0.3f);
        public Color playerRowColor = new Color(0.2f, 0.5f, 0.2f, 0.3f);
        public Color spawnRowColor = new Color(0.5f, 0.2f, 0.2f, 0.3f);

        [Header("默认角色")]
        public int defaultCharacterCount = 3;

        [Header("默认 Wave")]
        public int waveCount = 3;
        public int monstersPerWave = 5;
        public float spawnInterval = 1.5f;
        public float waveInterval = 2f;

        [Header("按键")]
        public KeyCode startKey = KeyCode.B;

        private void Start()
        {
            Build();
        }

        private void Update()
        {
            if (Input.GetKeyDown(startKey))
            {
                if (BattleManager.Instance != null)
                {
                    if (BattleManager.Instance.state == BattleManager.BattleState.Ready)
                    {
                        BattleManager.Instance.StartBattle();
                    }
                    else if (BattleManager.Instance.state == BattleManager.BattleState.Victory ||
                             BattleManager.Instance.state == BattleManager.BattleState.Defeat)
                    {
                        BattleManager.Instance.ResetBattle();
                    }
                }
            }
        }

        private void Build()
        {
            var root = new GameObject("BattleField");
            root.transform.position = gridCenter;

            // 1. 背景
            var bg = new GameObject("Background");
            bg.transform.SetParent(root.transform, false);
            var bgSr = bg.AddComponent<SpriteRenderer>();
            bgSr.sprite = MakeRectSprite(bgColor);
            bgSr.sortingOrder = -10;
            float bgW = columns * cellSize * 1.1f;
            float bgH = rows * cellSize * 1.1f;
            bg.transform.localScale = new Vector3(bgW, bgH, 1f);

            // 2. 格子系统
            var gridGo = new GameObject("GridSystem");
            gridGo.transform.SetParent(root.transform, false);
            var grid = gridGo.AddComponent<GridSystem>();
            grid.columns = columns;
            grid.rows = rows;
            grid.cellSize = cellSize;
            grid.gridCenter = Vector2.zero;
            grid.Build();

            // 网格可视化
            DrawGridLines(root.transform, grid);

            // 3. BattleManager
            var managerGo = new GameObject("BattleManager");
            managerGo.transform.SetParent(root.transform, false);
            var manager = managerGo.AddComponent<BattleManager>();
            manager.gridSystem = grid;

            // 4. MonsterSpawner
            var spawnerGo = new GameObject("MonsterSpawner");
            spawnerGo.transform.SetParent(root.transform, false);
            var spawner = spawnerGo.AddComponent<MonsterSpawner>();
            spawner.gridSystem = grid;
            spawner.waveInterval = waveInterval;

            // 5. 创建默认配置
            var charData = CreateDefaultCharacterData();
            var monsterData = CreateDefaultMonsterData();

            // 6. 初始角色
            manager.initialCharacters = new List<CharacterData>();
            for (int i = 0; i < defaultCharacterCount; i++)
                manager.initialCharacters.Add(charData);

            // 7. 默认 Waves
            spawner.waves = new List<WaveConfig>();
            for (int w = 0; w < waveCount; w++)
            {
                spawner.waves.Add(new WaveConfig
                {
                    waveName = $"Wave {w + 1}",
                    totalCount = monstersPerWave + w * 2, // 每波递增
                    spawnInterval = spawnInterval,
                    monsters = new List<MonsterData> { monsterData }
                });
            }

            manager.spawner = spawner;

            // 8. 自动配置 Camera 适配竖屏
            if (autoSetupCamera) SetupCamera();

            Debug.Log($"[BattleSetup] 战斗场景构建完成！{columns}×{rows} 格子，{waveCount} 波怪物");
            Debug.Log("[BattleSetup] 按 B 键开始战斗");
        }

        /// <summary>
        /// 自动配置 Main Camera：调整正交尺寸和位置，
        /// 让战斗区在竖屏 1080×2340 下居中且留白合理。
        /// </summary>
        private void SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                cam = camGo.AddComponent<Camera>();
                cam.tag = "MainCamera";
            }
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.1f);
            cam.transform.rotation = Quaternion.identity;

            // 计算战斗区尺寸
            float gridTotalW = columns * cellSize;
            float gridTotalH = rows * cellSize;

            // Camera 需要覆盖的区域 = 战斗区 + 留白
            float neededHeight = gridTotalH + bottomPadding + topPadding;
            float neededWidth = gridTotalW + sidePadding * 2f;

            // 正交尺寸 = 需要高度的一半（Unity orthographicSize 是半高）
            // 但也要考虑屏幕宽高比，如果屏幕太窄要加大 orthoSize 保证宽度也能放下
            float screenAspect = Screen.width > 0 && Screen.height > 0
                ? (float)Screen.width / Screen.height
                : 1080f / 2340f; // 默认竖屏 1080×2340

            float orthoSizeByHeight = neededHeight * 0.5f;
            float orthoSizeByWidth = neededWidth * 0.5f / Mathf.Max(0.01f, screenAspect);
            cam.orthographicSize = Mathf.Max(orthoSizeByHeight, orthoSizeByWidth);

            // Camera 位置：垂直居中战斗区 + 底部留白的一半偏移（让底部 UI 更宽松）
            float camX = gridCenter.x;
            float camY = gridCenter.y + (bottomPadding - topPadding) * 0.5f;
            cam.transform.position = new Vector3(camX, camY, -10f);

            // 强制竖屏（如果是移动端）
            Screen.orientation = ScreenOrientation.Portrait;
            Debug.Log($"[BattleSetup] Camera orthoSize={cam.orthographicSize:F2}, pos=({camX:F1}, {camY:F1}), aspect={screenAspect:F2}");
        }

        // ============ 默认配置 ============

        private CharacterData CreateDefaultCharacterData()
        {
            var data = ScriptableObject.CreateInstance<CharacterData>();
            data.displayName = "塔塔";
            data.maxHp = 120;
            data.attack = 15;
            data.attackRange = 2;
            data.attackInterval = 1f;
            data.skillMultiplier = 2.5f;
            data.skillCooldown = 6f;
            data.skillName = "AOE";
            data.bodyColor = new Color(0.4f, 0.7f, 1f);
            data.sprite = MakeRectSprite(data.bodyColor);
            return data;
        }

        private MonsterData CreateDefaultMonsterData()
        {
            var data = ScriptableObject.CreateInstance<MonsterData>();
            data.displayName = "史莱姆";
            data.maxHp = 50;
            data.attack = 8;
            data.attackRange = 1;
            data.attackInterval = 1.2f;
            data.moveInterval = 1.5f;
            data.bodyColor = new Color(1f, 0.4f, 0.4f);
            data.sprite = MakeRectSprite(data.bodyColor);
            return data;
        }

        // ============ 背景绘制辅助 ============

        private void DrawGridLines(Transform root, GridSystem grid)
        {
            var gridLines = new GameObject("GridLines");
            gridLines.transform.SetParent(root, false);
            var sr = gridLines.AddComponent<SpriteRenderer>();
            sr.sprite = MakeGridSprite(grid.columns, grid.rows, grid.cellSize, gridLineColor, playerRowColor, spawnRowColor);
            sr.sortingOrder = -5;
            float totalW = grid.columns * grid.cellSize;
            float totalH = grid.rows * grid.cellSize;
            gridLines.transform.localScale = new Vector3(totalW, totalH, 1f);
        }

        /// <summary>生成网格线纹理。</summary>
        private static Sprite MakeGridSprite(int cols, int rows, float cellSize,
            Color lineColor, Color playerRowColor, Color spawnRowColor)
        {
            int w = Mathf.CeilToInt(cols * cellSize * 10);
            int h = Mathf.CeilToInt(rows * cellSize * 10);
            if (w < 16) w = 16;
            if (h < 16) h = 16;

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            float pxPerUnit = 10f;
            float cellPx = cellSize * pxPerUnit;

            // 清空为透明
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    tex.SetPixel(x, y, new Color(0, 0, 0, 0));

            // 画行高亮
            for (int c = 0; c < cols; c++)
            {
                int colStart = Mathf.FloorToInt(c * cellPx);
                int colEnd = Mathf.FloorToInt((c + 1) * cellPx);
                // Player Row (row 0) 高亮
                for (int y = 0; y < Mathf.FloorToInt(cellPx); y++)
                    for (int x = colStart; x < colEnd; x++)
                        tex.SetPixel(x, y, playerRowColor);
                // Spawn Row (row rows-1) 高亮
                int spawnRowStart = Mathf.FloorToInt((rows - 1) * cellPx);
                for (int y = spawnRowStart; y < Mathf.FloorToInt(rows * cellPx); y++)
                    for (int x = colStart; x < colEnd; x++)
                        tex.SetPixel(x, y, spawnRowColor);
            }

            // 画网格线
            for (int c = 0; c <= cols; c++)
            {
                int x = Mathf.FloorToInt(c * cellPx);
                for (int y = 0; y < h; y++)
                {
                    if (x >= 0 && x < w) tex.SetPixel(x, y, lineColor);
                    if (x + 1 >= 0 && x + 1 < w) tex.SetPixel(x + 1, y, lineColor);
                }
            }
            for (int r = 0; r <= rows; r++)
            {
                int y = Mathf.FloorToInt(r * cellPx);
                for (int x = 0; x < w; x++)
                {
                    if (y >= 0 && y < h) tex.SetPixel(x, y, lineColor);
                    if (y + 1 >= 0 && y + 1 < h) tex.SetPixel(x, y + 1, lineColor);
                }
            }

            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), pxPerUnit);
            sprite.name = "GridSprite";
            return sprite;
        }

        /// <summary>生成纯色矩形纹理。</summary>
        private static Sprite MakeRectSprite(Color color)
        {
            int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
