using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using JayFos.Foliage;
using JayFos.Biomes;
using JayFos.World;

namespace JayFos.Editor
{
    [InitializeOnLoad]
    public static class BiomeContentBootstrap
    {
        private const string GUARD_KEY = "BiomeContentBootstrap_Run_v3";
        private static readonly string[] SEARCH_TERMS =
        {
            "Plains_TerrainParams", "Forest_TerrainParams", "Mountain_TerrainParams",
            "Desert_TerrainParams", "Swamp_TerrainParams", "Tundra_TerrainParams",
            "Snow_TerrainParams", "Ocean_TerrainParams"
        };

        static BiomeContentBootstrap()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorPrefs.GetBool(GUARD_KEY, false)) return;
                EditorPrefs.SetBool(GUARD_KEY, true);
                AssetDatabase.SaveAssets();

                LogBootstrapProgress("Creating biome content assets...", 1);
                CreateFoliageConfigs();
                LogBootstrapProgress("Creating terrain params...", 1);
                CreateBiomeTerrainParams();
                LogBootstrapProgress("Creating spawn rules...", 1);
                CreateFoliageSpawnRules();
                LogBootstrapProgress("Creating biome definitions...", 1);
                CreateBiomeDefinitions();
                LogBootstrapProgress("Configuring WorldSettings...", 1);
                ConfigureWorldSettings();
                LogBootstrapProgress("Done.", 1);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            };
        }

        private static void LogBootstrapProgress(string msg, int indent)
        {
            string prefix = new string(' ', indent * 2);
            Debug.Log($"{prefix}{msg}");
        }

        private static void CreateFoliageConfigs()
        {
            CreateFoliageConfig("Plains_FoliageConfig", 1.2f, 0.3f, 4, 0.5f, 2f, Vector2.zero, 1.0f);
            CreateFoliageConfig("Forest_FoliageConfig",   2.5f, 0.25f, 5, 0.6f, 2.2f, new Vector2(10f, 20f), 1.2f);
            CreateFoliageConfig("Mountain_FoliageConfig", 0.5f, 0.45f, 4, 0.55f, 2f, new Vector2(5f, 5f), 1.5f);

            CreateFoliageConfig("Desert_FoliageConfig", 0.3f, 0.5f, 3, 0.4f, 2.5f, new Vector2(8f, 12f), 0.6f);
            CreateFoliageConfig("Swamp_FoliageConfig",  1.8f, 0.2f, 4, 0.5f, 2f, new Vector2(15f, 15f), 1.3f);
            CreateFoliageConfig("Tundra_FoliageConfig", 0.2f, 0.6f, 3, 0.45f, 2.2f, new Vector2(7f, 9f), 0.8f);
            CreateFoliageConfig("Snow_FoliageConfig",   0.1f, 0.7f, 2, 0.3f, 2.5f, new Vector2(6f, 6f), 1.0f);
        }

        private static void CreateFoliageConfig(string name, float density, float threshold, int octaves, float persistence, float lacunarity, Vector2 offset, float heightMult)
        {
            string path = $"Assets/Script/BiomeContent/{name}.asset";
            if (File.Exists(path)) return;

            var cfg = ScriptableObject.CreateInstance<FoliageConfig>();
            cfg.densityPerUnitArea = density;
            cfg.noiseThreshold = threshold;
            cfg.octaves = octaves;
            cfg.persistence = persistence;
            cfg.lacunarity = lacunarity;
            cfg.noiseOffset = offset;
            cfg.heightMultiplier = heightMult;
            AssetDatabase.CreateAsset(cfg, path);
        }

        private static void CreateBiomeTerrainParams()
        {
            CreateTerrainParams("Plains_TerrainParams", 0.03f, 15f, 4, 0.5f, 2f, 1.2f, 0.35f, -5f, false);
            CreateTerrainParams("Forest_TerrainParams",   0.025f, 20f, 5, 0.6f, 2.2f, 2.5f, 0.25f, -3f, true);
            CreateTerrainParams("Mountain_TerrainParams", 0.04f, 35f, 6, 0.55f, 2f, 0.5f, 0.45f, -2f, true);

            CreateTerrainParams("Desert_TerrainParams",  0.035f, 10f, 4, 0.6f, 2.2f, 0.3f, 0.5f, -5f, false);
            CreateTerrainParams("Swamp_TerrainParams",   0.02f, 8f,  3, 0.4f, 1.8f, 1.5f, 0.15f, -1f, true);
            CreateTerrainParams("Tundra_TerrainParams",  0.035f, 18f, 5, 0.5f, 2f, 0.25f, 0.55f, -4f, false);
            CreateTerrainParams("Snow_TerrainParams",    0.03f, 40f, 6, 0.55f, 2.1f, 0.1f, 0.7f, -3f, true);
            CreateTerrainParams("Ocean_TerrainParams",   0.015f, 5f,  3, 0.3f, 1.5f, 0.05f, 0.8f, -10f, true);
        }

        private static void CreateTerrainParams(string name, float noiseScale, float heightMult, int octaves, float persistence, float lacunarity, float foliageDensity, float spawnThreshold, float waterLevel, bool overrideWater)
        {
            string path = $"Assets/Script/BiomeContent/{name}.asset";
            if (File.Exists(path)) return;

            var tp = ScriptableObject.CreateInstance<BiomeTerrainParams>();
            tp.noiseScale = noiseScale;
            tp.heightMultiplier = heightMult;
            tp.octaves = octaves;
            tp.persistence = persistence;
            tp.lacunarity = lacunarity;
            tp.foliageDensityMultiplier = foliageDensity;
            tp.spawnThreshold = spawnThreshold;
            tp.waterLevel = waterLevel;
            tp.overrideWaterLevel = overrideWater;
            AssetDatabase.CreateAsset(tp, path);
        }

        private static void CreateFoliageSpawnRules()
        {
            CreateSpawnRules("Plains_FoliageRules",
                new (string, string, int, float, float, float, float, float, float, float)[]
                {
                    ("Plains_Grass",      "plains", 0, 0.3f, 1.0f, 0f, 15f, 90f, -1f, 1f),
                    ("Plains_Shrub",      "plains", 1, 0.4f, 1.0f, 2f, 25f, 70f, -1f, 1f),
                },
                new (string, string, int, float, float, float, float, float, float, float)[]
                {
                    ("Forest_Tree",       "forest", 0, 0.5f, 1.0f, 5f, 45f, 45f, -1f, 1f),
                    ("Forest_DenseGrass", "forest", 1, 0.2f, 1.0f, 2f, 35f, 90f, -1f, 1f),
                    ("Forest_Bush",       "forest", 2, 0.3f, 1.0f, 1f, 30f, 80f, -1f, 1f),
                },
                new (string, string, int, float, float, float, float, float, float, float)[]
                {
                    ("Mountain_SparseGrass",  "mountain", 0, 0.3f, 1.0f, 20f, 70f, 90f, -1f, 1f),
                    ("Mountain_StuntedTree",  "mountain", 1, 0.5f, 1.0f, 25f, 55f, 50f, -1f, 1f),
                    ("Mountain_Rock",         "mountain", 2, 0.4f, 1.0f, 30f, 80f, 90f, -1f, 1f),
                });

            CreateSpawnRules("Desert_FoliageRules",
                new (string, string, int, float, float, float, float, float, float, float)[]
                {
                    ("Desert_Cactus",       "desert", 0, 0.2f, 0.8f, 5f, 30f, 90f, -1f, 1f),
                    ("Desert_DryGrass",     "desert", 1, 0.1f, 0.5f, 0f, 20f, 90f, -1f, 1f),
                },
                new (string, string, int, float, float, float, float, float, float, float)[]
                {
                    ("Desert_Rock",         "desert", 0, 0.3f, 1.0f, 0f, 40f, 90f, -1f, 1f),
                    ("Desert_Shrub",        "desert", 1, 0.15f, 0.6f, 10f, 25f, 80f, -1f, 1f),
                },
                new (string, string, int, float, float, float, float, float, float, float)[]
                {
                    ("Desert_BareRock",     "desert", 0, 0.4f, 1.0f, 15f, 50f, 90f, -1f, 1f),
                });

            CreateSpawnRules("Swamp_FoliageRules",
                new (string, string, int, float, float, float, float, float, float, float)[]
                {
                    ("Swamp_Reed",          "swamp", 0, 0.3f, 1.0f, -1f, 10f, 90f, -1f, 1f),
                    ("Swamp_Moss",          "swamp", 1, 0.2f, 0.8f, -1f, 5f, 90f, -1f, 1f),
                },
                new (string, string, int, float, float, float, float, float, float, float)[]
                {
                    ("Swamp_DenseReed",     "swamp", 0, 0.4f, 1.0f, -1f, 15f, 90f, -1f, 1f),
                    ("Swamp_Pine",          "swamp", 1, 0.25f, 0.7f, 0f, 30f, 60f, -1f, 1f),
                    ("Swamp_Fern",          "swamp", 2, 0.3f, 0.9f, -1f, 10f, 90f, -1f, 1f),
                },
                new (string, string, int, float, float, float, float, float, float, float)[]
                {
                    ("Swamp_SparseReed",    "swamp", 0, 0.25f, 0.7f, -1f, 8f, 90f, -1f, 1f),
                });

            CreateSpawnRules("Tundra_FoliageRules",
                new (string, string, int, float, float, float, float, float, float, float)[]
                {
                    ("Tundra_Lichen",       "tundra", 0, 0.15f, 0.5f, 0f, 20f, 90f, -1f, 1f),
                    ("Tundra_HardyShrub",   "tundra", 1, 0.1f, 0.4f, 5f, 30f, 70f, -1f, 1f),
                },
                new (string, string, int, float, float, float, float, float, float, float)[]
                {
                    ("Tundra_BerryBush",    "tundra", 0, 0.2f, 0.6f, 5f, 35f, 60f, -1f, 1f),
                    ("Tundra_SparseGrass",  "tundra", 1, 0.1f, 0.4f, 0f, 25f, 90f, -1f, 1f),
                    ("Tundra_Rock",         "tundra", 2, 0.3f, 0.8f, 10f, 40f, 90f, -1f, 1f),
                },
                new (string, string, int, float, float, float, float, float, float, float)[]
                {
                    ("Tundra_RockField",    "tundra", 0, 0.35f, 1.0f, 15f, 60f, 90f, -1f, 1f),
                });

            CreateSpawnRules("Snow_FoliageRules",
                new (string, string, int, float, float, float, float, float, float, float)[]
                {
                    ("Snow_Rock",           "snow", 0, 0.2f, 0.6f, 10f, 30f, 90f, -1f, 1f),
                },
                new (string, string, int, float, float, float, float, float, float, float)[]
                {
                    ("Snow_SparseRock",     "snow", 0, 0.3f, 0.8f, 5f, 35f, 90f, -1f, 1f),
                    ("Snow_IcePatch",       "snow", 1, 0.15f, 0.5f, 0f, 40f, 90f, -1f, 1f),
                },
                new (string, string, int, float, float, float, float, float, float, float)[]
                {
                    ("Snow_BareRock",       "snow", 0, 0.4f, 1.0f, 20f, 50f, 90f, -1f, 1f),
                });
        }

        private static void CreateSpawnRules(string name, (string, string, int, float, float, float, float, float, float, float)[] plainsRules, (string, string, int, float, float, float, float, float, float, float)[] forestRules, (string, string, int, float, float, float, float, float, float, float)[] mountainRules)
        {
            string path1 = $"Assets/Script/BiomeContent/{name}_Plains.asset";
            if (!File.Exists(path1))
                AssetDatabase.CreateAsset(CreateSpawnRulesAsset("Plains_FoliageRules", plainsRules), path1);

            string path2 = $"Assets/Script/BiomeContent/{name}_Forest.asset";
            if (!File.Exists(path2))
                AssetDatabase.CreateAsset(CreateSpawnRulesAsset("Forest_FoliageRules", forestRules), path2);

            string path3 = $"Assets/Script/BiomeContent/{name}_Mountain.asset";
            if (!File.Exists(path3))
                AssetDatabase.CreateAsset(CreateSpawnRulesAsset("Mountain_FoliageRules", mountainRules), path3);
        }

        private static FoliageSpawnRules CreateSpawnRulesAsset(string name, (string, string, int, float, float, float, float, float, float, float)[] rules)
        {
            var asset = ScriptableObject.CreateInstance<FoliageSpawnRules>();
            asset.rules = new FoliageSpawnRule[rules.Length];
            for (int i = 0; i < rules.Length; i++)
            {
                var r = rules[i];
                asset.rules[i] = new FoliageSpawnRule
                {
                    name = r.Item1,
                    biomeTag = r.Item2,
                    spawnPriority = r.Item3,
                    minDensity = r.Item4,
                    maxDensity = r.Item5,
                    minHeight = r.Item6,
                    maxHeight = r.Item7,
                    maxSlope = r.Item8,
                    noiseVariationMin = r.Item9,
                    noiseVariationMax = r.Item10
                };
            }
            return asset;
        }

        private static void CreateBiomeDefinitions()
        {
            CreateBiomeDefinition("Plains_BiomeDef", BiomeType.Plains, "Plains",
                0f, 30f, 0.6f, 0.5f, new Color(0.85f, 0.8f, 0.6f));

            CreateBiomeDefinition("Forest_BiomeDef", BiomeType.Forest, "Temperate Forest",
                5f, 45f, 0.5f, 0.7f, new Color(0.3f, 0.6f, 0.3f));

            CreateBiomeDefinition("Mountain_BiomeDef", BiomeType.Mountain, "Mountain Range",
                25f, 85f, 0.3f, 0.4f, new Color(0.65f, 0.6f, 0.55f));

            CreateBiomeDefinition("Desert_BiomeDef", BiomeType.Desert, "Arid Desert",
                0f, 20f, 0.9f, 0.1f, new Color(0.95f, 0.85f, 0.55f));

            CreateBiomeDefinition("Swamp_BiomeDef", BiomeType.Swamp, "Dense Swamp",
                -2f, 10f, 0.55f, 0.9f, new Color(0.35f, 0.45f, 0.25f));

            CreateBiomeDefinition("Tundra_BiomeDef", BiomeType.Tundra, "Frozen Tundra",
                10f, 50f, 0.15f, 0.3f, new Color(0.75f, 0.8f, 0.85f));

            CreateBiomeDefinition("Snow_BiomeDef", BiomeType.Snow, "Glacial Peak",
                40f, 90f, 0.05f, 0.25f, new Color(0.9f, 0.92f, 0.95f));

            CreateBiomeDefinition("Ocean_BiomeDef", BiomeType.Ocean, "Deep Ocean",
                -10f, 0f, 0.4f, 0.8f, new Color(0.1f, 0.25f, 0.6f));
        }

        private static void CreateBiomeDefinition(string assetName, BiomeType type, string name,
            float heightMin, float heightMax, float temp, float moisture, Color color)
        {
            string path = $"Assets/Script/BiomeContent/{assetName}.asset";
            if (File.Exists(path)) return;

            var def = ScriptableObject.CreateInstance<BiomeDefinition>();
            def.biomeType = type;
            def.biomeName = name;
            def.heightMin = heightMin;
            def.heightMax = heightMax;
            def.temperature = temp;
            def.moisture = moisture;
            def.color = color;

            WireBiomeDefinition(def);

            AssetDatabase.CreateAsset(def, path);
        }

        private static void WireBiomeDefinition(BiomeDefinition def)
        {
            string terrainName = "";
            string foliageName = "";
            switch (def.biomeType)
            {
                case BiomeType.Plains:     terrainName = "Plains_TerrainParams";   foliageName = "Plains_FoliageConfig";    break;
                case BiomeType.Forest:     terrainName = "Forest_TerrainParams";   foliageName = "Forest_FoliageConfig";    break;
                case BiomeType.Mountain:   terrainName = "Mountain_TerrainParams"; foliageName = "Mountain_FoliageConfig";  break;
                case BiomeType.Desert:     terrainName = "Desert_TerrainParams";   foliageName = "Desert_FoliageConfig";    break;
                case BiomeType.Swamp:      terrainName = "Swamp_TerrainParams";    foliageName = "Swamp_FoliageConfig";     break;
                case BiomeType.Tundra:     terrainName = "Tundra_TerrainParams";   foliageName = "Tundra_FoliageConfig";    break;
                case BiomeType.Snow:       terrainName = "Snow_TerrainParams";     foliageName = "Snow_FoliageConfig";      break;
                case BiomeType.Ocean:      terrainName = "Ocean_TerrainParams";    foliageName = "";                        break;
            }

            if (!string.IsNullOrEmpty(terrainName))
            {
                string[] terrainGuids = AssetDatabase.FindAssets(terrainName);
                if (terrainGuids.Length > 0)
                {
                    var tp = AssetDatabase.LoadAssetAtPath<BiomeTerrainParams>(AssetDatabase.GUIDToAssetPath(terrainGuids[0]));
                    def.terrainParams = tp;
                }
            }

            if (!string.IsNullOrEmpty(foliageName))
            {
                string[] foliageGuids = AssetDatabase.FindAssets(foliageName);
                if (foliageGuids.Length > 0)
                {
                    var fc = AssetDatabase.LoadAssetAtPath<FoliageConfig>(AssetDatabase.GUIDToAssetPath(foliageGuids[0]));
                    def.foliageConfig = fc;
                }
            }
        }

        private static void ConfigureWorldSettings()
        {
            string[] guids = AssetDatabase.FindAssets("t:WorldSettings");
            if (guids.Length == 0) return;

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var settings = AssetDatabase.LoadAssetAtPath<WorldSettings>(path);
            if (settings == null) return;

            bool needsUpdate = false;
            if (!settings.enableBiomeSystem)
            {
                settings.enableBiomeSystem = true;
                needsUpdate = true;
            }

            string[] biomeGuids = AssetDatabase.FindAssets("*_BiomeDef");
            var biomeDefs = new List<BiomeDefinition>();
            foreach (var guid in biomeGuids)
            {
                var def = AssetDatabase.LoadAssetAtPath<BiomeDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (def != null) biomeDefs.Add(def);
            }

            if (biomeDefs.Count >= 3)
            {
                BiomeDefinition[] biomesArray = biomeDefs.ToArray();
                if (settings.biomes == null || settings.biomes.Length < biomeDefs.Count)
                {
                    settings.biomes = biomesArray;
                    needsUpdate = true;
                }

                BiomeDefinition defaultBiome = null;
                foreach (var bd in biomeDefs)
                {
                    if (bd.biomeType == BiomeType.Plains)
                    {
                        defaultBiome = bd;
                        break;
                    }
                }
                if (defaultBiome != null && settings.defaultBiome != defaultBiome)
                {
                    settings.defaultBiome = defaultBiome;
                    needsUpdate = true;
                }
            }

            if (needsUpdate)
                EditorUtility.SetDirty(settings);
        }
    }
}
