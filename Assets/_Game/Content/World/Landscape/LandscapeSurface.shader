Shader "MyLittleFarm/World/Landscape Surface"
{
    Properties
    {
        [NoScaleOffset] _MeadowTex ("Meadow color map (Repeat)", 2D) = "white" {}
        [NoScaleOffset] _DirtTex ("Earth color map (Repeat)", 2D) = "white" {}
        _MeadowColor ("Meadow", Color) = (0.40, 0.51, 0.27, 1)
        _PackedColor ("Packed earth", Color) = (0.55, 0.43, 0.31, 1)
        _BedColor ("Tilled earth", Color) = (0.40, 0.26, 0.17, 1)
        _ShoreColor ("Damp bank", Color) = (0.35, 0.32, 0.24, 1)
        _MapScale ("World units per repeat (meadow, earth)", Vector) = (4.4, 3.6, 0, 0)
        _MapStrength ("Color map strength", Range(0, 1)) = 0.58
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Lambert vertex:vert addshadow fullforwardshadows
        #pragma target 3.0

        sampler2D _MeadowTex, _DirtTex;
        half3 _MeadowColor, _PackedColor, _BedColor, _ShoreColor;
        float4 _MapScale;
        half _MapStrength;

        struct Input
        {
            float3 worldPos;
            float4 cell;
            float2 weights;
            fixed4 palette : COLOR;
        };

        void vert(inout appdata_full v, out Input output)
        {
            UNITY_INITIALIZE_OUTPUT(Input, output);
            output.cell = v.texcoord;
            output.weights = v.texcoord3.xy;
            output.palette = v.color;
            if (v.texcoord.z >= 0.5)
                v.normal = normalize(float3(-v.texcoord3.z, 1, -v.texcoord3.w));
        }

        float Hash(float2 p)
        {
            float3 h = frac(float3(p.xyx) * 0.1031);
            h += dot(h, h.yzx + 33.33);
            return frac((h.x + h.y) * h.z);
        }

        float Noise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            f = f * f * (3 - 2 * f);
            return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x),
                        lerp(Hash(i + float2(0, 1)), Hash(i + 1), f.x), f.y);
        }

        half3 PaletteColor(half3 color)
        {
            #ifdef UNITY_COLORSPACE_GAMMA
            return color;
            #else
            return GammaToLinearSpace(color);
            #endif
        }

        half3 ColorMap(sampler2D map, float2 uv, half blend)
        {
            float2 rotated = mul(float2x2(0.8, -0.6, 0.6, 0.8), uv) * 0.61 + float2(0.37, 0.71);
            return lerp(tex2D(map, uv).rgb, tex2D(map, rotated).rgb, blend);
        }

        void surf(Input input, inout SurfaceOutput output)
        {
            output.Alpha = 1;
            if (input.cell.z < 0.5)
            {
                output.Albedo = PaletteColor(input.palette.rgb);
                return;
            }

            float2 world = input.worldPos.xz;
            float patch = Noise(world / 8.6);
            float warmPatch = Noise(float2(world.x + world.y, world.y - world.x) / 13.5 + 21.7);
            float2 distortion = float2(patch - 0.5, warmPatch - 0.5) * 1.45;
            float2 footprint = fwidth(world);
            float fine = 1 - smoothstep(0.026, 0.10, max(footprint.x, footprint.y));
            fine *= 1 - smoothstep(9, 21, distance(input.worldPos, _WorldSpaceCameraPos));
            half macro = 0.85 + patch * 0.22 + warmPatch * 0.09;

            float2 localEdge = min(input.cell.xy, 1 - input.cell.xy);
            float edge = min(localEdge.x, localEdge.y);
            float2 dirtUv = (world + distortion) / max(_MapScale.y, 0.1);
            if (input.cell.z > 1.5) dirtUv *= float2(1.45, 0.78);
            half mapBlend = lerp(0.32, 0.67, patch);
            half3 dirtMap = ColorMap(_DirtTex, dirtUv, mapBlend);
            half detail = _MapStrength * lerp(0.68, 1, fine);

            if (input.cell.z > 1.5)
            {
                // Furrows run along Z. A shared world-space phase keeps adjacent beds aligned.
                float rowNoise = Noise(world * float2(2.5, 1.65));
                float row = sin(world.x * 18.85 + rowNoise * 1.8);
                float rowVisibility = 1 - smoothstep(0.045, 0.15, footprint.x);
                half planted = step(2.5, input.cell.z);
                half watered = step(3.5, input.cell.z);
                half soilTexture = dot(dirtMap, half3(0.30, 0.59, 0.11));
                half3 soil = _BedColor * lerp(1, 0.74 + 1.12 * soilTexture, detail);
                soil *= 1 + (0.10 + 0.10 * rowNoise) * row * rowVisibility * lerp(0.52, 1, fine);
                soil *= lerp(0.84, 1, smoothstep(0, 0.11, edge));
                soil *= 1 - 0.13 * planted - 0.20 * watered;
                output.Albedo = soil * macro;
                return;
            }

            half3 meadowMap = ColorMap(_MeadowTex, (world + distortion) / max(_MapScale.x, 0.1), mapBlend);
            half3 meadow = lerp(_MeadowColor, meadowMap * half3(1.19, 1.19, 1.04), detail);
            half3 packed = lerp(_PackedColor, dirtMap * half3(0.72, 0.70, 0.66), detail);
            half3 bank = lerp(_ShoreColor, dirtMap * half3(0.38, 0.42, 0.41), detail);

            // The center retains the authored cell type. At its edge the world-corner field is shared.
            float packedWeight = lerp(input.weights.x, input.cell.w, 0.86 * smoothstep(0, 0.36, edge));
            float fray = (Noise(world * 2.25) - 0.5) * 0.20;
            packedWeight = smoothstep(0.32, 0.67, packedWeight + fray);
            float shoreWeight = smoothstep(0.10, 0.71, input.weights.y + fray * 0.44);
            half3 ground = lerp(lerp(meadow, packed, packedWeight), bank, shoreWeight);
            half grain = (Noise(world * 12.0) - 0.5) * 0.12 * fine;
            output.Albedo = ground * (macro + grain);
        }
        ENDCG
    }
    FallBack "My Little Farm/Crop Vertex Color"
}
