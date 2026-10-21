Shader "MyLittleFarm/Skybox/Day Night Cubemaps"
{
    Properties
    {
        [NoScaleOffset] _DayTex ("Day HDR cubemap", Cube) = "grey" {}
        [NoScaleOffset] _NightTex ("Night HDR cubemap", Cube) = "grey" {}
        _DayTint ("Day tint", Color) = (.5,.5,.5,1)
        _NightTint ("Night tint", Color) = (.5,.5,.5,1)
        _DayExposure ("Day exposure", Range(0,8)) = 1
        _NightExposure ("Night exposure", Range(0,8)) = .8
        _Blend ("Night blend", Range(0,1)) = 0
        _Rotation ("Rotation", Range(0,360)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            samplerCUBE _DayTex, _NightTex;
            half4 _DayTex_HDR, _NightTex_HDR;
            half4 _DayTint, _NightTint;
            half _DayExposure, _NightExposure, _Blend;
            float _Rotation;
            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex : SV_POSITION; float3 direction : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float angle = radians(_Rotation);
                float s, c;
                sincos(angle,s,c);
                float3 rotated = float3(c*v.vertex.x-s*v.vertex.z,v.vertex.y,s*v.vertex.x+c*v.vertex.z);
                o.vertex = UnityObjectToClipPos(rotated);
                o.direction = v.vertex.xyz;
                return o;
            }
            half4 frag(v2f i) : SV_Target
            {
                half3 day = DecodeHDR(texCUBE(_DayTex,i.direction),_DayTex_HDR);
                half3 night = DecodeHDR(texCUBE(_NightTex,i.direction),_NightTex_HDR);
                day *= _DayTint.rgb * unity_ColorSpaceDouble.rgb * _DayExposure;
                night *= _NightTint.rgb * unity_ColorSpaceDouble.rgb * _NightExposure;
                return half4(lerp(day,night,_Blend),1);
            }
            ENDCG
        }
    }
    Fallback Off
}
