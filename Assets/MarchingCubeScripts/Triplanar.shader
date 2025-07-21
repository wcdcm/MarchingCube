Shader "Custom/Triplanar" {
Properties {
        _TopTex("TopTexture", 2D) = "white" {}
        _SideTex("SideTexture", 2D) = "white" {}
        _BlendOffset("BlendOffset",Range(0,0.5)) = 0.25
        _BlendExponent ("Blend Exponent", Range(1, 8)) = 2
}
SubShader {
        Tags { "RenderType"="Opaque" }
        Cull Off
Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityStandardBRDF.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal:NORMAL;
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 normal:TEXCOORD1;
                float4 worldpos : TEXCOORD2;
            };

            fixed _BlendOffset;
            half _BlendExponent;
            sampler2D _TopTex;
            sampler2D _SideTex;
            float4 _TopTex_ST,_SideTex_ST;

            struct TriUV{
                float2 xUV,yUV,zUV;
            };
            TriUV GetTriUV (float4 worldpos) {
                TriUV triUV;
                triUV.xUV = worldpos.zy;
                triUV.yUV = worldpos.xz;
                triUV.zUV = worldpos.xy;
                return triUV;
            }
            fixed3 GetTriWeights(fixed3 normal){
                fixed3 weights = abs(normal);   //因为normal可能有负数
                weights = saturate(weights-_BlendOffset);   //控制权重
                weights = pow(weights,_BlendExponent);  //进一步控制权重
                return weights/(weights.x+ weights.y+ weights.z);   //使xyz相加=1
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.worldpos = mul(unity_ObjectToWorld,v.vertex);
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                //triplanar
                TriUV triuv = GetTriUV(i.worldpos);
                fixed4 colx = tex2D(_SideTex,triuv.xUV * _SideTex_ST.xy + _SideTex_ST.zw);
                fixed4 coly = tex2D(_TopTex,triuv.yUV * _TopTex_ST.xy + _TopTex_ST.zw);
                fixed4 colz = tex2D(_SideTex,triuv.zUV * _SideTex_ST.xy + _SideTex_ST.zw);
                fixed3 weights = GetTriWeights(i.normal);
                fixed4 tricol = colx*weights.x + coly*weights.y +colz*weights.z;
                return tricol;
            }
            ENDCG
        }
    }
}