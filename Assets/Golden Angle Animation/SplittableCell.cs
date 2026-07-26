using System.Collections;
using UnityEngine;

namespace GoldenAngleAnimation
{

    public class SplittableCell : MonoBehaviour
    {

        [Header("Spawn Behavior")]
        public GameObject cellPrefab;

        public Transform spawnLeftLoc;
        public Transform spawnRightLoc;

        public Animator animator;

        public string splitAnimationTrigger = "Split";
        public float spawnDelay = 0.5f;

        public GameObject spawnEffect; // a little pop effect

        public Vector3 targetPosition;
        public float moveSpeed = 5f;

        void Update()
        {
            transform.position = Vector3.Lerp(
                transform.position,
                targetPosition,
                Time.deltaTime * moveSpeed
            );
        }

        // grow to large version
        public void GrowCell()
        {

        }

        public void SplitCell(float t)
        {   
            StartCoroutine(SplitCellCoroutine(t));
        }

        private IEnumerator SplitCellCoroutine(float t)
        {
            animator.Play(splitAnimationTrigger);

            yield return new WaitForSeconds(spawnDelay);

            // split the cell into two smaller cells by t
            Instantiate(spawnEffect, transform.position, Quaternion.identity);

            SplittableCell newCellLeft = Instantiate(cellPrefab, spawnLeftLoc.position, Quaternion.identity).GetComponent<SplittableCell>();
            SplittableCell newCellRight = Instantiate(cellPrefab, spawnRightLoc.position, Quaternion.identity).GetComponent<SplittableCell>();

            SplitCellManager.Instance.currCells.Add(newCellLeft);
            SplitCellManager.Instance.currCells.Add(newCellRight);

            SplitCellManager.Instance.pendingSplits--;

            if (SplitCellManager.Instance.pendingSplits == 0)
            {
                SplitCellManager.Instance.UpdateLayout();
            }

            Destroy(gameObject);
        }

    }

}

