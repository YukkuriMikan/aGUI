Shader "aGUI/RichUI"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        [Header(Body Color)]
        _Color ("Tint", Color) = (1,1,1,1)
        _Brightness ("Brightness", Range(-1,1)) = 0
        _RecolorColor ("Target Color (RGB)", Color) = (1,1,1,1)
        _RecolorAmount ("Recolor Amount", Range(0,1)) = 0

        [Header(Outline)]
        [ToggleUI] _OutlineEnabled ("Outline Enabled", Float) = 1
        _OutlineColor ("Outline Color", Color) = (0.035,0.045,0.065,1)
        _OutlineWidth ("Outline Width (UI Units)", Range(0,16)) = 2
        [ToggleUI] _UseOutlineTexture ("Use Outline Texture", Float) = 0
        _OutlineTex ("Outline Texture (RGB + Alpha)", 2D) = "white" {}

        [Header(Shadow)]
        [ToggleUI] _ShadowEnabled ("Shadow Enabled", Float) = 1
        [Enum(Fast,0,Gaussian,1)] _ShadowBlurMode ("Shadow Blur Mode", Float) = 0
        _ShadowColor ("Shadow Color", Color) = (0,0,0,0.45)
        _ShadowOffsetX ("Shadow Offset X (UI Units)", Float) = 3
        _ShadowOffsetY ("Shadow Offset Y (UI Units)", Float) = -3
        _ShadowSoftness ("Shadow Softness (0 = Hard)", Range(0,32)) = 6
        _ShadowSpread ("Shadow Spread (UI Units)", Range(0,16)) = 0
        [Enum(Fast,8,Balanced,16,High,32)] _ShadowSamples ("Shadow Quality", Float) = 16

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [HideInInspector][Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "RichUI"
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            sampler2D _MainTex;
            sampler2D _OutlineTex;
            float4 _OutlineTex_ST;
            float4 _MainTex_TexelSize;
            half4 _TextureSampleAdd;
            half4 _Color, _OutlineColor, _ShadowColor;
            half4 _RecolorColor;
            float _Brightness, _RecolorAmount;
            float _OutlineEnabled, _OutlineWidth, _UseOutlineTexture;
            float _ShadowEnabled, _ShadowBlurMode, _ShadowOffsetX, _ShadowOffsetY;
            float _ShadowSoftness, _ShadowSpread, _ShadowSamples;
            float4 _ClipRect;
            float _UIMaskSoftnessX, _UIMaskSoftnessY;
            float _StencilOp;

            struct Attributes
            {
                float4 position : POSITION;
                half4 color : COLOR;
                float4 uv : TEXCOORD0;
                float4 uvRect : TEXCOORD1;
                float4 uvAxes : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 uvRect : TEXCOORD1;
                float4 uvAxes : TEXCOORD2;
                float4 mask : TEXCOORD3;
                float2 outlineUV : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.position = UnityObjectToClipPos(input.position);
                output.uv = input.uv.xy;
                output.color = input.color * _Color;

                // aImage supplies atlas bounds and UVs per local UI unit.
                // Plain Image keeps the standalone texture-pixel fallback.
                bool hasGeometryData = input.uvRect.z > input.uvRect.x &&
                                       input.uvRect.w > input.uvRect.y;
                output.uvRect = hasGeometryData ? input.uvRect : float4(0,0,1,1);
                output.uvAxes = hasGeometryData ? input.uvAxes :
                    float4(_MainTex_TexelSize.x,0,0,_MainTex_TexelSize.y);
                // UV0.zw spans the original UI rectangle, independently of the
                // sprite atlas or nine-slice cells. Padding extrapolates it.
                float2 outlineUV = hasGeometryData ? input.uv.zw : input.uv.xy;
                output.outlineUV = outlineUV * _OutlineTex_ST.xy + _OutlineTex_ST.zw;

                float2 pixelSize = output.position.w;
                pixelSize /= max(abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy)), float2(0.00001,0.00001));
                float4 rect = clamp(_ClipRect, -2e10, 2e10);
                output.mask = float4(input.position.xy * 2 - rect.xy - rect.zw,
                    0.25 / (0.25 * float2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize)));
                return output;
            }

            static const float2 Directions[16] =
            {
                float2(1,0), float2(0.9238795,0.3826834),
                float2(0.7071068,0.7071068), float2(0.3826834,0.9238795),
                float2(0,1), float2(-0.3826834,0.9238795),
                float2(-0.7071068,0.7071068), float2(-0.9238795,0.3826834),
                float2(-1,0), float2(-0.9238795,-0.3826834),
                float2(-0.7071068,-0.7071068), float2(-0.3826834,-0.9238795),
                float2(0,-1), float2(0.3826834,-0.9238795),
                float2(0.7071068,-0.7071068), float2(0.9238795,-0.3826834)
            };

            // Nested low-discrepancy disks: 8/16/32 samples retain the same radius.
            // Fixed offsets avoid temporal noise; Gaussian weights soften the edge.
            static const float3 ShadowKernel[32] =
            {
                float3(0.0000000, 0.0000000, 1.0000000),
                float3(-0.5213985, 0.4776438, 0.3678794),
                float3(0.0437129, -0.4980855, 0.6065307),
                float3(0.5269235, 0.6872784, 0.2231302),
                float3(-0.3481488, -0.0615826, 0.7788008),
                float3(0.6670471, -0.4243208, 0.2865048),
                float3(-0.1589745, 0.5913773, 0.4723666),
                float3(-0.4311390, -0.8301320, 0.1737739),
                float3(0.2348303, 0.0857597, 0.8824969),
                float3(-0.6932592, 0.2861673, 0.3246525),
                float3(0.2369371, -0.5063209, 0.5352614),
                float3(0.2697708, 0.8600719, 0.1969117),
                float3(-0.3746474, -0.2171159, 0.6872893),
                float3(0.8098168, -0.1780359, 0.2528396),
                float3(-0.3804124, 0.5410974, 0.4168620),
                float3(-0.1244299, -0.9602173, 0.1533550),
                float3(0.1351721, 0.1139232, 0.9394131),
                float3(-0.7282466, 0.0301153, 0.3455908),
                float3(0.3759136, -0.3740842, 0.5697828),
                float3(-0.0408279, 0.8829400, 0.2096114),
                float3(-0.2532625, -0.3034932, 0.7316156),
                float3(0.8028580, 0.1080234, 0.2691463),
                float3(-0.5231966, 0.3640266, 0.4437473),
                float3(0.2089400, -0.9287594, 0.1632455),
                float3(0.1522299, 0.2656615, 0.8290291),
                float3(-0.7340991, -0.2341976, 0.3049828),
                float3(0.5322397, -0.2459083, 0.5028316),
                float3(-0.3546197, 0.8473458, 0.1849814),
                float3(-0.1582966, -0.4401048, 0.6456485),
                float3(0.7504559, 0.3944186, 0.2375208),
                float3(-0.6620391, 0.1745114, 0.3916056),
                float3(0.5323201, -0.8278800, 0.1440637)
            };

            half4 SampleSprite(float2 uv, float4 bounds, float2 uvDx, float2 uvDy)
            {
                float2 footprint = max(abs(uvDx) + abs(uvDy), float2(0.000001,0.000001));
                float2 gate = saturate(min(uv - bounds.xy, bounds.zw - uv) / footprint + 0.5);
                float2 inset = min(max(abs(_MainTex_TexelSize.xy), footprint) * 0.5, (bounds.zw - bounds.xy) * 0.49);
                float2 safeUV = clamp(uv, bounds.xy + inset, bounds.zw - inset);
                // Atlas mip levels can already contain neighbouring sprites.
                // Clamp within this sprite and sample mip 0 to isolate every tap.
                half4 color = tex2Dlod(_MainTex, float4(safeUV, 0, 0)) + _TextureSampleAdd;
                color.a *= gate.x * gate.y;
                return color;
            }

            half DilatedAlpha(float2 uv, float radius, float4 bounds, float4 axes,
                float2 uvDx, float2 uvDy, bool outline)
            {
                half alpha = SampleSprite(uv, bounds, uvDx, uvDy).a;
                [branch] if (radius > 0.0001)
                {
                    int count = outline ? 16 : 8;
                    [loop] for (int n = 0; n < 16; ++n)
                    {
                        if (n >= count) break;
                        float2 direction = Directions[outline ? n : n * 2];
                        float2 offset = (axes.xy * direction.x + axes.zw * direction.y) * radius;
                        alpha = max(alpha, SampleSprite(uv + offset, bounds, uvDx, uvDy).a);
                    }
                    // Inner taps fill thin features between the center and outer ring.
                    [branch] if (radius > 1.0)
                    {
                        [unroll] for (int n = 0; n < 8; ++n)
                        {
                            float2 direction = Directions[n * 2];
                            float2 offset = (axes.xy * direction.x + axes.zw * direction.y) * (radius * 0.5);
                            alpha = max(alpha, SampleSprite(uv + offset, bounds, uvDx, uvDy).a);
                        }
                    }
                }
                return alpha;
            }

            float GaussianCdf(float value)
            {
                // Integral of the standard normal distribution (maximum error < 1e-6).
                float x = abs(value) * 0.70710678118;
                float t = rcp(1 + 0.3275911 * x);
                float erf = 1 - (((((1.061405429 * t - 1.453152027) * t +
                    1.421413741) * t - 0.284496736) * t + 0.254829592) * t) * exp(-x * x);
                return 0.5 + 0.5 * sign(value) * erf;
            }

            float GaussianQuantile(float coverage)
            {
                // Approximate inverse CDF for coverage-based spread. No extra texture taps.
                float v = 2 * clamp(coverage, 0.000001, 0.999999) - 1;
                float logarithm = log(max(1 - v * v, 0.000001));
                float t = 4.330746751 + 0.5 * logarithm;
                return sign(v) * sqrt(max(sqrt(t * t - logarithm / 0.147) - t, 0)) * 1.41421356237;
            }

            half GaussianShadowAlpha(float2 center, float spread, float softness,
                float4 bounds, float4 axes, float2 uvDx, float2 uvDy)
            {
                float radius = softness + spread;
                // Supported Image quads have axis-aligned, positive sprite UVs.
                float2 uvPerUnit = max(float2(axes.x, axes.w), float2(0.000001, 0.000001));
                float2 position = (center - bounds.xy) / uvPerUnit;
                float2 sourceSize = (bounds.zw - bounds.xy) / uvPerUnit;
                float sigma = radius / 3;
                int count = (int)clamp(_ShadowSamples, 8, 32);
                float cellSize = 4 * radius / (count - 2);
                float2 firstCell = floor((position - 2 * radius) / cellSize) * cellSize;
                float2 inset = min(abs(_MainTex_TexelSize.xy) * 0.5, (bounds.zw - bounds.xy) * 0.49);
                float total = 0;
                float secondMoment = 0;

                // Integrate Gaussian mass over source-aligned cells, rather than
                // moving a sparse collection of silhouettes with each output pixel.
                // The grid is fixed to the sprite; cell clipping integrates its edge
                // continuously. Quality bounds work to 8x8 / 16x16 / 32x32 taps.
                [loop] for (int y = 0; y < 32; ++y)
                {
                    if (y >= count) break;
                    float lowY = max(firstCell.y + y * cellSize, 0);
                    float highY = min(firstCell.y + (y + 1) * cellSize, sourceSize.y);
                    if (highY <= lowY) continue;
                    float weightY = GaussianCdf((highY - position.y) / sigma) -
                                    GaussianCdf((lowY - position.y) / sigma);
                    [loop] for (int x = 0; x < 32; ++x)
                    {
                        if (x >= count) break;
                        float lowX = max(firstCell.x + x * cellSize, 0);
                        float highX = min(firstCell.x + (x + 1) * cellSize, sourceSize.x);
                        if (highX <= lowX) continue;
                        float weightX = GaussianCdf((highX - position.x) / sigma) -
                                        GaussianCdf((lowX - position.x) / sigma);
                        float2 sampleUV = bounds.xy + float2(lowX + highX, lowY + highY) * 0.5 * uvPerUnit;
                        sampleUV = clamp(sampleUV, bounds.xy + inset, bounds.zw - inset);
                        // Area clipping above supplies the boundary coverage. Mip 0
                        // and the half-texel inset isolate every tap from atlas neighbours.
                        float alpha = saturate((tex2Dlod(_MainTex, float4(sampleUV, 0, 0)) + _TextureSampleAdd).a);
                        float weight = weightX * weightY;
                        total += alpha * weight;
                        secondMoment += alpha * alpha * weight;
                    }
                }

                // Expand the integrated coverage, then restore the requested softness.
                // This approximates rounded spread without a nested dilation loop or
                // discrete translated copies. Moments retain semi-transparent opacity.
                float result = total;
                [branch] if (spread >= 0.001 && total >= 0.000001)
                {
                    float opacity = secondMoment / max(total, 0.000001);
                    float edgeDistance = sigma * GaussianQuantile(total / max(opacity, 0.000001)) + spread;
                    [branch] if (softness > 0.001)
                        result = opacity * GaussianCdf(edgeDistance / (softness / 3));
                    else
                    {
                        float2 footprint = (abs(uvDx) + abs(uvDy)) / uvPerUnit;
                        result = opacity * saturate(0.5 + edgeDistance / max(max(footprint.x, footprint.y), 0.000001));
                    }
                }
                return (half)result;
            }

            half ShadowAlpha(float2 uv, float4 bounds, float4 axes, float2 uvDx, float2 uvDy)
            {
                float2 center = uv - axes.xy * _ShadowOffsetX - axes.zw * _ShadowOffsetY;
                float spread = max(_ShadowSpread, 0);
                float softness = max(_ShadowSoftness, 0);
                half alpha = 0;
                [branch] if (_ShadowBlurMode > 0.5 && softness + spread >= 0.001)
                    alpha = GaussianShadowAlpha(center, spread, softness, bounds, axes, uvDx, uvDy);
                else if (softness < 0.001)
                {
                    alpha = DilatedAlpha(center, spread, bounds, axes, uvDx, uvDy, false);
                }
                else
                {
                    float total = 0;
                    float weight = 0;
                    int count = (int)clamp(_ShadowSamples, 8, 32);
                    [loop] for (int n = 0; n < 32; ++n)
                    {
                        if (n >= count) break;
                        float3 kernel = ShadowKernel[n];
                        float2 offset = (axes.xy * kernel.x + axes.zw * kernel.y) * softness;
                        total += DilatedAlpha(center + offset, spread, bounds, axes, uvDx, uvDy, false) * kernel.z;
                        weight += kernel.z;
                    }
                    alpha = (half)(total / max(weight, 0.0001));
                }
                return alpha;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                // Derivatives must be evaluated before branches or loops.
                float2 uvDx = ddx(input.uv);
                float2 uvDy = ddy(input.uv);
                float2 outlineDx = ddx(input.outlineUV);
                float2 outlineDy = ddy(input.outlineUV);
                half4 sprite = SampleSprite(input.uv, input.uvRect, uvDx, uvDy);
                half4 color = half4(sprite.rgb * input.color.rgb * sprite.a, sprite.a);
                [branch] if (_RecolorAmount > 0 || _Brightness != 0)
                {
                    // RGB replacement also works with white/black source pixels
                    // or a black Tint. The original alpha stays intact.
                    half3 bodyRGB = lerp(sprite.rgb * input.color.rgb,
                        _RecolorColor.rgb, saturate(_RecolorAmount));
                    [branch] if (_Brightness != 0)
                        bodyRGB = saturate(bodyRGB + _Brightness);
                    color.rgb = bodyRGB * sprite.a;
                }

                // Decorations are behind the image. Opaque interiors skip all taps.
                [branch] if (sprite.a < 0.9999)
                {
                    half shadow = 0;
                    [branch] if (_ShadowEnabled > 0.5 && _ShadowColor.a > 0)
                        shadow = ShadowAlpha(input.uv, input.uvRect, input.uvAxes, uvDx, uvDy) * _ShadowColor.a;
                    half outline = 0;
                    half3 outlineRGB = _OutlineColor.rgb;
                    [branch] if (_OutlineEnabled > 0.5 && _OutlineWidth > 0 && _OutlineColor.a > 0)
                    {
                        outline = DilatedAlpha(input.uv, _OutlineWidth, input.uvRect, input.uvAxes, uvDx, uvDy, true) * _OutlineColor.a;
                        [branch] if (_UseOutlineTexture > 0.5)
                        {
                            half4 pattern = tex2Dgrad(_OutlineTex, input.outlineUV, outlineDx, outlineDy);
                            outlineRGB *= pattern.rgb;
                            outline *= pattern.a;
                        }
                    }

                    half4 under = half4(outlineRGB * outline, outline);
                    under += half4(_ShadowColor.rgb * shadow, shadow) * (1 - outline);
                    color += under * (1 - sprite.a);
                }

                color *= input.color.a;
                half clipFactor = 1;
                #ifdef UNITY_UI_CLIP_RECT
                float2 clipMask = saturate((_ClipRect.zw - _ClipRect.xy - abs(input.mask.xy)) * input.mask.zw);
                clipFactor = clipMask.x * clipMask.y;
                color *= clipFactor;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                // A Mask writes only the source image, not its shadow, to stencil.
                half coverage = _StencilOp > 0.5 ? sprite.a * input.color.a * clipFactor : color.a;
                clip(coverage - 0.001);
                #endif
                return color;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
