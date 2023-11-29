Shader "Custom/Deletion"
{
    Properties
    {
        _Color("Color",Color)=(0.65882,0.23921,0.23921,0.58823)
    }
    SubShader
    {
        Tags {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+4000" 
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZTest Always
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 viewDir: TEXCOORD1;
            };

            float4 _Color;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.viewDir= normalize(WorldSpaceViewDir(v.vertex));
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float rimLight=1-pow(dot(i.viewDir,normalize(i.normal)),2);
                return float4(_Color.xyz*rimLight,_Color.w);
            }
            ENDCG
        }
    }
}
