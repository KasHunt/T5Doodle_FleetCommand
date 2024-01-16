Shader "Hidden/BPR/HDRP/ApplyDepthCutoff"
{
    HLSLINCLUDE

    #pragma vertex Vert

    #pragma target 4.5
    #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch

    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassCommon.hlsl"

    struct frag_out
    {
        float4 color : SV_Target;
        float depth : DEPTH;
    };

    TEXTURE2D_FLOAT(_DepthTex);

    float _MaxDist;
    float _PlayerScale;

    inline float LinearEyeDepthToOutDepth(float z)
    {
        return (1 - _ZBufferParams.w * z) / (_ZBufferParams.z * z);
    }

    frag_out FullScreenPass(Varyings varyings)
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(varyings);
        float depth = LoadCameraDepth(varyings.positionCS.xy);
        PositionInputs posInput = GetPositionInput(varyings.positionCS.xy, _ScreenSize.zw, depth, UNITY_MATRIX_I_VP, UNITY_MATRIX_V);

        float col = SAMPLE_TEXTURE2D(_DepthTex, s_linear_clamp_sampler, posInput.positionNDC.xy).r;

        clip(0.999 - col);

        frag_out o;

        float dist = col * _MaxDist * _PlayerScale;
        o.depth = LinearEyeDepthToOutDepth(dist);
        o.color = 0;

        return o;
    }

    ENDHLSL

    SubShader
    {
        Tags{ "RenderPipeline" = "HDRenderPipeline" }
        Pass
        {
            Name "MixCast-ApplyDepthCutoff"

            ZTest LEqual
            ZWrite On
            Blend Off
            Cull Off
            ColorMask RGBA

            HLSLPROGRAM
                #pragma fragment FullScreenPass
            ENDHLSL
        }
    }
    Fallback Off
}
