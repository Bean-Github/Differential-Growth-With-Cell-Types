using System.Collections;
using UnityEngine;

namespace GoldenAngleAnimation
{
    public enum UnevenCellType
    {
        Large,
        Small
    }

    public class UnevenSplittableCell : MonoBehaviour
    {
        public UnevenCellType cellType = UnevenCellType.Small;

        [Header("Scale Targets")]
        public Vector3 targetScale = Vector3.one;
        private Vector3 baseScale = Vector3.one;
        private float scaleMultiplier = 1f;

        public void SetCellType(UnevenCellType newType)
        {
            cellType = newType;
            if (cellType == UnevenCellType.Large)
            {
                baseScale = Vector3.one; // Large base size
                cellRenderer.material = largeCellMaterial;
            }
            else
            {
                baseScale = Vector3.one * 0.7f; // Small base size
                cellRenderer.material = smallCellMaterial;
            }

            CalculateFinalTargetScale();
        }

        // Called by the Manager to scale down the cell if the row is too wide
        public void SetTargetScaleFactor(float factor)
        {
            scaleMultiplier = factor;
            CalculateFinalTargetScale();
        }

        private void CalculateFinalTargetScale()
        {
            targetScale = baseScale * scaleMultiplier;
        }

        [Header("References")]
        public Renderer cellRenderer;
        public Material largeCellMaterial;
        public Material smallCellMaterial;

        [Header("Spawn Behavior")]
        public GameObject cellPrefab;
        public Transform spawnLeftLoc;
        public Transform spawnRightLoc;
        public Animator animator;
        public string splitAnimationTrigger = "Split";
        public float spawnDelay = 0.5f;
        public string growAnimationTrigger = "Grow";
        public float growthDelay = 0.5f;
        public GameObject spawnEffect;

        public Vector3 targetPosition;
        public float moveSpeed = 5f;

        void Update()
        {
            // Lerp Position
            transform.position = Vector3.Lerp(
                transform.position,
                targetPosition,
                Time.deltaTime * moveSpeed
            );

            // Lerp Scale (this replaces the instant scale change from your old SetCellType)
            transform.localScale = Vector3.Lerp(
                transform.localScale,
                targetScale,
                Time.deltaTime * moveSpeed
            );
        }

        public void GrowCell(float t)
        {
            if (cellType == UnevenCellType.Small)
            {
                StartCoroutine(GrowToLargeCoroutine());
            }
            else
            {
                SplitCell(t);
            }
        }

        public void SplitCell(float t)
        {
            StartCoroutine(SplitCellCoroutine(t));
        }

        private IEnumerator GrowToLargeCoroutine()
        {
            animator.Play(growAnimationTrigger);

            yield return new WaitForSeconds(growthDelay);

            if (spawnEffect != null) Instantiate(spawnEffect, transform.position, Quaternion.identity);

            UnevenSplittableCell newCell = Instantiate(cellPrefab, transform.position, Quaternion.identity).GetComponent<UnevenSplittableCell>();
            newCell.SetCellType(UnevenCellType.Large);

            // Inherit parent's scale multiplier instantly so it doesn't start huge before layout updates
            newCell.SetTargetScaleFactor(this.scaleMultiplier);
            
            UnevenSplitCellManager.Instance.currCells.Add(newCell);

            FinishSplit();
        }

        private IEnumerator SplitCellCoroutine(float t)
        {
            if (animator != null) animator.Play(splitAnimationTrigger);

            yield return new WaitForSeconds(spawnDelay);

            if (spawnEffect != null) Instantiate(spawnEffect, transform.position, Quaternion.identity);

            UnevenSplittableCell newCellLeft = Instantiate(cellPrefab, spawnLeftLoc.position, Quaternion.identity).GetComponent<UnevenSplittableCell>();
            UnevenSplittableCell newCellRight = Instantiate(cellPrefab, spawnRightLoc.position, Quaternion.identity).GetComponent<UnevenSplittableCell>();

            newCellLeft.SetCellType(UnevenCellType.Large);
            newCellRight.SetCellType(UnevenCellType.Small);

            // Inherit parent's scale multiplier instantly
            newCellLeft.SetTargetScaleFactor(this.scaleMultiplier);
            newCellRight.SetTargetScaleFactor(this.scaleMultiplier);

            UnevenSplitCellManager.Instance.currCells.Add(newCellLeft);
            UnevenSplitCellManager.Instance.currCells.Add(newCellRight);

            FinishSplit();
        }

        private void FinishSplit()
        {
            UnevenSplitCellManager.Instance.pendingSplits--;

            if (UnevenSplitCellManager.Instance.pendingSplits == 0)
            {
                UnevenSplitCellManager.Instance.UpdateLayout();
            }
        }
    }
}