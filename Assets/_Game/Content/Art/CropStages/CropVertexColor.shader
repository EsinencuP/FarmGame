Shader "My Little Farm/Crop Vertex Color"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 150
        CGPROGRAM
        #pragma surface surf Lambert addshadow
        #pragma target 3.0
        #pragma multi_compile_instancing
        struct Input { float4 color : COLOR; };
        void surf(Input input, inout SurfaceOutput output)
        {
            // The authored palette is sRGB, just like the imported reference textures.
            #ifdef UNITY_COLORSPACE_GAMMA
            output.Albedo = input.color.rgb;
            #else
            output.Albedo = GammaToLinearSpace(input.color.rgb);
            #endif
            output.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
