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

Shader "Hidden/BPR/Blit"
{
	Properties
	{
		_Color ("Main Color", Color) = (1,1,1,1)
		_MainTex ("Texture", 2D) = "white" {}
		_TexTransform ("Texture Scale (XY) & Offset (ZW)", Vector) = (1,1,0,0)
	}
	SubShader
	{
		Tags { "RenderType"="Transparent" }
		ColorMask RGBA
		Blend One Zero
		ZWrite Off

		Pass
		{
			CGPROGRAM
			#pragma multi_compile __ CONVERT_TO_SRGB CONVERT_TO_LINEAR

			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			fixed4 _Color;
			sampler2D _MainTex;
			float4 _MainTex_ST;
			float4 _TexTransform;
			
			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex) * _TexTransform.xy + _TexTransform.zw;
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				// sample the texture
				fixed4 col = tex2D(_MainTex, i.uv);
				col *= _Color;
#if CONVERT_TO_SRGB
				col.rgb = LinearToGammaSpace(col.rgb);
#elif CONVERT_TO_LINEAR
				col.rgb = GammaToLinearSpace(col.rgb);
#endif
				return col;
			}
			ENDCG
		}
	}
}
