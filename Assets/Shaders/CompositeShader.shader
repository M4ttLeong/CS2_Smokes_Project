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
                fixed4 finalColor = lerp(sceneColor, smokeColor, smokeColor.a);
                return finalColor;
            }
            ENDCG
        }
    }
}