using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class TerrainChunk
{
    public Vector3 position;
    public GameObject meshObject;
    public MeshFilter meshFilter;

    private Mesh mesh;
    private MeshRenderer meshRenderer;
    private MeshCollider meshCollider;

    // Compute buffers
    public ComputeBuffer pointsBuffer;
    public ComputeBuffer biomesBuffer;

    // Mesh arrays
    public List<Vector3> vertices;
    public List<Color> colors;
    public List<Vector3> normals;
    public List<Vector3> uvs;
    public List<int>[] trianglesList;

    // Algorithm
    private Dictionary<int3, int> vertexIndexDict;
    private Vertex[] verts = new Vertex[3];

    public Vector3 offset;
    public HashSet<int> densityBoxes = new HashSet<int>();

    public TerrainChunk(Vector3 position, Transform parent, string name, Material[] materials, int numPointsPerAxis, Vector3 offset)
    {
        // Initialize game object
        meshObject = new GameObject(name);
        meshObject.transform.position = position;
        meshObject.transform.parent = parent;
        meshObject.transform.localScale = Vector3.one;
        meshObject.layer = 3;
        meshFilter = meshObject.AddComponent<MeshFilter>();
        meshRenderer = meshObject.AddComponent<MeshRenderer>();
        meshCollider = meshObject.AddComponent<MeshCollider>();
        meshRenderer.sharedMaterials = materials;

        this.position = position;
        this.offset = offset;

        int numPoints = numPointsPerAxis * numPointsPerAxis * numPointsPerAxis;
        pointsBuffer = new ComputeBuffer(numPoints, sizeof(float) * 4, ComputeBufferType.Default);
        int biomeStructSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Biome));
        biomesBuffer = new ComputeBuffer(numPoints, biomeStructSize, ComputeBufferType.Default);

        // Initialize Mesh
        mesh = new Mesh();
        mesh.name = name;
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        meshFilter.sharedMesh = mesh;

        vertices = new List<Vector3>();
        colors = new List<Color>();
        trianglesList = new List<int>[materials.Length];
        for (int i = 0; i < trianglesList.Length; i++)
        {
            trianglesList[i] = new List<int>();
        }
        normals = new List<Vector3>();
        vertexIndexDict = new Dictionary<int3, int>();
    }

    public void UpdateMesh(Triangle[] tris, int triCount)
    {
        vertexIndexDict.Clear();
        vertices.Clear();
        normals.Clear();
        colors.Clear();
        foreach (List<int> triangles in trianglesList)
        {
            triangles.Clear();
        }

        // Fill the arrays
        int vertexCount = 0;
        for (int i = 0; i < triCount; i++)
        {
            Triangle tri = tris[i];
            verts[0] = tris[i].v0;
            verts[1] = tris[i].v1;
            verts[2] = tris[i].v2;

            if(tri.biomeId >= meshRenderer.sharedMaterials.Length)
            {
                Debug.LogError("Seems like a terrain material is missing!");
            }

            foreach (Vertex vert in verts)
            {
                int vertexIndex;
                if (vertexIndexDict.TryGetValue(new int3(vert.id, tri.biomeId), out vertexIndex))
                {
                    trianglesList[tri.biomeId].Add(vertexIndex);

                    // NOTE: fix for strange blending, when the normals don't represent the 3d geometry properly
                    vertices[vertexIndex] = (vertices[vertexIndex] + (vert.position + offset)) / 2.0f;
                }
                else
                {
                    // Add new vertex
                    vertices.Add(vert.position + offset);
                    colors.Add(new Color(1, 1, 1, vert.weight));
                    normals.Add(vert.normal);
                    vertexIndexDict.Add(new int3(vert.id, tris[i].biomeId), vertexCount);
                    trianglesList[tri.biomeId].Add(vertexCount);
                    vertexCount++;
                }
            }
        }

        // Update mesh
        mesh.Clear();
        mesh.subMeshCount = meshRenderer.sharedMaterials.Length;
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetColors(colors);
        for (int i = 0; i < trianglesList.Length; i++)
        {
            mesh.SetTriangles(trianglesList[i], i, true);
        }
        UpdateMeshCollider();
    }

    private void UpdateMeshCollider()
    {
        if(mesh.vertexCount > 0)
        {
            meshCollider.sharedMesh = mesh;
        }
        else
        {
            meshCollider.sharedMesh = null;
        }
    }

    public void ReleaseBuffers()
    {
        pointsBuffer.Release();
        biomesBuffer.Release();
    }
}
