Shader "MyLittleFarm/BuildingGhost"
{
    Properties { _Color ("Tint", Color) = (0.25, 0.9, 0.35, 0.35) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            struct Input { float4 vertex : POSITION; };
            struct Output { float4 vertex : SV_POSITION; };
            Output vert(Input input) { Output output; output.vertex=UnityObjectToClipPos(input.vertex); return output; }
            fixed4 frag(Output input) : SV_Target { return _Color; }
            ENDCG
        }
    }
}
