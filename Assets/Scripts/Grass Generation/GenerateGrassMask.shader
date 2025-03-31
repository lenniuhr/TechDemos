Shader "Hidden/GenerateGrassMask"
{
    Properties
    {
        _MainTex("Main Texture", 2D) = "white" {}
    }
    SubShader
    {
        //Cull Off
		//ZTest Always
		//ZWrite Off

		Pass 
		{
			Name "Generate Grass Mask"

			HLSLPROGRAM
			#pragma vertex DefaultVertex
			#pragma fragment GrassMaskFragment
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			
			TEXTURE2D(_Control0);	SAMPLER(sampler_Control0);
			TEXTURE2D(_Control1);

			int _NumGrassLayers;

			struct Varyings
			{
				float4 positionHCS : SV_POSITION;
				float2 uv : TEXCOORD0;
			};

			Varyings DefaultVertex(uint vertexID : SV_VertexID)
			{
				Varyings OUT;
				OUT.positionHCS = float4(
					vertexID <= 1 ? -1.0 : 3.0,
					vertexID == 1 ? 3.0 : -1.0,
					0.0, 1.0
				);
				OUT.uv = float2(
					vertexID <= 1 ? 0.0 : 2.0,
					vertexID == 1 ? 2.0 : 0.0
				);
				if (_ProjectionParams.x < 0.0) { // flipped projection matrix
					OUT.positionHCS.y *= -1;
				}
				return OUT;
			}

			// Calculates the ids and weights of the three layers with the highest contribution
			void GetContributingLayers(half4 splatControl1, half4 splatControl2, out uint3 splatIds, out float3 weights)
			{
				float splatMap[8] =
				{
					splatControl1.r, splatControl1.g, splatControl1.b, splatControl1.a,
					splatControl2.r, splatControl2.g, splatControl2.b, splatControl2.a
				};
    
				splatIds = uint3(0, 0, 0);
				weights = float3(0, 0, 0);
    
				for (uint i = 0; i < 8; i++)
				{
					if (splatMap[i] > weights.x)
					{
						splatIds.z = splatIds.y;
						weights.z = weights.y;
            
						splatIds.y = splatIds.x;
						weights.y = weights.x;
            
						splatIds.x = i;
						weights.x = splatMap[i];
					}
					else if (splatMap[i] > weights.y)
					{
						splatIds.z = splatIds.y;
						weights.z = weights.y;
            
						splatIds.y = i;
						weights.y = splatMap[i];
					}
					else if (splatMap[i] > weights.z)
					{
						splatIds.z = i;
						weights.z = splatMap[i];
					}
				}
			}

			half4 GrassMaskFragment(Varyings IN) : SV_TARGET
			{
				half4 control0 = SAMPLE_TEXTURE2D(_Control0, sampler_Control0, IN.uv);
				half4 control1 = SAMPLE_TEXTURE2D(_Control1, sampler_Control0, IN.uv);

				uint3 ids;
				float3 weights;
				GetContributingLayers(control0, control1, ids, weights);
				
				half weight = 0;
				for(int i = 0; i < 3; i++)
				{
					if(ids[i] < _NumGrassLayers)
						weight += weights[i];
				}

				half density = smoothstep(0.5, 1.0, weight);
				return half4(density, 0, 0, 1);
			}
			ENDHLSL
		}
	}
}
