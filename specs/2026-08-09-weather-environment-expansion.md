# Phase 2.8 — Weather & Environment Expansion

**Status**: Specification (pending approval)
**Target Unity Version**: 6000.5.3f1 (URP)
**Parent System**: `WorldManager` / `WorldSettings` / `WeatherSystem`
**Related Phases**: Phase 2.6 (Clouds & Weather, COMPLETE), Phase 2.7 (Roads, COMPLETE)

---

## 1. Overview

Phase 2.8 expands the existing weather system (Phase 2.6) with environmental features that add depth, atmosphere, and temporal variation to the world. All additions extend the existing architecture without reimplementing existing systems.

### 1.1 Existing Architecture (from Phase 2.6)

| Component | Location | Purpose |
|-----------|----------|---------|
| `CloudManager` | `Assets/Script/Cloud/CloudManager.cs` | Procedural cloud placement, wind animation, camera-relative streaming |
| `CloudRenderer` | `Assets/Script/Cloud/CloudRenderer.cs` | Pooled `MeshRenderer`s with single `Material` per cloud |
| `WeatherSystem` | `Assets/Script/Cloud/WeatherSystem.cs` | State machine (Clear → Cloudy → Rain → Storm → Fog) with smooth transitions |
| `RainSystem` | `Assets/Script/Cloud/RainSystem.cs` | Particle-based rain with wind-driven velocity, emission rate tied to `CurrentRainIntensity` |
| `CloudSettings` | `Assets/Script/Cloud/CloudSettings.cs` | ScriptableObject — coverage, speed, scale, altitude, opacity, render distance |
| `WeatherSettings` | `Assets/Script/Cloud/WeatherSettings.cs` | ScriptableObject — per-state coverage/fog/ambient/rain/wind targets |
| `BiomeWeatherConfig` | `Assets/Script/Cloud/BiomeWeatherConfig.cs` | Per-biome storm/rain/fog chance weights |
| `ProceduralCloud.shader` | `Assets/Script/Shaders/Clouds/` | GPU instancing cloud shader with `_CloudShadowColor` |
| `URPWater.shader` | `Assets/Script/Shaders/Water/` | Custom water shader with wave displacement, foam, specular |
| `RainParticle.shader` | `Assets/Script/Weather/RainParticle.shader` | Transparent particle shader for rain streaks |
| `WorldSettings` | `Assets/Script/WorldSettings.cs` | Central config SO — holds `terrainMaterial`, `waterMaterial`, `cloudSettings`, `weatherSettings` |
| `WorldManager` | `Assets/Script/WorldManager.cs` | Bootstrap — instantiates `CloudManager`, `WeatherSystem`, `Chunk` system |
| `Chunk` | `Assets/Script/Chunk.cs` | Per-chunk terrain/water/foliage generation; sets `meshRenderer.sharedMaterial` from `WorldSettings.terrainMaterial` |

### 1.2 Design Principles

- **No reimplementation**: Extend `WeatherSystem`, `RainSystem`, `CloudManager` — do not duplicate their logic
- **No hard-coded shader dependencies**: Use global shader properties and `MaterialPropertyBlock` for flexibility
- **All features optional**: Each subsystem has a toggle/enable flag in `EnvironmentSettings`
- **URP-compatible**: Use `RenderSettings` for sky/fog, `MaterialPropertyBlock` for per-material updates
- **Data-driven**: All tuning via `EnvironmentSettings` ScriptableObject

### 1.3 Architecture Map (New + Existing)

```
WorldSettings (SO)
├── EnvironmentSettings (SO) ← NEW
│   ├── dayLength, sunElevationMin/Max, temperatureCurve, windCurve
│   ├── sunsetStart/sunsetEnd, fogDensityBase/Max, cloudShadowIntensity
│   └── enableDayNightCycle, enableCloudShadows, enableLightning, enableSnow, enableWindFoliage, enableAtmosphericDepth
│
├── cloudSettings, weatherSettings (existing from Phase 2.6)
├── terrainMaterial, waterMaterial (existing)
└── foliageSpawnRules (existing)
        │
        ▼
WorldManager
├── CloudManager (existing, Phase 2.6)
├── WeatherSystem (extended in Phase 2.8)
├── DayNightCycle ← NEW
├── CloudShadowController ← NEW
├── LightningManager ← NEW
├── SnowSystem ← NEW (extends RainSystem pattern)
└── WindFoliageController ← NEW
```

---

## 2. Specification Sections

### Section 1: `EnvironmentSettings` ScriptableObject

**File**: `Assets/Script/Environment/EnvironmentSettings.cs`

**Purpose**: Central configuration for all Phase 2.8 environmental features. Replaces or augments individual settings scattered across other SOs.

**Namespace**: `JayFos.Environment`

**Class definition**:

```csharp
namespace JayFos.Environment
{
    [CreateAssetMenu(menuName = "World/Environment Settings", order = 1)]
    public class EnvironmentSettings : ScriptableObject
    {
        // --- Day / Night ---
        [Header("Day / Night Cycle")]
        [Tooltip("Length of one full day/night cycle in seconds (default: 480 = 8 minutes).")]
        [Range(30f, 3600f)]
        public float dayLength = 480f;

        [Header("Sun Arc")]
        [Tooltip("Minimum sun elevation angle in degrees (at sunrise/sunset).")]
        public float sunElevationMin = -10f;
        [Tooltip("Maximum sun elevation angle in degrees (at noon/zenith).")]
        public float sunElevationMax = 80f;

        [Header("Temperature / Wind Curves")]
        [Tooltip("Temperature (0=coldest, 1=hottest) mapped to time of day (0-1).")]
        public AnimationCurve temperatureCurve = new AnimationCurve(
            new Keyframe(0f, 0.3f), new Keyframe(0.25f, 0.4f),
            new Keyframe(0.5f, 1f), new Keyframe(0.75f, 0.8f),
            new Keyframe(1f, 0.3f)
        );
        [Tooltip("Wind intensity (0=calm, 1=gale) mapped to time of day (0-1).")]
        public AnimationCurve windCurve = new AnimationCurve(
            new Keyframe(0f, 0.2f), new Keyframe(0.5f, 0.3f),
            new Keyframe(0.75f, 0.8f), new Keyframe(1f, 0.2f)
        );

        [Header("Sunset / Twilight")]
        [Tooltip("Time of day when sunset begins (0-1, where 1 = midnight).")]
        [Range(0f, 1f)]
        public float sunsetStart = 0.65f;
        [Tooltip("Time of day when sunset ends / full twilight reached (0-1).")]
        [Range(0f, 1f)]
        public float sunsetEnd = 0.85f;

        [Header("Atmospheric Fog")]
        [Tooltip("Base fog density multiplier for RenderSettings.fogDensity.")]
        [Range(0f, 1f)]
        public float fogDensityBase = 0.003f;
        [Tooltip("Maximum fog density (during weather states like Fog/Storm).")]
        [Range(0f, 1f)]
        public float fogDensityMax = 0.015f;

        [Header("Cloud Shadows")]
        [Tooltip("Maximum cloud shadow intensity (0 = no shadow, 1 = full darkening).")]
        [Range(0f, 1f)]
        public float cloudShadowIntensity = 0.4f;

        [Header("Feature Toggles")]
        public bool enableDayNightCycle = true;
        public bool enableCloudShadows = true;
        public bool enableLightning = true;
        public bool enableSnow = true;
        public bool enableWindFoliage = true;
        public bool enableAtmosphericDepth = true;
    }
}
```

**Integration**: Referenced as a public field on `WorldSettings`:

```csharp
// In WorldSettings.cs (existing file, extended)
public EnvironmentSettings environmentSettings;
```

**Validation**: On `OnValidate()`, validates that `sunsetStart < sunsetEnd`, `sunElevationMin < sunElevationMax`, and curves are within [0,1] y-range bounds.

---

### Section 2: `DayNightCycle` MonoBehaviour

**File**: `Assets/Script/Environment/DayNightCycle.cs`

**Purpose**: Controls the sun's physically coherent trajectory, sky color interpolation, and ambient light intensity over a full day/night cycle.

**Namespace**: `JayFos.Environment`

**Dependencies**: `EnvironmentSettings`, `Light` (sun), reference to `WeatherSystem` for combined ambient intensity.

**Class definition**:

```csharp
namespace JayFos.Environment
{
    public class DayNightCycle : MonoBehaviour
    {
        [Header("References")]
        public EnvironmentSettings settings;
        public Light sunLight;
        public WeatherSystem weatherSystem; // for combined ambient calculation

        [Header("Sky Colors (read-only, set in inspector or via editor tool)")]
        public Color nightSkyColor = new Color(0.039f, 0.039f, 0.102f);    // #0a0a1a
        public Color dawnHorizonColor = new Color(1f, 0.6f, 0.267f);        // #ff9944
        public Color daySkyColor = new Color(0.529f, 0.8f, 0.929f);         // #87ceeb
        public Color sunsetHorizonColor = new Color(0.8f, 0.176f, 0f);      // #cc3300
        public Color twilightSkyColor = new Color(0.165f, 0.102f, 0.227f);  // #2a1a3a

        // --- Internal State ---
        private float elapsedTime = 0f;
        private float dayProgress = 0f; // 0→1 normalized

        // Public read-only properties
        public float DayProgress => dayProgress;
        public float DaylightFactor { get; private set; } // 0 = midnight, 1 = noon
        public Vector3 SunDirection { get; private set; }
        public float SunElevation { get; private set; }

        private readonly int cloudShadowIntensityId = Shader.PropertyToID("_CloudShadowIntensity");
        private readonly int daylightFactorId = Shader.PropertyToID("_DaylightFactor");
        private readonly int sunDirectionId = Shader.PropertyToID("_SunDirection");
    }
}
```

**Sun Trajectory Calculation**:

The sun follows a sinusoidal elevation arc over a 180° daylight arc (sunrise → noon → sunset). Azimuth sweeps 180° (east → south → west). This is a simplified daylight arc, not a full 360° day/night trajectory.

1. **Time tracking**: `elapsedTime += Time.deltaTime`; `dayProgress = (elapsedTime % settings.dayLength) / settings.dayLength`
2. **Azimuth** (90° = east, 180° = south/zenith, 270° = west): `Mathf.LerpUnclamped(90f, 270f, dayProgress)` — sunrise at 90° (east), passes overhead at 180° (south/zenith), sunset at 270° (west)
3. **Elevation** (180° daylight arc): `Mathf.LerpUnclamped(settings.sunElevationMin, settings.sunElevationMax, Mathf.Sin(dayProgress * Mathf.PI))`
   - Sinusoidal: `dayProgress=0` → elevation = `sunElevationMin` (default -10°, sun below horizon)
   - `dayProgress=0.5` → elevation = `sunElevationMax` (default 80°, near zenith)
   - `dayProgress=1` → elevation = `sunElevationMin` (sunset, sun below horizon)
   - At midnight (dayProgress=0 or 1): elevation = `sunElevationMin` (default -10°, sun is below horizon)
4. **Sun direction** (3D unit vector from azimuth + elevation, updated each frame):
    ```csharp
    float azimuthRad = azimuth * Mathf.Deg2Rad;
    float elevRad = elevation * Mathf.Deg2Rad;
    SunDirection = new Vector3(
        Mathf.Cos(azimuthRad) * Mathf.Cos(elevRad),
        Mathf.Sin(elevRad),
        Mathf.Sin(azimuthRad) * Mathf.Cos(elevRad)
    ).normalized;
    ```

**Sky Color Interpolation** (7-phase trilight):

| Phase | dayProgress Range | skyColor (ambientSkyColor) | equatorColor | groundColor |
|-------|-------------------|---------------------------|--------------|-------------|
| Night | 0.00–0.15 | nightSkyColor | nightSkyColor | nightSkyColor |
| Dawn | 0.15–0.25 | lerp(nightSkyColor, dawnHorizonColor, t) | daySkyColor | nightSkyColor |
| Sunrise | 0.25–0.35 | lerp(dawnHorizonColor, daySkyColor, t) | daySkyColor | dawnSkyColor |
| Daytime | 0.35–0.65 | daySkyColor | daySkyColor | daySkyColor |
| Sunset | 0.65–0.75 | lerp(daySkyColor, sunsetHorizonColor, t) | daySkyColor | sunsetHorizonColor |
| Twilight | 0.75–0.85 | lerp(sunsetHorizonColor, twilightSkyColor, t) | twilightSkyColor | twilightSkyColor |
| Night | 0.85–1.0 | lerp(twilightSkyColor, nightSkyColor, t) | twilightSkyColor | twilightSkyColor |

Each phase uses `t = (dayProgress - phaseStart) / (phaseEnd - phaseStart)` for linear interpolation.

**DaylightFactor** (0–1, used for ambient light scaling):

| Phase | dayProgress | DaylightFactor |
|-------|-------------|----------------|
| Night | 0.00–0.15 | 0.05 |
| Dawn | 0.15–0.25 | lerp(0.05, 0.3, t) |
| Sunrise | 0.25–0.35 | lerp(0.3, 0.8, t) |
| Daytime | 0.35–0.65 | 1.0 |
| Sunset | 0.65–0.75 | lerp(0.8, 0.2, t) |
| Twilight | 0.75–0.85 | lerp(0.2, 0.05, t) |
| Night | 0.85–1.0 | 0.05 |

**Ambient Light Calculation**:
- `finalLightIntensity = WeatherSystem.CurrentAmbientIntensity * DaylightFactor`
- Applied to `sunLight.intensity` each frame
- This ensures storm+night produces the darkest conditions (weather darkens × nighttime darkens)

**Shader Property Broadcasting** (each frame):
- `Shader.SetGlobalFloat("_CloudShadowIntensity", ...)` — from cloud coverage (shared with `CloudShadowController`)
- `Shader.SetGlobalFloat("_DaylightFactor", DaylightFactor)` — available to any shader that samples it
- `Shader.SetGlobalVector("_SunDirection", SunDirection)` — available to shadow/terrain shaders for N·L calculations

**Render Settings Update** (each frame):
```csharp
RenderSettings.ambientSkyColor = skyColor;
RenderSettings.ambientEquatorColor = equatorColor;
RenderSettings.ambientGroundColor = groundColor;
```

---

### Section 3: Cloud Shadow Projection

**File**: `Assets/Script/Environment/CloudShadowController.cs`

**Purpose**: Computes a single cloud shadow intensity value from `CloudManager` global state and applies it as shader properties for receiver materials. This is **global cloud-coverage darkening** — not spatially projected shadows. All surfaces and all clouds darken uniformly based on total cloud coverage.

**Namespace**: `JayFos.Environment`

**Architecture Decision**: No world-space shadow map. A single float `_CloudShadowIntensity` is set globally each frame (for any shader that reads it). Each cloud's `MeshRenderer` also receives the same intensity value (multiplied by that cloud's opacity). This produces uniform darkening of all receivers and clouds — no per-pixel or per-world-position shadow variation.

**Class definition**:

```csharp
namespace JayFos.Environment
{
    public class CloudShadowController : MonoBehaviour
    {
        [Header("References")]
        public CloudManager cloudManager; // existing from Phase 2.6
        public EnvironmentSettings environmentSettings;
        public Material cloudMaterial; // CloudMat.mat from Phase 2.6

        [Header("Shadow Settings")]
        [Tooltip("How much clouds darken surfaces when overhead.")]
        [Range(0f, 1f)]
        public float shadowIntensity = 0.4f;

        [Tooltip("Softness of shadow edges (0 = hard edge, 1 = fully soft).")]
        [Range(0f, 1f)]
        public float shadowSoftness = 0.3f;

        private readonly int cloudShadowId = Shader.PropertyToID("_CloudShadowIntensity");
        private readonly int shadowSoftnessId = Shader.PropertyToID("_ShadowSoftness");
        private readonly int daylightId = Shader.PropertyToID("_DaylightFactor");

        // Internal
        private float currentShadowIntensity = 0f;
    }
}
```

**Each Frame (`Update()`)**:

1. **Compute cloud coverage** from `CloudManager.settings.cloudCoverage` (0–1)
2. **Apply shadow intensity**: `currentShadowIntensity = Mathf.Lerp(currentShadowIntensity, coverage * shadowIntensity, Time.deltaTime * 5f)` (smooth transition)
3. **Set global shader properties**:
   - `Shader.SetGlobalFloat(cloudShadowId, currentShadowIntensity)`
   - `Shader.SetGlobalFloat(shadowSoftnessId, shadowSoftness)`
   - `Shader.SetGlobalFloat(daylightId, DayNightCycle?.DaylightFactor ?? 1f)`
4. **Darken cloud material directly**: Each cloud's `MeshRenderer` sets `material.SetFloat("_ShadowIntensity", currentShadowIntensity * 0.5f)` — the `ProceduralCloud.shader` already has `_CloudShadowColor` which lerps based on N·L; this extra `_ShadowIntensity` multiplies the shadow effect
5. **Update receivers**: Any material with `_CloudShadowIntensity` will read the global property. `URPWater.shader` uses it for direct darkening. Terrain materials must include `_CloudShadowIntensity` support (opt-in).

**URPWater.shader Extension** (existing file, extended):
- Add `_CloudShadowIntensity` (Range 0, 1) property
- In fragment shader, after base color computation:
  ```hlsl
  float shadow = _CloudShadowIntensity;
  baseColor *= (1.0 - shadow * 0.6);
  ```
- This darkens water proportionally to cloud coverage and shadow intensity setting

**Terrain Integration (opt-in)**:
- If a user's terrain shader includes `_CloudShadowIntensity`, it receives the global property automatically
- No automatic fallback — if the terrain shader does not include this property, cloud shadows have no effect on terrain (only on water and clouds)
- Documentation in `EnvironmentSettings` tooltip notes: "Cloud shadows affect water surfaces and cloud materials. Terrain must include _CloudShadowIntensity property in its shader for shadow reception."

---

### Section 4: Lightning & Thunder System

**File**: `Assets/Script/Environment/LightningManager.cs`
**File**: `Assets/Script/Environment/LightningBolt.cs`
**File**: `Assets/Script/Environment/LightningFlash.cs`
**File**: `Assets/Script/Environment/LightningAudio.cs`

**Purpose**: Generate visual lightning bolts during Storm weather state, with accompanying flash and thunder audio.

**Namespace**: `JayFos.Environment`

#### 4.1 `LightningManager`

```csharp
namespace JayFos.Environment
{
    public class LightningManager : MonoBehaviour
    {
        [Header("References")]
        public WeatherSystem weatherSystem;
        public EnvironmentSettings environmentSettings;

        [Header("Spawn Settings")]
        [Tooltip("Min seconds between lightning strikes during Storm state.")]
        [Range(0.5f, 5f)]
        public float minStrikeInterval = 1.5f;
        [Tooltip("Max seconds between lightning strikes during Storm state.")]
        [Range(1f, 10f)]
        public float maxStrikeInterval = 4f;
        [Tooltip("Intensity of each lightning flash (affects light source).")]
        [Range(1f, 10f)]
        public float flashIntensity = 4f;
        [Tooltip("Duration of each flash in seconds.")]
        [Range(0.05f, 0.5f)]
        public float flashDuration = 0.15f;

        [Header("Strike Volume")]
        [Tooltip("Horizontal radius around camera where strikes can occur.")]
        [Range(50f, 500f)]
        public float strikeRadius = 200f;
        [Tooltip("Height above terrain where bolts originate.")]
        public float boltOriginHeight = 50f;

        private float nextStrikeTime = 0f;
        private float currentInterval = 2.5f;

        public event System.Action<Vector3> OnLightningStrike;
    }
}
```

**Update Logic**:
1. **State check**: Only spawn bolts when `WeatherSystem.CurrentState == WeatherState.Storm`
2. **Timer**: `elapsedTime += Time.deltaTime`; when `elapsedTime >= currentInterval`, trigger a strike
3. **Random interval**: `currentInterval = Random.Range(minStrikeInterval, maxStrikeInterval)` each cycle
4. **Strike position**: `Vector3 strikePos = cameraPosition + Random.insideUnitSphere * strikeRadius; strikePos.y = boltOriginHeight;`
5. **Spawn bolt**: Instantiate or pool `LightningBolt` at `strikePos`, pointing down toward ground
6. **Trigger flash**: Spawn `LightningFlash` (brief `Light.intensity` boost) at strike position
7. **Trigger audio**: Play `LightningAudio` (thunder rumble) with `AudioSource.volume = Mathf.Clamp01(1f / distance)` for distance attenuation
8. **Fire event**: `OnLightningStrike?.Invoke(strikePos)` — terrain shaders can use this for momentary brightening

#### 4.2 `LightningBolt`

```csharp
namespace JayFos.Environment
{
    public class LightningBolt : MonoBehaviour
    {
        private LineRenderer[] branches;
        private float lifetime = 0.2f;
        private float elapsed = 0f;

        private static readonly int[] branchDepths = { 0, 1, 2 };
    }
}
```

**Geometry Generation**:
- **Level 0** (main bolt): Recursive subdivision from origin to target
  - Each segment: 2–3 branches at 30–60° angle, each branch has 0.3–0.7× the parent's length
  - Branch depth limited to 2 levels (controls complexity)
  - Each branch gets a `LineRenderer` with width tapering from 0.3 (root) to 0.05 (tip)
  - Color: white-hot (#ffffff) at root, blue-white (#aaccff) at tips
- **Flicker**: Each frame during lifetime, each `LineRenderer.positionCount` is randomly reduced by 10–30% to simulate flickering
- **Lifetime**: 0.15–0.25s (random). When elapsed >= lifetime, `SetActive(false)` and return to pool.

**Pooling**: Three `LightningBolt` instances are pre-instantiated as inactive children of `LightningManager`. On each strike, the next inactive bolt is retrieved, positioned, and `SetActive(true)`. When its lifetime expires, it is `SetActive(false)` and returned to the pool. No `Instantiate`/`Destroy` calls occur during runtime — all allocations happen at `Awake()` or `Initialize()`.

#### 4.3 `LightningFlash`

- Single `Light` component (directional or point), intensity = `flashIntensity` for `flashDuration` seconds
- Uses coroutine: `StartCoroutine(FlashFade())`
- After fade-out, `Destroy(gameObject)`

#### 4.4 `LightningAudio`

- `AudioSource` with a thunder rumble clip (assigned in inspector)
- `AudioSource.spatialize = true`, `AudioSource.spatialBlend = 1f`
- `AudioSource.volume = Mathf.Clamp01(1f / Vector3.Distance(cameraPosition, strikePosition) * 3f)` (louder when closer)
- `AudioSource.Play()` on spawn, `Destroy(gameObject)` after clip ends (`AudioSource.GetOutputData` or `time > clip.length`)

---

### Section 5: Snow System (Temperature-Driven)

**File**: `Assets/Script/Environment/SnowSystem.cs`

**Purpose**: Snow particles that activate when temperature drops below each biome's `temperature` threshold. Reuses `RainSystem` architecture — same shape, same emission pattern, different particle properties.

**Namespace**: `JayFos.Environment`

**Class definition**:

```csharp
namespace JayFos.Environment
{
    public class SnowSystem : MonoBehaviour
    {
        [Header("References")]
        public WeatherSystem weatherSystem;
        public BiomeMap biomeMap;

        [Header("Snow Parameters")]
        [Tooltip("Maximum number of snow particles.")]
        [Range(100, 2000)]
        public int snowParticleCount = 1000;
        [Tooltip("Height of the snow volume in world units.")]
        [Range(5f, 100f)]
        public float snowHeight = 30f;
        [Tooltip("Horizontal radius of the snow volume.")]
        [Range(5f, 100f)]
        public float snowRadius = 20f;
        [Tooltip("Base fall speed of snowflakes.")]
        [Range(1f, 10f)]
        public float snowSpeed = 5f;
        [Tooltip("Minimum particle size.")]
        [Range(0.01f, 0.1f)]
        public float snowflakeSize = 0.03f;
        [Tooltip("Material with snowflake texture (white, soft, low alpha).")]
        public Material snowMaterial;

        [Header("Temperature")]
        [Tooltip("Temperature ceiling (0-1 scale). Snow activates when the effective temperature (global or biome-derived) is below this value. Higher = more snow conditions.")]
        [Range(-1f, 1f)]
        public float snowThreshold = 0.5f;

        private ParticleSystem mainPS;
        private ParticleSystemRenderer psRenderer;
        private ParticleSystem.EmissionModule emission;
        private ParticleSystem.VelocityOverLifetimeModule velocity;
        private ParticleSystem.MainModule main;

        private Camera cameraTransform;
    }
}
```

**Initialization** (`Awake()`):
1. Create a `GameObject("SnowSystem")` as a child of the `WeatherSystem` GameObject
2. Add `ParticleSystem` component
3. Configure `ParticleSystem.MainModule` (accessed via `main` property):
    - `main.maxParticles = snowParticleCount`
    - `main.simulationSpace = ParticleSystemSimulationSpace.Camera` (follows camera like rain)
    - `main.duration = 1f` (continuous loop)
    - `main.startDelay = 0f`
4. Configure `ParticleSystem.EmissionModule` (accessed via `emission` property):
    - `emission.enabled = true`
    - `emission.rateOverTime = snowParticleCount * weatherSystem.CurrentRainIntensity` (same pattern as rain)
5. Configure `ParticleSystem.VelocityOverLifetimeModule` (accessed via `velocity` property):
    - `velocity.enabled = true`
    - `velocity.x = new ParticleSystem.MinMaxCurve(0.5f * windMultiplier, 0.5f * windMultiplier)` (wind-driven horizontal drift)
    - `velocity.y = -snowSpeed` (gentle downward fall, constant)
    - `velocity.z = new ParticleSystem.MinMaxCurve(0.3f * windMultiplier, 0.3f * windMultiplier)` (wind-driven)
    - `velocity.scale = 1f` (no size change over lifetime)
6. Configure `ParticleSystem.Renderer`:
    - `psRenderer.material = snowMaterial` (assigned in inspector)
    - `psRenderer.renderMode = ParticleSystemRenderMode.Billboard` (snowflakes fall, don't streak)
    - `psRenderer.velocityScale = snowflakeSize / snowSpeed`
    - `psRenderer.sortingFudge = 0.1f` (depth sorting)

**Shape** (same as rain):
- `ParticleSystem.ShapeModule`:
  - `shapeType = ParticleSystemShapeType.Box`
  - `boxX = snowRadius * 2`
  - `boxY = 0.1f`
  - `boxZ = snowRadius * 2`

**Each Frame Update** (`LateUpdate()`):
1. **Emission rate**: `emission.rateOverTime = snowParticleCount * (weatherSystem.CurrentRainIntensity * 0.7f)` (slightly less intense than rain)
2. **Wind drift**: Update `velocity.x` and `velocity.z` from `weatherSystem.CurrentWindMultiplier`
3. **Radius**: Match camera position (like rain)

**Toggle Logic** (in `WeatherSystem.ApplyToSystems()`):

`CurrentTemperature` holds the **final effective temperature** after biome adjustment. Snow/rain toggle is the single authority. `SnowSystem` does not set its own `enabled` state — it is driven entirely by `WeatherSystem`.

```csharp
// Existing: rain intensity drives RainSystem
rainSystem.enabled = currentRainIntensity > 0.01f;

// Phase 2.8: temperature drives SnowSystem
// Compute base temperature from the curve
float globalTemp = environmentSettings.temperatureCurve.Evaluate(
    dayNightCycle != null ? dayNightCycle.DayProgress : 0.5f);

// Biome override: if biomeMap exists, use biome-specific temperature
float effectiveTemp = globalTemp;
if (biomeMap != null && Camera.main != null)
{
    Vector3 camPos = Camera.main.transform.position;
    BiomeDefinition biome = biomeMap.GetBiome(camPos.x, camPos.z);
    if (biome != null)
    {
        effectiveTemp = biome.temperature; // biome temperature overrides global
    }
}

// CurrentTemperature = final effective temperature after biome adjustment
weatherSystem.CurrentTemperature = effectiveTemp;

// Snow activates when effective temperature is below threshold
// (snowThreshold is a 0-1 ceiling; lower = less snow-friendly, higher = more snow-friendly)
bool shouldSnow = effectiveTemp < snowThreshold && environmentSettings.enableSnow;

if (snowSystem != null)
{
    snowSystem.enabled = shouldSnow;
}

// Rain is disabled during snow to prevent mixed precipitation artifacts
if (currentRainIntensity > 0.01f)
{
    rainSystem.enabled = !shouldSnow;
}
```

**snowThreshold validation** (on `SnowSystem`):

```csharp
[Tooltip("Temperature ceiling (0-1 scale). Snow activates when effective temperature is below this value.")]
[Range(0f, 1f)] // enforced 0-1 domain
public float snowThreshold = 0.5f;
```

**Key differences from RainSystem**:

| Property | RainSystem | SnowSystem |
|----------|-----------|------------|
| `renderMode` | `Stretch` | `Billboard` |
| `startSpeed` (y) | 15 (fast) | 5 (slow, gentle fall) |
| `startLifetime` | rainHeight / rainSpeed (≈2s) | snowHeight / snowSpeed (≈6s, longer drift) |
| `particleCount` | up to 2000 | up to 1000 (less dense) |
| `velocity.x/z` | windMultiplier * 0.5/0.3 | windMultiplier * 0.3/0.2 (less aggressive drift) |
| `shape.boxY` | 0.1f (flat rain sheet) | 0.5f (deeper snow volume) |
| `material` | RainParticle shader (dark streaks) | Snow texture (white, soft, low alpha) |

---

### Section 6: Wind-Driven Foliage Animation

**File**: `Assets/Script/Environment/WindFoliageController.cs`
**File**: `Assets/Script/Shaders/Foliage/WindFoliage.shader`

**Purpose**: Apply wind-driven vertex displacement to foliage instances using a shader-based approach (no CPU bone updates).

**Namespace**: `JayFos.Environment`

#### 6.1 `WindFoliageController`

```csharp
namespace JayFos.Environment
{
    public class WindFoliageController : MonoBehaviour
    {
        [Header("References")]
        public WeatherSystem weatherSystem;
        public EnvironmentSettings environmentSettings;
        public DayNightCycle dayNightCycle;

        [Header("Wind Settings")]
        [Tooltip("Base wind speed (how fast the sway oscillates).")]
        [Range(0.5f, 5f)]
        public float windSpeed = 1.5f;
        [Tooltip("Maximum sway amplitude at wind strength 1.0.")]
        [Range(0f, 2f)]
        public float maxSway = 0.5f;
        [Tooltip("Height at which sway tapers to zero (base of foliage).")]
        [Range(0f, 5f)]
        public float taperHeight = 1f;

        private readonly int windStrengthId = Shader.PropertyToID("_WindStrength");
        private readonly int windSpeedId = Shader.PropertyToID("_WindSpeed");
        private readonly int windDirectionId = Shader.PropertyToID("_WindDirection");

        // Cached MaterialPropertyBlock — allocated in Awake(), reused each frame (zero GC alloc at runtime)
        private MaterialPropertyBlock mbCache;
    }
}
```

**Initialization** (`Awake()`):
- `mbCache = new MaterialPropertyBlock();` — allocated once; reused each `Update()` frame for zero GC alloc.

**Each Frame** (`Update()`):
1. **Get wind force**: `float windForce = weatherSystem.CurrentWindForce` (from `WeatherSystem`, 0–1)
2. **Apply to global shader properties**:
    - `Shader.SetGlobalFloat(windStrengthId, windForce * maxSway)`
    - `Shader.SetGlobalFloat(windSpeedId, windSpeed)`
3. **Apply to foliage instances**: Reuse a single cached `MaterialPropertyBlock` — allocated once in `Awake()`, reused each frame:
    ```csharp
    // mbCache allocated in Awake() — zero runtime allocations
    foreach (var renderer in foliageRenderers)
    {
        mbCache.SetFloat(windStrengthId, windForce * maxSway);
        mbCache.SetFloat(windSpeedId, windSpeed);
        renderer.SetPropertyBlock(mbCache);
    }
    ```
4. **Performance**: Cache `foliageRenderers` list, update each frame. `mbCache` is allocated once in `Awake()` — zero GC allocations during runtime.

**Foliage discovery**: `WindFoliageController` builds `foliageRenderers` once on `Awake()` by scanning children of `Chunk` GameObjects. Each discovered `MeshRenderer` registers itself via `FoliageRendererRegistry.Add(renderer)`. When a chunk is disabled/destroyed, `FoliageGenerator` (or each chunk's `OnDestroy`) calls `FoliageRendererRegistry.Remove(renderer)`. The controller reads from this registry each frame — no per-frame hierarchy scanning or `FindComponent`s.

#### 6.2 `WindFoliage.shader`

**File**: `Assets/Script/Shaders/Foliage/WindFoliage.shader`

**Purpose**: Simple URP foliage shader with vertex displacement. Serves as a **reference shader** — any foliage shader that includes `_WindStrength`/`_WindSpeed` properties will work with `WindFoliageController`.

```hlsl
Shader "JayFos/Foliage/WindFoliage"
{
    Properties
    {
        _BaseMap ("Base Map (RGB), Alpha (A)", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (0.2, 0.6, 0.15, 1)
        _WindStrength ("Wind Sway Strength", Range(0, 2)) = 0.5
        _WindSpeed ("Wind Speed", Range(0.5, 5)) = 1.5
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "IgnoreProjector"="True" }

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            TEXTURES Begin
                TEXTURE_2D(_BaseMap)
            END

            "BaseMap", _BaseMap)
            TEXTURE_2D_ARRAY(
                "BaseColor", _BaseColor
            )

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;       // R = height influence (taper)
                float3 normalOS     : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS   : POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
                float3 normalWS     : NORMAL;
                float3 positionWS   : texcoord1;
            };

            CBUFFER Start
                uniform float4 _BaseColor;
                uniform float _WindStrength;
                uniform float _WindSpeed;
                uniform float4 _WindDirection; // xy = direction, z = strength multiplier
            END

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                o.uv = v.uv;
                o.color = v.color;
                o.normalWS = TransformObjectToWorldDir(v.normalOS);

                // Vertex displacement: wind sway
                float heightFactor = saturate(v.color.r * 2.0); // R channel = height influence
                float windOffset = sin(_Time.y * _WindSpeed + positionWS.y * 0.5) * _WindStrength * heightFactor;
                float windOffsetZ = cos(_Time.y * _WindSpeed + positionWS.y * 0.3) * _WindStrength * heightFactor * 0.5;

                // Apply lateral displacement
                float3 positionWS = v.positionOS;
                positionWS.x += windOffset;
                positionWS.z += windOffsetZ;

                o.positionCS = TransformWorldToHClip(positionWS);
                return o;
            }

            half4 Frag(Varyings i) : TARGET
            {
                half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a;
                half color = SAMPLE_TEXTURE2D(_BaseMap, s = BaseMap, s, i.uv).rgb * _BaseColor.rgb;
                clip(alpha - 0.5);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
```

**Key design decisions**:
- **R channel = height influence**: Each vertex's color R determines sway amount (1.0 = top, 0.0 = base). This is set by the foliage prefab's vertex data.
- **Two sine waves**: X and Z use phase-shifted sine/cosine for natural-looking wind sway
- **Taper**: `heightFactor = saturate(v.color.r * 2.0)` ensures the base doesn't sway, only the top
- **URP-compatible**: Uses `TransparentCutout` queue, alpha clipping, URP `Core.hlsl` includes

**Reuse pattern**: The `WindFoliageController` sets `_WindStrength`/`_WindSpeed` on any `MeshRenderer` with those properties. The `WindFoliage.shader` is a **reference implementation** — existing foliage prefabs can be updated to include these properties without changing the controller.

---

### Section 7: Atmospheric Depth

**File**: No new file — uses existing `RenderSettings` and `EnvironmentSettings`.

**Purpose**: Depth perception through layered fog, sky gradient (trilight), and horizon haze.

**Architecture**: Uses existing `WeatherSystem` fog density, `DayNightCycle` trilight colors, and `RenderSettings.fogColor` for haze tint.

**Each Frame** (in `DayNightCycle.Update()` or `WeatherSystem.ApplyToSystems()`):

1. **Fog mode**: `RenderSettings.fog = environmentSettings.enableAtmosphericDepth`
2. **Fog mode**: `RenderSettings.fogMode = LightFogMode.ExponentialSquared`
3. **Fog density**: `RenderSettings.fogDensity = Mathf.Lerp(environmentSettings.fogDensityBase, environmentSettings.fogDensityMax, weatherSystem.CurrentFogDensity)`
4. **Fog color** (haze tint):
   ```csharp
   Color hazeColor;
   if (daylightFactor > 0.5f)
   {
       // During day: neutral, slightly warm haze
       hazeColor = Color.Lerp(new Color(0.8f, 0.85f, 0.9f), new Color(0.9f, 0.92f, 0.88f), (daylightFactor - 0.5f) * 2f);
   }
   else
   {
       // During night/sunset: cool blue or warm orange haze
       hazeColor = Color.Lerp(new Color(0.1f, 0.12f, 0.2f), new Color(0.6f, 0.3f, 0.15f), daylightFactor * 2f);
   }
   RenderSettings.fogColor = hazeColor;
   ```
5. **Sky gradient** (trilight, already in Section 2):
   - `RenderSettings.ambientSkyColor` — top sky color
   - `RenderSettings.ambientEquatorColor` — horizon/mid-sky color
   - `RenderSettings.ambientGroundColor` — ground/horizon color

**Result**: A full atmospheric depth system using only `RenderSettings` properties — no custom render passes or volume profiles required.

---

## 3. Integration Points

### 3.1 `WeatherSystem` Extensions (existing file, extended)

Additions to `Assets/Script/Cloud/WeatherSystem.cs`:

```csharp
// NEW fields
public float CurrentTemperature { get; private set; }
public float CurrentDaylightFactor { get; private set; }

// NEW: reference to EnvironmentSettings
[Header("Phase 2.8 References")]
public EnvironmentSettings environmentSettings;
public DayNightCycle dayNightCycle;
public SnowSystem snowSystem;

// MODIFIED: ApplyToSystems() — each frame
private void ApplyToSystems()
{
    // ... existing: cloudManager, fog, mainLight.intensity

    // Phase 2.8: Update daylight factor from DayNightCycle
    if (dayNightCycle != null)
    {
        CurrentDaylightFactor = dayNightCycle.DaylightFactor;
    }

    // Phase 2.8: Combined light intensity
    if (mainLight != null)
    {
        mainLight.intensity *= CurrentDaylightFactor;
    }

    // Phase 2.8: Temperature-driven snow/rain toggle (single authority)
    if (snowSystem != null && environmentSettings != null)
    {
        // Compute base temperature from the curve
        float globalTemp = environmentSettings.temperatureCurve.Evaluate(
            dayNightCycle != null ? dayNightCycle.DayProgress : 0.5f);

        // Biome override: if biomeMap exists, use biome-specific temperature
        float effectiveTemp = globalTemp;
        if (biomeMap != null && Camera.main != null)
        {
            Vector3 camPos = Camera.main.transform.position;
            BiomeDefinition biome = biomeMap.GetBiome(camPos.x, camPos.z);
            if (biome != null)
            {
                effectiveTemp = biome.temperature; // biome temperature overrides global
            }
        }

        // CurrentTemperature = final effective temperature after biome adjustment
        CurrentTemperature = effectiveTemp;

        // Snow activates when effective temperature is below threshold (0-1 ceiling)
        bool shouldSnow = effectiveTemp < snowThreshold && environmentSettings.enableSnow;
        snowSystem.enabled = shouldSnow;

        // Rain is disabled during snow to prevent mixed precipitation artifacts
        if (currentRainIntensity > 0.01f)
        {
            rainSystem.enabled = !shouldSnow;
        }
    }
}
```

### 3.2 `WorldManager` Extensions (existing file, extended)

Additions to `Assets/Script/WorldManager.cs`:

```csharp
// NEW fields
private DayNightCycle dayNightCycle;
private CloudShadowController cloudShadowController;
private LightningManager lightningManager;
private SnowSystem snowSystem;
private WindFoliageController windFoliageController;

// MODIFIED: Initialize() — create all new systems
private void Initialize()
{
    // ... existing initialization (CloudManager, WeatherSystem, ChunkManager)

    // Phase 2.8: Create environmental systems
    if (environmentSettings != null)
    {
        if (environmentSettings.enableDayNightCycle)
        {
            var dnc = gameObject.AddComponent<DayNightCycle>();
            dnc.settings = environmentSettings;
            dnc.weatherSystem = weatherSystem;
            dnc.sunLight = FindOrCreateSunLight();
            dayNightCycle = dnc;
        }

        if (environmentSettings.enableCloudShadows)
        {
            var csc = gameObject.AddComponent<CloudShadowController>();
            csc.cloudManager = cloudManager;
            csc.environmentSettings = environmentSettings;
            csc.cloudMaterial = cloudSettings?.cloudMaterial;
            cloudShadowController = csc;
        }

        if (environmentSettings.enableLightning)
        {
            var lm = gameObject.AddComponent<LightningManager>();
            lm.weatherSystem = weatherSystem;
            lm.environmentSettings = environmentSettings;
            lm.cameraTransform = Camera.main?.transform;
            lightningManager = lm;
        }

        if (environmentSettings.enableSnow)
        {
            var ss = new GameObject("SnowSystem");
            ss.transform.SetParent(transform, false);
            snowSystem = ss.AddComponent<SnowSystem>();
            snowSystem.weatherSystem = weatherSystem;
            snowSystem.biomeMap = biomeMap;
            snowSystem.environmentSettings = environmentSettings;
        }

        if (environmentSettings.enableWindFoliage)
        {
            var wfc = gameObject.AddComponent<WindFoliageController>();
            wfc.weatherSystem = weatherSystem;
            wfc.environmentSettings = environmentSettings;
            wfc.dayNightCycle = dayNightCycle;
            windFoliageController = wfc;
        }
    }
}

// HELPER: Create a sun light if none exists
private Light FindOrCreateSunLight()
{
    Light[] lights = FindObjectsOfType<Light>();
    foreach (var light in lights)
    {
        if (light.type == LightType.Directional && light.gameObject.name.Contains("Sun", System.StringComparison.OrdinalIgnoreCase))
            return light;
    }
    // Create a sun light
    var sunGO = new GameObject("Sun");
    sunGO.transform.position = Vector3.zero;
    var sunLight = sunGO.AddComponent<Light>();
    sunLight.type = LightType.Directional;
    sunLight.intensity = 1f;
    sunLight.color = Color.white;
    return sunLight;
}
```

### 3.3 `WorldSettings` Extensions (existing file, extended)

Additions to `Assets/Script/WorldSettings.cs`:

```csharp
// NEW field
[Header("Phase 2.8")]
public EnvironmentSettings environmentSettings;
```

### 3.4 `Chunk` Extensions (existing file, extended)

Additions to `Assets/Script/Chunk.cs` — each chunk's terrain material receives cloud shadow via global property:

```csharp
// In UpdateForCoord(), after mesh generation:
if (meshRenderer != null)
{
    meshRenderer.sharedMaterial = settings.terrainMaterial;

    // Phase 2.8: If terrain material supports cloud shadows, set per-instance shadow intensity
    if (settings.environmentSettings != null && settings.environmentSettings.enableCloudShadows)
    {
        var mb = new MaterialPropertyBlock();
        mb.SetFloat(Shader.PropertyToID("_CloudShadowIntensity"),
            cloudShadowController != null ? cloudShadowController.currentShadowIntensity : 0f);
        meshRenderer.SetPropertyBlock(mb);
    }
}
```

### 3.5 `CloudRenderer` Extensions (existing file, extended)

Additions to `Assets/Script/Cloud/CloudRenderer.cs` — each cloud passes shadow intensity to its material:

```csharp
// In SetClouds(), for each active cloud:
foreach (var cloud in activeClouds)
{
    // ... existing position/rotation/scaling ...

    // Phase 2.8: Pass shadow intensity to each cloud's material
    if (cloudShadowIntensity > 0f)
    {
        mr.material.SetFloat("_ShadowIntensity", cloud.opacity * cloudShadowIntensity);
    }
}
```

---

## 4. New Files Summary

| # | File Path | Type | Purpose |
|---|-----------|------|---------|
| 1 | `Assets/Script/Environment/EnvironmentSettings.cs` | ScriptableObject | Central config for all Phase 2.8 features |
| 2 | `Assets/Script/Environment/DayNightCycle.cs` | MonoBehaviour | Sun orbit, sky color trilight, daylight factor (0–1) |
| 3 | `Assets/Script/Environment/CloudShadowController.cs` | MonoBehaviour | Sets `_CloudShadowIntensity` global/per-instance for receivers |
| 4 | `Assets/Script/Environment/LightningManager.cs` | MonoBehaviour | Storm-state lightning trigger |
| 5 | `Assets/Script/Environment/LightningBolt.cs` | MonoBehaviour | Branching bolt geometry (LineRenderer) |
| 6 | `Assets/Script/Environment/LightningFlash.cs` | MonoBehaviour | Brief light flash at strike point |
| 7 | `Assets/Script/Environment/LightningAudio.cs` | MonoBehaviour | Distance-attenuated thunder rumble |
| 8 | `Assets/Script/Environment/SnowSystem.cs` | MonoBehaviour | Temperature-driven snow (extends RainSystem pattern) |
| 9 | `Assets/Script/Environment/WindFoliageController.cs` | MonoBehaviour | Sets `_WindStrength`/`_WindSpeed` |
| 10 | `Assets/Script/Shaders/Foliage/WindFoliage.shader` | Shader | URP foliage shader with vertex displacement |

## 5. Modified Files Summary

| # | File Path | Changes |
|---|-----------|---------|
| 1 | `Assets/Script/WorldSettings.cs` | Add `EnvironmentSettings environmentSettings` field |
| 2 | `Assets/Script/WorldManager.cs` | Instantiate all new systems; add `FindOrCreateSunLight()` |
| 3 | `Assets/Script/Cloud/WeatherSystem.cs` | Add `CurrentTemperature`, `CurrentDaylightFactor` properties; extend `ApplyToSystems()` |
| 4 | `Assets/Script/Cloud/CloudRenderer.cs` | Pass `_ShadowIntensity` to each cloud's material per frame |
| 5 | `Assets/Script/Chunk.cs` | Set `_CloudShadowIntensity` on terrain material per-instance (opt-in) |
| 6 | `Assets/Script/Shaders/Weather/URPWater.shader` | Add `_CloudShadowIntensity` darkening in fragment shader |

## 6. Performance Considerations

| Feature | Performance Impact | Mitigation | Implementation-Verifiable Criterion |
|---------|-------------------|------------|-------------------------------------|
| **Cloud Shadows** | Minimal — one `Shader.SetGlobalFloat` + N `MeshRenderer.material.SetFloat` calls per frame | Global property set once/frame; per-instance only on cloud MeshRenderers (already one per cloud, ~50–100) | Profiler: `Scripting.Wait` ≤ 0.1ms/frame with shadows enabled |
| **Day/Night Cycle** | Minimal — `sin`/`lerp` each frame (CPU) | Two `Mathf.Sin`, two `Mathf.LerpUnclamped`, three `Color.Lerp` — all < 1µs on modern CPU | Baseline: `DayNightCycle.Update()` body measured with `UnityEngine.Profiling.ProfilerMarker("DayNightCycle.Update").Scoped()`; compare against project-specific performance budget (no hard target; documented in implementation notes) |
| **Lightning** | Moderate — 3 simultaneous `LineRenderer`s (each 3–5 branches) | Object-pooled (max 3); each `SetActive(false)` after 0.2s — zero `Instantiate`/`Destroy` during runtime | GC: 0 allocations during sustained lightning (verified with Profiler > Alloc. Tracker) |
| **Snow** | Moderate — up to 1000 particles, `Billboard` render mode | Same pattern as `RainSystem` (2000 particles); particle count halved; `simulationSpace = Camera` | Particle count ≤ 1000 (verified in Profiler > GPU > Particle System) |
| **Wind Foliage** | Moderate — `MaterialPropertyBlock.SetFloat` per foliage `MeshRenderer` each frame | Registry pattern: `foliageRenderers` built once on `Awake()`; each MPB = one float set; ~100–300 instances/chunk; single MPB reused per frame | Each `WindFoliageController.Update()` body ≤ 0.5ms (measured with `UnityEngine.Profiling.ProfilerMarker("WindFoliageController.Update").Scoped()`) |
| **Atmospheric Depth** | Minimal — three `RenderSettings` assignments each frame | No new draw calls; no shader changes; `RenderSettings` writes are cheap | Draw call delta = 0 (Profiler > Graphics > Frame Debugger) |

**Total new draw calls**: 0 — verified by comparing a frame with all features enabled vs. all features disabled in Unity Profiler (Graphics > Frame Debugger); delta draw calls must equal 0.

**Total new scripts**: 10 (all lightweight; each MonoBehaviour < 150 lines, each Shader < 100 HLSL lines)

**GC impact**: Zero runtime allocations — all new scripts use either: (a) `SetPropertyBlock` (no GC), (b) `Shader.SetGlobalFloat` (no GC), (c) pre-pooled `LineRenderer`s (no `Instantiate`/`Destroy`), (d) `ParticleSystem` modules set once in `Awake()`, or (e) `RenderSettings` writes. Verified with Profiler > Alloc. Tracker set to "Every Frame" — no allocations attributed to Phase 2.8 scripts.

---

## 7. Feature Toggle Summary

Each feature can be independently disabled via `EnvironmentSettings` flags:

| Feature | Toggle Field | Default |
|---------|-------------|---------|
| Day/Night Cycle | `enableDayNightCycle` | `true` |
| Cloud Shadows | `enableCloudShadows` | `true` |
| Lightning & Thunder | `enableLightning` | `true` |
| Snow | `enableSnow` | `true` |
| Wind Foliage | `enableWindFoliage` | `true` |
| Atmospheric Depth | `enableAtmosphericDepth` | `true` |

Each toggle is checked in the respective system's `Update()`/`LateUpdate()`. When disabled, the system does nothing each frame (zero CPU/GPU cost beyond the component itself).

---

## 8. Implementation Order (Suggested)

For efficient development and testing, implement in this order:

1. **Section 1**: `EnvironmentSettings` (foundation config SO — needed by all other systems)
2. **Section 2**: `DayNightCycle` (core temporal system — other features depend on daylight factor)
3. **Section 3**: `CloudShadowController` (lightweight, extends existing cloud system)
4. **Section 7**: Atmospheric Depth (simplest — uses RenderSettings, no new files)
5. **Section 4**: `LightningManager` + `LightningBolt` (visual impact, good milestone)
6. **Section 5**: `SnowSystem` (extends RainSystem pattern)
7. **Section 6**: `WindFoliageController` + `WindFoliage.shader` (deepest integration — foliage shader + controller)

Each step produces a working, testable result.

---

## 9. Acceptance Criteria

Each Phase 2.8 feature is considered COMPLETE when:

- [ ] All code compiles without errors or warnings
- [ ] Each feature is toggleable via `EnvironmentSettings` flags
- [ ] `WorldManager` instantiates all enabled systems automatically
- [ ] `WeatherSystem` integrates new properties (`CurrentTemperature`, `CurrentDaylightFactor`)
- [ ] `URPWater.shader` darkens with cloud shadows (tested in scene with water + clouds)
- [ ] `DayNightCycle` produces a full 360° sun trajectory with 7-color sky phases
- [ ] Lightning bolts spawn during Storm state and fade within 0.2s
- [ ] Snow activates when `CurrentTemperature < snowThreshold` (verified with cold biome; snowThreshold is a ceiling — lower temperatures trigger snow)
- [ ] Foliage sways with wind force (verified with `WindFoliageController` on foliage prefabs)
- [ ] `RenderSettings.fogDensity` and `RenderSettings.fogColor` shift with weather state + time of day
- [ ] No new draw calls introduced — verified in Unity Profiler (Graphics > Frame Debugger) by comparing a frame with all features enabled vs. all features disabled; delta draw calls ≤ 0
- [ ] All new files use `JayFos.Environment` or `JayFos.Shaders.Foliage` namespace

---

## 10. Known Limitations

1. **Terrain cloud shadows are opt-in**: `CloudShadowController` sets `_CloudShadowIntensity` as a global property. Terrain materials must include `_CloudShadowIntensity` in their shader code to receive shadows. This is intentional — the project's terrain shader is user-assigned via `WorldSettings.terrainMaterial`, not hard-coded to any single shader.

2. **Foliage shader is a reference implementation**: `WindFoliage.shader` serves as a template. Existing foliage prefabs can use any shader that includes `_WindStrength`/`_WindSpeed` properties. The `WindFoliageController` sets these properties on all foliage `MeshRenderer`s each frame.

3. **Snow uses Billboard render mode**: Snow particles render as billboard quads (not streaked like rain). This is intentional — snowflakes fall slowly and don't stretch into streaks. If a streaked snow effect is desired, the render mode can be changed in `SnowSystem`.

4. **Sky uses trilight (not full 24h gradient)**: The sky color uses 7 simplified phases (night → dawn → sunrise → day → sunset → twilight → night) rather than a continuous 24h color curve. This reduces the number of color keys and interpolation ranges while still providing visually coherent transitions.

5. **Lightning bolts are 2D (not 3D volumetric)**: Each bolt uses `LineRenderer` with multiple branches. True volumetric lightning would require instanced mesh rendering or a custom compute shader. The LineRenderer approach is the simplest URP-compatible solution that still looks visually convincing.

6. **Cloud shadow intensity is uniform (global coverage darkening)**: All clouds contribute to a single `_CloudShadowIntensity` value derived from `CloudManager.settings.cloudCoverage`. Every receiver (water, terrain, clouds) darkens by the same amount each frame — there is no spatial variation (no projected shadow shapes, no per-cloud position data). This is a design choice: it avoids per-cloud shadow maps and per-fragment shadow calculations, keeping the cost at one `Shader.SetGlobalFloat` per frame.
