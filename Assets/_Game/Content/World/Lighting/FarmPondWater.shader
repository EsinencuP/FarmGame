Shader "MyLittleFarm/World/Animated Pond"
{
    Properties
    {
        _DeepColor ("Deep water", Color) = (.06,.27,.31,1)
        _ShallowColor ("Ripple color", Color) = (.16,.47,.50,1)
        _Smoothness ("Smoothness", Range(0,1)) = .78
        _RippleStrength ("Ripple strength", Range(0,.3)) = .025
        _PhaseOffset ("Wave phase", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        fixed4 _DeepColor,_ShallowColor;
        half _Smoothness,_RippleStrength;
        float _PhaseOffset;
        struct Input { float3 worldPos; };
        void surf(Input IN,inout SurfaceOutputStandard o)
        {
            float t=_Time.y+_PhaseOffset;
            float a=dot(IN.worldPos.xz,float2(1.3,.35))*2.4+sin(IN.worldPos.z*.7+t*.23)*.5+t*.65;
            float b=dot(IN.worldPos.xz,float2(.7,1.2))*1.5+sin(IN.worldPos.x*.4-t*.3)*.4-t*.35;
            float2 slope=float2(cos(a)*1.7-cos(b)*.6,cos(a)*.8+cos(b)*1.5)*_RippleStrength;
            o.Normal=normalize(float3(-slope.x,-slope.y,1));
            half ripple=.52+.04*sin(b)+.14*pow(saturate(.5+.5*sin(a)),10);
            o.Albedo=lerp(_DeepColor.rgb,_ShallowColor.rgb,ripple);
            o.Metallic=.08;o.Smoothness=_Smoothness;o.Occlusion=1;o.Alpha=1;
            o.Emission=o.Albedo*.015;
        }
        ENDCG
    }
    Fallback "Standard"
}
