# Phase 2.6 — Procedural Cloud + Weather System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a procedural cloud generation + weather system that produces actual visible cloud formations, deterministically controlled by `WorldSettings.seed`, with weather states and biome integration.

**Architecture:** Camera-relative cloud system using GPU-instanced procedural meshes placed via deterministic Perlin noise. A centralized `WeatherSystem` MonoBehaviour manages weather state machine transitions. Clouds are generated around the camera position and recycled via object pooling. Weather influences lighting, fog, and rain via shader parameters and Unity's built-in systems.

**Tech Stack:** Unity URP, C#, HLSL shaders, `Mathf.PerlinNoise` for deterministic placement, `MaterialPropertyBlock` for GPU parameter updates, Unity Particle System for rain.

---

## Investigation Summary

### Key Architecture Findings

| Aspect | Finding |
|--------|---------|
| **Seed system** | `WorldSettings.seed` (int) used by `NoiseGenerator.ComputeSeedOffset()` — all randomness is seed-offset based via `Mathf.PerlinNoise` |
| **Deterministic RNG** | No formal RNG class. `NoiseGenerator.Hash()` and `BiomeMap.ComputeBiomeHash()` provide hash-based randomness |
| **Architecture** | `WorldManager` → `ChunkManager` → `Chunk` → generators. Settings in `WorldSettings` ScriptableObject |
| **Rendering** | URP (Mobile + PC configs). Custom shaders (e.g., `URPWater.shader`). No existing cloud/sky system |
| **Performance** | Chunk pooling (`ChunkPool`), foliage pooling (`FoliagePool`), 0.2s update interval |
| **Biome extension** | `BiomeDefinition.weatherEnvironment` field exists as `ScriptableObject` — perfect for weather influence |
| **No VFX/particles** | No existing particle systems or VFX Graph in project |

### Cloud Rendering Decision

**Chosen approach: GPU-instanced procedural cloud meshes** (billboard quad clusters)

**Rationale:**
- Fits low-poly/stylized aesthetic of the project
- No existing volumetric infrastructure to build on
- URP mobile renderer targets suggest avoiding expensive raymarching
- Instanced rendering keeps draw calls low
- Procedural mesh generation matches existing patterns (terrain, water)
- Stylized flat-shaded cloud meshes are performant and visually appropriate

**Rejected alternatives:**
- Volumetric raymarching: Too expensive for mobile URP target, no existing infrastructure
- Single sky dome texture: Doesn't meet "actual visible cloud formations" requirement
- VFX Graph clouds: Not designed for large-scale sky coverage

### Deterministic Seed Strategy

```
WorldSettings.seed (e.g., 12345)
    ↓
cloudSeed = hash(worldSeed ^ CLOUD_SEED_CONSTANT)
    ↓
CloudNoiseGenerator uses cloudSeed for all placement noise
```

- Cloud seed is derived via a simple integer hash, completely separate from terrain/foliage RNG
- `CloudNoiseGenerator` uses its own `ComputeSeedOffset()` with the derived cloud seed
- Same world seed → same cloud seed → same cloud distribution
- Different world seed → different cloud seed → different cloud distribution
- Cloud generation never touches terrain/foliage noise streams

---

## File Structure

### Files to Create

| File | Responsibility |
|------|---------------|
| `Assets/Script/Cloud/CloudSettings.cs` | ScriptableObject for cloud configuration |
| `Assets/Script/Cloud/CloudNoiseGenerator.cs` | Deterministic noise sampling for cloud placement |
| `Assets/Script/Cloud/CloudMeshGenerator.cs` | Procedural cloud mesh generation |
| `Assets/Script/Cloud/CloudManager.cs` | Camera-relative cloud lifecycle management |
| `Assets/Script/Cloud/CloudRenderer.cs` | GPU instancing and material property updates |
| `Assets/Script/Cloud/WeatherSystem.cs` | Weather state machine and transitions |
| `Assets/Script/Cloud/WeatherSettings.cs` | ScriptableObject for weather configuration |
| `Assets/Script/Cloud/BiomeWeatherConfig.cs` | Per-biome weather influence (extends BiomeDefinition) |
| `Assets/Script/Shaders/Clouds/ProceduralCloud.shader` | Stylized cloud shader |
| `Assets/Script/Shaders/Clouds/CloudShadow.shader` | Optional ground shadow projector |
| `Assets/Script/Shaders/Weather/RainEffect.shader` | Rain particle shader |
| `Assets/Script/Materials/Clouds/Cloud.mat` | Cloud material instance |
| `Assets/Script/Materials/Weather/Rain.mat` | Rain particle material |

### Files to Modify

| File | Change |
|------|--------|
| `Assets/Script/WorldSettings.cs` | Add cloud/weather settings section |
| `Assets/Script/WorldManager.cs` | Initialize WeatherSystem and CloudManager |
| `Assets/Script/BiomeDefinition.cs` | Add weather influence fields (already has `weatherEnvironment`) |

---

## Tasks

### Task 1: CloudSettings ScriptableObject

**Files:**
- Create: `Assets/Script/Cloud/CloudSettings.cs`

**Interfaces:**
- Produces: `CloudSettings` class with all cloud configuration parameters

- [ ] **Step 1: Create CloudSettings.cs**

```csharp
using UnityEngine;

namespace JayFos.Cloud
{
    [CreateAssetMenu(fileName = "CloudSettings", menuName = "World/Cloud Settings")]
    public class CloudSettings : ScriptableObject
    {
        [Header("Cloud Generation")]
        public bool cloudEnabled = true;
        [Range(0f, 1f)]
        public float cloudCoverage = 0.5f;
        [Range(0f, 1f)]
        public float cloudDensity = 0.6f;

        [Header("Cloud Dimensions")]
        [Range(50f, 500f)]
        public float cloudAltitude = 200f;
        [Range(10f, 100f)]
        public float cloudHeight = 30f;
        [Range(20f, 200f)]
        public float cloudScale = 80f;

        [Header("Cloud Appearance")]
        [Range(0f, 1f)]
        public float cloudSoftness = 0.5f;
        [Range(0f, 1f)]
        public float cloudOpacity = 0.85f;
        public Color cloudColor = new Color(0.95f, 0.97f, 1f, 1f);
        public Color cloudShadowColor = new Color(0.6f, 0.65f, 0.75f, 1f);

        [Header("Cloud Movement")]
        [Range(0f, 50f)]
        public float cloudSpeed = 5f;
        public Vector2 cloudWindDirection = new Vector2(1f, 0.3f);

        [Header("Cloud Streaming")]
        [Range(200f, 2000f)]
        public float cloudRenderDistance = 800f;
        [Range(50f, 300f)]
        public float cloudCellSize = 150f;
        [Range(1, 10)]
        public int cloudsPerCell = 3;

        [Header("Noise")]
        public float noiseScale = 0.008f;
        [Range(1, 8)]
        public int octaves = 4;
        [Range(0f, 1f)]
        public float persistence = 0.5f;
        public float lacunarity = 2f;

        [Header("Seed")]
        public int cloudSeedOffset = 98765;

        public int DeriveCloudSeed(int worldSeed)
        {
            return worldSeed ^ cloudSeedOffset;
        }
    }
}
```

- [ ] **Step 2: Verify compilation**

Run: Unity console check or `compile_verify.bat`
Expected: No errors

- [ ] **Step 3: Commit**

```bash
git add Assets/Script/Cloud/CloudSettings.cs
git commit -m "feat: add CloudSettings ScriptableObject"
```

---

### Task 2: WeatherSettings ScriptableObject

**Files:**
- Create: `Assets/Script/Cloud/WeatherSettings.cs`

**Interfaces:**
- Produces: `WeatherSettings` class with weather state configuration

- [ ] **Step 1: Create WeatherSettings.cs**

```csharp
using UnityEngine;

namespace JayFos.Cloud
{
    public enum WeatherState
    {
        Clear = 0,
        Cloudy = 1,
        Rain = 2,
        Storm = 3,
        Fog = 4
    }

    [CreateAssetMenu(fileName = "WeatherSettings", menuName = "World/Weather Settings")]
    public class WeatherSettings : ScriptableObject
    {
        [Header("Transition")]
        [Range(1f, 120f)]
        public float transitionDuration = 10f;

        [Header("Clear")]
        [Range(0f, 1f)]
        public float clearCloudCoverage = 0.15f;
        public float clearFogDensity = 0f;
        public float clearAmbientIntensity = 1f;

        [Header("Cloudy")]
        [Range(0f, 1f)]
        public float cloudyCloudCoverage = 0.65f;
        public float cloudyFogDensity = 0.002f;
        public float cloudyAmbientIntensity = 0.85f;

        [Header("Rain")]
        [Range(0f, 1f)]
        public float rainCloudCoverage = 0.8f;
        public float rainFogDensity = 0.005f;
        public float rainAmbientIntensity = 0.65f;
        [Range(0f, 1f)]
        public float rainIntensity = 0.7f;
        public float rainSpeed = 15f;

        [Header("Storm")]
        [Range(0f, 1f)]
        public float stormCloudCoverage = 0.95f;
        public float stormFogDensity = 0.01f;
        public float stormAmbientIntensity = 0.4f;
        [Range(0f, 1f)]
        public float stormRainIntensity = 1f;
        public float stormRainSpeed = 25f;
        public float stormWindMultiplier = 2f;

        [Header("Fog")]
        [Range(0f, 1f)]
        public float fogCloudCoverage = 0.3f;
        public float fogFogDensity = 0.03f;
        public float fogAmbientIntensity = 0.7f;

        public float GetTargetCoverage(WeatherState state)
        {
            switch (state)
            {
                case WeatherState.Clear: return clearCloudCoverage;
                case WeatherState.Cloudy: return cloudyCloudCoverage;
                case WeatherState.Rain: return rainCloudCoverage;
                case WeatherState.Storm: return stormCloudCoverage;
                case WeatherState.Fog: return fogCloudCoverage;
                default: return clearCloudCoverage;
            }
        }

        public float GetTargetFogDensity(WeatherState state)
        {
            switch (state)
            {
                case WeatherState.Clear: return clearFogDensity;
                case WeatherState.Cloudy: return cloudyFogDensity;
                case WeatherState.Rain: return rainFogDensity;
                case WeatherState.Storm: return stormFogDensity;
                case WeatherState.Fog: return fogFogDensity;
                default: return clearFogDensity;
            }
        }

        public float GetTargetAmbientIntensity(WeatherState state)
        {
            switch (state)
            {
                case WeatherState.Clear: return clearAmbientIntensity;
                case WeatherState.Cloudy: return cloudyAmbientIntensity;
                case WeatherState.Rain: return rainAmbientIntensity;
                case WeatherState.Storm: return stormAmbientIntensity;
                case WeatherState.Fog: return fogAmbientIntensity;
                default: return clearAmbientIntensity;
            }
        }

        public float GetRainIntensity(WeatherState state)
        {
            switch (state)
            {
                case WeatherState.Rain: return rainIntensity;
                case WeatherState.Storm: return stormRainIntensity;
                default: return 0f;
            }
        }

        public float GetWindMultiplier(WeatherState state)
        {
            if (state == WeatherState.Storm) return stormWindMultiplier;
            return 1f;
        }
    }
}
```

- [ ] **Step 2: Verify compilation**

Expected: No errors

- [ ] **Step 3: Commit**

```bash
git add Assets/Script/Cloud/WeatherSettings.cs
git commit -m "feat: add WeatherSettings ScriptableObject"
```

---

### Task 3: BiomeWeatherConfig

**Files:**
- Create: `Assets/Script/Cloud/BiomeWeatherConfig.cs`

**Interfaces:**
- Consumes: `BiomeDefinition.weatherEnvironment` field (already exists)
- Produces: Per-biome weather influence parameters

- [ ] **Step 1: Create BiomeWeatherConfig.cs**

```csharp
using UnityEngine;

namespace JayFos.Cloud
{
    [CreateAssetMenu(fileName = "BiomeWeatherConfig", menuName = "World/Biome Weather Config")]
    public class BiomeWeatherConfig : ScriptableObject
    {
        [Header("Cloud Influence")]
        [Range(-0.5f, 0.5f)]
        public float cloudCoverageModifier = 0f;
        [Range(-0.5f, 0.5f)]
        public float cloudDensityModifier = 0f;

        [Header("Weather Probability")]
        [Range(0f, 1f)]
        public float rainChance = 0.3f;
        [Range(0f, 1f)]
        public float stormChance = 0.1f;
        [Range(0f, 1f)]
        public float fogChance = 0.2f;

        [Header("Fog")]
        [Range(-0.01f, 0.05f)]
        public float fogDensityModifier = 0f;
    }
}
```

- [ ] **Step 2: Verify compilation**

Expected: No errors

- [ ] **Step 3: Commit**

```bash
git add Assets/Script/Cloud/BiomeWeatherConfig.cs
git commit -m "feat: add BiomeWeatherConfig for per-biome weather influence"
```

---

### Task 4: CloudNoiseGenerator (Deterministic)

**Files:**
- Create: `Assets/Script/Cloud/CloudNoiseGenerator.cs`

**Interfaces:**
- Consumes: `WorldSettings.seed`, `CloudSettings.cloudSeedOffset`
- Produces: Deterministic noise values for cloud placement

- [ ] **Step 1: Create CloudNoiseGenerator.cs**

```csharp
using UnityEngine;

namespace JayFos.Cloud
{
    public class CloudNoiseGenerator
    {
        private readonly int cloudSeed;

        public CloudNoiseGenerator(int worldSeed, int cloudSeedOffset)
        {
            cloudSeed = worldSeed ^ cloudSeedOffset;
        }

        public static int DeriveCloudSeed(int worldSeed, int cloudSeedOffset)
        {
            return worldSeed ^ cloudSeedOffset;
        }

        private static float ComputeSeedOffset(float worldCoord, int seed)
        {
            return worldCoord + (float)seed * 0.1f;
        }

        public float SampleCloudNoise(float worldX, float worldZ, float scale, int octaves, float persistence, float lacunarity)
        {
            float seededX = ComputeSeedOffset(worldX, cloudSeed);
            float seededZ = ComputeSeedOffset(worldZ, cloudSeed);

            float amplitude = 1f;
            float frequency = 1f;
            float noiseHeight = 0f;

            float baseX = seededX * scale;
            float baseZ = seededZ * scale;

            for (int i = 0; i < octaves; i++)
            {
                float sampleX = baseX * frequency;
                float sampleZ = baseZ * frequency;

                float perlin = Mathf.PerlinNoise(sampleX, sampleZ) * 2f - 1f;
                noiseHeight += perlin * amplitude;

                amplitude *= persistence;
                frequency *= lacunarity;
            }

            return noiseHeight;
        }

        public float GetCloudPresence(float worldX, float worldZ, float coverage, float noiseScale, int octaves, float persistence, float lacunarity)
        {
            float noise = SampleCloudNoise(worldX, worldZ, noiseScale, octaves, persistence, lacunarity);
            float normalized = (noise + 1f) * 0.5f;
            float threshold = 1f - coverage;
            return Mathf.Clamp01((normalized - threshold) / Mathf.Max(coverage, 0.01f));
        }

        public int Hash(float x, float z)
        {
            int h = cloudSeed;
            h ^= (int)(x * 73856093f);
            h ^= (int)(z * 19349663f);
            h ^= (h >> 16);
            h *= 0x85ebca6b;
            h ^= (h >> 13);
            h *= 0xc2b2ae35;
            h ^= (h >> 16);
            return h;
        }

        public float Hash01(float x, float z)
        {
            return Mathf.Abs(Hash(x, z) % 10000) / 10000f;
        }
    }
}
```

- [ ] **Step 2: Verify compilation**

Expected: No errors

- [ ] **Step 3: Commit**

```bash
git add Assets/Script/Cloud/CloudNoiseGenerator.cs
git commit -m "feat: add CloudNoiseGenerator with deterministic seed isolation"
```

---

### Task 5: ProceduralCloud Shader

**Files:**
- Create: `Assets/Script/Shaders/Clouds/ProceduralCloud.shader`

**Interfaces:**
- Consumes: Cloud color, opacity, softness from material properties
- Produces: Stylized cloud rendering

- [ ] **Step 1: Create ProceduralCloud.shader**

```hlsl
Shader "JayFos/Clouds/ProceduralCloud"
{
    Properties
    {
        _CloudColor("Cloud Color", Color) = (0.95, 0.97, 1, 1)
        _CloudShadowColor("Shadow Color", Color) = (0.6, 0.65, 0.75, 1)
        _Opacity("Opacity", Range(0, 1)) = 0.85
        _SoftEdge("Soft Edge", Range(0, 1)) = 0.5
        _NoiseScale("Noise Scale", Range(0.01, 1)) = 0.3
        _HeightFade("Height Fade", Range(0, 1)) = 0.3
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent-100"
        }

        Pass
        {
            Name "CloudForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _CloudColor;
                float4 _CloudShadowColor;
                float _Opacity;
                float _SoftEdge;
                float _NoiseScale;
                float _HeightFade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float3 viewDir : TEXCOORD2;
            };

            float hash(float2 p)
            {
                float h = dot(p, float2(127.1, 311.7));
                return frac(sin(h) * 43758.5453);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash(i);
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                for (int i = 0; i < 4; i++)
                {
                    value += amplitude * noise(p);
                    p *= 2.0;
                    amplitude *= 0.5;
                }
                return value;
            }

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = input.uv;
                o.viewDir = normalize(_WorldSpaceCameraPos.xyz - o.positionWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 noiseUV = i.positionWS.xz * _NoiseScale;
                float n = fbm(noiseUV);

                float edgeDist = min(min(i.uv.x, 1.0 - i.uv.x), min(i.uv.y, 1.0 - i.uv.y));
                float edgeFade = smoothstep(0.0, _SoftEdge, edgeDist);

                float heightFade = 1.0 - abs(i.uv.y - 0.5) * 2.0 * _HeightFade;
                heightFade = saturate(heightFade);

                float alpha = n * edgeFade * heightFade * _Opacity;

                float3 lightDir = normalize(_MainLightPosition.xyz);
                float ndotl = dot(float3(0, 1, 0), lightDir) * 0.5 + 0.5;
                float3 cloudLit = lerp(_CloudShadowColor.rgb, _CloudColor.rgb, ndotl);

                return half4(cloudLit, alpha);
            }
            ENDHLSL
        }
    }
}
```

- [ ] **Step 2: Verify shader compilation**

Expected: No shader errors in Unity console

- [ ] **Step 3: Commit**

```bash
git add Assets/Script/Shaders/Clouds/ProceduralCloud.shader
git commit -m "feat: add ProceduralCloud shader"
```

---

### Task 6: CloudMeshGenerator

**Files:**
- Create: `Assets/Script/Cloud/CloudMeshGenerator.cs`

**Interfaces:**
- Consumes: Cloud scale, height parameters
- Produces: Mesh objects for cloud rendering

- [ ] **Step 1: Create CloudMeshGenerator.cs**

```csharp
using UnityEngine;

namespace JayFos.Cloud
{
    public static class CloudMeshGenerator
    {
        private static Mesh _sharedQuadMesh;

        public static Mesh GetQuadMesh()
        {
            if (_sharedQuadMesh != null)
                return _sharedQuadMesh;

            _sharedQuadMesh = new Mesh { name = "CloudQuad" };

            float s = 1f;
            _sharedQuadMesh.vertices = new Vector3[]
            {
                new Vector3(-s, 0, -s),
                new Vector3(-s, 0,  s),
                new Vector3( s, 0,  s),
                new Vector3( s, 0, -s)
            };

            _sharedQuadMesh.uv = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(0, 1),
                new Vector2(1, 1),
                new Vector2(1, 0)
            };

            _sharedQuadMesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
            _sharedQuadMesh.RecalculateNormals();
            _sharedQuadMesh.RecalculateBounds();

            return _sharedQuadMesh;
        }

        public static Mesh GenerateCloudChunk(float scale, float height, int subdivisions)
        {
            subdivisions = Mathf.Max(1, subdivisions);
            int vertCount = (subdivisions + 1) * (subdivisions + 1);
            int triCount = subdivisions * subdivisions * 6;

            Vector3[] vertices = new Vector3[vertCount];
            Vector2[] uvs = new Vector2[vertCount];
            int[] triangles = new int[triCount];

            float halfScale = scale * 0.5f;
            float step = scale / subdivisions;

            int vertIdx = 0;
            for (int z = 0; z <= subdivisions; z++)
            {
                for (int x = 0; x <= subdivisions; x++)
                {
                    float px = -halfScale + x * step;
                    float pz = -halfScale + z * step;
                    float py = 0f;

                    float edgeX = Mathf.Abs(x / (float)subdivisions - 0.5f) * 2f;
                    float edgeZ = Mathf.Abs(z / (float)subdivisions - 0.5f) * 2f;
                    float edgeFactor = Mathf.Max(edgeX, edgeZ);
                    py -= edgeFactor * edgeFactor * height * 0.5f;

                    vertices[vertIdx] = new Vector3(px, py, pz);
                    uvs[vertIdx] = new Vector2(x / (float)subdivisions, z / (float)subdivisions);
                    vertIdx++;
                }
            }

            int triIdx = 0;
            for (int z = 0; z < subdivisions; z++)
            {
                for (int x = 0; x < subdivisions; x++)
                {
                    int a = z * (subdivisions + 1) + x;
                    int b = a + 1;
                    int c = a + (subdivisions + 1);
                    int d = c + 1;

                    triangles[triIdx++] = a;
                    triangles[triIdx++] = c;
                    triangles[triIdx++] = b;

                    triangles[triIdx++] = b;
                    triangles[triIdx++] = c;
                    triangles[triIdx++] = d;
                }
            }

            Mesh mesh = new Mesh { name = "CloudChunk" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }
    }
}
```

- [ ] **Step 2: Verify compilation**

Expected: No errors

- [ ] **Step 3: Commit**

```bash
git add Assets/Script/Cloud/CloudMeshGenerator.cs
git commit -m "feat: add CloudMeshGenerator for procedural cloud meshes"
```

---

### Task 7: CloudRenderer (GPU Instancing)

**Files:**
- Create: `Assets/Script/Cloud/CloudRenderer.cs`

**Interfaces:**
- Consumes: Cloud mesh, material, transform data
- Produces: GPU-instanced cloud rendering

- [ ] **Step 1: Create CloudRenderer.cs**

```csharp
using UnityEngine;
using System.Collections.Generic;

namespace JayFos.Cloud
{
    public class CloudRenderer
    {
        private readonly Material cloudMaterial;
        private readonly Mesh cloudMesh;
        private readonly MaterialPropertyBlock propertyBlock;

        private Matrix4x4[] matrices;
        private Vector4[] colors;
        private int activeCount;

        private const int MAX_BATCH_SIZE = 1023;

        public CloudRenderer(Material material, Mesh mesh)
        {
            cloudMaterial = material;
            cloudMesh = mesh;
            propertyBlock = new MaterialPropertyBlock();
            matrices = new Matrix4x4[MAX_BATCH_SIZE];
            colors = new Vector4[MAX_BATCH_SIZE];
            activeCount = 0;
        }

        public void SetClouds(List<CloudData> clouds)
        {
            activeCount = Mathf.Min(clouds.Count, MAX_BATCH_SIZE);

            for (int i = 0; i < activeCount; i++)
            {
                CloudData cloud = clouds[i];
                matrices[i] = Matrix4x4.TRS(
                    cloud.position,
                    Quaternion.Euler(0, cloud.rotation, 0),
                    new Vector3(cloud.scale, 1f, cloud.scale)
                );
                colors[i] = new Vector4(cloud.opacity, cloud.softness, 0, 0);
            }
        }

        public void Render()
        {
            if (activeCount == 0 || cloudMaterial == null || cloudMesh == null)
                return;

            int batchStart = 0;
            while (batchStart < activeCount)
            {
                int batchSize = Mathf.Min(MAX_BATCH_SIZE, activeCount - batchStart);

                propertyBlock.SetVectorArray("_CloudParams", colors);

                Graphics.DrawMeshInstanced(
                    cloudMesh,
                    0,
                    cloudMaterial,
                    matrices,
                    batchSize,
                    propertyBlock
                );

                batchStart += batchSize;
            }
        }

        public void Cleanup()
        {
            activeCount = 0;
        }
    }

    public struct CloudData
    {
        public Vector3 position;
        public float scale;
        public float rotation;
        public float opacity;
        public float softness;
    }
}
```

- [ ] **Step 2: Verify compilation**

Expected: No errors

- [ ] **Step 3: Commit**

```bash
git add Assets/Script/Cloud/CloudRenderer.cs
git commit -m "feat: add CloudRenderer with GPU instancing"
```

---

### Task 8: CloudManager (Camera-Relative Streaming)

**Files:**
- Create: `Assets/Script/Cloud/CloudManager.cs`

**Interfaces:**
- Consumes: `CloudSettings`, `CloudNoiseGenerator`, `CloudRenderer`
- Produces: Cloud lifecycle management around camera

- [ ] **Step 1: Create CloudManager.cs**

```csharp
using UnityEngine;
using System.Collections.Generic;

namespace JayFos.Cloud
{
    public class CloudManager
    {
        private readonly CloudSettings settings;
        private readonly CloudNoiseGenerator noiseGenerator;
        private readonly CloudRenderer renderer;

        private readonly List<CloudData> activeClouds;
        private readonly Dictionary<Vector2Int, List<CloudData>> cloudCells;

        private Transform cameraTransform;
        private Vector2Int lastCameraCell;
        private float windOffsetX;
        private float windOffsetZ;

        public CloudManager(CloudSettings settings, int worldSeed)
        {
            this.settings = settings;
            noiseGenerator = new CloudNoiseGenerator(worldSeed, settings.cloudSeedOffset);

            Mesh cloudMesh = CloudMeshGenerator.GenerateCloudChunk(
                settings.cloudScale, settings.cloudHeight, 4);
            renderer = new CloudRenderer(settings.cloudMaterial, cloudMesh);

            activeClouds = new List<CloudData>();
            cloudCells = new Dictionary<Vector2Int, List<CloudData>>();
        }

        public void SetCamera(Transform camera)
        {
            cameraTransform = camera;
        }

        public void Update(float deltaTime)
        {
            if (cameraTransform == null || !settings.cloudEnabled)
                return;

            windOffsetX += settings.cloudWindDirection.x * settings.cloudSpeed * deltaTime;
            windOffsetZ += settings.cloudWindDirection.y * settings.cloudSpeed * deltaTime;

            Vector2Int currentCell = GetCameraCell();

            if (currentCell != lastCameraCell)
            {
                lastCameraCell = currentCell;
                UpdateActiveClouds();
            }

            ApplyWindOffset();
            renderer.SetClouds(activeClouds);
            renderer.Render();
        }

        public void SetCoverage(float coverage)
        {
            settings.cloudCoverage = coverage;
        }

        private Vector2Int GetCameraCell()
        {
            Vector3 pos = cameraTransform.position;
            int cellSize = (int)settings.cloudCellSize;
            int cx = Mathf.FloorToInt(pos.x / cellSize);
            int cz = Mathf.FloorToInt(pos.z / cellSize);
            return new Vector2Int(cx, cz);
        }

        private void UpdateActiveClouds()
        {
            activeClouds.Clear();

            int cellRadius = Mathf.CeilToInt(settings.cloudRenderDistance / settings.cloudCellSize);
            float cellSize = settings.cloudCellSize;

            for (int dx = -cellRadius; dx <= cellRadius; dx++)
            {
                for (int dz = -cellRadius; dz <= cellRadius; dz++)
                {
                    int cellX = lastCameraCell.x + dx;
                    int cellZ = lastCameraCell.y + dz;
                    Vector2Int cellKey = new Vector2Int(cellX, cellZ);

                    float cellWorldX = cellX * cellSize;
                    float cellWorldZ = cellZ * cellSize;

                    float dist = Vector2.Distance(
                        new Vector2(cellWorldX + cellSize * 0.5f, cellWorldZ + cellSize * 0.5f),
                        new Vector2(cameraTransform.position.x, cameraTransform.position.z));

                    if (dist > settings.cloudRenderDistance)
                        continue;

                    GenerateCloudsForCell(cellKey, cellWorldX, cellWorldZ, cellSize);
                }
            }
        }

        private void GenerateCloudsForCell(Vector2Int cellKey, float cellOriginX, float cellOriginZ, float cellSize)
        {
            if (cloudCells.ContainsKey(cellKey))
            {
                activeClouds.AddRange(cloudCells[cellKey]);
                return;
            }

            List<CloudData> cellClouds = new List<CloudData>();

            for (int i = 0; i < settings.cloudsPerCell; i++)
            {
                float sampleX = cellOriginX + noiseGenerator.Hash01(cellKey.x * 17 + i * 31, cellKey.y * 13 + i * 47) * cellSize;
                float sampleZ = cellOriginZ + noiseGenerator.Hash01(cellKey.x * 23 + i * 53, cellKey.y * 19 + i * 61) * cellSize;

                float presence = noiseGenerator.GetCloudPresence(
                    sampleX, sampleZ,
                    settings.cloudCoverage,
                    settings.noiseScale,
                    settings.octaves,
                    settings.persistence,
                    settings.lacunarity);

                if (presence < 0.1f)
                    continue;

                float scaleVariation = noiseGenerator.Hash01(cellKey.x + i * 7, cellKey.y + i * 11);
                float cloudScale = settings.cloudScale * (0.6f + scaleVariation * 0.8f);

                float rotation = noiseGenerator.Hash01(cellKey.x + i * 3, cellKey.y + i * 5) * 360f;

                float heightVariation = noiseGenerator.Hash01(cellKey.x + i * 13, cellKey.y + i * 17);
                float cloudY = settings.cloudAltitude + (heightVariation - 0.5f) * settings.cloudHeight;

                CloudData cloud = new CloudData
                {
                    position = new Vector3(sampleX, cloudY, sampleZ),
                    scale = cloudScale,
                    rotation = rotation,
                    opacity = presence * settings.cloudOpacity,
                    softness = settings.cloudSoftness
                };

                cellClouds.Add(cloud);
                activeClouds.Add(cloud);
            }

            cloudCells[cellKey] = cellClouds;
        }

        private void ApplyWindOffset()
        {
            for (int i = 0; i < activeClouds.Count; i++)
            {
                CloudData cloud = activeClouds[i];
                cloud.position.x += windOffsetX;
                cloud.position.z += windOffsetZ;
                activeClouds[i] = cloud;
            }
        }

        public void Cleanup()
        {
            renderer.Cleanup();
            activeClouds.Clear();
            cloudCells.Clear();
        }
    }
}
```

- [ ] **Step 2: Verify compilation**

Expected: No errors

- [ ] **Step 3: Commit**

```bash
git add Assets/Script/Cloud/CloudManager.cs
git commit -m "feat: add CloudManager with camera-relative streaming"
```

---

### Task 9: WeatherSystem

**Files:**
- Create: `Assets/Script/Cloud/WeatherSystem.cs`

**Interfaces:**
- Consumes: `WeatherSettings`, `CloudManager`, `BiomeMap`
- Produces: Weather state management and transitions

- [ ] **Step 1: Create WeatherSystem.cs**

```csharp
using UnityEngine;
using JayFos.Biomes;

namespace JayFos.Cloud
{
    public class WeatherSystem : MonoBehaviour
    {
        [SerializeField] private WeatherSettings weatherSettings;
        [SerializeField] private CloudSettings cloudSettings;
        [SerializeField] private float autoChangeInterval = 120f;

        private WeatherState currentState = WeatherState.Clear;
        private WeatherState targetState = WeatherState.Clear;
        private float transitionProgress = 1f;
        private float autoChangeTimer;

        private float currentCoverage;
        private float currentFogDensity;
        private float currentAmbientIntensity;
        private float currentRainIntensity;
        private float currentWindMultiplier;

        private CloudManager cloudManager;
        private BiomeMap biomeMap;
        private Light mainLight;

        public WeatherState CurrentState => currentState;
        public float CurrentCoverage => currentCoverage;
        public float CurrentFogDensity => currentFogDensity;

        public void Initialize(CloudManager cloudManager, BiomeMap biomeMap)
        {
            this.cloudManager = cloudManager;
            this.biomeMap = biomeMap;
            mainLight = FindMainLight();

            ApplyStateInstant(currentState);
        }

        private void Update()
        {
            if (weatherSettings == null)
                return;

            UpdateTransition();
            UpdateAutoChange();
            ApplyToSystems();
        }

        public void SetWeather(WeatherState newState)
        {
            if (newState == targetState && transitionProgress >= 1f)
                return;

            targetState = newState;
            transitionProgress = 0f;
        }

        public void SetWeatherFromBiome(Vector3 worldPosition)
        {
            if (biomeMap == null)
                return;

            BiomeDefinition biome = biomeMap.GetBiome(worldPosition.x, worldPosition.z);
            if (biome == null)
                return;

            BiomeWeatherConfig weatherConfig = biome.weatherEnvironment as BiomeWeatherConfig;
            if (weatherConfig == null)
                return;

            float roll = Random.value;
            WeatherState newState = WeatherState.Clear;

            if (roll < weatherConfig.stormChance)
                newState = WeatherState.Storm;
            else if (roll < weatherConfig.stormChance + weatherConfig.rainChance)
                newState = WeatherState.Rain;
            else if (roll < weatherConfig.stormChance + weatherConfig.rainChance + weatherConfig.fogChance)
                newState = WeatherState.Fog;
            else
                newState = WeatherState.Cloudy;

            SetWeather(newState);
        }

        private void UpdateTransition()
        {
            if (transitionProgress >= 1f)
                return;

            transitionProgress += Time.deltaTime / weatherSettings.transitionDuration;
            transitionProgress = Mathf.Clamp01(transitionProgress);

            float t = transitionProgress * transitionProgress * (3f - 2f * transitionProgress);

            WeatherState fromState = currentState;
            WeatherState toState = targetState;

            currentCoverage = Mathf.Lerp(
                weatherSettings.GetTargetCoverage(fromState),
                weatherSettings.GetTargetCoverage(toState),
                t);

            currentFogDensity = Mathf.Lerp(
                weatherSettings.GetTargetFogDensity(fromState),
                weatherSettings.GetTargetFogDensity(toState),
                t);

            currentAmbientIntensity = Mathf.Lerp(
                weatherSettings.GetTargetAmbientIntensity(fromState),
                weatherSettings.GetTargetAmbientIntensity(toState),
                t);

            float fromRain = weatherSettings.GetRainIntensity(fromState);
            float toRain = weatherSettings.GetRainIntensity(toState);
            currentRainIntensity = Mathf.Lerp(fromRain, toRain, t);

            float fromWind = weatherSettings.GetWindMultiplier(fromState);
            float toWind = weatherSettings.GetWindMultiplier(toState);
            currentWindMultiplier = Mathf.Lerp(fromWind, toWind, t);

            if (transitionProgress >= 1f)
            {
                currentState = targetState;
            }
        }

        private void UpdateAutoChange()
        {
            autoChangeTimer += Time.deltaTime;
            if (autoChangeTimer >= autoChangeInterval)
            {
                autoChangeTimer = 0f;
                RandomWeatherChange();
            }
        }

        private void RandomWeatherChange()
        {
            float roll = Random.value;
            WeatherState newState;

            if (roll < 0.3f)
                newState = WeatherState.Clear;
            else if (roll < 0.6f)
                newState = WeatherState.Cloudy;
            else if (roll < 0.8f)
                newState = WeatherState.Rain;
            else if (roll < 0.9f)
                newState = WeatherState.Storm;
            else
                newState = WeatherState.Fog;

            SetWeather(newState);
        }

        private void ApplyToSystems()
        {
            if (cloudManager != null)
                cloudManager.SetCoverage(currentCoverage);

            RenderSettings.fogDensity = currentFogDensity;
            RenderSettings.fog = currentFogDensity > 0.001f;

            if (mainLight != null)
                mainLight.intensity = currentAmbientIntensity;
        }

        private void ApplyStateInstant(WeatherState state)
        {
            currentCoverage = weatherSettings.GetTargetCoverage(state);
            currentFogDensity = weatherSettings.GetTargetFogDensity(state);
            currentAmbientIntensity = weatherSettings.GetTargetAmbientIntensity(state);
            currentRainIntensity = weatherSettings.GetRainIntensity(state);
            currentWindMultiplier = weatherSettings.GetWindMultiplier(state);
            transitionProgress = 1f;
        }

        private Light FindMainLight()
        {
            Light[] lights = FindObjectsOfType<Light>();
            Light best = null;
            float bestIntensity = float.MinValue;

            foreach (Light light in lights)
            {
                if (light.type == LightType.Directional && light.intensity > bestIntensity)
                {
                    best = light;
                    bestIntensity = light.intensity;
                }
            }

            return best;
        }
    }
}
```

- [ ] **Step 2: Verify compilation**

Expected: No errors

- [ ] **Step 3: Commit**

```bash
git add Assets/Script/Cloud/WeatherSystem.cs
git commit -m "feat: add WeatherSystem state machine"
```

---

### Task 10: Update WorldSettings

**Files:**
- Modify: `Assets/Script/WorldSettings.cs:1-187`

**Interfaces:**
- Consumes: `CloudSettings`, `WeatherSettings`
- Produces: Integrated cloud/weather settings in WorldSettings

- [ ] **Step 1: Add cloud/weather fields to WorldSettings**

Add after the Water section (after line 113):

```csharp
[Header("Clouds & Weather")]
public CloudSettings cloudSettings;
public WeatherSettings weatherSettings;
public bool enableClouds = true;
```

Add using statement at top:

```csharp
using JayFos.Cloud;
```

- [ ] **Step 2: Verify compilation**

Expected: No errors

- [ ] **Step 3: Commit**

```bash
git add Assets/Script/WorldSettings.cs
git commit -m "feat: integrate cloud/weather settings into WorldSettings"
```

---

### Task 11: Update WorldManager

**Files:**
- Modify: `Assets/Script/WorldManager.cs:1-80`

**Interfaces:**
- Consumes: `CloudManager`, `WeatherSystem`
- Produces: Cloud/weather initialization in world lifecycle

- [ ] **Step 1: Add cloud/weather references and initialization**

Add fields:

```csharp
private CloudManager cloudManager;
private WeatherSystem weatherSystem;
```

Add using:

```csharp
using JayFos.Cloud;
```

In `Awake()`, after chunkManager initialization, add:

```csharp
if (settings.enableClouds && settings.cloudSettings != null)
{
    cloudManager = new CloudManager(settings.cloudSettings, settings.seed);
    cloudManager.SetCamera(Camera.main?.transform);

    if (settings.weatherSettings != null)
    {
        weatherSystem = gameObject.AddComponent<WeatherSystem>();
        weatherSystem.Initialize(cloudManager, biomeMap);
    }
}
```

In `Update()`, add cloud update:

```csharp
if (cloudManager != null)
{
    if (cloudManager != null && Camera.main != null)
        cloudManager.SetCamera(Camera.main.transform);
    cloudManager.Update(Time.deltaTime);
}
```

- [ ] **Step 2: Verify compilation**

Expected: No errors

- [ ] **Step 3: Commit**

```bash
git add Assets/Script/WorldManager.cs
git commit -m "feat: integrate CloudManager and WeatherSystem into WorldManager"
```

---

### Task 12: Rain Particle System

**Files:**
- Create: `Assets/Script/Cloud/RainSystem.cs`
- Create: `Assets/Script/Shaders/Weather/RainParticle.shader`

**Interfaces:**
- Consumes: `WeatherSystem` rain intensity
- Produces: Camera-relative rain particles

- [ ] **Step 1: Create RainSystem.cs**

```csharp
using UnityEngine;

namespace JayFos.Cloud
{
    public class RainSystem : MonoBehaviour
    {
        [SerializeField] private Material rainMaterial;
        [Range(100, 5000)]
        [SerializeField] private int particleCount = 2000;
        [SerializeField] private float rainHeight = 30f;
        [SerializeField] private float rainRadius = 20f;
        [SerializeField] private float rainSpeed = 15f;
        [SerializeField] private float rainWidth = 0.02f;
        [SerializeField] private float rainLength = 1.5f;

        private ParticleSystem rainParticles;
        private WeatherSystem weatherSystem;
        private Transform cameraTransform;

        public void Initialize(WeatherSystem weather)
        {
            weatherSystem = weather;
            cameraTransform = Camera.main?.transform;
            CreateRainParticles();
        }

        private void Update()
        {
            if (rainParticles == null || weatherSystem == null)
                return;

            float intensity = weatherSystem.CurrentRainIntensity;

            if (intensity > 0.01f)
            {
                if (!rainParticles.isPlaying)
                    rainParticles.Play();

                var emission = rainParticles.emission;
                emission.rateOverTime = particleCount * intensity;

                var velocity = rainParticles.velocityOverLifetime;
                velocity.enabled = true;
                velocity.x = weatherSystem.CurrentWindMultiplier * 0.5f;
                velocity.z = weatherSystem.CurrentWindMultiplier * 0.3f;
            }
            else
            {
                if (rainParticles.isPlaying)
                    rainParticles.Stop();
            }

            if (cameraTransform != null)
            {
                Vector3 pos = cameraTransform.position;
                pos.y += rainHeight * 0.5f;
                transform.position = pos;
            }
        }

        private void CreateRainParticles()
        {
            GameObject rainGO = new GameObject("RainParticles");
            rainGO.transform.SetParent(transform, false);

            rainParticles = rainGO.AddComponent<ParticleSystem>();

            var main = rainParticles.main;
            main.maxParticles = particleCount;
            main.startLifetime = rainHeight / rainSpeed;
            main.startSpeed = rainSpeed;
            main.startSize = new ParticleSystem.MinMaxCurve(rainWidth, rainWidth * 2f);
            main.startRotation = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;
            main.playOnAwake = false;

            var emission = rainParticles.emission;
            emission.rateOverTime = 0;

            var shape = rainParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(rainRadius * 2f, 0.1f, rainRadius * 2f);

            var renderer = rainGO.GetComponent<ParticleSystemRenderer>();
            renderer.material = rainMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = rainLength / rainSpeed;
            renderer.lengthScale = rainLength;

            var colorLifetime = rainParticles.colorOverLifetime;
            colorLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.6f, 0.1f), new GradientAlphaKey(0.6f, 0.9f), new GradientAlphaKey(0f, 1f) }
            );
            colorLifetime.color = gradient;

            rainParticles.Stop();
        }
    }
}
```

- [ ] **Step 2: Create RainParticle.shader**

```hlsl
Shader "JayFos/Weather/RainParticle"
{
    Properties
    {
        _Color("Color", Color) = (0.7, 0.8, 1, 0.6)
        _MainTex("Albedo", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _MainTex_ST;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.color = input.color * _Color;
                o.uv = input.texcoord;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                return tex * i.color;
            }
            ENDHLSL
        }
    }
}
```

- [ ] **Step 3: Verify compilation**

Expected: No errors

- [ ] **Step 4: Commit**

```bash
git add Assets/Script/Cloud/RainSystem.cs Assets/Script/Shaders/Weather/RainParticle.shader
git commit -m "feat: add RainSystem with particle-based rain"
```

---

### Task 13: Cloud Material Creation

**Files:**
- Create: Cloud material asset (manual step or script)

**Interfaces:**
- Consumes: ProceduralCloud shader
- Produces: Cloud material instance

- [ ] **Step 1: Create cloud material via Editor script or manual**

Create `Assets/Script/Cloud/CloudMaterialSetup.cs` (Editor script):

```csharp
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace JayFos.Cloud.Editor
{
    public static class CloudMaterialSetup
    {
        [MenuItem("World/Create Cloud Material")]
        public static void CreateCloudMaterial()
        {
            Shader shader = Shader.Find("JayFos/Clouds/ProceduralCloud");
            if (shader == null)
            {
                Debug.LogError("ProceduralCloud shader not found");
                return;
            }

            Material mat = new Material(shader);
            mat.name = "CloudMat";
            mat.SetColor("_CloudColor", new Color(0.95f, 0.97f, 1f, 1f));
            mat.SetColor("_CloudShadowColor", new Color(0.6f, 0.65f, 0.75f, 1f));
            mat.SetFloat("_Opacity", 0.85f);
            mat.SetFloat("_SoftEdge", 0.5f);
            mat.SetFloat("_NoiseScale", 0.3f);
            mat.SetFloat("_HeightFade", 0.3f);

            string path = "Assets/Script/Materials/Clouds";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder("Assets/Script/Materials", "Clouds");
            }

            AssetDatabase.CreateAsset(mat, $"{path}/CloudMat.mat");
            AssetDatabase.SaveAssets();
            Debug.Log("Cloud material created at " + path + "/CloudMat.mat");
        }
    }
}
#endif
```

- [ ] **Step 2: Verify compilation**

Expected: No errors

- [ ] **Step 3: Commit**

```bash
git add Assets/Script/Cloud/CloudMaterialSetup.cs
git commit -m "feat: add Editor utility for cloud material creation"
```

---

### Task 14: Integration Verification

**Files:**
- None (verification only)

**Interfaces:**
- Consumes: All previous tasks
- Produces: Verified working system

- [ ] **Step 1: Compile all C# scripts**

Run: Unity console or compile script
Expected: Zero errors

- [ ] **Step 2: Create Cloud and Weather assets in Editor**

Use menu: World > Create Cloud Material
Create CloudSettings and WeatherSettings ScriptableObjects
Assign to WorldSettings

- [ ] **Step 3: Enter Play Mode and verify**

Expected:
- Clouds visible in sky
- Clouds move with wind
- Weather transitions work
- Terrain still generates correctly
- Foliage still generates correctly
- Water still works
- Player movement unaffected
- No console errors

- [ ] **Step 4: Seed reproducibility test**

Test A:
- Set seed = 12345, record cloud positions
- Reload scene with seed = 12345
- Verify same cloud positions

Test B:
- Set seed = 67890, verify different cloud positions

- [ ] **Step 5: Commit verification**

```bash
git add -A
git commit -m "feat: Phase 2.6 procedural cloud + weather system complete"
```

---

## Performance Considerations

1. **GPU Instancing**: Cloud meshes rendered via `Graphics.DrawMeshInstanced` — single draw call per 1023 clouds
2. **Cell-based streaming**: Only generate clouds for cells within render distance
3. **Deterministic caching**: Cloud cells cached in dictionary, regenerated only when camera moves to new cell
4. **No per-cloud Update()**: CloudManager updates centrally, no MonoBehaviour per cloud
5. **Particle rain**: Single particle system follows camera, not world-anchored
6. **Material property blocks**: Avoid material instance creation
7. **Shared mesh**: All clouds share one procedural mesh, scaled via matrix

## Known Limitations

1. Cloud shadows not implemented (would require additional projector shader)
2. Lightning not implemented (can be added in Phase 2.7)
3. Cloud-to-cloud occlusion not handled (billboard approach)
4. No LOD system for distant clouds (could add simplified meshes)

## Recommended Phase 2.7

- Cloud shadow projection on terrain
- Lightning system with flash effects
- Snow particles for cold biomes
- Wind visualization (grass/particle bending)
- Day/night cycle integration
- Cloud color changes at sunset/sunrise
