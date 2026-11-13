Shader "Artillery/ArenaSurface"
{
    Properties
    {
        _TerrainDetail ("Terrain Detail", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            Cull Off
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            half _TerrainDetail;

            struct Attributes
            {
                float4 position : POSITION;
                fixed4 color : COLOR;
            };
            struct Varyings
            {
                float4 position : SV_POSITION;
                fixed4 color : COLOR;
                float2 world : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.position);
                output.color = input.color;
                output.world = mul(unity_ObjectToWorld, input.position).xy;
                return output;
            }

            fixed4 frag(Varyings input) : SV_Target
            {
                if (_TerrainDetail <= 0)
                    return input.color;
                float2 cell = floor(input.world * float2(15, 17));
                half grain = frac(sin(dot(cell, float2(127.1, 311.7))) * 43758.5453);
                half strata = abs(frac(input.world.y * 1.7 + input.world.x * 0.037) - 0.5);
                half detail = (grain - 0.5) * 0.12 - smoothstep(0.43, 0.50, strata) * 0.08;
                return fixed4(input.color.rgb * (1 + detail * _TerrainDetail), 1);
            }
            ENDCG
        }
    }
}
