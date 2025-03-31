Shader "Hidden/Grass"
{
    Properties
    { 
        [NoScaleOffset] 
        _MainTex("Main Tex", 2D) = "white" {}
        _Color("Color", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline" 
        }
        Pass
        {
            Name "ForwardLit"
            Tags
            {
                "LightMode" = "UniversalForward"
            }
            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
	        #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _SSAO
            #pragma multi_compile _ _OUTLINE
            #pragma vertex GrassVertex
            #pragma fragment GrassFragment
            #include "Assets/Scripts/Grass Generation/Shaders/GrassPasses.hlsl"
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
            Cull Off

            HLSLPROGRAM
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #include "Assets/Scripts/Grass Generation/Shaders/GrassPasses.hlsl"
            ENDHLSL
        }
    }
}