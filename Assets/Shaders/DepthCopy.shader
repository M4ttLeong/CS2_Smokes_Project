Shader "Hidden/CopyDepth"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            ZTest Always Cull Off ZWrite Off

            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _CameraDepthTexture;

            float4 frag(v2f_img i) : SV_Target
            {
                float depth = tex2D(_CameraDepthTexture, i.uv).r;
                return float4(depth, 0, 0, 0);
            }
            ENDCG
        }
    }
}
