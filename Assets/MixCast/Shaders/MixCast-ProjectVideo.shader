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

Shader "Hidden/MixCast/Project Video"
{
	Properties
	{
		_MainTex("Color Texture", 2D) = "white" {}
		_DepthTex ("Depth Texture", 2D) = "white" {}
	}
	SubShader
	{
		Tags { "RenderType"="AlphaTest" }
		ZWrite On

		Pass
		{
			CGPROGRAM
			#pragma glsl
			#pragma target 3.0

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

			struct frag_out
			{
				float4 color : COLOR0;
			};

			sampler2D _MainTex;
			sampler2D _DepthTex;
			float4 _DepthTex_ST;
			float2 _ProjectionExtents;
			float _MaxDist;
			
			v2f vert (appdata v)
			{
				v2f o;

				float depth = tex2Dlod(_DepthTex, float4(v.uv, 0, 0)).r * _MaxDist;
				float3 localPos = depth * float3(v.vertex.xy * _ProjectionExtents, 1);

				o.vertex = UnityObjectToClipPos(localPos);
				o.uv = v.uv;
				return o;
			}
			frag_out frag (v2f i)
			{
				frag_out o;

				float4 col = tex2D(_MainTex, i.uv);
				o.color = col;
				clip(o.color.a - 0.01);

				return o;
			}
			ENDCG
		}
	}
}
