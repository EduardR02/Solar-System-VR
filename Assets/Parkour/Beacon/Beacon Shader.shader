Shader "Unlit/Beacon Shader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _Segments ("Segments", Float) = 3
        _StartHeight ("Start Height", Float) = 0.5
        _ScrollSpeed ("Scroll Speed", Float) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 normal : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _Segments;
            float _StartHeight;
            float _ScrollSpeed;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                //o.uv = v.uv;
                float phase = _Time.y * _ScrollSpeed;
                o.uv = v.uv;
                o.normal = v.normal;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = lerp(0, 0.75, 1-i.uv.y) * _Color;
                if (i.uv.y > _StartHeight) {
                    float phase = _Time.y * _ScrollSpeed;
                    float adjustedSegments = _Segments / (1.0 - _StartHeight);
                    float alphaFactor = frac(i.uv.y * adjustedSegments - phase);
                    //float smoothAlpha = (sin(alphaFactor * 2.0 * 3.14159) + 1.0) / 2.0;
                    float smoothAlpha = step(0.5, alphaFactor);
                    col.a *= smoothAlpha;
                }
                // remove top and bottom of cylinder
                col.a *= abs(i.normal.y) < 0.9;
                return col;
            }
            ENDCG
        }
    }
}
