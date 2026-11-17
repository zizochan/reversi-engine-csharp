using System;

namespace Zizo.Reversi
{
    // 探索中に盤面を元に戻すための履歴
    // 局面ごとに盤面をコピーしないことで、GC（WebGLで重くなりやすい）を抑える
    internal class ReversiUndoStack
    {
        private const int InitialCapacity = 256;

        private int[] _changedCells = new int[InitialCapacity];
        private int[] _previousValues = new int[InitialCapacity];

        public int Count { get; private set; }

        public void Push(int cell, int previousValue)
        {
            if (Count == _changedCells.Length)
            {
                Array.Resize(ref _changedCells, Count * 2);
                Array.Resize(ref _previousValues, Count * 2);
            }

            _changedCells[Count] = cell;
            _previousValues[Count] = previousValue;
            Count++;
        }

        // index番目に積んだ変更（変更したマスと、変更前の値）
        public int GetChangedCell(int index) => _changedCells[index];

        public int GetPreviousValue(int index) => _previousValues[index];

        // 最後に積んだ変更を取り出す（盤面は戻さない）
        public void Pop()
        {
            Count--;
        }
    }
}
