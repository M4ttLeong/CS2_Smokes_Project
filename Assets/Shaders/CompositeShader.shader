Shader "Unlit/CompositeShader"
//Totally AI generated this composite shader
{
    Properties
    {
        _MainTex ("Base (RGB)", 2D) = "white" {}
        _SmokeTex ("Smoke (RGBA)", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _SmokeTex;
            sampler2D _SmokeDepthTex;

            //CameraDepthTexture is in clip space from the camera
            sampler2D _CameraDepthTexture;
            float4 _MainTex_ST;

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

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 sceneColor = tex2D(_MainTex, i.uv);
                fixed4 smokeColor = tex2D(_SmokeTex, i.uv);
                float smokeDepth = tex2D(_SmokeDepthTex, i.uv).r; // eye-space depth of smoke hit
                float tex2DSceneDepth = tex2D(_CameraDepthTexture, i.uv).r; // raw [0,1] depth

                //https://halisavakis.com/shader-bits-camera-depth-texture/
                float sceneDepth = LinearEyeDepth(tex2DSceneDepth);
                
                //depth info stored in the first channel r

                //In the raymarch compute shader if we don't hit smoke marked as 0.0
                if (smokeDepth == 0.0)
                {
                    return sceneColor;
                }

                if (smokeDepth < sceneDepth)
                {
                    //Smoke is closer
                    //Blend
                    return lerp(sceneColor, smokeColor, smokeColor.a);
                } else 
                {
                    //Smoke is behind geometry return the color of the main texture
                    return sceneColor;
                }


                //Simple smoke blending, doesnt account for depth
                //fixed4 finalColor = lerp(sceneColor, smokeColor, smokeColor.a);
                
                //return finalColor;
            }
            ENDCG
        }
    }
}