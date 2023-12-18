
Shader "Custom/Preview"
{
	Properties
	{
		_Color1 ("Color1", Color) = (0.64313,0.64313,0.64313,0.78431)
		_Color2 ("Color2", Color) = (0.37647,0.37647,0.37647,0.78431)
        _DepthDist("depth",Range(-5,5))=0

	}
	SubShader
	{
        Tags { "RenderType"="Transparent" "Queue+4000" = "Transparent" }
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
                float3 normal: NORMAL;
			};

			struct v2f
			{
				float4 screenPos : TEXCOORD0;
				float4 vertex : SV_POSITION;
                float3 normal: TEXCOORD1;
                float3 viewDir: TEXCOORD2;
			};

			sampler2D _CameraDepthTexture; 
			float4 _Color1;
			float4 _Color2;
            float _DepthDist;

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.screenPos = ComputeScreenPos(o.vertex);
                o.normal=UnityObjectToWorldNormal(v.normal);
                o.viewDir=normalize(WorldSpaceViewDir(v.vertex));
				return o;
			}

			fixed4 frag (v2f i) : SV_Target
			{
                float rimLight=1-(0.5*pow(dot(i.viewDir,normalize(i.normal)),2));
                float2 screenUv=i.screenPos.xy/i.screenPos.w;
                float depth=LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,screenUv));  
                float screenDepth=i.screenPos.w-depth+_DepthDist; 
                float t=smoothstep(0.01,0.1,screenDepth);
                float4 color=lerp(_Color1,_Color2,t);
				rimLight=lerp(rimLight,1,t);
                return rimLight*color;
			}
			ENDCG
		}
	}
}