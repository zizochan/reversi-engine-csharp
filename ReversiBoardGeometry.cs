using System;
using System.Collections.Generic;
using System.Linq;

namespace Zizo.Reversi
{
    // 盤の形から計算する、探索中に変わらない情報（マスのつながり、マスの重み、手を調べる順番）
    internal class ReversiBoardGeometry
    {
        private const int AxisCount = 4;

        public ReversiBoardTopology Topology { get; }

        public int Width { get; }

        // 壁以外のマス（手を調べる順番に並べたもの）
        public int[] PlayableCells { get; }

        // [マス] → PlayableCells の中での順番。壁は -1
        public int[] MoveOrderRanks { get; }

        public int[] PositionWeights { get; }

        // 隅の隣のマスと、その隅のペア（同じ添字同士がペア）
        public int[] CornerNeighborCells { get; }
        public int[] AdjacentCorners { get; }
        public int CornerNeighborWeight { get; }

        // 位置の評価を -1〜1 に揃えるための基準値
        public int PositionScale { get; }

        // 盤の形（壁の配置）だけを使う。石の配置は使わない
        public ReversiBoardGeometry(ReversiBoard board, ReversiCpuSettings settings)
        {
            int width = board.Width;
            int height = board.Height;
            int[] cells = board.RawCells;
            Width = width;
            Topology = board.Topology;
            CornerNeighborWeight = settings.CornerNeighborWeight;

            int[] playableCells = Enumerable
                .Range(0, cells.Length)
                .Where(cell => cells[cell] != ReversiBoard.Wall)
                .ToArray();

            if (settings.CustomPositionWeights != null)
            {
                PositionWeights = ConvertCustomWeights(
                    width,
                    height,
                    settings.CustomPositionWeights
                );
                CornerNeighborCells = Array.Empty<int>();
                AdjacentCorners = Array.Empty<int>();
            }
            else
            {
                PositionWeights = BuildPositionWeights(cells.Length, playableCells, settings);
                (CornerNeighborCells, AdjacentCorners) = BuildCornerNeighborPairs(playableCells);
            }

            PositionScale = Math.Max(1, PositionWeights.Max(weight => Math.Abs(weight)));
            PlayableCells = playableCells.OrderByDescending(GetMoveOrderKey).ToArray();
            MoveOrderRanks = BuildMoveOrderRanks(cells.Length, PlayableCells);
        }

        public bool IsCorner(int cell) => CountBlockedAxes(cell) == AxisCount;

        public bool IsEdge(int cell) => CountBlockedAxes(cell) > 0;

        // 縦・横・斜め2本の4つの軸のうち、片側が盤外・壁になっている軸の数
        // 4軸とも塞がっているマスの石は二度と裏返らないため「隅」、1軸以上なら「辺」として扱う
        private int CountBlockedAxes(int cell)
        {
            int count = 0;
            for (int axis = 0; axis < AxisCount; axis++)
            {
                if (
                    Topology.GetNeighbor(cell, axis) == ReversiBoardTopology.NoNeighbor
                    || Topology.GetNeighbor(cell, axis + AxisCount)
                        == ReversiBoardTopology.NoNeighbor
                )
                    count++;
            }
            return count;
        }

        private int[] BuildPositionWeights(
            int cellCount,
            int[] playableCells,
            ReversiCpuSettings settings
        )
        {
            var weights = new int[cellCount];
            foreach (int cell in playableCells)
            {
                if (IsCorner(cell))
                    weights[cell] = settings.CornerWeight;
                else if (IsEdge(cell))
                    weights[cell] = settings.EdgeWeight;
                else
                    weights[cell] = settings.InnerWeight;
            }
            return weights;
        }

        private (int[] neighborCells, int[] corners) BuildCornerNeighborPairs(int[] playableCells)
        {
            var neighborCells = new List<int>();
            var corners = new List<int>();
            foreach (int corner in playableCells.Where(IsCorner))
            {
                for (
                    int direction = 0;
                    direction < ReversiBoardTopology.DirectionCount;
                    direction++
                )
                {
                    int neighbor = Topology.GetNeighbor(corner, direction);
                    if (neighbor == ReversiBoardTopology.NoNeighbor || IsCorner(neighbor))
                        continue;

                    neighborCells.Add(neighbor);
                    corners.Add(corner);
                }
            }
            return (neighborCells.ToArray(), corners.ToArray());
        }

        private static int[] ConvertCustomWeights(int width, int height, int[,] customWeights)
        {
            if (customWeights.GetLength(0) != height || customWeights.GetLength(1) != width)
                throw new ArgumentException(
                    "CustomPositionWeights size must match the board size",
                    nameof(ReversiCpuSettings.CustomPositionWeights)
                );

            var weights = new int[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    weights[y * width + x] = customWeights[y, x];
                }
            }
            return weights;
        }

        private static int[] BuildMoveOrderRanks(int cellCount, int[] playableCells)
        {
            int[] ranks = Enumerable.Repeat(-1, cellCount).ToArray();
            for (int rank = 0; rank < playableCells.Length; rank++)
            {
                ranks[playableCells[rank]] = rank;
            }
            return ranks;
        }

        // 良さそうなマスから先に調べると、αβ探索の枝刈りが効きやすくなる
        private int GetMoveOrderKey(int cell)
        {
            int key = PositionWeights[cell];
            if (CornerNeighborCells.Contains(cell))
                key += CornerNeighborWeight;
            return key;
        }
    }
}
