Shader "MyLittleFarm/Weather/Light Rain Drop"
{
    Properties { _Color ("Tint", Color) = (.72,.83,.94,.28) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            struct appdata { float4 vertex:POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; };
            v2f vert(appdata v)
            {v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.color=v.color*_Color;o.uv=v.uv;return o;}
            fixed4 frag(v2f i):SV_Target
            {
                fixed4 color=i.color;
                color.a*=(1-smoothstep(.08,.5,abs(i.uv.x-.5)))*saturate((1-abs(i.uv.y-.5)*2)*4);
                return color;
            }
            ENDCG
        }
    }
}
