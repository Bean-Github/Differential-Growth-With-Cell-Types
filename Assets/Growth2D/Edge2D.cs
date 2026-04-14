using UnityEngine;

namespace Growth2D
{ 
    public class Edge2D
    {
        // references
        public Node2D nodeA;
        public Node2D nodeB;
        public int id;

        public float Curvature
        {
            get => (nodeA.Curvature + nodeB.Curvature) / 2.0f;
            private set { }
        }
        public float Length
        {
            get => (nodeB.position - nodeA.position).magnitude;
            private set { }
        }

        public Edge2D(Node2D a, Node2D b, int id)
        {
            nodeA = a;
            nodeB = b;
            this.id = id;
        }

        public float GetLength()
        {
            return (nodeB.position - nodeA.position).magnitude;
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            // TODO: write your implementation of Equals() here
            return this.id == ((Edge2D)obj).id;
        }
        public override int GetHashCode()
        {
            return id.GetHashCode();
        }
    }

}
