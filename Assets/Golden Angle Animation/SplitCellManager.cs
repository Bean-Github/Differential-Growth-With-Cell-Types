using UnityEngine;

using System.Collections.Generic;


namespace GoldenAngleAnimation
{

    public class SplitCellManager : MonoBehaviour
    {
        #region Singleton Implementation

        public static SplitCellManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject); // Optional
        }
        #endregion


        public float t = 0.5f;

        public List<SplittableCell> currCells;


        // Update is called once per frame
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                SplitAllCells();
            }
        }

        public int gridWidth = 4;
        public float cellSpacing = 1f;
        public Transform center;

        public void UpdateLayout()
        {
            int count = currCells.Count;

            for (int i = 0; i < count; i++)
            {
                currCells[i].targetPosition = GetGridPosition(i, count);
            }
        }

        public int pendingSplits = 0;

        void SplitAllCells()
        {
            if (currCells.Count == 8)
            {
                gridWidth = 4;
            }

            MoveCellsToSplitPositions();

            List<SplittableCell> oldCells = new List<SplittableCell>();
            int count = currCells.Count;

            pendingSplits = count;

            for (int i = 0; i < count; i++)
            {
                SplittableCell cell = currCells[i];

                if (cell != null)
                {
                    cell.SplitCell(t);

                    oldCells.Add(cell);
                }
            }

            foreach (SplittableCell cell in oldCells)
            {
                currCells.Remove(cell);
            }
        }


        public void MoveCellsToSplitPositions()
        {
            int currentCount = currCells.Count;

            // The future count after every cell splits
            int futureCount = currentCount * 2;

            for (int i = 0; i < currentCount; i++)
            {
                // Current cell's children indices
                int childLeftIndex = i * 2;
                int childRightIndex = i * 2 + 1;

                Vector3 leftPos = GetGridPosition(childLeftIndex, futureCount);
                Vector3 rightPos = GetGridPosition(childRightIndex, futureCount);

                // Middle between the two children
                Vector3 splitPosition = (leftPos + rightPos) * 0.5f;

                currCells[i].targetPosition = splitPosition;
            }
        }


        private Vector3 GetGridPosition(int index, int count)
        {
            int row = index / gridWidth;
            int col = index % gridWidth;

            int rows = Mathf.CeilToInt((float)count / gridWidth);

            float x = (col - (gridWidth - 1) * 0.5f) * cellSpacing;
            float y = -(row - (rows - 1) * 0.5f) * cellSpacing;

            return center.position +
                   center.right * x +
                   center.up * y;
        }


    }

}
