Shader "Hidden/CameraFlip"
{
    Properties
    {
        _FlipX ("Flip X", Float) = 0
        _FlipY ("Flip Y", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "CameraFlip"
            ZTest Always ZWrite Off Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _FlipX;
            float _FlipY;

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                uv.x = lerp(uv.x, 1.0 - uv.x, _FlipX);
                uv.y = lerp(uv.y, 1.0 - uv.y, _FlipY);
                return SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv);
            }
            ENDHLSL
        }
    }
}
