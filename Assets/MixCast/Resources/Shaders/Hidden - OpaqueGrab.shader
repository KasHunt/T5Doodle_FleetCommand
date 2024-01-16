/**********************************************************************************
* Blueprint Reality Inc. CONFIDENTIAL
* 2023 Blueprint Reality Inc.
* All Rights Reserved.
*
* NOTICE:  All information contained herein is, and remains, the property of
* Blueprint Reality Inc. and its suppliers, if any.  The intellectual and
* technical concepts contained herein are proprietary to Blueprint Reality Inc.
* and its suppliers and may be covered by Patents, pending patents, and are
* protected by trade secret or copyright law.
*
* Dissemination of this information or reproduction of this material is strictly
* forbidden unless prior written permission is obtained from Blueprint Reality Inc.
***********************************************************************************/

Shader "Hidden/BPR/OpaqueGrab"
{
	Properties
	{
		_MainTex("Main Tex", 2D) = "white" {}
	}
		SubShader
	{
		Tags { "RenderType" = "Transparent" }

		Pass
		{
			CGPROGRAM
			#pragma multi_compile __ CONVERT_TO_SRGB CONVERT_TO_LINEAR

			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"

			sampler2D _MainTex;

			sampler2D _CameraDepthTexture;

			struct v2f {
				float4 pos : SV_POSITION;
				float2 tex : TEXCOORD0;
			};
			
			v2f vert (appdata_base v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.tex = v.texcoord;
				return o;
			}
			
			float4 frag(v2f i) : SV_Target
			{
				float4 o = tex2D(_MainTex, i.tex);
				float depth = tex2D(_CameraDepthTexture, i.tex).r;
				o.a = saturate(LinearEyeDepth(depth) / 1000);
#if CONVERT_TO_SRGB
				o.rgb = LinearToGammaSpace(o.rgb);
#elif CONVERT_TO_LINEAR
				o.rgb = GammaToLinearSpace(o.rgb);
#endif
				return o;
			}
			ENDCG
		}
	}
}
