using UnityEngine;

namespace Battle
{
    /// <summary>
    /// 战斗场景中的单个格子。
    /// 存储格子坐标 (col, row) 和世界坐标 center，
    /// 以及当前占用的实体。
    /// </summary>
    public class GridCell
    {
        /// <summary>列索引（x 方向）。</summary>
        public readonly int col;

        /// <summary>行索引（y 方向）。row 0 为最底部，row 递增向上。</summary>
        public readonly int row;

        /// <summary>格子中心的世界坐标。</summary>
        public readonly Vector2 center;

        /// <summary>当前占用此格子的实体（null 表示空）。</summary>
        public EntityBase occupant;

        public GridCell(int col, int row, Vector2 center)
        {
            this.col = col;
            this.row = row;
            this.center = center;
        }

        /// <summary>格子是否为空。</summary>
        public bool IsEmpty => occupant == null;

        /// <summary>曼哈顿距离。</summary>
        public int ManhattanDistanceTo(GridCell other)
        {
            return Mathf.Abs(col - other.col) + Mathf.Abs(row - other.row);
        }

        /// <summary>切比雪夫距离（8 方向相邻距离）。</summary>
        public int ChebyshevDistanceTo(GridCell other)
        {
            return Mathf.Max(Mathf.Abs(col - other.col), Mathf.Abs(row - other.row));
        }
    }
}
