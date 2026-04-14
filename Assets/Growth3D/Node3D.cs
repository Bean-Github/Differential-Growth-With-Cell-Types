using UnityEngine;
using System.Collections.Generic;


namespace Growth3D
{
    [System.Serializable]
    public class Node3D
    {
        public Vector3 position;
        public Vector3 currVelocity;
        public float mass = 1.0f;

        public Dictionary<int, Node3D> neighbors;

        static int nextID = 0;

        public int id;

        public float Curvature
        {
            get => m_curvature;
            private set { }
        }
        private float m_curvature;

        public Vector3Int currentCell; // for spatial hashing

        public Node3D(Vector3 pos, float mass = 1.0f)
        {
            position = pos;
            neighbors = new Dictionary<int, Node3D>();

            // assign unique ID
            id = nextID;
            nextID++;
            this.mass = mass;
        }

        // physics update
        // moves the node based on its current velocity
        public void UpdatePosition()
        {
            position += currVelocity * Time.deltaTime;
        }

        public void ApplyForce(Vector3 force)
        {
            // F = m * a  =>  a = F / m
            Vector3 acceleration = force / mass;
            currVelocity += acceleration * Time.deltaTime;
        }

        // structure management
        public void AddNeighbor(Node3D neighbor)
        {
            if (neighbor == null) return;

            if (!neighbors.ContainsKey(neighbor.id))
            {
                neighbors.Add(neighbor.id, neighbor);
            }
        }

        public void RemoveNeighbor(Node3D neighbor)
        {
            if (neighbor == null) return;

            if (neighbors.ContainsKey(neighbor.id))
            {
                neighbors.Remove(neighbor.id);
            }
        }

        // Calculates the Discrete Gaussian Curvature (Angle Defect) of a vertex on a surface mesh
        public void CalculateCurvature()
        {
            // A vertex on a surface needs at least 3 neighbors to form a 3D umbrella of triangles
            if (neighbors.Count < 3)
            {
                m_curvature = 0f;
                return;
            }

            float angleSum = 0f;
            var enumerator = neighbors.Values.GetEnumerator();

            // 1. Grab the very first neighbor so we can close the loop at the end
            enumerator.MoveNext();
            Node3D firstNeighbor = enumerator.Current;
            Node3D currentNeighbor = firstNeighbor;

            // 2. Loop through the rest of the neighbors in the ring
            while (enumerator.MoveNext())
            {
                Node3D nextNeighbor = enumerator.Current;

                angleSum += GetAngleBetweenNeighbors(currentNeighbor.position, nextNeighbor.position);

                // Shift our reference forward for the next iteration
                currentNeighbor = nextNeighbor;
            }
            enumerator.Dispose();

            // 3. Close the loop by connecting the very last neighbor back to the first
            angleSum += GetAngleBetweenNeighbors(currentNeighbor.position, firstNeighbor.position);

            // Gaussian Curvature = 2 * PI - (Sum of angles)
            m_curvature = (2f * Mathf.PI) - angleSum;
        }

        // Helper function to keep the main loop clean and readable
        private float GetAngleBetweenNeighbors(Vector3 posA, Vector3 posB)
        {
            Vector3 v1 = (posA - this.position).normalized;
            Vector3 v2 = (posB - this.position).normalized;

            // Clamp dot product to prevent NaN errors from floating point imprecision
            float dot = Mathf.Clamp(Vector3.Dot(v1, v2), -1f, 1f);
            return Mathf.Acos(dot); // Returns radians
        }

    }
}

