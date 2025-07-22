Shader "Custom/Triplanar-MetalEff" {
Properties {
        _MainTint("MainTint",Color) = (1,1,1,1)
        _TopTex("TopTexture", 2D) = "white" {}
        _SideTex("SideTexture", 2D) = "white" {}
        _TopNormal("Top Normal Map", 2D) = "bump" {}
        _SideNormal("Side Normal Map", 2D) = "bump" {}
        _Metallic("Metallic", Range(0,1)) = 0.81  // 高金属度
        _Smoothness("Smoothness", Range(0,1)) = 0.76  // 高光滑度
        _BlendOffset("BlendOffset",Range(0,0.5)) = 0.25
        _BlendExponent ("Blend Exponent", Range(1, 8)) = 1
    
        //从脚本里面实时传参    
        _BrushPos("Brush Position", Vector) = (0,0,0,0)
        _BrushRadius("Brush Radius", Float) = 1.0
        _BrushColor("Brush Color", Color) = (1,0.34,0,1)
        _FadeTime("FadeTime",Float) = 2.0
    
        _MetalColor("Metal Base Color", Color) = (0.97, 0.97, 0.98, 1) // 银色基础色
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
                fixed4 color : COLOR; // 添加顶点色输入
            };
            
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 tangentWS : TEXCOORD1;
                float3 bitangentWS : TEXCOORD2;
                float4 worldPos : TEXCOORD3;
                fixed4 color : TEXCOORD4; // 添加顶点色插值寄存器
                LIGHTING_COORDS(5,6)
            };

            fixed4 _MainTint;
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
            fixed4 _MetalColor; // 金属基础色

           //笔刷控制
            fixed4 _BrushPos;
            fixed _BrushRadius;
            fixed4 _BrushColor;
            float _FadeTime;

            float _Timer;

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

            // 菲涅尔反射计算
            half3 F_Schlick(half3 F0, half dotVH) {
                return F0 + (1 - F0) * pow(1 - dotVH, 5);
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
                
                o.color = v.color; // 传递顶点色到片元着色器
                
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
                
                // 金属工作流参数
                half3 baseColor = _MetalColor.rgb * _MainTint.rgb;
                half metallic = _Metallic;
                half smoothness = _Smoothness;
                
                // 计算基础反射率
                half3 dielectricSpec = half3(0.04, 0.04, 0.04); // 非金属默认反射率
                half3 metalSpec = baseColor; // 金属反射率等于颜色
                half3 F0 = lerp(dielectricSpec, metalSpec, metallic); // 插值计算最终基础反射率
                
                // 计算漫反射和镜面反射比例
                half kD = (1 - metallic); // 金属没有漫反射
                
                // 计算半程向量
                fixed3 halfDir = normalize(lightDir + viewDir);
                
                // 计算菲涅尔反射
                half dotVH = saturate(dot(viewDir, halfDir));
                half3 F = F_Schlick(F0, dotVH);
                
                // 计算漫反射 (仅对非金属材质)
                fixed ndotl = saturate(dot(normalWS, lightDir));
                fixed3 diffuse = kD * baseColor * _LightColor0.rgb * ndotl * atten;
                
                // 计算基于物理的高光反射
                half roughness = 1 - smoothness;
                half specularPower = 2.0 / (roughness * roughness + 0.001) - 2.0; // 转换为Blinn-Phong指数
                fixed3 specular = _LightColor0.rgb * pow(saturate(dot(normalWS, halfDir)), specularPower) * F * atten;
                
                // 添加环境光反射
                fixed3 reflectDir = reflect(-viewDir, normalWS);
                fixed3 envSpecular = UNITY_SAMPLE_TEXCUBE(unity_SpecCube0, reflectDir).rgb * F * smoothness;
                
                // 添加环境光
                fixed3 ambient = UNITY_LIGHTMODEL_AMBIENT.rgb * baseColor * kD;
                
                // 最终颜色
                fixed3 finalColor = diffuse + specular + ambient + envSpecular;

                //笔刷设置
                float dist = distance(i.worldPos, _BrushPos);
                float brushEffect = smoothstep(_BrushRadius, _BrushRadius * 0.5, dist);

                float elapsed = _Time.y - _Timer;
                float fade = saturate(1.0 - elapsed/_FadeTime);
                finalColor.rgb = lerp(finalColor.rgb, _BrushColor.rgb, brushEffect * fade);

                return fixed4(finalColor, 1.0);
            }
            ENDCG
        }
    }
}    