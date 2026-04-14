using System.Collections.Generic;
using UnityEngine;

namespace Growth3D
{
    public class HE_Vertex
    {
        public Vector3 position;
        public Vector3 velocity;

        // Only needs to point to ONE outgoing half-edge
        public HE_HalfEdge halfEdge;

        public HE_Vertex(Vector3 pos)
        {
            position = pos;
        }
    }

    public class HE_Face
    {
        // Only needs to point to ONE of the half-edges that make up its border
        public HE_HalfEdge halfEdge;
    }

    public class HE_HalfEdge
    {
        public HE_Vertex origin;      // The vertex this edge starts from
        public HE_HalfEdge twin;      // The half-edge going the opposite direction
        public HE_HalfEdge next;      // The next half-edge in the face loop
        public HE_HalfEdge prev;      // (Optional but highly recommended) The previous half-edge
        public HE_Face face;          // The face this half-edge belongs to
    }
}
