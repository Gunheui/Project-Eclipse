Shader "Eclipse/SpriteOutlineURP2D"
{
    // URP 2D Renderer(Renderer2D)에서 SpriteRenderer에 붙이는 unlit 스프라이트 셰이더.
    // 알파 팽창(alpha dilation)으로 실루엣 바깥에 아웃라인을 그린다.
    // 아웃라인 토글·색·두께는 MaterialPropertyBlock으로 SpriteRenderer마다 오버라이드한다.
    Properties
    {
        // [PerRendererData]: 2D Renderer가 스프라이트/아틀라스 텍스처를 렌더러별로 이 슬롯에 바인딩한다.
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        // --- 아웃라인 (MaterialPropertyBlock으로 렌더러별 오버라이드) ---
        _OutlineEnabled ("Outline Enabled", Float) = 0
        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        // 단위는 소스 텍스처 텍셀. 화면상 굵기는 스프라이트 PPU에 반비례하므로 호출부가 PPU로 환산해 넣는다
        // (PPU 315 아트에서 3px 수준이면 약 9~10텍셀).
        _OutlineThickness ("Outline Thickness (texels)", Range(0,32)) = 1

        // 기본 스프라이트 머티리얼 호환용 레거시 프로퍼티(숨김).
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "Queue"             = "Transparent"
            "RenderType"        = "Transparent"
            "RenderPipeline"    = "UniversalPipeline"   // URP SubShader로 인식시키는 필수 태그
            "IgnoreProjector"   = "True"
            "PreviewType"       = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        // 스프라이트 기본값: straight-alpha 블렌딩, 양면, 깊이 기록 없음.
        Blend SrcAlpha OneMinusSrcAlpha
        Cull  Off
        ZWrite Off

        Pass
        {
            // 2D Renderer 경로에서 그려지게 하는 결정적 태그. 없으면 SRPDefaultUnlit로 처리돼 2D에서 안 그려진다.
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // 텍스처/샘플러는 CBUFFER 밖(SRP Batcher 규칙).
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // 엔진이 바인딩된 텍스처마다 자동으로 채우는 (1/w, 1/h, w, h). CBUFFER 밖에 둔다.
            // 2D SRP-Batcher는 이 자동 세팅을 지원하지 않아 Dynamic Batching으로 폴백된다(의도된 트레이드오프).
            float4 _MainTex_TexelSize;

            // SRP Batcher 호환: 모든 머티리얼 스칼라/벡터는 UnityPerMaterial 하나에.
            CBUFFER_START(UnityPerMaterial)
                half4  _Color;
                half4  _OutlineColor;
                float  _OutlineThickness;
                float  _OutlineEnabled;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color      : COLOR;   // SpriteRenderer.color가 정점 색으로 들어온다(디밍도 여기로).
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            // 22.5° 간격 16방향 단위 벡터. 방향이 성기면 두꺼운 아웃라인에서 별 모양 갭이 생긴다.
            static const float2 kDirs[16] =
            {
                float2( 1.00000000,  0.00000000), float2( 0.92387953,  0.38268343),
                float2( 0.70710678,  0.70710678), float2( 0.38268343,  0.92387953),
                float2( 0.00000000,  1.00000000), float2(-0.38268343,  0.92387953),
                float2(-0.70710678,  0.70710678), float2(-0.92387953,  0.38268343),
                float2(-1.00000000,  0.00000000), float2(-0.92387953, -0.38268343),
                float2(-0.70710678, -0.70710678), float2(-0.38268343, -0.92387953),
                float2( 0.00000000, -1.00000000), float2( 0.38268343, -0.92387953),
                float2( 0.70710678, -0.70710678), float2( 0.92387953, -0.38268343)
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS);
                OUT.uv    = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // 원본 스프라이트 색 = 텍스처 * 정점색 * 틴트.
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * IN.color * _Color;

                // 아웃라인 off면 원본 그대로.
                if (_OutlineEnabled < 0.5)
                    return c;

                // 이웃 샘플 오프셋 = 텍셀 크기 * 두께(텍셀).
                float2 off = _MainTex_TexelSize.xy * _OutlineThickness;

                // 실루엣을 바깥으로 팽창시킬 양 = 이웃 중 최대 알파.
                // 16방향 × 2반경(전체·절반)으로 훑는다. 한 반경만 보면 얇은 부위에서 링이 끊긴다.
                half maxA = 0;
                [unroll]
                for (int k = 0; k < 16; k++)
                {
                    float2 dir = kDirs[k] * off;
                    maxA = max(maxA, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + dir).a);
                    maxA = max(maxA, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + dir * 0.5).a);
                }

                // 불투명부(c.a=1)는 원본색, 투명부(c.a=0)는 아웃라인색.
                half3 rgb = lerp(_OutlineColor.rgb, c.rgb, c.a);
                // 알파 확장: 원본 알파와 (이웃최대알파 * 아웃라인알파) 중 큰 값.
                half  a   = max(c.a, maxA * _OutlineColor.a);

                return half4(rgb, a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
