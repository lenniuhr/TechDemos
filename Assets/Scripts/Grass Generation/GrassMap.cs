using LenniUhr.Grass;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace LenniUhr.Grass
{
    [ExecuteAlways]
    [RequireComponent(typeof(Terrain))]
    public class GrassMap : MonoBehaviour
    {
        public Texture2D NoiseTexture;
        public Texture2D ColorTexture;
        public LayerMask MeshLayer;
        public LayerMask OcclusionLayer;
        [Header("Grass Masking")]
        public Shader GrassMaskShader;
        public int GrassLayers = 1;

        [SerializeField]
        [HideInInspector]
        private Texture2D m_GrassMask;

        [SerializeField]
        [HideInInspector]
        private Bounds m_Bounds;

        [SerializeField]
        [HideInInspector]
        private Mesh m_GrassMesh;

        [SerializeField]
        [HideInInspector]
        private int[] m_StartIndices;

        [SerializeField]
        [HideInInspector]
        private int[] m_TriangleCount;

        [SerializeField]
        [HideInInspector]
        private float[] m_BlockHeights;

        private GraphicsBuffer m_TriangleBuffer;
        private GraphicsBuffer m_VertexBuffer;
        private GraphicsBuffer m_NormalBuffer;

        private const int BLOCK_SIZE = 8;
        private const int BLOCKS = 16;
        private const int MAX_GRASS_ANGLE = 45;
        private const float TARGET_TRI_AREA = 0.25f;

        void OnEnable()
        {
            if(!BuffersInitialized())
            {
                InitBuffers();
            }
        }

        public bool BuffersInitialized()
        {
            return m_TriangleBuffer != null && m_VertexBuffer != null && m_NormalBuffer != null;
        }

        public void InitBuffers()
        {
            if (m_GrassMesh == null)
            {
                Debug.LogWarning("Grass mesh is missing, skipping buffer generation.");
                return;
            }

            m_TriangleBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, m_GrassMesh.triangles.Length, sizeof(int));
            m_TriangleBuffer.SetData(m_GrassMesh.triangles);
            m_VertexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, m_GrassMesh.vertices.Length, 3 * sizeof(float));
            m_VertexBuffer.SetData(m_GrassMesh.vertices);
            m_NormalBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, m_GrassMesh.normals.Length, 3 * sizeof(float));
            m_NormalBuffer.SetData(m_GrassMesh.normals);
        }

        private void ReleaseBuffers()
        {
            m_TriangleBuffer?.Release();
            m_VertexBuffer?.Release();
            m_NormalBuffer?.Release();
        }

        private void OnDisable()
        {
            ReleaseBuffers();
        }

        public Terrain GetTerrain()
        {
            return GetComponent<Terrain>();
        }

        public Bounds GetBounds()
        {
            return m_Bounds;
        }

        public void GetBlockProperties(Vector3 center, out int startIndex, out int triangleCount, out float blockHeight)
        {
            // Calculate the id of the center pos
            Vector3 centerOS = center - transform.position;
            int x = (int)math.floor(centerOS.x / BLOCK_SIZE);
            int z = (int)math.floor(centerOS.z / BLOCK_SIZE);

            if (m_StartIndices == null || m_TriangleCount == null || m_BlockHeights == null)
            {
                Debug.LogWarning("Block properties are not initialized.");
                startIndex = 0;
                triangleCount = 0;
                blockHeight = 0;
                return;
            }
            if(x < 0 && x >= BLOCK_SIZE || z < 0 && z >= BLOCK_SIZE)
            {
                Debug.LogWarning("Invalid grass block index.");
                startIndex = 0;
                triangleCount = 0;
                blockHeight = 0;
                return;
            }
            if(x + z * BLOCKS > m_StartIndices.Length)
            {
                Debug.LogWarning("Block index is larger than grass map.");
                startIndex = 0;
                triangleCount = 0;
                blockHeight = 0;
                return;
            }

            startIndex = m_StartIndices[x + z * BLOCKS];
            triangleCount = m_TriangleCount[x + z * BLOCKS];
            blockHeight = m_BlockHeights[x + z * BLOCKS];
        }

        public void GetGraphicsBuffers(out GraphicsBuffer triangleBuffer, out GraphicsBuffer vertexBuffer, out GraphicsBuffer normalBuffer)
        {
            triangleBuffer = m_TriangleBuffer;
            vertexBuffer = m_VertexBuffer;
            normalBuffer = m_NormalBuffer;
        }

        #region Mesh Generation

        public void GenerateMeshBuffer()
        {
            // Initalize terrain and bounds
            OnEnable();

            List<Vector3> mapVertices = new List<Vector3>();
            List<Vector3> mapNormals = new List<Vector3>();
            List<int> mapTriangles = new List<int>();

            m_StartIndices = new int[BLOCKS * BLOCKS];
            m_TriangleCount = new int[BLOCKS * BLOCKS];
            m_BlockHeights = new float[BLOCKS * BLOCKS];

            // For each grass block
            for (int x = 0; x < BLOCKS; x++)
            {
                for (int z = 0; z < BLOCKS; z++)
                {
                    Vector3 center = transform.position + BLOCK_SIZE * new Vector3( 0.5f + x, 0, 0.5f + z);
                    Bounds grassBlockBounds = new Bounds(center, new Vector3(BLOCK_SIZE, 100, BLOCK_SIZE));

                    Collider[] spawners = Physics.OverlapBox(grassBlockBounds.center, grassBlockBounds.extents, Quaternion.identity, MeshLayer);

                    if (spawners.Length == 0)
                    {
                        continue;
                    }

                    List<MeshTriangle> meshTriangles = new List<MeshTriangle>();

                    for (int c = 0; c < spawners.Length; c++)
                    {
                        Collider spawner = spawners[c];

                        List<MeshTriangle> grassMeshTris;
                        if (ProcessMesh(spawner, grassBlockBounds, out grassMeshTris))
                        {
                            MeshUtils.ClampNormals(ref grassMeshTris, MAX_GRASS_ANGLE);
                            MeshUtils.MergeEdgesByDistance(ref grassMeshTris);
                            MeshUtils.SubdivideTriangles(ref grassMeshTris, TARGET_TRI_AREA);
                            MeshUtils.SubdivideTriangles(ref grassMeshTris, TARGET_TRI_AREA);
                            MeshUtils.SubdivideTriangles(ref grassMeshTris, TARGET_TRI_AREA);
                            MeshUtils.SubdivideTriangles(ref grassMeshTris, TARGET_TRI_AREA);

                            // Disable own collider temporarily
                            spawner.gameObject.SetActive(false);
                            FilterColliderOverlaps(ref grassMeshTris);
                            spawner.gameObject.SetActive(true);

                            meshTriangles.AddRange(grassMeshTris);
                        }
                    }
                    List<Vector3> vertices;
                    List<Vector3> normals;
                    List<int> triangles;
                    MeshUtils.IndexTriangleList(meshTriangles, out vertices, out normals, out triangles);
                    MeshUtils.OffsetIndexedList(ref triangles, mapVertices.Count);
                    m_StartIndices[x + z * BLOCKS] = mapTriangles.Count;
                    m_TriangleCount[x + z * BLOCKS] = triangles.Count / 3;
                    m_BlockHeights[x + z * BLOCKS] = Utils.GetHighestPoint(vertices);

                    mapVertices.AddRange(vertices);
                    mapNormals.AddRange(normals);
                    mapTriangles.AddRange(triangles);
                }
            }

            Debug.Log("Mesh contains " + mapTriangles.Count + " triangles and " + mapVertices.Count + " vertices.");
            CreateGrassMesh(mapVertices, mapNormals, mapTriangles);

            ReleaseBuffers();
            InitBuffers();

            // Set bounds
            m_Bounds = GetComponent<Terrain>().terrainData.bounds;
            m_Bounds.center += transform.position;
        }

        private void CreateGrassMesh(List<Vector3> vertices, List<Vector3> normals, List<int> triangles)
        {
            if(m_GrassMesh == null)
            {
                m_GrassMesh = new Mesh();
                m_GrassMesh.name = "Grass Mesh";
            }
            m_GrassMesh.Clear();
            m_GrassMesh.SetVertices(vertices);
            m_GrassMesh.SetNormals(normals);
            m_GrassMesh.SetTriangles(triangles, 0, true);
            m_GrassMesh.RecalculateBounds();
        }

        enum QuadSide
        {
            LEFT,
            RIGHT,
            BOTTOM,
            TOP
        }

        private bool OutsideRect(MeshVertex vert, Rect rect, QuadSide side)
        {
            switch (side)
            {
                case QuadSide.LEFT:
                    return vert.position.x < rect.xMin;
                case QuadSide.RIGHT:
                    return vert.position.x > rect.xMax;
                case QuadSide.BOTTOM:
                    return vert.position.z < rect.yMin;
                case QuadSide.TOP:
                    return vert.position.z > rect.yMax;
            }
            return false;
        }

        private MeshVertex InterpolateOnEdge(MeshVertex vertIn, MeshVertex vertOut, Rect rect, QuadSide side)
        {
            float length01 = 0;
            switch (side)
            {
                case QuadSide.LEFT:
                    length01 = (rect.xMin - vertIn.position.x) / (vertOut.position.x - vertIn.position.x);
                    break;
                case QuadSide.RIGHT:
                    length01 = (rect.xMax - vertIn.position.x) / (vertOut.position.x - vertIn.position.x);
                    break;
                case QuadSide.BOTTOM:
                    length01 = (rect.yMin - vertIn.position.z) / (vertOut.position.z - vertIn.position.z);
                    break;
                case QuadSide.TOP:
                    length01 = (rect.yMax - vertIn.position.z) / (vertOut.position.z - vertIn.position.z);
                    break;
            }

            Vector3 intersectionA = vertIn.position + (vertOut.position - vertIn.position) * length01;
            Vector3 interpolNormal = Utils.InterpolateNormal(vertIn.normal, vertOut.normal, length01);
            return new MeshVertex(intersectionA, interpolNormal);
        }

        private List<MeshTriangle> ClampTriangle(MeshTriangle inputTri, Rect boundsXZ)
        {
            List<MeshTriangle> inputTris = new List<MeshTriangle>();
            inputTris.Add(inputTri);

            for (int i = 0; i < 4; i++)
            {
                QuadSide side = (QuadSide)i;
                List<MeshTriangle> outputTris = new List<MeshTriangle>();

                foreach (MeshTriangle tri in inputTris)
                {
                    // Clamp top Z
                    int outside = 0;
                    if (OutsideRect(tri.a, boundsXZ, side))
                        outside++;
                    if (OutsideRect(tri.b, boundsXZ, side))
                        outside++;
                    if (OutsideRect(tri.c, boundsXZ, side))
                        outside++;

                    // All three vertices outside
                    if (outside == 0)
                    {
                        // Add triangle and continue
                        outputTris.Add(tri);
                    }
                    else if (outside == 1)
                    {
                        // a -> b -> c ->
                        MeshVertex vertOut;
                        MeshVertex vertA;
                        MeshVertex vertB;
                        if (OutsideRect(tri.a, boundsXZ, side))
                        {
                            vertA = tri.b;
                            vertB = tri.c;
                            vertOut = tri.a;
                        }
                        else if (OutsideRect(tri.b, boundsXZ, side))
                        {
                            vertA = tri.c;
                            vertB = tri.a;
                            vertOut = tri.b;
                        }
                        else
                        {
                            vertA = tri.a;
                            vertB = tri.b;
                            vertOut = tri.c;
                        }
                        MeshVertex vertAOut = InterpolateOnEdge(vertA, vertOut, boundsXZ, side);
                        MeshVertex vertBOut = InterpolateOnEdge(vertB, vertOut, boundsXZ, side);

                        outputTris.Add(new MeshTriangle(vertA, vertB, vertBOut));
                        outputTris.Add(new MeshTriangle(vertA, vertBOut, vertAOut));
                    }
                    else if (outside == 2)
                    {
                        MeshVertex vertA;
                        MeshVertex vertOutB;
                        MeshVertex vertOutC;
                        if (!OutsideRect(tri.a, boundsXZ, side))
                        {
                            vertA = tri.a;
                            vertOutB = tri.b;
                            vertOutC = tri.c;
                        }
                        else if (!OutsideRect(tri.b, boundsXZ, side))
                        {
                            vertA = tri.b;
                            vertOutB = tri.c;
                            vertOutC = tri.a;
                        }
                        else
                        {
                            vertA = tri.c;
                            vertOutB = tri.a;
                            vertOutC = tri.b;
                        }
                        MeshVertex vertBOut = InterpolateOnEdge(vertA, vertOutB, boundsXZ, side);
                        MeshVertex vertCOut = InterpolateOnEdge(vertA, vertOutC, boundsXZ, side);

                        outputTris.Add(new MeshTriangle(vertA, vertBOut, vertCOut));
                    }
                    else if (outside == 3)
                    {
                        // Skip and continue
                    }
                    inputTris = outputTris;
                }
            }

            return inputTris;
        }

        private Bounds GetColliderBounds(Collider collider)
        {
            MeshRenderer meshRenderer = collider.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                return meshRenderer.bounds;
            }
            Terrain terrain = collider.GetComponent<Terrain>();
            if (terrain != null)
            {
                return terrain.terrainData.bounds;
            }
            return new Bounds();
        }

        private bool ProcessMesh(Collider collider, Bounds grassBlockBounds, out List<MeshTriangle> grassMeshVertices)
        {
            grassMeshVertices = new List<MeshTriangle>();

            Bounds colliderBounds = GetColliderBounds(collider);

            // Skip meshes that are not in the grass block
            if (!Utils.IntersectsXZ(grassBlockBounds, colliderBounds))
                return false;

            Rect rectXZ = Utils.BoundsToRectXZ(grassBlockBounds);

            MeshFilter meshObj = collider.GetComponent<MeshFilter>();

            if (meshObj == null)
            {
                Debug.LogWarning("GameObject " + collider.name + " doesn't contain a MeshFilter component.");
                return false;
            }

            Mesh mesh = meshObj.sharedMesh;

            int[] triangles = mesh.triangles;
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 normalA = meshObj.transform.TransformVector(normals[triangles[i]]);
                Vector3 normalB = meshObj.transform.TransformVector(normals[triangles[i + 1]]);
                Vector3 normalC = meshObj.transform.TransformVector(normals[triangles[i + 2]]);

                // NOTE: In play mode, the returned mesh data is in world space...
                if (Application.isPlaying)
                {
                    normalA = normals[triangles[i]];
                    normalB = normals[triangles[i + 1]];
                    normalC = normals[triangles[i + 2]];
                }

                if (Vector3.Angle(normalA, Vector3.up) > MAX_GRASS_ANGLE &&
                    Vector3.Angle(normalB, Vector3.up) > MAX_GRASS_ANGLE &&
                    Vector3.Angle(normalC, Vector3.up) > MAX_GRASS_ANGLE)
                {
                    continue;
                }

                Vector3 posA = meshObj.transform.TransformPoint(vertices[triangles[i]]);
                Vector3 posB = meshObj.transform.TransformPoint(vertices[triangles[i + 1]]);
                Vector3 posC = meshObj.transform.TransformPoint(vertices[triangles[i + 2]]);

                if (Application.isPlaying)
                {
                    posA = vertices[triangles[i]];
                    posB = vertices[triangles[i + 1]];
                    posC = vertices[triangles[i + 2]];
                }

                MeshVertex a = new MeshVertex(posA, normalA);
                MeshVertex b = new MeshVertex(posB, normalB);
                MeshVertex c = new MeshVertex(posC, normalC);

                MeshTriangle tri = new MeshTriangle(a, b, c);

                List<MeshTriangle> tris = ClampTriangle(tri, rectXZ);
                grassMeshVertices.AddRange(tris);
            }
            return grassMeshVertices.Count > 0;
        }
#endregion

        #region Occlusion Clipping

        private bool InsideCollider(MeshVertex v)
        {
            RaycastHit hit;
            if (Physics.Raycast(v.position, v.normal, out hit, 30f, OcclusionLayer))
            {
                if (Vector3.Dot(hit.normal, v.normal) > 0)
                    return true;
            }
            return false;
        }

        private MeshVertex RaycastIntersectionPos(MeshVertex a, MeshVertex inside)
        {
            Vector3 dir = inside.position - a.position;
            float length = dir.magnitude;
            RaycastHit hit;
            if (Physics.Raycast(a.position, dir, out hit, length, OcclusionLayer))
            {
                float length01 = hit.distance / length;
                return new MeshVertex(hit.point, Utils.InterpolateNormal(a.normal, inside.normal, length01));
            }
            return inside;
        }

        private void FilterColliderOverlaps(ref List<MeshTriangle> triangles)
        {
            List<MeshTriangle> filteredTriangles = new List<MeshTriangle>();

            foreach (MeshTriangle tri in triangles)
            {
                MeshVertex a = tri.a;
                MeshVertex b = tri.b;
                MeshVertex c = tri.c;

                int inside = 0;
                if (InsideCollider(a))
                    inside++;
                if (InsideCollider(b))
                    inside++;
                if (InsideCollider(c))
                    inside++;

                // Triangle does not hit collider
                if (inside == 0)
                {
                    filteredTriangles.Add(tri);
                    continue;
                }
                else if (inside == 1)
                {
                    MeshVertex vertA;
                    MeshVertex vertB;
                    MeshVertex vertIn;
                    if (InsideCollider(a))
                    {
                        vertA = b;
                        vertB = c;
                        vertIn = a;
                    }
                    else if (InsideCollider(b))
                    {
                        vertA = c;
                        vertB = a;
                        vertIn = b;
                    }
                    else
                    {
                        vertA = a;
                        vertB = b;
                        vertIn = c;
                    }

                    MeshVertex vertInA = RaycastIntersectionPos(vertA, vertIn);
                    MeshVertex vertInB = RaycastIntersectionPos(vertB, vertIn);

                    MeshTriangle triA;
                    MeshTriangle triB;
                    MeshUtils.GetBestTriangles(vertA, vertB, vertInA, vertInB, out triA, out triB);

                    filteredTriangles.Add(triA);
                    filteredTriangles.Add(triB);
                }
                else if (inside == 2)
                {
                    MeshVertex vertA;
                    MeshVertex vertInB;
                    MeshVertex vertInC;
                    if (!InsideCollider(a))
                    {
                        vertA = a;
                        vertInB = b;
                        vertInC = c;
                    }
                    else if (!InsideCollider(b))
                    {
                        vertA = b;
                        vertInB = c;
                        vertInC = a;
                    }
                    else
                    {
                        vertA = c;
                        vertInB = a;
                        vertInC = b;
                    }

                    vertInB = RaycastIntersectionPos(vertA, vertInB);
                    vertInC = RaycastIntersectionPos(vertA, vertInC);

                    filteredTriangles.Add(new MeshTriangle(vertA, vertInB, vertInC));
                }
            }

            triangles = filteredTriangles;
        }
        #endregion

        #region Mask Generation

        public void GenerateMaskTexture()
        {
            Terrain terrain = GetComponent<Terrain>();
            Texture2D alphaMap = terrain.terrainData.alphamapTextures[0];

            RenderTexture rt = new RenderTexture(alphaMap.width, alphaMap.height, 0, RenderTextureFormat.ARGB32, 0);
            rt.Create();
            Graphics.SetRenderTarget(rt);

            Material material = new Material(GrassMaskShader);
            material.SetTexture("_Control0", alphaMap);
            if(terrain.terrainData.alphamapTextures.Length > 1)
                material.SetTexture("_Control1", terrain.terrainData.alphamapTextures[1]);
            material.SetInt("_NumGrassLayers", GrassLayers);
            Graphics.Blit(alphaMap, rt, material, 0);

            m_GrassMask = new Texture2D(rt.width, rt.height, TextureFormat.ARGB32, false);
            m_GrassMask.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            m_GrassMask.Apply();

            Debug.Log($"Generated grass mask with dimension ({m_GrassMask.width}, {m_GrassMask.height})");
        }

        public Texture2D GetMaskTexture()
        {
            return m_GrassMask;
        }

        #endregion

        private void OnDrawGizmosSelected()
        {
            if (m_Bounds != null)
            {
                Gizmos.DrawWireCube(m_Bounds.center, m_Bounds.size);
            }

            if (m_GrassMesh != null && m_GrassMesh.vertices.Length > 0)
            {
                Gizmos.color = new Color(0.3f, 0.3f, 1f, 0.3f);
                Gizmos.DrawMesh(m_GrassMesh, Vector3.up * 0.01f);

                Gizmos.color = new Color(1f, 1f, 1f, 0.2f);
                Gizmos.DrawWireMesh(m_GrassMesh, Vector3.up * 0.01f);

                //Gizmos.color = new Color(0.7f, 0.7f, 0.7f, 1f);
                //Gizmos.DrawMesh(m_GizmosMesh, Vector3.up * 0.1f);
            }
        }
    }
}