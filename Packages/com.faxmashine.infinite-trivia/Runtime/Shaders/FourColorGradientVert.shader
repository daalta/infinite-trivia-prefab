Shader "Custom/FourColorGradientVert"
{
    Properties
    {
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _SpeedR ("Speed R", Range(0.1, 10.0)) = 1.0
        _SpeedG ("Speed G", Range(0.1, 10.0)) = 1.0
        _SpeedB ("Speed B", Range(0.1, 10.0)) = 1.0
        _Multiplier("Multiplier", Range(0.1, 10.0)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard vertex:vert exclude_path:prepass exclude_path:deferred noforwardadd noshadow nodynlightmap nolppv noshadowmask

        #pragma target 3.0

        sampler2D _MainTex;
        fixed _SpeedR;
        fixed _SpeedG;
        fixed _SpeedB;
        fixed _Multiplier;
        
        struct Input
        {
            fixed2 uv_MainTex;
            fixed3 color;
        };
        
        fixed3 GetCornerColor(float time, float2 uv)
        {
            fixed3 colors;
            colors.r = sin(time * _SpeedR + uv.x + uv.y) * 0.5;
            colors.g = sin(time * _SpeedG + uv.x - uv.y) * 0.5;
            colors.b = sin(time * _SpeedB - uv.x + uv.y);
            return colors * 0.5 + 0.5; // Map range from [-1, 1] to [0, 1]
        }

		void vert (inout appdata_full v, out Input o)
		{
            UNITY_INITIALIZE_OUTPUT(Input, o);
            fixed2 uv = v.texcoord;
            fixed3 gradient = GetCornerColor(_Time.y, uv) * lerp(1, 0.1, uv.y);
		    o.color = gradient * _Multiplier;
		}

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            o.Albedo = tex2D(_MainTex, IN.uv_MainTex) * IN.color;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
