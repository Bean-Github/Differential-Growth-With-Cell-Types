using UnityEngine;
using System.Collections.Generic;


namespace Growth2D
{
    [System.Serializable]
    public class Node2D
    {
        public Vector2 position;
        public Vector2 currVelocity;
        public float mass = 1.0f;

        public Dictionary<int, Node2D> neighbors;

        static int nextID = 0;

        public int id;

        public float Curvature
        {
            get => m_curvature;
            private set { }
        }
        private float m_curvature;

        public Vector2Int currentCell; // for spatial hashing

        public Node2D(Vector2 pos, float mass = 1.0f)
        {
            position = pos;
            neighbors = new Dictionary<int, Node2D>();

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

        public void ApplyForce(Vector2 force)
        {
            // F = m * a  =>  a = F / m
            Vector2 acceleration = force / mass;
            currVelocity += acceleration * Time.deltaTime;
        }

        // structure management
        public void AddNeighbor(Node2D neighbor)
        {
            if (neighbor == null) return;

            if (!neighbors.ContainsKey(neighbor.id))
            {
                neighbors.Add(neighbor.id, neighbor);
            }
        }

        public void RemoveNeighbor(Node2D neighbor)
        {
            if (neighbor == null) return;

            if (neighbors.ContainsKey(neighbor.id))
            {
                neighbors.Remove(neighbor.id);
            }
        }

        public void CalculateCurvature()
        {
            // Menger curvature requires exactly 3 points (the current node and exactly 2 neighbors).
            // If it's an endpoint (1) or a branch (>2), Menger curvature is undefined for the node as a whole.
            if (neighbors.Count != 2)
            {
                m_curvature = 0f;
                return;
            }

            // Extract the two neighbors from the dictionary
            var enumerator = neighbors.Values.GetEnumerator();
            enumerator.MoveNext();
            Node2D prev = enumerator.Current;
            enumerator.MoveNext();
            Node2D next = enumerator.Current;
            enumerator.Dispose(); // Free up memory

            // Vectors pointing from THIS node to its neighbors
            Vector2 v1 = prev.position - this.position;
            Vector2 v2 = next.position - this.position;

            // Get the lengths of the three sides of the triangle (a, b, c)
            float len1 = v1.magnitude;               // Distance to prev (a)
            float len2 = v2.magnitude;               // Distance to next (b)
            float len3 = (next.position - prev.position).magnitude; // Distance between neighbors (c)

            // Safety check: Prevent division by zero if nodes are sitting exactly on top of each other
            if (len1 < 0.0001f || len2 < 0.0001f || len3 < 0.0001f)
            {
                m_curvature = 0f;
                return;
            }

            // The magnitude of the 2D cross product gives us 2 * Area of the triangle
            float crossProduct2D = Mathf.Abs((v1.x * v2.y) - (v1.y * v2.x));

            // Menger formula: (4 * Area) / (a * b * c)
            // Since our cross product is already (2 * Area), we just multiply it by 2.
            m_curvature = (2.0f * crossProduct2D) / (len1 * len2 * len3);
        }

    }
}