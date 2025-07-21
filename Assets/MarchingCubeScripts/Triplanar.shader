Shader "Custom/Triplanar" {
Properties {
        _TopTex("TopTexture", 2D) = "white" {}
        _SideTex("SideTexture", 2D) = "white" {}
        _TopNormal("Top Normal Map", 2D) = "bump" {}
        _SideNormal("Side Normal Map", 2D) = "bump" {}
        _Metallic("Metallic", Range(0,1)) = 0
        _Smoothness("Smoothness", Range(0,1)) = 0.5
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
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
            };
            
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 tangentWS : TEXCOORD1;
                float3 bitangentWS : TEXCOORD2;
                float4 worldPos : TEXCOORD3;
                LIGHTING_COORDS(4,5)
            };

            fixed _BlendOffset;
            half _BlendExponent;
            half _Metallic;
            half _Smoothness;
            sampler2D _TopTex;
            sampler2D _SideTex;
            sampler2D _TopNormal;
            sampler2D _SideNormal;
            float4 _TopTex_ST, _SideTex_ST;
            float4 _TopNormal_ST, _SideNormal_ST;

            struct TriUV{
                float2 xUV, yUV, zUV;
            };
            
            TriUV GetTriUV(float4 worldPos) {
                TriUV triUV;
                triUV.xUV = worldPos.zy;
                triUV.yUV = worldPos.xz;
                triUV.zUV = worldPos.xy;
                return triUV;
            }
            
            half3 GetTriWeights(half3 normal) {
                half3 weights = abs(normal);
                weights = saturate(weights - _BlendOffset);
                weights = pow(weights, _BlendExponent);
                return weights / (weights.x + weights.y + weights.z);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                
                o.normalWS = UnityObjectToWorldNormal(v.normal);
                
                half4 tangentWS = half4(UnityObjectToWorldDir(v.tangent.xyz), v.tangent.w);
                o.tangentWS = tangentWS.xyz;
                
                o.bitangentWS = cross(o.normalWS, tangentWS.xyz) * tangentWS.w;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                
                TRANSFER_VERTEX_TO_FRAGMENT(o);
                
                return o;
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
                TriUV triuv = GetTriUV(i.worldPos);
                
                // 颜色采样
                fixed4 colx = tex2D(_SideTex, triuv.xUV * _SideTex_ST.xy + _SideTex_ST.zw);
                fixed4 coly = tex2D(_TopTex, triuv.yUV * _TopTex_ST.xy + _TopTex_ST.zw);
                fixed4 colz = tex2D(_SideTex, triuv.zUV * _SideTex_ST.xy + _SideTex_ST.zw);
                
                // 法线采样
                fixed4 normalx = tex2D(_SideNormal, triuv.xUV * _TopNormal_ST.xy + _TopNormal_ST.zw);
                fixed4 normaly = tex2D(_TopNormal, triuv.yUV * _SideNormal_ST.xy + _SideNormal_ST.zw);
                fixed4 normalz = tex2D(_SideNormal, triuv.zUV * _SideNormal_ST.xy + _SideNormal_ST.zw);
                
                // 计算混合权重
                half3 weights = GetTriWeights(i.normalWS);
                
                // 混合颜色
                fixed4 albedo = colx * weights.x + coly * weights.y + colz * weights.z;
                
                // 解包法线
                fixed3 normalTS_x = UnpackNormal(normalx);
                fixed3 normalTS_y = UnpackNormal(normaly);
                fixed3 normalTS_z = UnpackNormal(normalz);
                
                // 构建TBN矩阵
                float3x3 TBN = float3x3(
                    i.tangentWS,
                    i.bitangentWS,
                    i.normalWS
                );
                
                // 转换法线到世界空间
                fixed3 normalWS_x = mul(normalTS_x, TBN);
                fixed3 normalWS_y = mul(normalTS_y, TBN);
                fixed3 normalWS_z = mul(normalTS_z, TBN);
                
                // 混合法线
                fixed3 normalWS = normalize(normalWS_x * weights.x + normalWS_y * weights.y + normalWS_z * weights.z);
                
                // 光照计算
                fixed3 viewDir = normalize(UnityWorldSpaceViewDir(i.worldPos.xyz));
                fixed3 lightDir = _WorldSpaceLightPos0.xyz;
                fixed atten = LIGHT_ATTENUATION(i);
                
                // 计算漫反射
                fixed ndotl = saturate(dot(normalWS, lightDir));
                fixed3 diffuse = _LightColor0.rgb * albedo.rgb * ndotl * atten;
                
                // 计算高光
                fixed3 halfDir = normalize(lightDir + viewDir);
                fixed ndoth = saturate(dot(normalWS, halfDir));
                fixed specularPower = lerp(10.0, 250.0, _Smoothness);
                fixed3 specular = _LightColor0.rgb * pow(ndoth, specularPower) * _Metallic * atten;
                
                // 添加环境光
                fixed3 ambient = UNITY_LIGHTMODEL_AMBIENT.rgb * albedo.rgb;
                
                // 最终颜色
                fixed3 finalColor = diffuse + specular + ambient;
                
                return fixed4(finalColor, 1.0);
            }
            ENDCG
        }
    }
}