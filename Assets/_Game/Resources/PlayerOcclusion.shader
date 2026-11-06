Shader "MyLittleFarm/Player Occlusion"
{
    SubShader
    {
        CGINCLUDE
        #include "UnityCG.cginc"

        float4 vert(float4 vertex : POSITION) : SV_POSITION
        {
            return UnityObjectToClipPos(vertex);
        }

        fixed4 frag() : SV_Target
        {
            return fixed4(.30, .63, .69, .69);
        }
        ENDCG

        // Mark pixels already occupied by visible player geometry to avoid tinting self-overlap.
        Pass
        {
            Cull Back
            ZWrite Off
            ZTest Equal
            ColorMask 0
            Stencil { Ref 128 WriteMask 128 Comp Always Pass Replace }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDCG
        }
        Pass
        {
            Cull Back
            ZWrite Off
            ZTest Greater
            Blend SrcAlpha OneMinusSrcAlpha
            Stencil { Ref 128 ReadMask 128 Comp NotEqual }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDCG
        }
        Pass
        {
            Cull Back
            ZWrite Off
            ZTest Equal
            ColorMask 0
            Stencil { Ref 0 WriteMask 128 Comp Always Pass Replace }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDCG
        }
    }
}
