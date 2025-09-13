Shader "Custom/Triplanar-WeldingEff-Black" {
Properties {
    _MainTint("MainTint",Color) = (1,1,1,1)
    _TopTex("TopTexture", 2D) = "white" {}
    _SideTex("SideTexture", 2D) = "white" {}
    _TopNormal("Top Normal Map", 2D) = "bump" {}
    _SideNormal("Side Normal Map", 2D) = "bump" {}
    _DistortionMap("Distortion Map", 2D) = "bump" {}
    _DistortionStrength("Distortion Strength", Range(0, 1)) = 0.5
    _DistortionSpeed("Distortion Speed", Range(0, 5)) = 1.0
    _MaskSizeMultiplier("Mask Size Multiplier", Range(1, 3)) = 1.5
    _Metallic("Metallic", Range(0,1)) = 0.95  // 高金属度
    _Smoothness("Smoothness", Range(0,1)) = 0.85  // 高光滑度
    _BlendOffset("BlendOffset",Range(0,0.5)) = 0.25
    _BlendExponent ("Blend Exponent", Range(1, 8)) = 1

    // 笔刷参数
    _BrushPos("Brush Position", Vector) = (0,0,0,0)
    _BrushRadius("Brush Radius", Float) = 1.0
    _BrushColor("Brush Color", Color) = (1,0.34,0,1)
    _FadeTime("FadeTime",Float) = 2.0

    _MetalColor("Metal Base Color", Color) = (0.05, 0.05, 0.08, 1)  // 亮黑色基础
    _SpecularBoost("Specular Boost", Range(1,5)) = 2.5  // 高光亮度增强
    _EnvSpecularBoost("Env Specular Boost", Range(1,3)) = 1.8  // 环境高光增强
    
    // 新增：高光范围控制参数
    _SpecularSize("Specular Size", Range(0.1, 5)) = 1.0  // 高光范围控制
}

SubShader {
    Tags { "RenderType"="Opaque" "Queue"="Geometry" }
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
        fixed4 color : COLOR;
    };
    
    struct v2f
    {
        float4 vertex : SV_POSITION;
        float3 normalWS : TEXCOORD0;
        float3 tangentWS : TEXCOORD1;
        float3 bitangentWS : TEXCOORD2;
        float4 worldPos : TEXCOORD3;
        fixed4 color : TEXCOORD4;
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
    sampler2D _DistortionMap;
    float _DistortionStrength;
    float _DistortionSpeed;
    float _MaskSizeMultiplier;
    float4 _TopTex_ST, _SideTex_ST;
    float4 _TopNormal_ST, _SideNormal_ST;
    float4 _DistortionMap_ST;
    fixed4 _MetalColor;

    // 笔刷控制
    fixed4 _BrushPos;
    fixed _BrushRadius;
    fixed4 _BrushColor;
    float _FadeTime;

    float _Timer;
    float _SpecularBoost;  // 高光亮度
    float _EnvSpecularBoost;  // 环境高光亮度
    
    // 新增：高光范围控制变量
    float _SpecularSize;  // 高光范围控制

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
        return F0 * 1.2 + (1 - F0) * pow(1 - dotVH, 4);
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
        o.color = v.color;
        TRANSFER_VERTEX_TO_FRAGMENT(o);
        return o;
    }
    
    fixed4 frag(v2f i) : SV_Target
    {
        TriUV triuv = GetTriUV(i.worldPos);
        
        // 笔刷效果
        float dist = distance(i.worldPos, _BrushPos);
        float maskRadius = _BrushRadius * _MaskSizeMultiplier;
        float brushMask = smoothstep(maskRadius, maskRadius * 0.8, dist);
        
        // 纹理扰动
        float elapsed = _Time.y - _Timer;
        float fade = saturate(1.0 - elapsed/_FadeTime);
        float timeFactor = _Time.y * _DistortionSpeed * fade;
        float2 distortionUV = i.worldPos.xz * _DistortionMap_ST.xy + _DistortionMap_ST.zw;
        float2 distortion = tex2D(_DistortionMap, distortionUV + float2(timeFactor, 0)).xy * 2 - 1;
        distortion *= _DistortionStrength * fade * brushMask;
        
        // 应用扰动
        triuv.xUV += distortion;
        triuv.yUV += distortion;
        triuv.zUV += distortion;
        
        // 纹理采样
        fixed4 colx = tex2D(_SideTex, triuv.xUV * _SideTex_ST.xy + _SideTex_ST.zw) * 0.2;
        fixed4 coly = tex2D(_TopTex, triuv.yUV * _TopTex_ST.xy + _TopTex_ST.zw) * 0.2;
        fixed4 colz = tex2D(_SideTex, triuv.zUV * _SideTex_ST.xy + _SideTex_ST.zw) * 0.2;
        
        // 法线采样
        fixed4 normalx = tex2D(_SideNormal, triuv.xUV * _TopNormal_ST.xy + _TopNormal_ST.zw);
        fixed4 normaly = tex2D(_TopNormal, triuv.yUV * _SideNormal_ST.xy + _SideNormal_ST.zw);
        fixed4 normalz = tex2D(_SideNormal, triuv.zUV * _SideNormal_ST.xy + _SideNormal_ST.zw);
        
        // 混合权重
        half3 weights = GetTriWeights(i.normalWS);
        
        // 基础颜色
        fixed4 albedo = (colx * weights.x + coly * weights.y + colz * weights.z) * 0.5;
        fixed3 baseColor = _MetalColor.rgb * _MainTint.rgb + albedo.rgb;
        
        // 法线混合
        fixed3 normalTS_x = UnpackNormal(normalx);
        fixed3 normalTS_y = UnpackNormal(normaly);
        fixed3 normalTS_z = UnpackNormal(normalz);
        float3x3 TBN = float3x3(i.tangentWS, i.bitangentWS, i.normalWS);
        fixed3 normalWS_x = mul(normalTS_x, TBN);
        fixed3 normalWS_y = mul(normalTS_y, TBN);
        fixed3 normalWS_z = mul(normalTS_z, TBN);
        fixed3 normalWS = normalize(normalWS_x * weights.x + normalWS_y * weights.y + normalWS_z * weights.z);
        
        // 光照计算
        half metallic = _Metallic;
        half smoothness = _Smoothness;
        half3 dielectricSpec = half3(0.08, 0.08, 0.08);
        half3 metalSpec = baseColor * 1.1;
        half3 F0 = lerp(dielectricSpec, metalSpec, metallic);
        
        fixed3 viewDir = normalize(UnityWorldSpaceViewDir(i.worldPos.xyz));
        fixed3 lightDir = _WorldSpaceLightPos0.xyz;
        fixed atten = LIGHT_ATTENUATION(i);
        fixed3 halfDir = normalize(lightDir + viewDir);
        
        // 菲涅尔反射
        half dotVH = saturate(dot(viewDir, halfDir));
        half3 F = F_Schlick(F0, dotVH);
        
        // 漫反射
        half kD = (1 - metallic) * 0.3;
        fixed ndotl = saturate(dot(normalWS, lightDir));
        fixed3 diffuse = kD * baseColor * _LightColor0.rgb * ndotl * atten;
        
        // 高光反射 - 关键修改点
        half roughness = 1 - smoothness;
        // 使用_SpecularSize参数控制高光范围：值越大，高光越分散；值越小，高光越集中
        half specularPower = 2.0 / (roughness * roughness * _SpecularSize + 0.001) - 2.0;
        fixed3 specular = _LightColor0.rgb * pow(saturate(dot(normalWS, halfDir)), specularPower) * F * atten * _SpecularBoost;
        
        // 环境高光
        fixed3 reflectDir = reflect(-viewDir, normalWS);
        fixed3 envSpecular = UNITY_SAMPLE_TEXCUBE(unity_SpecCube0, reflectDir).rgb * F * smoothness * _EnvSpecularBoost;
        
        // 环境光
        fixed3 ambient = UNITY_LIGHTMODEL_AMBIENT.rgb * baseColor * kD * 0.7;
        
        // 最终颜色
        fixed3 finalColor = diffuse + specular + ambient;// + envSpecular;

        // 笔刷效果叠加
        float brushEffect = smoothstep(_BrushRadius, _BrushRadius * 0.5, dist);
        finalColor.rgb = lerp(finalColor.rgb, _BrushColor.rgb, brushEffect * fade);

        return fixed4(finalColor, 1.0);
    }
    ENDCG
    }
}
FallBack "Diffuse"
}