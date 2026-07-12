using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace Growth3DCompute
{

    // generates a node hoard from a simple shape, like a mesh, sphere, plane, or hexagon
    public class NodeHoardGenerator
    {
        public NodeHoardCompute nodeHoard = new NodeHoardCompute();

        // loads a mesh from a Unity Mesh object into the NodeHoardCompute structure
        // also welds vertices that are at the same physical location to avoid duplicate nodes
        public void LoadMesh(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;

            // map the original Unity index (0 to vertices.Length) 
            // to our new, compressed NodeHoard index.
            int[] indexMap = new int[vertices.Length];

            Dictionary<Vector3Int, uint> weldedVertices = new Dictionary<Vector3Int, uint>();

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 vert = vertices[i];

                // Quantize the vector to 3 decimal places to safely compare floats
                Vector3Int quantizedPos = new Vector3Int(
                    Mathf.RoundToInt(vert.x * 1000f),
                    Mathf.RoundToInt(vert.y * 1000f),
                    Mathf.RoundToInt(vert.z * 1000f)
                );

                // If we've already created a node at this exact physical location, reuse its ID
                if (weldedVertices.TryGetValue(quantizedPos, out uint existingNodeId))
                {
                    indexMap[i] = (int)existingNodeId;
                }
                else
                {
                    // This is a brand new physical location. Add it to the hoard.
                    uint newNodeId = nodeHoard.AddNode(vert); // Assuming '0' is default type

                    weldedVertices.Add(quantizedPos, newNodeId);
                    indexMap[i] = (int)newNodeId;
                }
            }

            // Add triangles using the mapped/welded indices
            for (int i = 0; i < triangles.Length; i += 3)
            {
                uint a = (uint)indexMap[triangles[i]];
                uint b = (uint)indexMap[triangles[i + 1]];
                uint c = (uint)indexMap[triangles[i + 2]];

                // Safety Check: If welding collapsed an edge (a == b), it creates a degenerate 
                // line/point instead of a triangle. We skip those to prevent crashing SutureTwin.
                if (a != b && b != c && a != c)
                {
                    nodeHoard.AddTriangle(a, b, c);
                }
            }

            nodeHoard.SealOpenBoundaries();
        }

        #region Random Debug Test Shapes
        public void CreateTestSphere(float startRadius, int subdivisions = 0)
        {
            // Adjust this for detail. 
            // 0 = tabletop die (12 vertices)
            // 2 = low poly sphere (162 vertices)
            // 3 = smooth sphere (642 vertices)

            List<Vector3> vertices = new List<Vector3>();
            List<int[]> faces = new List<int[]>();

            // We use a cache to avoid creating duplicate vertices when two triangles share the same edge
            Dictionary<long, int> midpointCache = new Dictionary<long, int>();

            // 1. Setup the Base Icosahedron (Your original code!)
            float t = (1.0f + Mathf.Sqrt(5.0f)) / 2.0f;
            vertices.Add(new Vector3(-1, t, 0).normalized * startRadius);
            vertices.Add(new Vector3(1, t, 0).normalized * startRadius);
            vertices.Add(new Vector3(-1, -t, 0).normalized * startRadius);
            vertices.Add(new Vector3(1, -t, 0).normalized * startRadius);
            vertices.Add(new Vector3(0, -1, t).normalized * startRadius);
            vertices.Add(new Vector3(0, 1, t).normalized * startRadius);
            vertices.Add(new Vector3(0, -1, -t).normalized * startRadius);
            vertices.Add(new Vector3(0, 1, -t).normalized * startRadius);
            vertices.Add(new Vector3(t, 0, -1).normalized * startRadius);
            vertices.Add(new Vector3(t, 0, 1).normalized * startRadius);
            vertices.Add(new Vector3(-t, 0, -1).normalized * startRadius);
            vertices.Add(new Vector3(-t, 0, 1).normalized * startRadius);

            faces.AddRange(new int[][] {
                    new int[]{0, 11, 5}, new int[]{0, 5, 1},  new int[]{0, 1, 7},   new int[]{0, 7, 10},  new int[]{0, 10, 11},
                    new int[]{1, 5, 9},  new int[]{5, 11, 4}, new int[]{11, 10, 2}, new int[]{10, 7, 6},  new int[]{7, 1, 8},
                    new int[]{3, 9, 4},  new int[]{3, 4, 2},  new int[]{3, 2, 6},   new int[]{3, 6, 8},   new int[]{3, 8, 9},
                    new int[]{4, 9, 5},  new int[]{2, 4, 11}, new int[]{6, 2, 10},  new int[]{8, 6, 7},   new int[]{9, 8, 1}
                });

            // Local Helper to get the midpoint of an edge, and push it out to the sphere's surface
            int GetMidpoint(int p1, int p2)
            {
                // Create a unique key for the edge so we don't calculate it twice
                long min = Mathf.Min(p1, p2);
                long max = Mathf.Max(p1, p2);
                long key = (min << 32) + max; // Bitwise shift to combine two ints into a long

                if (midpointCache.TryGetValue(key, out int index))
                {
                    return index; // We already split this edge! Return the existing midpoint.
                }

                // Calculate the new vertex position
                Vector3 middle = ((vertices[p1] + vertices[p2]) / 2f).normalized * startRadius;

                vertices.Add(middle);
                int newIndex = vertices.Count - 1;
                midpointCache.Add(key, newIndex);

                return newIndex;
            }

            // 2. Subdivide the faces
            for (int i = 0; i < subdivisions; i++)
            {
                List<int[]> nextFaces = new List<int[]>();
                foreach (int[] face in faces)
                {
                    // Replace the triangle with 4 smaller triangles
                    int a = GetMidpoint(face[0], face[1]);
                    int b = GetMidpoint(face[1], face[2]);
                    int c = GetMidpoint(face[2], face[0]);

                    nextFaces.Add(new int[] { face[0], a, c });
                    nextFaces.Add(new int[] { face[1], b, a });
                    nextFaces.Add(new int[] { face[2], c, b });
                    nextFaces.Add(new int[] { a, b, c }); // The center triangle
                }
                faces = nextFaces;
            }

            // Add to NodeHoard securely
            List<uint> finalNodes = new List<uint>();
            foreach (Vector3 vert in vertices)
            {
                finalNodes.Add(nodeHoard.AddNode(vert));
            }

            foreach (int[] face in faces)
            {
                uint a = finalNodes[face[0]];
                uint b = finalNodes[face[1]];
                uint c = finalNodes[face[2]];

                nodeHoard.AddTriangle(a, b, c);
            }

        }

        public void CreateTestPlane(float width, float length, int widthSegments, int lengthSegments)
        {
            // Ensure we have at least 1 segment
            widthSegments = Mathf.Max(1, widthSegments);
            lengthSegments = Mathf.Max(1, lengthSegments);

            List<Vector3> vertices = new List<Vector3>();
            List<int[]> faces = new List<int[]>();

            // 1. Calculate spacing between vertices
            float xSpacing = width / widthSegments;
            float zSpacing = length / lengthSegments;

            // Shift the starting point so the plane is centered at (0, 0, 0)
            float xOffset = -width / 2f;
            float zOffset = -length / 2f;

            // 2. Generate Vertices (Rows and Columns)
            // There is always 1 more vertex than segments in each direction
            for (int z = 0; z <= lengthSegments; z++)
            {
                for (int x = 0; x <= widthSegments; x++)
                {
                    float xPos = xOffset + (x * xSpacing);
                    float zPos = zOffset + (z * zSpacing);

                    // A flat plane leaves the Y-axis at 0. 
                    // Inner pressure or growth forces will eventually deform this Y value!
                    vertices.Add(new Vector3(xPos, 0f, zPos));
                }
            }

            // 3. Generate Triangles (Faces)
            int rowVertices = widthSegments + 1;

            for (int z = 0; z < lengthSegments; z++)
            {
                for (int x = 0; x < widthSegments; x++)
                {
                    // Find the 4 vertex indices that make up this grid quad
                    int bottomLeft = x + (z * rowVertices);
                    int bottomRight = bottomLeft + 1;
                    int topLeft = bottomLeft + rowVertices;
                    int topRight = bottomRight + rowVertices;

                    // Split the quad into 2 triangles (maintaining clockwise winding order)
                    // First Triangle
                    faces.Add(new int[] { bottomLeft, topLeft, topRight });
                    // Second Triangle
                    faces.Add(new int[] { bottomLeft, topRight, bottomRight });
                }
            }

            // 4. Add to NodeHoard securely
            List<uint> finalNodes = new List<uint>();
            foreach (Vector3 vert in vertices)
            {
                finalNodes.Add(nodeHoard.AddNode(vert));

                //Debug.Log(vert);
            }

            foreach (int[] face in faces)
            {
                uint a = finalNodes[face[0]];
                uint b = finalNodes[face[1]];
                uint c = finalNodes[face[2]];

                nodeHoard.AddTriangle(a, b, c);
            }
        }

        public void CreateTestHexagon(float radius)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<int[]> faces = new List<int[]>();

            // Center
            vertices.Add(Vector3.zero);

            int sides = 6;

            // First ring
            float innerY = 3.0f;
            float outerY = 3.1f;
            float outerRadius = radius * 1.5f;

            int firstRingStart = vertices.Count;
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;

                vertices.Add(new Vector3(x, innerY, z));
            }

            // Second ring
            int secondRingStart = vertices.Count;
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                float x = Mathf.Cos(angle) * outerRadius;
                float z = Mathf.Sin(angle) * outerRadius;

                vertices.Add(new Vector3(x, outerY, z));
            }

            // Center fan
            for (int i = 0; i < sides; i++)
            {
                int a = firstRingStart + i;
                int b = firstRingStart + ((i + 1) % sides);

                faces.Add(new int[] { 0, b, a });
            }

            // Connect the two rings
            for (int i = 0; i < sides; i++)
            {
                int innerA = firstRingStart + i;
                int innerB = firstRingStart + ((i + 1) % sides);

                int outerA = secondRingStart + i;
                int outerB = secondRingStart + ((i + 1) % sides);

                // Quad split into two triangles
                faces.Add(new int[] { innerA, innerB, outerB });
                faces.Add(new int[] { innerA, outerB, outerA });
            }

            // Add to NodeHoard
            List<uint> finalNodes = new List<uint>();
            foreach (Vector3 vert in vertices)
                finalNodes.Add(nodeHoard.AddNode(vert));

            foreach (int[] face in faces)
            {
                nodeHoard.AddTriangle(
                    finalNodes[face[0]],
                    finalNodes[face[1]],
                    finalNodes[face[2]]);
            }

            nodeHoard.SealOpenBoundaries();
        }
        #endregion
    }

    // linker between cpu and gpu data for the actual topology of the grower
    // stores all nodes and edges, and provides methods for adding new nodes and connecting them to neighbors
    public class NodeHoardCompute
    {
        // storage
        public List<Node3D> allNodes;
        public List<HalfEdge3D> halfEdges;
        public List<Face3D> faces;
        private Dictionary<long, HalfEdge3D> edgeSutures;

        // globals
        float globalBaseRestLength = 0.5f;
        float globalSpringStiffness = 3.0f;
        float globalSplitDistanceThreshold = 4.0f;

        public NodeHoardCompute()
        {
            allNodes = new List<Node3D>();
            halfEdges = new List<HalfEdge3D>();
            faces = new List<Face3D>();
            edgeSutures = new Dictionary<long, HalfEdge3D>();
        }

        public NodeHoardCompute(Node3D[] nodes, HalfEdge3D[] edges, Face3D[] faces)
        {
            allNodes = nodes.ToList();
            halfEdges = edges.ToList();
            this.faces = faces.ToList();
            edgeSutures = new Dictionary<long, HalfEdge3D>();
        }

        public void InitGlobalValues(float baseRestLength, float springStiffness, float splitDistanceThreshold)
        {
            globalBaseRestLength = baseRestLength;
            globalSpringStiffness = springStiffness;
            globalSplitDistanceThreshold = splitDistanceThreshold;
        }

        #region Getters
        public Node3D GetNode(uint id)
        {
            return allNodes[(int)id];
        }

        public HalfEdge3D GetEdge(uint id)
        {
            return halfEdges[(int)id];
        }

        public Face3D GetFace(uint id)
        {
            return faces[(int)id];
        }

        public uint AddNode(Vector3 position, int type = 0)
        {
            Node3D newNode = new Node3D { 
                position = position, 
                halfEdge = uint.MaxValue
            };
            newNode.id = (uint)allNodes.Count;
            newNode.baseType = (uint)type;

            allNodes.Add(newNode);
            return newNode.id;
        }

        uint AddEdge(ref HalfEdge3D edge)
        {
            edge.id = (uint)halfEdges.Count;

            edge.baseRestLength = globalBaseRestLength;
            edge.currRestLength = globalBaseRestLength;
            edge.springStiffness = globalSpringStiffness;
            edge.splitDistanceThreshold = globalSplitDistanceThreshold;

            halfEdges.Add(edge);
            return edge.id;
        }

        #endregion

        #region Edge Management
        // currently unused
        //public HalfEdge3D AddEdge(Node3D from, Node3D to)
        //{
        //    HalfEdge3D he = new HalfEdge3D();
        //    halfEdges.Add(he);
        //    he.id = (uint)halfEdges.Count - 1;

        //    he.origin = from.id;
        //    from.halfEdge = he.id; // set from's half edge to the new half edge
        //    SutureTwin(he, from, to);
        //    return he;
        //}

        public Face3D AddTriangle(uint a, uint b, uint c)
        {
            Node3D nodeA = GetNode(a);
            Node3D nodeB = GetNode(b);
            Node3D nodeC = GetNode(c);


            Face3D face = new Face3D();
            faces.Add(face);
            face.id = (uint)faces.Count - 1;

            HalfEdge3D he1 = new HalfEdge3D { origin = nodeA.id, face = face.id };
            HalfEdge3D he2 = new HalfEdge3D { origin = nodeB.id, face = face.id };
            HalfEdge3D he3 = new HalfEdge3D { origin = nodeC.id, face = face.id };

            AddEdge(ref he1);
            AddEdge(ref he2);
            AddEdge(ref he3);

            he1.next = he2.id; he1.prev = he3.id; he1.target = b;
            he2.next = he3.id; he2.prev = he1.id; he2.target = c;
            he3.next = he1.id; he3.prev = he2.id; he3.target = a;

            SutureTwin(ref he1, ref nodeA, ref nodeB);
            SutureTwin(ref he2, ref nodeB, ref nodeC);
            SutureTwin(ref he3, ref nodeC, ref nodeA);

            face.halfEdge = he1.id;
            if (nodeA.halfEdge == uint.MaxValue) nodeA.halfEdge = he1.id;
            if (nodeB.halfEdge == uint.MaxValue) nodeB.halfEdge = he2.id;
            if (nodeC.halfEdge == uint.MaxValue) nodeC.halfEdge = he3.id;

            // Write the modifications back to the master list!
            halfEdges[(int)he1.id] = he1;
            halfEdges[(int)he2.id] = he2;
            halfEdges[(int)he3.id] = he3;

            allNodes[(int)nodeA.id] = nodeA;
            allNodes[(int)nodeB.id] = nodeB;
            allNodes[(int)nodeC.id] = nodeC;

            faces[(int)face.id] = face;

            return face;
        }

        public Face3D AddQuad(uint a, uint b, uint c, uint d)
        {
            Node3D nodeA = GetNode(a);
            Node3D nodeB = GetNode(b);
            Node3D nodeC = GetNode(c);
            Node3D nodeD = GetNode(d);

            Face3D face = new Face3D();
            faces.Add(face);
            face.id = (uint)faces.Count - 1;

            HalfEdge3D he1 = new HalfEdge3D { origin = nodeA.id, face = face.id };
            HalfEdge3D he2 = new HalfEdge3D { origin = nodeB.id, face = face.id };
            HalfEdge3D he3 = new HalfEdge3D { origin = nodeC.id, face = face.id };
            HalfEdge3D he4 = new HalfEdge3D { origin = nodeD.id, face = face.id };

            AddEdge(ref he1);
            AddEdge(ref he2);
            AddEdge(ref he3);
            AddEdge(ref he4);

            // Link the quad loop
            he1.next = he2.id; he1.prev = he4.id; he1.target = b;
            he2.next = he3.id; he2.prev = he1.id; he2.target = c;
            he3.next = he4.id; he3.prev = he2.id; he3.target = d;
            he4.next = he1.id; he4.prev = he3.id; he4.target = a;

            // Suture twins
            SutureTwin(ref he1, ref nodeA, ref nodeB);
            SutureTwin(ref he2, ref nodeB, ref nodeC);
            SutureTwin(ref he3, ref nodeC, ref nodeD);
            SutureTwin(ref he4, ref nodeD, ref nodeA);

            face.halfEdge = he1.id;
            if (nodeA.halfEdge == uint.MaxValue) nodeA.halfEdge = he1.id;
            if (nodeB.halfEdge == uint.MaxValue) nodeB.halfEdge = he2.id;
            if (nodeC.halfEdge == uint.MaxValue) nodeC.halfEdge = he3.id;
            if (nodeD.halfEdge == uint.MaxValue) nodeD.halfEdge = he4.id;

            // Write back to lists
            halfEdges[(int)he1.id] = he1;
            halfEdges[(int)he2.id] = he2;
            halfEdges[(int)he3.id] = he3;
            halfEdges[(int)he4.id] = he4;

            allNodes[(int)nodeA.id] = nodeA;
            allNodes[(int)nodeB.id] = nodeB;
            allNodes[(int)nodeC.id] = nodeC;
            allNodes[(int)nodeD.id] = nodeD;

            faces[(int)face.id] = face;

            return face;
        }

        // this function checks if there is already a half-edge going in the opposite direction (to -> from).
        // If so, it stitches them together as twins.
        // If not, it stores this half-edge in the edgeSutures dictionary to potentially be stitched
        // later when we encounter the opposite direction.
        private void SutureTwin(ref HalfEdge3D halfEdge, ref Node3D from, ref Node3D to)
        {
            long min = System.Math.Min(from.id, to.id);
            long max = System.Math.Max(from.id, to.id);
            long key = (min << 32) | max;

            // if there exists and edge going to -> from, then that is our twin!
            if (edgeSutures.TryGetValue(key, out HalfEdge3D twin))
            {
                if (twin.origin != halfEdge.target)
                {
                    Debug.LogWarning($"Non-manifold geometry or duplicate face detected at nodes {from.id} and {to.id}");
                    return;
                }

                halfEdge.twin = twin.id;
                twin.twin = halfEdge.id;

                halfEdge.isBoundary = 0;
                twin.isBoundary = 0;

                halfEdges[(int)twin.id] = twin;

                edgeSutures.Remove(key);
            }
            else
            {
                halfEdge.twin = uint.MaxValue; // No twin yet
                halfEdge.isBoundary = 1;

                edgeSutures.Add(key, halfEdge);
            }
        }

        // Call this after all triangles have been added to seal the mesh boundaries
        public void SealOpenBoundaries()
        {
            // If edgeSutures is empty, the mesh is fully closed (e.g., a perfect sphere)
            if (edgeSutures.Count == 0) return;

            // 1. Create a master Ghost Face to represent the "outside" of the mesh
            //Face3D ghostFace = new Face3D();
            //ghostFace.id = (uint)faces.Count;
            //faces.Add(ghostFace);

            // This dictionary maps [Origin Node ID] -> [Ghost HalfEdge ID]
            // We need this to connect the ghost edges end-to-end for vertex circulation
            Dictionary<uint, uint> ghostStartsAt = new Dictionary<uint, uint>();
            List<uint> newlyCreatedGhostEdges = new List<uint>();

            // 2. Generate a Ghost Edge for every naked boundary edge
            foreach (var kvp in edgeSutures)
            {
                HalfEdge3D nakedEdge = kvp.Value;

                HalfEdge3D ghostEdge = new HalfEdge3D();

                ghostEdge.isBoundary = 1;
                ghostEdge.isGhost = 1;

                ghostEdge.id = (uint)halfEdges.Count;

                // Ghost edge travels in the opposite direction of the naked edge
                ghostEdge.origin = nakedEdge.target;
                ghostEdge.target = nakedEdge.origin;

                // Pair them as twins
                ghostEdge.twin = nakedEdge.id;
                nakedEdge.twin = ghostEdge.id;

                //ghostEdge.face = ghostFace.id;

                // Add to master lists
                halfEdges.Add(ghostEdge);
                newlyCreatedGhostEdges.Add(ghostEdge.id);

                // Record where this ghost edge begins so we can link the 'next' pointers later
                ghostStartsAt[ghostEdge.origin] = ghostEdge.id;

                // Write the updated naked edge (with its new twin) back to the master list
                halfEdges[(int)nakedEdge.id] = nakedEdge;
            }

            // 3. Link the Ghost Edges end-to-end
            foreach (uint ghostId in newlyCreatedGhostEdges)
            {
                HalfEdge3D ghostEdge = halfEdges[(int)ghostId];

                // The current ghost edge ends at 'ghostEdge.target'.
                // Therefore, the 'next' ghost edge MUST be the one that starts at 'ghostEdge.target'.
                if (ghostStartsAt.TryGetValue(ghostEdge.target, out uint nextGhostId))
                {
                    ghostEdge.next = nextGhostId;

                    // Also set the 'prev' pointer of that next edge back to us
                    HalfEdge3D nextGhostEdge = halfEdges[(int)nextGhostId];
                    nextGhostEdge.prev = ghostEdge.id;
                    halfEdges[(int)nextGhostId] = nextGhostEdge;
                }
                else
                {
                    // Note: If this fails, your mesh has non-manifold boundary geometry 
                    // (e.g., two distinct holes touching at exactly one shared vertex).
                    throw new System.Exception($"Non-manifold boundary at Node {ghostEdge.target}");
                }

                // Write the linked ghost edge back
                halfEdges[(int)ghostId] = ghostEdge;
            }

            //// 4. Assign an arbitrary half-edge to the ghost face
            //ghostFace.halfEdge = newlyCreatedGhostEdges[0];
            //faces[(int)ghostFace.id] = ghostFace;

            // Clear sutures because the mesh is now mathematically perfectly sealed
            edgeSutures.Clear();
        }


        #endregion
    }


}
