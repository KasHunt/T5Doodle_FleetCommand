Shader "Tilt Five/Simple Blend Shader PMA"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "black" {}
	}
    
	SubShader {
        Tags {"Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100

        ZWrite Off
        Cull Off
        
        Blend One OneMinusSrcAlpha, OneMinusDstAlpha One
             
        Pass {
            SetTexture [_MainTex] { combine texture }
        }
    }
    FallBack "Diffuse"
}
