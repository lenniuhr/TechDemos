using UnityEngine;

public class DuneDensityBox : DensityBox
{
    public int seed = 0;
    [Range(0, 1)]
    public float noiseWeight = 0.25f;
    [Range(-0.5f, 0.5f)]
    public float floorOffset = 0;

    // const density variables
    private const float SCALE = 0.03f;
    private const float PERSISTANCE = 0.3f;

    public override int octaves
    {
        get { return 3; }
    }

    public override Color boxColor
    {
        get { return new Color(0.4f, 0.4f, 0.1f); }
    }

    public override void InitComputeShader()
    {
        densityShader.SetFloat("_NoiseWeight", noiseWeight);
        densityShader.SetFloat("_FloorOffset", floorOffset);
        densityShader.SetFloat("_Scale", SCALE);
        densityShader.SetFloat("_Persistance", PERSISTANCE);
        densityShader.SetInt("_Octaves", octaves);

        // Generate octave offsets
        var prng = new System.Random(seed);
        Vector3[] octaveOffsets = new Vector3[octaves];
        float offsetRange = 1000;
        for (int i = 0; i < octaves; i++)
        {
            octaveOffsets[i] = new Vector3((float)prng.NextDouble() * 2 - 1, (float)prng.NextDouble() * 2 - 1, (float)prng.NextDouble() * 2 - 1) * offsetRange;
        }        
        octaveOffsetsBuffer.SetData(octaveOffsets);

        int kernel = densityShader.FindKernel("Density");
        densityShader.SetBuffer(kernel, "_OctaveOffsets", octaveOffsetsBuffer);
    }
}
