Shader "Hidden/CorridorKey/AbSplitPreview"
{
    Properties
    {
        // Side A must be _MainTex: Graphics.Blit(source, dest, mat) binds `source` as _MainTex only.
        _MainTex ("Texture A (Blit source)", 2D) = "white" {}
        _TexB ("Texture B", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _TexB;
            // Normalized midpoint (0..1, origin top-left) — same convention as AbScrubberOverlayController.
            float4 _SplitCenter;
            // Unit normal in pixel-isotropic space (same as AbComparisonPreviewMath / CPU composite).
            float4 _SplitNormal;
            // xy = RenderTexture width/height; split math must run in this space so angles match the UI line on non-square viewports.
            float4 _SplitViewportPx;
            float _InputIsLinear;

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv;
                float2 uiUv = float2(uv.x, 1.0 - uv.y);
                float2 pixelPos = uiUv * _SplitViewportPx.xy;
                float2 centerPx = _SplitCenter.xy * _SplitViewportPx.xy;
                float signedDistance = dot(pixelPos - centerPx, normalize(_SplitNormal.xy));
                fixed4 colorA = tex2D(_MainTex, uv);
                if (_InputIsLinear > 0.5)
                    colorA.rgb = LinearToGammaSpace(colorA.rgb);
                fixed4 colorB = tex2D(_TexB, uv);
                return signedDistance <= 0.0 ? colorA : colorB;
            }
            ENDCG
        }
    }
}
