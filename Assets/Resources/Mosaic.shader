Shader "Inkomino/Sticker"
{
    Properties { [PerRendererData] _MainTex("Ink grain",2D)="white"{} }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; };
            sampler2D _MainTex;
            v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.color=v.color;o.uv=v.uv;return o;}
            fixed4 frag(v2f i):SV_Target {
                fixed4 grain=tex2D(_MainTex,i.uv);
                fixed3 pigment=i.color.rgb*lerp(0.84,1.04,grain.r);
                return fixed4(pigment,i.color.a*lerp(0.72,0.98,grain.g));
            }
            ENDCG
        }
    }
}
