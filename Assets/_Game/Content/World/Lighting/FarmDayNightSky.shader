Shader "MyLittleFarm/Skybox/Day Night Cubemaps"
{
    Properties
    {
        [NoScaleOffset] _DayTex ("Day HDR cubemap", Cube) = "grey" {}
        [NoScaleOffset] _NightTex ("Night HDR cubemap", Cube) = "grey" {}
        [NoScaleOffset] _CloudTex ("Overcast HDR cubemap", Cube) = "grey" {}
        _CloudCover ("Cloud cover", Range(0,1)) = 0
        _CloudRotation ("Cloud rotation", Float) = 0
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
            samplerCUBE _DayTex, _NightTex, _CloudTex;
            half4 _DayTex_HDR, _NightTex_HDR, _CloudTex_HDR;
            half4 _DayTint, _NightTint;
            half _DayExposure, _NightExposure, _Blend;
            float _Rotation;
            float _CloudRotation;
            half _CloudCover;
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
                float s,c;
                sincos(radians(_CloudRotation),s,c);
                float3 cloudDirection=float3(c*i.direction.x-s*i.direction.z,i.direction.y,s*i.direction.x+c*i.direction.z);
                half3 clouds=DecodeHDR(texCUBE(_CloudTex,cloudDirection),_CloudTex_HDR);
                half cloudLuminance=dot(clouds,half3(.2126,.7152,.0722));
                clouds=lerp(clouds,cloudLuminance.xxx,.85);
                day=lerp(day,clouds*.65,_CloudCover);
                night=lerp(night,clouds*half3(.075,.09,.13),_CloudCover*.9);
                day *= _DayTint.rgb * unity_ColorSpaceDouble.rgb * _DayExposure;
                night *= _NightTint.rgb * unity_ColorSpaceDouble.rgb * _NightExposure;
                return half4(lerp(day,night,_Blend),1);
            }
            ENDCG
        }
    }
    Fallback Off
}
