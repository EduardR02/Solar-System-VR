Shader "Unlit/Beacon Shader"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Segments ("Segments", Float) = 3
        _StartHeight ("Start Height", Range(0,0.99)) = 0.5
        _ScrollSpeed ("Scroll Speed", Float) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off ZTest LEqual

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 normal : TEXCOORD1;
                float phase : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            fixed4 _Color;
            float _Segments;
            float _StartHeight;
            float _ScrollSpeed;


            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v); //Insert
				UNITY_INITIALIZE_OUTPUT(v2f, o); //Insert
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); //Insert
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.normal = v.normal;
                o.phase = _Time.y * _ScrollSpeed;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                fixed4 col = _Color;
                // fade out the top
                col.a *= lerp(0, 0.75, 1-i.uv.y);
                float adjustedSegments = _Segments / (1.0 - _StartHeight);
                float alphaFactor = frac(i.uv.y * adjustedSegments - i.phase);
                float smoothAlpha = step(0.5, alphaFactor);
                // instead of an if statement
                col.a *= 1.0 - (i.uv.y > _StartHeight) * (1.0 - smoothAlpha);;
                // remove top and bottom of cylinder
                col.a *= abs(i.normal.y) < 0.9;
                return col;
            }
            ENDCG
        }
    }
}
