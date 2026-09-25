Shader "JianHai/IsoBloom"
{
    Properties
    {
        _MainTex ("", 2D) = "white" {}
        _Threshold ("Threshold", Float) = 0.72
        _Intensity ("Intensity", Float) = 0.9
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Threshold;
            float _Intensity;

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata_img v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                float2 t = _MainTex_TexelSize.xy * 2.5;
                fixed3 bloom = 0;
                bloom += max(0, tex2D(_MainTex, i.uv + float2(t.x, 0)).rgb - _Threshold);
                bloom += max(0, tex2D(_MainTex, i.uv + float2(-t.x, 0)).rgb - _Threshold);
                bloom += max(0, tex2D(_MainTex, i.uv + float2(0, t.y)).rgb - _Threshold);
                bloom += max(0, tex2D(_MainTex, i.uv + float2(0, -t.y)).rgb - _Threshold);
                c.rgb += bloom * (_Intensity * 0.25);
                return c;
            }
            ENDCG
        }
    }
}
