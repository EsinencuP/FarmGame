Shader "MyLittleFarm/Decor/Flag"
{
    Properties { _Color ("Cloth color", Color) = (.18,.48,.24,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off
        CGPROGRAM
        #pragma surface surf Standard vertex:vert addshadow fullforwardshadows
        #pragma target 3.0
        fixed4 _Color;
        struct Input { float3 worldPos; };
        void vert(inout appdata_full v)
        {
            float amplitude=v.texcoord.x*.055;
            float phase=_Time.y*2.8+v.texcoord.x*6+v.texcoord.y*1.5;
            v.vertex.z+=sin(phase)*amplitude;
            v.normal=normalize(float3(-cos(phase)*amplitude*6,0,1));
        }
        void surf(Input IN,inout SurfaceOutputStandard o)
        {o.Albedo=_Color.rgb;o.Smoothness=.12;o.Alpha=1;}
        ENDCG
    }
    Fallback "Diffuse"
}
