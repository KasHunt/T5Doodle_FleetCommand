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

Shader "Hidden/BPR/ApplyDepthCutoff"
{
	Properties
	{
		_DepthTex ("Depth Texture", 2D) = "white" {}
		_MaxDist ("Max Distance", Float) = 65.535
		_PlayerScale ("Player Scale", Float) = 1
	}
	SubShader
	{
		Tags
		{
			"Queue" = "Geometry-1"
			"IgnoreProjector" = "True"
			"RenderType" = "Opaque"
		}

		//ZTest Off
		ZWrite On
		ColorMask RGBA

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
				float4 color : COLOR;
				float depth : DEPTH;
			};

			sampler2D _DepthTex;
			float4 _DepthTex_ST;
			float _MaxDist;
			float _PlayerScale;

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _DepthTex);
				return o;
			}
			inline float LinearEyeDepthToOutDepth(float z)
			{
				return (1 - _ZBufferParams.w * z) / (_ZBufferParams.z * z);
			}
			frag_out frag (v2f i)
			{
				float col = tex2D(_DepthTex, i.uv).r;

				clip(0.999 - col);

				frag_out o;

				o.depth = LinearEyeDepthToOutDepth(col * _MaxDist * _PlayerScale);
				o.color = 0;

				return o;
			}
			ENDCG
		}
	}
}
