Shader "MyLittleFarm/Crop Sway"
{
    Properties { _Color("Tint",Color)=(1,1,1,1) _MainTex("Texture",2D)="white"{} _UseVertexColor("Vertex palette",Float)=0 }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Lambert vertex:vert addshadow fullforwardshadows
        #pragma target 3.0
        sampler2D _MainTex;fixed4 _Color;float _UseVertexColor;
        float _FarmCropBaseY,_FarmCropHeight;
        struct Input { float2 uv_MainTex;float4 color:COLOR; };
        void vert(inout appdata_full v)
        {
            float3 p=mul(unity_ObjectToWorld,v.vertex).xyz;
            float h=max(0,p.y-_FarmCropBaseY);
            float w=saturate(h/max(.1,_FarmCropHeight));
            float phase=_Time.y*1.45+dot(p.xz,float2(.63,.41));
            p.xz+=float2(.85,.53)*sin(phase)*.028*w*w;
            v.vertex=mul(unity_WorldToObject,float4(p,1));
        }
        void surf(Input input,inout SurfaceOutput output)
        {
            float3 palette=input.color.rgb;
            #ifndef UNITY_COLORSPACE_GAMMA
            palette=GammaToLinearSpace(palette);
            #endif
            output.Albedo=tex2D(_MainTex,input.uv_MainTex).rgb*_Color.rgb*lerp(float3(1,1,1),palette,_UseVertexColor);
            output.Alpha=1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
