Shader "Custom/wow.unity/TerrainShader"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MaskTex ("Mask (RGBA)", 2D) = "black" {}
        _Layer0Tex ("Layer 0 (RGBA)", 2D) = "white" {}
        _Layer1Tex ("Layer 1 (RGBA)", 2D) = "white" {}
        _Layer2Tex ("Layer 2 (RGBA)", 2D) = "white" {}
        _Layer3Tex ("Layer 3 (RGBA)", 2D) = "white" {}
        _Layer4Tex ("Layer 4 (RGBA)", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        sampler2D _MaskTex;
        sampler2D _Layer0Tex;
        sampler2D _Layer1Tex;
        sampler2D _Layer2Tex;
        sampler2D _Layer3Tex;
        sampler2D _Layer4Tex;

        struct Input
        {
            float2 uv_MaskTex;
            float2 uv_Layer0Tex;
        };

        fixed4 _Color;

        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 blend = tex2D (_MaskTex, IN.uv_MaskTex);
            float2 layerUv = IN.uv_Layer0Tex * 8;
            fixed4 c0 = tex2D (_Layer0Tex, layerUv);
            fixed4 c1 = tex2D (_Layer1Tex, layerUv);
            fixed4 c2 = tex2D (_Layer2Tex, layerUv);
            fixed4 c3 = tex2D (_Layer3Tex, layerUv);
            fixed4 c4 = tex2D (_Layer4Tex, layerUv);

            fixed4 c = lerp(c0, c1, blend.r);
            c = lerp(c, c2, blend.g);
            c = lerp(c, c3, blend.b);
            c = lerp(c4, c, blend.a);
            o.Albedo = c.rgb * _Color;
            o.Metallic = 0;

            fixed s = lerp(c0.a, c1.a, blend.r);
            s = lerp(s, c2.a, blend.g);
            s = lerp(s, c3.a, blend.b);
            s = lerp(c4.a, s, blend.a);
            o.Smoothness = s;

            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
