Shader "JianHai/IsoSpriteLit"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        [PerRendererData] _NormalMap ("Normal", 2D) = "bump" {}
        [PerRendererData] _EmissionTex ("Emission", 2D) = "black" {}
        [PerRendererData] _HasNormal ("Has Normal", Float) = 0
        [PerRendererData] _HasEmission ("Has Emission", Float) = 0
        [PerRendererData] _LightDir ("Light Dir", Vector) = (-0.55, 0.75, 0.37, 0)
        [PerRendererData] _LightColor ("Light", Color) = (0.78, 0.86, 1, 1)
        [PerRendererData] _Ambient ("Ambient", Color) = (0.28, 0.30, 0.36, 1)
        [PerRendererData] _EmissionGain ("Emission Gain", Float) = 1.6
        [PerRendererData] _Color ("Tint", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "CanUseSpriteAtlas"="True"
            "PreviewType"="Plane"
        }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

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
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            sampler2D _NormalMap;
            sampler2D _EmissionTex;
            float _HasNormal;
            float _HasEmission;
            float4 _LightDir;
            fixed4 _LightColor;
            fixed4 _Ambient;
            float _EmissionGain;
            fixed4 _Color;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 albedo = tex2D(_MainTex, i.uv) * i.color;
                float3 n = float3(0, 0, 1);
                if (_HasNormal > 0.5)
                {
                    fixed3 packed = tex2D(_NormalMap, i.uv).rgb;
                    n = normalize(packed * 2.0 - 1.0);
                }
                float3 L = normalize(_LightDir.xyz);
                float ndotl = saturate(dot(n, L));
                fixed3 lit = albedo.rgb * (_Ambient.rgb + _LightColor.rgb * ndotl);
                fixed3 emission = 0;
                if (_HasEmission > 0.5)
                    emission = tex2D(_EmissionTex, i.uv).rgb * _EmissionGain;
                fixed4 col;
                col.rgb = (lit + emission) * albedo.a;
                col.a = albedo.a;
                return col;
            }
            ENDCG
        }
    }
}
