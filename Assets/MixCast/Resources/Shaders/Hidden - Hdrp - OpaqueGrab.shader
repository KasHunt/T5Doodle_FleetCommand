Shader "Hidden/BPR/HDRP/OpaqueGrab"
{
    HLSLINCLUDE

    #pragma vertex Vert

    #pragma target 4.5
    #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch

    #pragma multi_compile __ CONVERT_TO_SRGB CONVERT_TO_LINEAR

    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassCommon.hlsl"

    float4 FullScreenPass(Varyings varyings) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(varyings);
        float depth = LoadCameraDepth(varyings.positionCS.xy);
        PositionInputs posInput = GetPositionInput(varyings.positionCS.xy, _ScreenSize.zw, depth, UNITY_MATRIX_I_VP, UNITY_MATRIX_V);

        float2 uv = posInput.positionNDC.xy * _RTHandleScale.xy;
        float3 col = CustomPassSampleCameraColor(uv, 0);

#if CONVERT_TO_SRGB
        col = LinearToGammaSpace(col);
#elif CONVERT_TO_LINEAR
        col = GammaToLinearSpace(col);
#endif

        return float4(col, posInput.linearDepth / 1000);
    }

    ENDHLSL

    SubShader
    {
        Tags{ "RenderPipeline" = "HDRenderPipeline" }
        Pass
        {
            Name "MixCast-OpaqueGrab"

            ZTest Off
            ZWrite On
            Blend Off
            Cull Off

            HLSLPROGRAM
                #pragma fragment FullScreenPass
            ENDHLSL
        }
    }
    Fallback Off
}
