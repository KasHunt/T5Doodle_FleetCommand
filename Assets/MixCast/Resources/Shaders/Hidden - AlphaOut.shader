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

Shader "Hidden/BPR/AlphaOut" {
	Properties{ 
		_MainTex("Base (RGB)", 2D) = "white" {} 
		_TexTransform("Texture Scale (XY) & Offset (ZW)", Vector) = (1,1,0,0)
	}

	SubShader {
		Pass{
			ZTest Always Cull Off ZWrite Off
			Fog{ Mode Off }

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag


			#include "UnityCG.cginc"

			sampler2D _MainTex;
			float4 _TexTransform;

			struct v2f {
				float4 pos : SV_POSITION;
				float2 tex : TEXCOORD0;
			};

			v2f vert(appdata_base v) {
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.tex = v.texcoord * _TexTransform.xy + _TexTransform.zw;
				return o;
			}

			float4 frag(v2f i) : COLOR{
				float4 color = tex2D(_MainTex, i.tex);
				return float4(color.a, color.a, color.a, color.a);
			}
			ENDCG
		}
	}
}
