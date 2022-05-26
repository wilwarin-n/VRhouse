Shader "ZibraLiquids/FluidShader"
{
    SubShader
    {
        Pass
        {
            Cull Off
            ZWrite On

            CGPROGRAM
            // Physically based Standard lighting model
            #pragma multi_compile_local __ HDRP
            #pragma multi_compile_local __ CUSTOM_REFLECTION_PROBE
            #pragma multi_compile_local __ VISUALIZE_SDF
            #pragma multi_compile_local __ FLIP_BACKGROUND
            #pragma instancing_options procedural:setup
            #pragma vertex VSMain
            #pragma fragment PSMain
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityStandardBRDF.cginc"
            #include "UnityImageBasedLighting.cginc"

            struct VSIn
            {
                uint vertexID : SV_VertexID;
            };

            struct VSOut
            {
                float4 position : POSITION;
                float3 raydir : TEXCOORD1;
                float2 uv : TEXCOORD0;
            };

            struct PSOut
            {
                float4 color : COLOR;
                float depth : DEPTH;
            };

            // Fluid material parameters, see SetMaterialParams()
            float4x4 ProjectionInverse;
            float ParticleDiameter;
            float Roughness;
            float Opacity;
            float RefractionDistortion;
            float4 RefractionColor;
            float4 ReflectionColor;
            float Metalness;
            float FoamIntensity;
            float FoamAmount;
            float3 GridSize;
            float3 ContainerScale;
            float3 ContainerPosition;

            // Light and reflection params
            UNITY_DECLARE_TEXCUBE(ReflectionProbe);
            float4 ReflectionProbe_BoxMax;
            float4 ReflectionProbe_BoxMin;
            float4 ReflectionProbe_ProbePosition;
            float4 ReflectionProbe_HDR;
            float4 WorldSpaceLightPos;
            
            // Camera params
            float2 TextureScale;

            // Input resources
            sampler2D Background;
            float4 Background_TexelSize;
            sampler2D FluidColor;
            StructuredBuffer<float4> GridNormal;

            #if VISUALIZE_SDF
                sampler2D SDFRender;
            #endif

            float3 GetNodeF(float3 p)
            {
                return GridSize * (p - (ContainerPosition - ContainerScale * 0.5)) / ContainerScale;
            }

            int GetNodeID(int3 node)
            {
                node = clamp(node, int3(0, 0, 0), int3(GridSize) - int3(1, 1, 1));
                return node.x + node.y * GridSize.x +
                       node.z * GridSize.x * GridSize.y;
            }

            int GetNodeID(float3 node)
            {
                return GetNodeID(int3(node));
            }
            
            // Trilinear interpolation over grid
            float4 TrilinearInterpolation(float3 p)
            {
                float3 node = GetNodeF(p);
                float3 ni = floor(node);
                float3 nf = frac(node);

                //load the 8 node values
                float4 n000 = GridNormal[GetNodeID(ni + float3(0,0,0))];
                float4 n001 = GridNormal[GetNodeID(ni + float3(0,0,1))];
                float4 n010 = GridNormal[GetNodeID(ni + float3(0,1,0))];
                float4 n011 = GridNormal[GetNodeID(ni + float3(0,1,1))];
                float4 n100 = GridNormal[GetNodeID(ni + float3(1,0,0))];
                float4 n101 = GridNormal[GetNodeID(ni + float3(1,0,1))];
                float4 n110 = GridNormal[GetNodeID(ni + float3(1,1,0))];
                float4 n111 = GridNormal[GetNodeID(ni + float3(1,1,1))];

                //interpolate the node pairs along Z
                float4 n00 = lerp(n000, n001, nf.z);
                float4 n01 = lerp(n010, n011, nf.z);
                float4 n10 = lerp(n100, n101, nf.z);
                float4 n11 = lerp(n110, n111, nf.z);

                //interpolate the interpolated pairs along Y
                float4 n0 = lerp(n00, n01, nf.y);
                float4 n1 = lerp(n10, n11, nf.y);

                //interpolate the rest along X
                return lerp(n0, n1, nf.x);
            }

            float3 BoxProjection(float3 rayOrigin, float3 rayDir, float3 cubemapPosition, float3 boxMin, float3 boxMax)
            {
                float3 tMin = (boxMin - rayOrigin) / rayDir;
                float3 tMax = (boxMax - rayOrigin) / rayDir;
                float3 t1 = min(tMin, tMax);
                float3 t2 = max(tMin, tMax);
                float tFar = min(min(t2.x, t2.y), t2.z);
                return normalize(rayOrigin + rayDir*tFar - cubemapPosition);
            };

            float3 DepthToWorld(float2 uv, float depth)
            {
                float2 pos = uv - 0.5;
                float3 direction = mul(ProjectionInverse, float4(pos, 1.0f, 1.0f)).xyz;
                float4 worldDirection = mul(transpose(UNITY_MATRIX_V), float4(direction, 0.0f));
                depth = LinearEyeDepth(depth);
                return worldDirection * depth;
            }

            // built-in Unity sampler name - do not change
            sampler2D _CameraDepthTexture;

            float2 GetFlippedUV(float2 uv)
            {
                if (_ProjectionParams.x > 0)
                    return float2(uv.x, 1 - uv.y);
                return uv;
            }

            float2 GetFlippedUVBackground(float2 uv)
            {
                uv = GetFlippedUV(uv);
            #ifdef FLIP_BACKGROUND
                // Temporary fix for flipped reflection on iOS
                uv.y = 1 - uv.y;
            #else
                if (Background_TexelSize.y < 0)
                {
                    uv.y = 1 - uv.y;
                }
            #endif
                return uv;
            }

            float4 GetDepthAndPos(float2 uv)
            {
                float depth = tex2D(_CameraDepthTexture, uv).x;
                float3 pos = DepthToWorld(uv, depth);
                return float4(pos, depth);
            }

            float3 GetCameraRay(float2 uv)
            {
                uv = (uv - 0.5) * float2(2.0, -2.0);
                float3 direction = mul(ProjectionInverse, float4(uv, 1.0f, 1.0f)).xyz;
                return mul(transpose(UNITY_MATRIX_V), float4(direction, 0.0f)).xyz;
            }

            VSOut VSMain(VSIn input)
            {
                VSOut output;

                float2 vertexBuffer[4] = {
                    float2(0.0f, 0.0f),
                    float2(0.0f, 1.0f),
                    float2(1.0f, 0.0f),
                    float2(1.0f, 1.0f),
                };
                uint indexBuffer[6] = { 0, 1, 2, 2, 1, 3 };
                uint indexID = indexBuffer[input.vertexID];

                float2 uv = vertexBuffer[indexID];
                float2 flippedUV = GetFlippedUV(uv);

                output.position = float4(2 * flippedUV.x - 1, 1 - 2 * flippedUV.y, 0.5, 1.0);
                output.uv = uv;
                output.raydir = GetCameraRay(uv);

                return output;
            }

            float3 GetFluidSurfacePosition(float2 uv)
            {
                float4 pos = tex2D(FluidColor, uv * TextureScale);
                float3 cameraPos = _WorldSpaceCameraPos;
                float3 cameraRay = GetCameraRay(uv);
                float depth = pos.w;
                if (depth < 0.0 || depth > 1e4)
                {
                    return cameraPos + 1e-4 * cameraRay;
                }
                return cameraPos + cameraRay * depth;
               
            }

            float PositionToDepth(float3 pos)
            {
                float4 clipPos = mul(UNITY_MATRIX_VP, float4(pos, 1));
                return (1.0 / clipPos.w - _ZBufferParams.w) / _ZBufferParams.z; //inverse of linearEyeDepth
            }

            float4 RenderFluidSurface(float3 cameraPos, float3 cameraRay, float2 uv)
            {
                float4 pos = tex2D(FluidColor, uv * TextureScale);
                float depth = pos.w;
                float3 newPos = cameraPos + cameraRay * depth;

                if (depth < 0.0 || depth >= 1e4) return 0.0;
                
                float4 f0 = TrilinearInterpolation(newPos.xyz);
                float3 normal = -normalize(f0.xyz);
                //which normal to use? make it density dependent
                float nu = smoothstep(0.0, FoamAmount, f0.w);
                normal = normalize(lerp(normal, normalize(pos.xyz), 1.0 - nu));
                float rdotn = dot(normal, cameraRay);

                // lighting vectors:
                float3 worldView = -cameraRay;

#ifdef HDRP
                float3 lightDirWorld = normalize(WorldSpaceLightPos.xyz);
#else
                float3 lightDirWorld = normalize(_WorldSpaceLightPos0.xyz);
#endif

                float4 reflColor = ReflectionColor;
                half3 h = normalize(lightDirWorld + worldView);
                float nh = BlinnTerm(normal, h);
                float nl = DotClamped(normal, lightDirWorld);
                float nlsmooth = dot(normal, lightDirWorld) * 0.5 + 0.65;
                float nv = max(abs(dot(normal, worldView)), 1 - reflColor.w); //hardcoded to not be orthogonal
                float foamamount = FoamIntensity * (1.0 - nu);
                float rough = clamp(Roughness + 0.5 * foamamount, 0., 1.0);
                half V = SmithBeckmannVisibilityTerm(nl, nv, rough);
                half D = NDFBlinnPhongNormalizedTerm(nh, RoughnessToSpecPower(rough));
                float spec = (V * D) * (UNITY_PI / 4);
                spec = max(0, spec * nl);

                Unity_GlossyEnvironmentData g;
                g.roughness = rough;

#if defined(CUSTOM_REFLECTION_PROBE) || defined(HDRP)
                g.reflUVW = BoxProjection(newPos.xyz, reflect(cameraRay, normal),
                    ReflectionProbe_ProbePosition,
                    ReflectionProbe_BoxMin, ReflectionProbe_BoxMax
                );
                float3 reflection = Unity_GlossyEnvironment(UNITY_PASS_TEXCUBE(ReflectionProbe), ReflectionProbe_HDR, g) * reflColor.xyz;
#else
                g.reflUVW = reflect(cameraRay, normal);
                g.reflUVW.y = g.reflUVW.y; //don't render the bottom part of the cubemap
                g.roughness = rough;
                float3 reflection = Unity_GlossyEnvironment(UNITY_PASS_TEXCUBE(unity_SpecCube0), unity_SpecCube0_HDR, g) * reflColor.xyz;
#endif

                float fresnel = FresnelTerm(Metalness, nv) * reflColor.w;
                //refraction direction
                float3 refr = refract(cameraRay, normal, 0.8);
                //virtual camera plane, TODO: use the MVP matrix instead
                float3 d1 = normalize(cross(cameraRay, float3(0.0, 1.0, 0.0)));
                float3 d2 = normalize(cross(cameraRay, d1));
                //camera plane projection
                float2 del = RefractionDistortion * float2(dot(d1, refr), dot(d2, refr));
                float2 refrUV = (1.0 - 0.4 * RefractionDistortion) * (uv - 0.5) + 0.5 + del;
                float4 refrPos = GetDepthAndPos(refrUV);
                // GetDepthAndPos needs uv before flip, so flipping only after it
                refrUV = GetFlippedUVBackground(refrUV);
                float3 fluidRefrPos = GetFluidSurfacePosition(refrUV);
                float sceneDepth = refrPos.w;
                float refractedFluidDepth = PositionToDepth(fluidRefrPos);
                float3 refractionColor = 0;

                if (refractedFluidDepth > sceneDepth) //background sample is occluded by the fluid surface
                {
                    refractionColor = tex2D(Background, refrUV).xyz;
                }
                else
                {
                    float2 flippedUV = GetFlippedUVBackground(uv);
                    refractionColor = tex2D(Background, flippedUV).xyz;
                }

                float smoothdepth = PositionToDepth(newPos);
                float fluid_thickness = clamp(abs(0.01 * (LinearEyeDepth(smoothdepth) - LinearEyeDepth(sceneDepth)) / ParticleDiameter), 0., 2.0);
                float3 opacity = 1.0 - exp(-fluid_thickness * Opacity); //Beer–Lambert law

                float opac = clamp(opacity + foamamount, 0., 1.0);
                float3 foam = lerp(lerp(1., nlsmooth, opac) * RefractionColor.xyz, nlsmooth * 1.0,
                                   foamamount);
                float3 rcol = lerp(refractionColor, foam, opac);
                float3 base = lerp(rcol, reflection, fresnel);

            #ifdef HDRP
                return float4(clamp(base + spec, 0., 10000.0), smoothdepth);
            #else
                return float4(clamp(base + spec, 0., 1.0), smoothdepth);
            #endif
            }

        #if VISUALIZE_SDF
            float4 RenderSDFSurface(float3 cameraPos, float3 cameraRay, float2 uv)
            {
                float4 sdfout = tex2D(SDFRender, uv);
                float3 sdfPos = cameraPos + cameraRay * sdfout.w;
                float sdfDepth =  PositionToDepth(sdfPos);

                if(sdfout.w < 1e2)
                {
                       // lighting vectors:
                    float3 worldView = -cameraRay;
            #ifdef HDRP
                    float3 lightDirWorld = normalize(WorldSpaceLightPos.xyz);
            #else
                    float3 lightDirWorld = normalize(_WorldSpaceLightPos0.xyz);
            #endif

                    float3 normal = sdfout.xyz;
                    half3 h = normalize(lightDirWorld + worldView);
                    float nh = BlinnTerm(normal, h);
                    float nl = DotClamped(normal, lightDirWorld);
                    float nv = dot(normal, worldView); 
                    float rough = 0.55;
                    half V = SmithBeckmannVisibilityTerm(nl, nv, rough);
                    half D = NDFBlinnPhongNormalizedTerm(nh, RoughnessToSpecPower(rough));
                    float spec = (V * D) * (UNITY_PI / 4);
                    spec = max(0, spec * nl);

                    return float4(spec + (normal*0.5 + 0.5)*(dot(lightDirWorld, normal)*0.5 + 0.5), sdfDepth);
                }

                return 0.0;
            }
        #endif
          
            float4 MinIntersection(float4 a, float4 b)
            {
                return (a.w > b.w) ? a : b;
            }

            PSOut PSMain(VSOut input)
            {
                PSOut output;

                float3 cameraPos = _WorldSpaceCameraPos;
                float3 cameraRay = normalize(input.raydir);
                
                float4 intersection = 0.0;
               
                intersection = MinIntersection(intersection, RenderFluidSurface(cameraPos, cameraRay, input.uv));
            
            #if VISUALIZE_SDF
                intersection = MinIntersection(intersection, RenderSDFSurface(cameraPos, cameraRay, input.uv));
            #endif

                if (intersection.w == 0.0)
                {
                    //didn't hit anything
                    discard;
                }

                output.color = float4(intersection.xyz, 1.0);
                output.depth = intersection.w;
                return output;
            }
            ENDCG
        }
    }
}
