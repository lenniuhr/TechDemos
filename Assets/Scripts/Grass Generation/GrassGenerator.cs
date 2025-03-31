using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace LenniUhr.Grass
{
    [ExecuteAlways]
    public class GrassGenerator : MonoBehaviour
    {
        public ComputeShader generatePointsCS;
        public ComputeShader generatePointsOnMeshCS;
        public ComputeShader grassComputeShader;
        public Material material;

        public Transform player;
        public float Radius = 32;
        public float ViewDistance = 40;
        [Range(0, 1)]
        public float DistanceFadeOut = 0.2f;
        public bool CameraCulling = true;

        [Header("Grass Style")]
        public Color GrassColor;
        [Range(0, 1)]
        public float GrassHeight = 0.5f;
        [Range(0, 1)]
        public float GrassWidth = 0.2f;
        [Range(0, 1)]
        public float RandomBend = 0.2f;
        [Range(0, 1)]
        public float RandomSize = 0.2f;
        [Range(0, 1)]
        public float RandomRotation = 0.2f;

        [Header("Wind")]
        public Texture2D WindTexture;
        public float WindScale = 100.0f;
        public float WindStrength = 1.0f;
        public float WindSpeed = 4.0f;

        // Private variables
        private Bounds m_Bounds;

        private const int BLOCK_SIZE = 8;
        private const int MAX_DENSITY = 64; // Grass blades per square meter
        private const int MAX_SV_PER_BLOCK = BLOCK_SIZE * BLOCK_SIZE * MAX_DENSITY * 2;  // Double the area * density

        private GrassMap[] m_GrassMaps;

        private Dictionary<Vector2Int, GrassBlock> activeBlocks;

        private bool m_Initialized = false;

        // Compute shader stuff
        private ComputeBuffer m_DrawBuffer;
        private GraphicsBuffer m_CommandBuffer;

        private const int SOURCE_VERTEX_STRIDE = sizeof(float) * (3 + 3 + 4 + 1);
        private const int DRAW_STRIDE = (3 + 4 * (3 + 3 + 2 + 1 + 4)) * sizeof(float);

        private readonly int[] DRAW_ARGS_RESET = new int[] { 0, 1, 0, 0 };
        private readonly int[] INFO_RESET = new int[] { 0 };

        private int m_Kernel;
        private int m_DispatchSize;

        private void LateUpdate()
        {
            if (!m_Initialized)
                return;

            UpdateGrassBlocks();

            RenderGrassBlades();
        }

        private void OnEnable()
        {
            m_GrassMaps = FindObjectsByType<GrassMap>(FindObjectsSortMode.None);
            foreach(GrassMap grassMap in m_GrassMaps)
            {
                grassMap.InitBuffers();
            }
            Initialize();
        }

        private void OnDisable()
        {
            if (!m_Initialized)
                return;

            ReleaseBuffers();

            foreach (GrassBlock block in activeBlocks.Values)
                block.ReleaseBuffer();

            m_Initialized = false;
        }

        public void Initialize()
        {
            m_Bounds = new Bounds();
            activeBlocks = new Dictionary<Vector2Int, GrassBlock>();

            ReleaseBuffers();
            InitBuffers();
            m_Initialized = true;

            for(int x = 0; x < 6; x++)
            {
                for (int z = 0; z < 6; z++)
                {
                    Vector2Int id = new Vector2Int(x, z);
                    Bounds bounds = GetBlockBounds(id);
                    CreateBlock(id, bounds);
                }
            }
            m_Bounds = new Bounds(player.position, new Vector3(2 * Radius, 50, 2 * Radius));
        }

        private void InitBuffers()
        {
            // TODO calculate proper estimation
            float area = Radius * Radius * math.PI;
            int maxDrawTriangles = (int)(2 * area * MAX_DENSITY) * 2;

            Debug.Log("Max draw triangles: " + maxDrawTriangles);

            // Create buffers
            m_DrawBuffer = new ComputeBuffer(maxDrawTriangles, DRAW_STRIDE, ComputeBufferType.Append);
            m_DrawBuffer.SetCounterValue(0);

            m_CommandBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawArgs.size);

            // Prepare draw triangle shader
            m_Kernel = grassComputeShader.FindKernel("CSMain");

            grassComputeShader.SetBuffer(m_Kernel, "_DrawTriangles", m_DrawBuffer);
            grassComputeShader.SetBuffer(m_Kernel, "_IndirectArgsBuffer", m_CommandBuffer);
            grassComputeShader.SetInt("_MaxGrassBlades", maxDrawTriangles);

            grassComputeShader.GetKernelThreadGroupSizes(m_Kernel, out uint threadGroupSize, out _, out _);
            m_DispatchSize = Mathf.CeilToInt((float)maxDrawTriangles / threadGroupSize);
        }

        private void ReleaseBuffers()
        {
            m_DrawBuffer?.Release();
            m_CommandBuffer?.Release();
        }

        private void RenderGrassBlades()
        {
            // Reset buffers
            // TODO: improve performance by resetting the command/info buffer on GPU instead of CPU?
            m_CommandBuffer.SetData(DRAW_ARGS_RESET);
            m_DrawBuffer.SetCounterValue(0);

            Plane[] cameraPlanes = GeometryUtility.CalculateFrustumPlanes(Camera.main);

            foreach (GrassBlock block in activeBlocks.Values)
            {
                block.Visible = false;

                if (!block.Initialized)
                    continue;

                // Frustum culling
                if(CameraCulling && !GeometryUtility.TestPlanesAABB(cameraPlanes, block.GetBounds()))
                    continue;

                block.Visible = true;

                grassComputeShader.SetBuffer(m_Kernel, "_SourceVertices", block.SourceVertexBuffer);
                grassComputeShader.SetBuffer(m_Kernel, "_InfoBuffer", block.InfoBuffer);
                grassComputeShader.SetVector("_CameraPos", Camera.main.transform.position);
                grassComputeShader.SetVector("_PlayerPos", player.transform.position);
                grassComputeShader.SetFloat("_ViewDistance", ViewDistance);
                grassComputeShader.SetFloat("_DistanceFadeOut", DistanceFadeOut);
                grassComputeShader.SetFloat("_RandomRotation", RandomRotation);
                grassComputeShader.SetFloat("_GrassHeight", GrassHeight);
                grassComputeShader.SetFloat("_GrassWidth", GrassWidth);
                grassComputeShader.SetFloat("_RandomSize", RandomSize);
                grassComputeShader.SetFloat("_RandomBend", RandomBend);
                grassComputeShader.SetVector("_TipColor", GrassColor.linear);

                grassComputeShader.SetTexture(m_Kernel, "_WindTex", WindTexture);
                grassComputeShader.SetFloat("_WindStrength", WindStrength);
                grassComputeShader.SetFloat("_WindScale", WindScale);
                grassComputeShader.SetFloat("_WindSpeed", WindSpeed);
                grassComputeShader.SetVector("_Time", Shader.GetGlobalVector("_Time"));

                // Dispatch compute shader
                grassComputeShader.Dispatch(m_Kernel, m_DispatchSize, 1, 1);
            }

            //float area = radius * radius * math.PI;
            //int maxDrawTriangles = (int)(2 * area * (MAX_DENSITY * 4));
            //GraphicsBuffer.IndirectDrawArgs[] drawArgs = new GraphicsBuffer.IndirectDrawArgs[1];
            //m_CommandBuffer.GetData(drawArgs);
            //Debug.Log("Command buffer vertex count: " + drawArgs[0].vertexCountPerInstance + ", max draw triangles: " + maxDrawTriangles);

            RenderParams rp = new RenderParams(material);
            rp.worldBounds = m_Bounds;
            rp.shadowCastingMode = ShadowCastingMode.Off;
            rp.receiveShadows = true;
            rp.layer = gameObject.layer;
            rp.matProps = new MaterialPropertyBlock();
            rp.matProps.SetBuffer("_DrawTriangles", m_DrawBuffer);
            Graphics.RenderPrimitivesIndirect(rp, MeshTopology.Triangles, m_CommandBuffer);
        }

        private Vector3 GetBlockPosition(Vector2Int id)
        {
            return new Vector3(id.x * BLOCK_SIZE, 0, id.y * BLOCK_SIZE);
        }
        private Bounds GetBlockBounds(Vector2Int id)
        {
            Vector3 blockPos = GetBlockPosition(id);
            Vector3 offset = new Vector3(BLOCK_SIZE / 2, 0, BLOCK_SIZE / 2);
            Vector3 size = new Vector3(BLOCK_SIZE, 2, BLOCK_SIZE);
            return new Bounds(blockPos + offset, size);
        }

        private void GenerateGrassBlocks()
        {
            GetMinMaxIndices(out int minIndexX, out int maxIndexX, out int minIndexZ, out int maxIndexZ);
            float sqrRadius = Radius * Radius;

            for(int x = minIndexX; x <= maxIndexX; x++)
            {
                for (int z = minIndexZ; z <= maxIndexZ; z++)
                {
                    Vector2Int id = new Vector2Int(x, z);
                    Bounds bounds = GetBlockBounds(id);

                    if (Utils.SqrDistanceXZ(bounds, player.position) > sqrRadius)
                        continue;
                    
                    CreateBlock(id, bounds);
                }
            }
        }

        private void UpdateGrassBlocks()
        {
            List<Vector2Int> blocksToDelete = new List<Vector2Int>();

            // Delete blocks out of range
            float sqrRadius = Radius * Radius;
            Vector3 spawnCenter = player.transform.position;
            foreach (var item in activeBlocks)
            {
                GrassBlock block = item.Value;
                if(Utils.SqrDistanceXZ(block.GetBounds(), spawnCenter) > sqrRadius)
                {
                    blocksToDelete.Add(item.Key);
                }
            }

            foreach(Vector2Int id in blocksToDelete)
            {
                activeBlocks[id].ReleaseBuffer();
                activeBlocks.Remove(id);
            }

            // Generate new blocks
            GetMinMaxIndices(out int minIndexX, out int maxIndexX, out int minIndexZ, out int maxIndexZ);

            for (int x = minIndexX; x <= maxIndexX; x++)
            {
                for (int z = minIndexZ; z <= maxIndexZ; z++)
                {
                    Vector2Int id = new Vector2Int(x, z);

                    if (activeBlocks.ContainsKey(id))
                        continue;
                    Bounds bounds = GetBlockBounds(id);

                    if (Utils.SqrDistanceXZ(bounds, spawnCenter) > sqrRadius)
                        continue;

                    CreateBlock(id, bounds);
                }
            }

            // Update bounds
            m_Bounds = new Bounds(spawnCenter, new Vector3(2 * Radius, 100, 2 * Radius));
        }

        private bool GetGrassMap(Vector3 center, out GrassMap grassMap)
        {
            foreach(GrassMap map in m_GrassMaps)
            {
                if(Utils.ContainsXZ(map.GetBounds(), center))
                {
                    grassMap = map;
                    return true;
                }
            }
            grassMap = null;
            return false;
        }

        #region Block Generation

        public void CreateBlock(Vector2Int id, Bounds bounds)
        {
            // Get terrain object
            GrassMap grassMap;
            if (!GetGrassMap(bounds.center, out grassMap))
                return;

            Terrain terrain = grassMap.GetTerrain();

            // Sample heightmap
            float height0 = terrain.SampleHeight(bounds.center + new Vector3(-BLOCK_SIZE * 0.5f, 0, -BLOCK_SIZE * 0.5f));
            float height1 = terrain.SampleHeight(bounds.center + new Vector3(-BLOCK_SIZE * 0.5f, 0, BLOCK_SIZE * 0.5f));
            float height2 = terrain.SampleHeight(bounds.center + new Vector3(BLOCK_SIZE * 0.5f, 0, -BLOCK_SIZE * 0.5f));
            float height3 = terrain.SampleHeight(bounds.center + new Vector3(BLOCK_SIZE * 0.5f, 0, BLOCK_SIZE * 0.5f));

            float maxHeight = math.max(math.max(math.max(height0, height1), height2), height3);
            float minHeight = math.min(math.min(math.min(height0, height1), height2), height3);

            float height = (minHeight + maxHeight) * 0.5f;
            float sizeY = maxHeight - minHeight + 2;

            bounds.center = new Vector3(bounds.center.x, height, bounds.center.z);
            bounds.size = new Vector3(bounds.size.x, sizeY, bounds.size.z);

            // Create grass block
            ComputeShader csInstance = Instantiate(grassComputeShader);
            Material materialInstance = Instantiate(material);

            // Create source vertex buffer
            ComputeBuffer sourceVertexBuffer = new ComputeBuffer(MAX_SV_PER_BLOCK, SOURCE_VERTEX_STRIDE, ComputeBufferType.Append);
            sourceVertexBuffer.SetCounterValue(0);

            // Create info buffer
            GraphicsBuffer infoBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, sizeof(uint));
            infoBuffer.SetData(INFO_RESET);

            // Initialize generate points compute shader
            int kernel = generatePointsCS.FindKernel("CSMain");
            generatePointsCS.SetBuffer(kernel, "_SourceVertices", sourceVertexBuffer);
            generatePointsCS.SetTexture(kernel, "_HeightMap", terrain.terrainData.heightmapTexture);
            generatePointsCS.SetTexture(kernel, "_BaseMap", grassMap.ColorTexture);
            generatePointsCS.SetTexture(kernel, "_NoiseTex", grassMap.GetMaskTexture());
            generatePointsCS.SetFloat("_HeightMapResolution", terrain.terrainData.heightmapResolution);
            generatePointsCS.SetVector("_HeightMapScale", terrain.terrainData.heightmapScale);
            generatePointsCS.SetVector("_TerrainSize", terrain.terrainData.size);
            generatePointsCS.SetVector("_TerrainPosition", terrain.transform.position);
            generatePointsCS.SetVector("_BlockPosition", GetBlockPosition(id));
            generatePointsCS.SetInt("_BlockID", id.x + 16 * id.y);
            generatePointsCS.SetBuffer(kernel, "_InfoBuffer", infoBuffer);

            //Debug.Log("Terrain size: " + terrain.terrainData.size);
            //Debug.Log("Heightmap resolution: " + terrain.terrainData.heightmapResolution);

            // Dispatch compute shader
            generatePointsCS.Dispatch(kernel, 1, 1, 1);

            //uint[] info = new uint[1];
            //infoBuffer.GetData(info);
            //Debug.Log("Source vertex count before: " + info[0] + "/" + MAX_SV_PER_BLOCK);

            // Spawn grass on meshes
            // TODO: Improve for multiple blocks
            grassMap.GetBlockProperties(bounds.center, out int startIndex, out int triangleCount, out float blockHeight);
            if(grassMap.BuffersInitialized() && triangleCount > 0)
            {
                Utils.UpdateBounds(ref bounds, blockHeight + 0.5f);

                kernel = generatePointsOnMeshCS.FindKernel("CSMain");
                generatePointsOnMeshCS.SetBuffer(kernel, "_SourceVertices", sourceVertexBuffer);
                generatePointsOnMeshCS.SetBuffer(kernel, "_InfoBuffer", infoBuffer);

                grassMap.GetGraphicsBuffers(out GraphicsBuffer triangleBuffer, out GraphicsBuffer vertexBuffer, out GraphicsBuffer normalBuffer);
                generatePointsOnMeshCS.SetBuffer(kernel, "_Triangles", triangleBuffer);
                generatePointsOnMeshCS.SetBuffer(kernel, "_Positions", vertexBuffer);
                generatePointsOnMeshCS.SetBuffer(kernel, "_Normals", normalBuffer);
                generatePointsOnMeshCS.SetTexture(kernel, "_BaseMap", grassMap.ColorTexture);
                generatePointsOnMeshCS.SetFloat("_HeightMapResolution", terrain.terrainData.heightmapResolution);
                generatePointsOnMeshCS.SetVector("_TerrainSize", terrain.terrainData.size);
                generatePointsOnMeshCS.SetInt("_StartIndex", startIndex);
                generatePointsOnMeshCS.SetInt("_TriangleCount", triangleCount);

                // Calculate dispatch size -> 1 kernel execution per triangle
                generatePointsOnMeshCS.GetKernelThreadGroupSizes(kernel, out uint threadGroupSize, out _, out _);
                int dispatchSize = Mathf.CeilToInt((float)triangleCount / threadGroupSize);
                generatePointsOnMeshCS.Dispatch(kernel, dispatchSize, 1, 1);
            }

            //infoBuffer.GetData(info);
            //Debug.Log("Source vertex count after: " + info[0] + "/" + MAX_SV_PER_BLOCK);

            // Finish grass block initialization
            GrassBlock block = new GrassBlock();
            block.Setup(bounds, sourceVertexBuffer, infoBuffer);

            activeBlocks.Add(id, block);
        }

        #endregion

        private void GetMinMaxIndices(out int minIndexX, out int maxIndexX, out int minIndexZ, out int maxIndexZ)
        {
            float maxX = player.position.x + Radius;
            float minX = player.position.x - Radius;

            float maxZ = player.position.z + Radius;
            float minZ = player.position.z - Radius;

            maxIndexX = (int)math.floor(maxX / BLOCK_SIZE);
            minIndexX = (int)math.floor(minX / BLOCK_SIZE);

            maxIndexZ = (int)math.floor(maxZ / BLOCK_SIZE);
            minIndexZ = (int)math.floor(minZ / BLOCK_SIZE);
        }

        private void OnDrawGizmosSelected()
        {
            if (!m_Initialized)
                return;

            Vector3 spawnCenter = player.transform.position;
            CustomDebug.DrawCircle(spawnCenter, Quaternion.Euler(90, 0, 0), Radius, 32, Color.green);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(m_Bounds.center, m_Bounds.size);

            foreach (GrassBlock block in activeBlocks.Values)
            {
                Gizmos.color = (block.Visible) ? Color.green : Color.red;
                Gizmos.DrawWireCube(block.GetBounds().center, block.GetBounds().size);
            }
        }
    }
}