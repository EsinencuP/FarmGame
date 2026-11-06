Shader "MyLittleFarm/World/Animated Pond"
{
    Properties
    {
        _DeepColor ("Deep water", Color) = (.06,.27,.31,1)
        _ShallowColor ("Shallow water", Color) = (.16,.47,.50,1)
        _Smoothness ("Smoothness", Range(0,1)) = .67
        _RippleStrength ("Ripple strength", Range(0,.3)) = .018
        _PhaseOffset ("Wave phase", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard vertex:vert fullforwardshadows
        #pragma target 3.0
        fixed4 _DeepColor,_ShallowColor;
        half _Smoothness,_RippleStrength;
        float _PhaseOffset;
        struct Input { float3 worldPos; float waterDepth; };
        void vert(inout appdata_full v, out Input output)
        {
            UNITY_INITIALIZE_OUTPUT(Input, output);
            output.waterDepth = v.texcoord3.x;
        }
        void surf(Input IN,inout SurfaceOutputStandard o)
        {
            float2 p = IN.worldPos.xz;
            float t = _Time.y + _PhaseOffset;
            float shallow = 1 - smoothstep(.015, .46, IN.waterDepth);
            float2 d1 = float2(1.24, .78), d2 = float2(-.68, 1.64), d3 = float2(2.1, -.74);
            float a = dot(p, d1) + .31 * t + .55 * sin(p.y * .54 - .13 * t);
            float b = dot(p, d2) - .23 * t;
            float c = dot(p, d3) + .19 * t;
            float2 slope = (cos(a) * d1 * .55 + cos(b) * d2 * .30 + cos(c) * d3 * .15)
                * _RippleStrength * lerp(1, .45, shallow);
            o.Normal = normalize(float3(-slope, 1));
            half ripple = (sin(a) * sin(b) + .5 * sin(c)) * .035;
            o.Albedo = lerp(_DeepColor.rgb, _ShallowColor.rgb, .18 + .61 * shallow + ripple);
            o.Metallic = 0;
            o.Smoothness = _Smoothness - .14 * shallow;
            o.Occlusion = 1;
            o.Alpha = 1;
            o.Emission = o.Albedo * .012;
        }
        ENDCG
    }
    Fallback "Standard"
}
