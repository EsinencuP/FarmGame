Shader "MyLittleFarm/World/Landmark Breeze"
{
    Properties
    {
        _MainTex ("Leaf texture", 2D) = "white" {}
        _Tint ("Leaf tint", Color) = (1,1,1,1)
        _Cutoff ("Alpha cutoff", Range(0,1)) = .63
        [HideInInspector] _VertexPalette ("Use vertex palette", Float) = 0
        [HideInInspector] _RootY ("Root height in mesh units", Float) = 0
        [HideInInspector] _HeightInv ("Inverse plant height", Float) = 1
        _BendMeters ("Maximum bend in world units", Range(0,.2)) = .07
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Lambert vertex:bend addshadow fullforwardshadows
        #pragma target 3.0
        #pragma shader_feature_local _ALPHATEST_ON

        sampler2D _MainTex;
        half4 _Tint;
        half _Cutoff, _VertexPalette;
        float _RootY, _HeightInv, _BendMeters;

        struct Input { float2 uv_MainTex; fixed4 color : COLOR; };

        void bend(inout appdata_full v)
        {
            float rootWeight = saturate((v.vertex.y - _RootY) * _HeightInv);
            float2 root = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xz;
            float phase = dot(root, float2(.46, .32));
            float breeze = .71 * sin(_Time.y * 1.38 + phase) + .29 * sin(_Time.y * 2.15 + phase * 1.9);
            float3 drift = float3(.87, 0, .49) * (breeze * _BendMeters * rootWeight * rootWeight);
            v.vertex.xyz += mul((float3x3)unity_WorldToObject, drift);
        }

        void surf(Input input, inout SurfaceOutput output)
        {
            fixed4 leaf = tex2D(_MainTex, input.uv_MainTex);
            #ifdef _ALPHATEST_ON
            clip(leaf.a - _Cutoff);
            #endif
            half3 palette = input.color.rgb;
            #ifndef UNITY_COLORSPACE_GAMMA
            palette = GammaToLinearSpace(palette);
            #endif
            // The imported atlas has white RGB outside the alpha mask. Keep the leaf hue at the edge.
            half value = saturate(dot(leaf.rgb, half3(.28, .64, .08)) * 2);
            output.Albedo = lerp(_Tint.rgb * lerp(.63, 1.15, value), palette, _VertexPalette);
            output.Alpha = 1;
        }
        ENDCG
    }
    FallBack "My Little Farm/Crop Vertex Color"
}
