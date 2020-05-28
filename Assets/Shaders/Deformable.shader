Shader "Lily/Deformable"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _CageVertCount ("Cage Vertices Count", Int) = 12
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        Cull Off // better idea would be to make two shaders, one with each cull mode and batch renders in two sets of instances depending on whether their faces must be flipped

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows addshadow vertex:vert
        #pragma multi_compile_instancing
        #include "UnityCG.cginc"

        #pragma target 5.0

        sampler2D _MainTex;

#ifdef SHADER_API_D3D11
        StructuredBuffer<float> _Weights;
        StructuredBuffer<float3> _ControlPoints;
        StructuredBuffer<int> _Flags;
#endif
        struct appdata {
            float4 vertex : POSITION;
            float3 normal : NORMAL;
            float4 texcoord : TEXCOORD0;
            float4 texcoord1 : TEXCOORD1;
            float4 texcoord2 : TEXCOORD2;

            uint id : SV_VertexID;
            uint inst : SV_InstanceID;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Input
        {
            float2 uv_MainTex;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;
        uint _CageVertCount;

        void vert(inout appdata v) {
#ifdef SHADER_API_D3D11
#ifdef UNITY_INSTANCING_ENABLED 
            UNITY_SETUP_INSTANCE_ID(v);
#else // UNITY_INSTANCING_ENABLED 
            uint unity_InstanceID = 0;
#endif // UNITY_INSTANCING_ENABLED 
            v.vertex.xyz = 0;
            for (uint i = 0; i < _CageVertCount; ++i)
            {
                v.vertex.xyz += _Weights[v.id * _CageVertCount + i] * _ControlPoints[i + 12 * unity_InstanceID];
            }
            //if (_Flags[unity_InstanceID] & 1 != 0)
            {
                v.normal = -v.normal;
            }
#endif
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Albedo comes from a texture tinted by color
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            // Metallic and smoothness come from slider variables
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
