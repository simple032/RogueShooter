Shader "JianHai/PlayerRim"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _OutlinePixels ("Rim pixels", Float) = 5
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("Renderer color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("External alpha enabled", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" "IgnoreProjector"="True" }
        Cull Off Lighting Off ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment RimFrag
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"
            float4 _MainTex_TexelSize;
            float _OutlinePixels;
            fixed4 RimFrag(v2f IN) : SV_Target
            {
                float2 d = _MainTex_TexelSize.xy * _OutlinePixels;
                float a = SampleSpriteTexture(IN.texcoord).a;
                float edge = 0;
                for (int y = -1; y <= 1; y++)
                    for (int x = -1; x <= 1; x++)
                        edge = max(edge, SampleSpriteTexture(IN.texcoord + float2(x,y)*d).a);
                fixed4 c = IN.color;
                c.a *= saturate(edge - a);
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
