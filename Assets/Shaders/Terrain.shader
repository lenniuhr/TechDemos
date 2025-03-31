Shader "LenniUhr/Terrain"
{
    Properties
    {
        [Header(General)]
        _Smoothness("Smoothness", Range(0, 1)) = 0.3
        _Metallic("Metallic", Range(0, 1)) = 0
        
        [Header(Terrain)]
		_RockLight ("Rock Light", Color) = (1,1,1,1)
		_RockDark ("Rock Dark", Color) = (1,1,1,1)
		_GrassLight ("Grass Light", Color) = (1,1,1,1)
		_GrassDark ("Grass Dark", Color) = (1,1,1,1)

        [Header(Terrain Noise)]
		_BlendNoiseTex("Blend Noise Texture", 2D) = "White" {}
        _BlendNoiseScale("Blend Noise Scale", Range(0, 50)) = 30
        _BlendNoiseOffset("Blend Noise Offset", Range(-1, 1)) = 0.25
        _BlendNoiseWeight("Blend Noise weight", Range(0, 5)) = 1
        
        _NormalBias("Normal Bias", Range(-1, 0)) = 0
    }

    SubShader
    {
        Tags 
        { 
            "RenderType"="Opaque" 
            "RenderPipeline" = "UniversalRenderPipeline"
        }
        Pass
        {
            Name "ForwardLit"
            Tags
            {
                "LightMode" = "UniversalForward"
            }
            ZWrite On
            Cull Back

            HLSLPROGRAM

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
	        #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #pragma vertex TerrainForwardVertex
            #pragma fragment TerrainForwardFragment
            #include "Assets/Shaders/TerrainForwardPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags
            {
                "LightMode" = "ShadowCaster"
            }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex TerrainShadowCasterVertex
            #pragma fragment TerrainShadowCasterFragment
            #include "Assets/Shaders/TerrainShadowCasterPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags
            {
                "LightMode" = "DepthOnly"
            }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #include "Assets/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags
            {
                "LightMode" = "DepthNormals"
            }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #include "Assets/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }
    }
}
