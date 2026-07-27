using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;

namespace GoldenAngleAnimation
{
    public class UnevenSplitCellManager : MonoBehaviour
    {
        #region Singleton Implementation

        public static UnevenSplitCellManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        #endregion

        public float t = 0.5f;
        public Camera mainCam;

        [Header("Camera Controls")]
        public float cameraPadding = 2f;
        private Vector3 camTargetPos;
        private float defaultOrthoSize;
        private float targetOrthoSize;
        private bool isViewingAll = false;

        [Header("Cell Lists")]
        public List<UnevenSplittableCell> currCells;
        public List<UnevenSplittableCell> historicalCells = new List<UnevenSplittableCell>();

        [Header("Pyramid Layout")]
        public float cellSpacing = 1f;
        public float rowSpacing = 1.5f;
        public float maxRowWidth = 10f;
        public Transform center;

        [Header("Row Labels")]
        public GameObject rowTextPrefab;
        public float textOffset = 1.5f; // How far to the side of the outermost cells the text spawns

        // Track the spawned texts so we can animate them later
        private List<TMP_Text> leftTexts = new List<TMP_Text>();
        private List<TMP_Text> rightTexts = new List<TMP_Text>();

        [Header("Animation State")]
        public float columnAnimationDuration = 1.5f;
        public float targetTextScale = 2.5f;
        private bool isSequenceMode = false;

        public int currentDepth = 0;
        public int pendingSplits = 0;

        private void Start()
        {
            camTargetPos = mainCam.transform.position;
            defaultOrthoSize = mainCam.orthographicSize;
            targetOrthoSize = defaultOrthoSize;

            // Spawn the labels for the initial starting cell(s) at Depth 0
            if (currCells.Count > 0)
            {
                SpawnRowLabels(currCells.Count, 1f, currentDepth);
            }
        }

        void Update()
        {
            // Block normal inputs if we are playing the ending column sequence
            if (!isSequenceMode)
            {
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    GrowAllCells();
                }

                if (Input.GetKeyDown(KeyCode.Z))
                {
                    ToggleZoomView();
                }

                if (Input.GetKeyDown(KeyCode.X))
                {
                    StartCoroutine(AnimateToColumnSequence());
                }
            }

            mainCam.transform.position = Vector3.Lerp(mainCam.transform.position, camTargetPos, 3.0f * Time.deltaTime);
            mainCam.orthographicSize = Mathf.Lerp(mainCam.orthographicSize, targetOrthoSize, 3.0f * Time.deltaTime);

            if (finishedAnimatingToColumn)
                UpdateTextPulse();
        }

        bool finishedAnimatingToColumn = false;

        private IEnumerator AnimateToColumnSequence()
        {
            isSequenceMode = true;

            // 1. Zoom the camera out to see the whole column
            isViewingAll = true;
            CalculateViewAllTargets();

            // 2. Hide all cells (current and historical)
            foreach (var cell in currCells)
            {
                if (cell != null) cell.gameObject.SetActive(false);
            }
            foreach (var cell in historicalCells)
            {
                if (cell != null) cell.gameObject.SetActive(false);
            }

            // 3. Hide the generation text on the left
            foreach (var txt in leftTexts)
            {
                if (txt != null) txt.gameObject.SetActive(false);
            }

            // 4. Setup target positions and scales for the sequence numbers
            Vector3[] startPositions = new Vector3[rightTexts.Count];
            Vector3[] targetPositions = new Vector3[rightTexts.Count];
            Vector3 startScale = Vector3.one;
            Vector3 endScale = Vector3.one * targetTextScale;

            for (int i = 0; i < rightTexts.Count; i++)
            {
                if (rightTexts[i] == null) continue;

                startPositions[i] = rightTexts[i].transform.position;

                // Keep the same Y, but move to the center X
                targetPositions[i] = new Vector3(center.position.x, startPositions[i].y, startPositions[i].z);
            }

            // 5. Smoothly animate them over time
            float elapsed = 0f;
            while (elapsed < columnAnimationDuration)
            {
                elapsed += Time.deltaTime;
                float pct = Mathf.SmoothStep(0, 10.0f, elapsed / columnAnimationDuration);

                for (int i = 0; i < rightTexts.Count; i++)
                {
                    if (rightTexts[i] == null) continue;

                    rightTexts[i].transform.position = Vector3.Lerp(rightTexts[i].transform.position, targetPositions[i], pct * Time.deltaTime);
                    rightTexts[i].transform.localScale = Vector3.Lerp(rightTexts[i].transform.localScale, endScale, pct * Time.deltaTime);
                }

                yield return null;
            }

            originalRightTextLocalScale = rightTexts[0].transform.localScale; // Store the final scale for pulsing
            finishedAnimatingToColumn = true;
        }
        Vector3 originalRightTextLocalScale;
        float timePulseStart = 0f;
        [Header("Pulse Animation")]
        public float pulseSpeed = 4f;       // How fast the wave oscillates
        public float pulseFrequency = 1.5f; // How tightly packed the waves are vertically
        public float pulseMagnitude = 0.4f; // How much the text grows during a pulse
        private void UpdateTextPulse()
        {
            float currentTime = timePulseStart * pulseSpeed;

            for (int i = 0; i < rightTexts.Count; i++)
            {
                if (rightTexts[i] == null) continue;
                float y = rightTexts[i].transform.position.y;

                float wave = (Mathf.Sin(currentTime + y * pulseFrequency) + 1f) * 0.5f;
                rightTexts[i].transform.localScale = Vector3.Lerp(rightTexts[i].transform.localScale, originalRightTextLocalScale * (1f + wave * pulseMagnitude), Time.deltaTime * 10.0f);
            }

            timePulseStart += Time.deltaTime;
        }

        private void ToggleZoomView()
        {
            isViewingAll = !isViewingAll;

            if (isViewingAll)
            {
                CalculateViewAllTargets();
            }
            else
            {
                camTargetPos.y = center.position.y - (currentDepth * rowSpacing);
                targetOrthoSize = defaultOrthoSize;
            }
        }

        private void CalculateViewAllTargets()
        {
            float totalHeight = currentDepth * rowSpacing;
            float centerY = center.position.y - (totalHeight / 2f);

            camTargetPos = new Vector3(-0.3f, centerY, camTargetPos.z);

            float actualWidth = (currCells.Count > 1) ? (currCells.Count - 1) * cellSpacing : 0;
            float currentWidth = Mathf.Min(actualWidth, maxRowWidth);

            // Add textOffset to the width calculation so the labels don't get cut off when zoomed out
            float sizeHeight = (totalHeight / 2f) + cameraPadding;
            float sizeWidth = ((currentWidth / 2f) + cameraPadding + textOffset) / mainCam.aspect;

            targetOrthoSize = Mathf.Max(sizeHeight, sizeWidth, defaultOrthoSize);
        }

        public void UpdateLayout()
        {
            currCells.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));

            int count = currCells.Count;
            float expectedWidth = (count > 1) ? (count - 1) * cellSpacing : 0;
            float scaleFactor = 1f;

            if (expectedWidth > maxRowWidth && expectedWidth > 0)
            {
                scaleFactor = maxRowWidth / expectedWidth;
            }

            for (int i = 0; i < count; i++)
            {
                currCells[i].targetPosition = GetPyramidPosition(i, count, currentDepth, scaleFactor);
                currCells[i].SetTargetScaleFactor(scaleFactor);
            }

            // Spawn the text labels for this new row
            SpawnRowLabels(count, scaleFactor, currentDepth);

            if (isViewingAll)
            {
                CalculateViewAllTargets();
            }
            else
            {
                camTargetPos.y = center.position.y - (currentDepth * rowSpacing);
            }
        }

        private void SpawnRowLabels(int count, float scaleFactor, int depth)
        {
            if (rowTextPrefab == null || count == 0) return;

            // Get exact positions of the leftmost and rightmost cells in this row
            Vector3 leftCellPos = GetPyramidPosition(0, count, depth, scaleFactor);
            Vector3 rightCellPos = GetPyramidPosition(count - 1, count, depth, scaleFactor);

            // Offset them outwards
            Vector3 leftPos = leftCellPos + Vector3.left * textOffset * 1.5f;
            Vector3 rightPos = rightCellPos + Vector3.right * textOffset;

            // Instantiate and set text for Generation (Left side)
            GameObject leftTextObj = Instantiate(rowTextPrefab, leftPos, Quaternion.identity);
            TMP_Text leftTMP = leftTextObj.GetComponentInChildren<TMP_Text>();
            if (leftTMP != null)
            {
                leftTMP.text = "Gen: " + depth;
                leftTMP.alignment = TextAlignmentOptions.Center; // Right-aligned so text grows outward
                leftTexts.Add(leftTMP);
            }

            // Instantiate and set text for Count (Right side)
            GameObject rightTextObj = Instantiate(rowTextPrefab, rightPos, Quaternion.identity);
            TMP_Text rightTMP = rightTextObj.GetComponentInChildren<TMP_Text>();
            if (rightTMP != null)
            {
                rightTMP.text = count.ToString();
                rightTMP.alignment = TextAlignmentOptions.Center; // Left-aligned so text grows outward
                rightTexts.Add(rightTMP);
            }
        }

        void GrowAllCells()
        {
            List<UnevenSplittableCell> parents = new List<UnevenSplittableCell>(currCells);
            historicalCells.AddRange(parents);
            currCells.Clear();

            currentDepth++;
            pendingSplits = parents.Count;

            for (int i = 0; i < parents.Count; i++)
            {
                if (parents[i] != null)
                {
                    parents[i].GrowCell(t);
                }
            }
        }

        private Vector3 GetPyramidPosition(int index, int countInRow, int depth, float scaleFactor)
        {
            float adjustedSpacing = cellSpacing * scaleFactor;
            float x = (index - (countInRow - 1) * 0.5f) * adjustedSpacing;
            float y = -depth * rowSpacing;

            return center.position +
                   center.right * x +
                   center.up * y;
        }
    }
}