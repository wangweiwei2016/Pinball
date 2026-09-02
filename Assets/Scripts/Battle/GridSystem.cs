using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 战斗场景格子系统。
    /// 管理所有格子的创建、查询、占用/释放，
    /// 为角色放置、怪物移动、攻击范围判定提供格子坐标查询服务。
    /// </summary>
    public class GridSystem : MonoBehaviour
    {
        [Header("格子布局")]
        [Tooltip("列数（宽度方向）。")]
        public int columns = 5;

        [Tooltip("行数（高度方向）。row 0 为最底部，row rows-1 为最顶部。")]
        public int rows = 8;

        [Tooltip("格子大小（边长）。")]
        public float cellSize = 1.5f;

        [Tooltip("网格中心世界坐标。")]
        public Vector2 gridCenter = Vector2.zero;

        private GridCell[,] cells;

        /// <summary>怪物出生行（最顶部）。</summary>
        public int SpawnRow => rows - 1;

        /// <summary>玩家放置行（最底部）。</summary>
        public int PlayerRow => 0;

        /// <summary>怪物到达此行算失败。</summary>
        public int FailRow => 0;

        public void Build()
        {
            cells = new GridCell[columns, rows];
            Vector2 origin = gridCenter - new Vector2(columns * cellSize, rows * cellSize) * 0.5f;
            origin += new Vector2(cellSize, cellSize) * 0.5f; // 偏移到第一个格子中心

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    Vector2 center = origin + new Vector2(c, r) * cellSize;
                    cells[c, r] = new GridCell(c, r, center);
                }
            }
        }

        /// <summary>获取指定坐标的格子，越界返回 null。</summary>
        public GridCell GetCell(int col, int row)
        {
            if (col < 0 || col >= columns || row < 0 || row >= rows) return null;
            return cells[col, row];
        }

        /// <summary>根据世界坐标查找最近的格子（越界返回 null）。</summary>
        public GridCell GetCellAtWorld(Vector2 worldPos)
        {
            Vector2 origin = gridCenter - new Vector2(columns * cellSize, rows * cellSize) * 0.5f;
            Vector2 rel = worldPos - origin;
            int col = Mathf.FloorToInt(rel.x / cellSize);
            int row = Mathf.FloorToInt(rel.y / cellSize);
            return GetCell(col, row);
        }

        /// <summary>占用格子。</summary>
        public void Occupy(EntityBase entity, GridCell cell)
        {
            if (cell != null) cell.occupant = entity;
        }

        /// <summary>释放格子。</summary>
        public void Release(GridCell cell)
        {
            if (cell != null && cell.occupant != null) cell.occupant = null;
        }

        /// <summary>沿列向下移动一格。返回目标格子（可能为 null 表示已到边界）。</summary>
        public GridCell GetCellBelow(int col, int currentRow)
        {
            return GetCell(col, currentRow - 1);
        }

        /// <summary>获取某列的所有格子（从上到下）。</summary>
        public GridCell[] GetColumn(int col)
        {
            var colCells = new GridCell[rows];
            for (int r = 0; r < rows; r++) colCells[r] = cells[col, r];
            return colCells;
        }

        /// <summary>
        /// 查询攻击范围内的敌方实体。返回最近的目标。
        /// range 使用切比雪夫距离（8 方向范围），与格子布局对齐。
        /// </summary>
        public EntityBase GetAttackTarget(GridCell fromCell, Team myTeam, int range)
        {
            if (fromCell == null) return null;

            EntityBase closest = null;
            int closestDist = int.MaxValue;

            // 遍历 range 半径内的所有格子
            for (int dr = -range; dr <= range; dr++)
            {
                for (int dc = -range; dc <= range; dc++)
                {
                    int c = fromCell.col + dc;
                    int r = fromCell.row + dr;
                    var cell = GetCell(c, r);
                    if (cell == null || cell.IsEmpty) continue;

                    var target = cell.occupant;
                    if (!target.IsAlive) continue;
                    if (target.Team == myTeam) continue; // 跳过友方

                    int dist = fromCell.ChebyshevDistanceTo(cell);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closest = target;
                    }
                }
            }
            return closest;
        }

        /// <summary>在某一行查找空列（用于玩家放置）。</summary>
        public GridCell[] GetEmptyCellsInRow(int row)
        {
            var result = new System.Collections.Generic.List<GridCell>();
            for (int c = 0; c < columns; c++)
            {
                var cell = GetCell(c, row);
                if (cell != null && cell.IsEmpty) result.Add(cell);
            }
            return result.ToArray();
        }

        private void OnDrawGizmosSelected()
        {
            if (cells == null) return;
            Gizmos.color = new Color(1f, 1f, 1f, 0.2f);
            foreach (var cell in cells)
            {
                var size = Vector3.one * cellSize * 0.9f;
                Gizmos.DrawWireCube(cell.center, size);
            }
        }
    }
}
