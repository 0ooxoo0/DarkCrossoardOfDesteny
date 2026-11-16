Shader "Inventory/Outline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1, 0.85, 0.35, 1)
        _OutlineWidth ("Outline Width", Float) = 0.05
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Outline"

            // Вывернутые нормали: видна только обратная сторона расширенной копии,
            // поэтому вокруг объекта появляется ровная кайма без пост-обработки.
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 expanded = v.positionOS.xyz + normalize(v.normalOS) * _OutlineWidth;
                o.positionCS = TransformObjectToHClip(expanded);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
