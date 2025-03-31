using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public abstract class DensityBox : MonoBehaviour
{
    public ComputeShader densityShader;
    public int biomeId = 0;

    // private attributes
    private bool hasChanged = true;
    private HashSet<Vector3Int> lastOverlappingChunks = new HashSet<Vector3Int>();
    private Transform sampleTransform;
    private TerrainGenerator terrainGenerator;
    protected ComputeBuffer octaveOffsetsBuffer;

    public abstract int octaves
    {
        get;
    }

    public abstract Color boxColor
    {
        get;
    }

    void Update()
    {
        if(transform.hasChanged)
        {
            hasChanged = true;
            transform.hasChanged = false;
        }
        if(sampleTransform.hasChanged)
        {
            hasChanged = true;
            sampleTransform.hasChanged = false;
        }
    }

    public abstract void InitComputeShader();

    private void OnEnable()
    {
        hasChanged = true;
        sampleTransform = transform.Find("Sample Transform");
        terrainGenerator = GetComponentInParent<TerrainGenerator>();
        octaveOffsetsBuffer = new ComputeBuffer(octaves, sizeof(float) * 3, ComputeBufferType.Default);
    }

    public void OnDisable()
    {
        if (terrainGenerator)
        {
            terrainGenerator.AddChunksToQueue(lastOverlappingChunks);
        }
        octaveOffsetsBuffer.Release();
    }

    public bool HasChanged()
    {
        if(hasChanged)
        {
            hasChanged = false;
            return true;
        }
        return false;
    }

    public Matrix4x4 GetSampleMatrix()
    {
        if (sampleTransform != null)
        {
            return (transform.localToWorldMatrix.inverse * sampleTransform.localToWorldMatrix).inverse;
        }
        else return Matrix4x4.identity;
    }

    public HashSet<Vector3Int> GetLastOverlappingChunks()
    {
        return lastOverlappingChunks;
    }

    public void ClearLastOverlappingChunks()
    {
        lastOverlappingChunks.Clear();
    }

    public void AddLastOverlappingChunk(Vector3Int chunkId)
    {
        lastOverlappingChunks.Add(chunkId);
    }

    public void SetLastOverlappingChunks(HashSet<Vector3Int> overlappingChunks)
    {
        lastOverlappingChunks = overlappingChunks;
    }

    private void OnValidate()
    {
        hasChanged = true;
    }

    private void OnDestroy()
    {
        if(terrainGenerator)
        {
            terrainGenerator.AddChunksToQueue(lastOverlappingChunks);
        }
    }

    private void OnDrawGizmos()
    {
        if(terrainGenerator && terrainGenerator.enableGizmos)
        {
            Gizmos.color = new Color(boxColor.r, boxColor.g, boxColor.b, 0.25f);
            Gizmos.DrawCube(transform.position, transform.localScale * 0.75f);
            Gizmos.color = new Color(boxColor.r, boxColor.g, boxColor.b, 1.0f);
            Gizmos.DrawWireCube(transform.position, transform.localScale * 0.75f);
        }
    }
}
