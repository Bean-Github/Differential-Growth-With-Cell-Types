using UnityEngine;

namespace Growth3D
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class NodeHoard3DMeshRenderer : MonoBehaviour
    {
        private NodeHoard3D _nodeHoard;
        private MeshFilter _meshFilter;
        private Vector3[] m_vertexBuffer;

        public void Initialize(NodeHoard3D nodeHoard)
        {
            this._nodeHoard = nodeHoard;

            _meshFilter = GetComponent<MeshFilter>();
            GenerateNodeHoardMesh();
        }

        public void GenerateNodeHoardMesh()
        {
            Mesh mesh = new Mesh();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // allows >65k vertices

            // init arrays
            Vector3[] vertices = new Vector3[_nodeHoard.allNodes.Count];
            int[] triangles = new int[_nodeHoard.faces.Count * 3];

            // copy nodes into vertices
            for (int i = 0; i < _nodeHoard.allNodes.Count; i++)
            {
                Node3D node = _nodeHoard.allNodes[i];
                vertices[i] = node.position;
                node.meshIndex = i; // Cache the index so faces can find it instantly!
            }

            // flatten the faces
            int t = 0;
            foreach (Face3D face in _nodeHoard.faces)
            {
                Edge3D e1 = face.halfEdge;
                Edge3D e2 = e1.next;
                Edge3D e3 = e2.next;

                triangles[t++] = e1.origin.meshIndex;
                triangles[t++] = e2.origin.meshIndex;
                triangles[t++] = e3.origin.meshIndex;
            }

            // apply to mesh
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); // Let Unity calculate the lighting normals

            _meshFilter.mesh = mesh;
        }

        public void UpdateMeshPositions()
        {
            // Ensure our buffer is the right size
            if (m_vertexBuffer == null || m_vertexBuffer.Length != _nodeHoard.allNodes.Count)
            {
                m_vertexBuffer = new Vector3[_nodeHoard.allNodes.Count];
            }

            // Just copy the new positions using the cached indices
            for (int i = 0; i < _nodeHoard.allNodes.Count; i++)
            {
                Node3D node = _nodeHoard.allNodes[i];
                m_vertexBuffer[node.meshIndex] = node.position;
            }

            // Apply to mesh (Massively faster than SetTriangles)
            _meshFilter.mesh.SetVertices(m_vertexBuffer);
            _meshFilter.mesh.RecalculateNormals();
        }
    }

}

