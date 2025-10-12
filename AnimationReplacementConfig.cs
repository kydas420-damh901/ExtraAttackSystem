using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ExtraAttackSystem
{
    // Manage YAML for animation replacement maps; keeps AnimationManager.ReplacementMap in sync
    public static class AnimationReplacementConfig
    {
        // Use folder name without GUID to meet expected path: BepInEx\config\ExtraAttackSystem
        private static string ConfigFolderPath => Path.Combine(BepInEx.Paths.ConfigPath, "ExtraAttackSystem");
        private static string WeaponTypesConfigFilePath => Path.Combine(ConfigFolderPath, "AnimationReplacement_WeaponTypes.yaml");
        private static string IndividualWeaponsConfigFilePath => Path.Combine(ConfigFolderPath, "AnimationReplacement_IndividualWeapons.yaml");

        // YAML structure: maps: { styleKey: { vanillaClip: externalClip } }
        public class ReplacementYaml
        {
            public Dictionary<string, Dictionary<string, string>> Maps { get; set; } = new();
            // New YAML sections for AOC pair format: type-level and item-level maps
            public Dictionary<string, Dictionary<string, string>> AocTypes { get; set; } = new();
            public Dictionary<string, Dictionary<string, string>> AocItems { get; set; } = new();
            
            // Direct weapon type mappings removed - using AocTypes only
        }

        private static ReplacementYaml current = new ReplacementYaml();
        private static bool s_isApplyingToManager = false; // Prevent infinite loop

        // Clean up YAML content to handle malformed structure
        private static string CleanupYamlContent(string yaml)
        {
            try
            {
                ExtraAttackPlugin.LogInfo("System", "CleanupYamlContent: Starting cleanup");
                
                // Remove duplicate headers and fix structure
                var lines = yaml.Split('\n').ToList();
                var cleanedLines = new List<string>();
                bool inAocTypes = false;
                bool foundFirstWeaponType = false;
                
                foreach (var line in lines)
                {
                    // Skip duplicate headers after AocTypes:
                    if (line.Trim() == "AocTypes:")
                    {
                        if (!inAocTypes)
                        {
                            cleanedLines.Add(line);
                            inAocTypes = true;
                        }
                        continue;
                    }
                    
                    // Skip duplicate headers until we find the first weapon type
                    if (inAocTypes && !foundFirstWeaponType)
                    {
                        if (line.Trim().StartsWith("#") || string.IsNullOrWhiteSpace(line))
                        {
                            continue;
                        }
                        
                        // Found first weapon type
                        if (line.Trim().EndsWith(":"))
                        {
                            foundFirstWeaponType = true;
                            cleanedLines.Add(line);
                        }
                        continue;
                    }
                    
                    // Add all other lines
                    cleanedLines.Add(line);
                }
                
                string cleanedYaml = string.Join("\n", cleanedLines);
                ExtraAttackPlugin.LogInfo("System", $"CleanupYamlContent: Cleaned YAML length: {cleanedYaml.Length}");
                
                return cleanedYaml;
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error in CleanupYamlContent: {ex.Message}");
                return yaml; // Return original if cleanup fails
            }
        }

        // ConvertDirectMappingsToAocTypes method removed - YAML is already in 2-layer format

        // Initialize: create or load YAML, then apply to AnimationManager.ReplacementMap
        public static void Initialize()
        {
            try
            {
                if (!Directory.Exists(ConfigFolderPath))
                {
                    Directory.CreateDirectory(ConfigFolderPath);
                }

                // Check and create weapon types config if needed, then load it
                if (ShouldCreateOrRegenerateWeaponTypesConfig())
                {
                    CreateDefaultWeaponTypesConfig();
                }
                LoadWeaponTypesConfig();

                // Check and create individual weapons config if needed, then load it
                if (ShouldCreateOrRegenerateIndividualWeaponsConfig())
                {
                    CreateDefaultIndividualWeaponsConfig();
                }
                LoadIndividualWeaponsConfig();

                // Apply loaded (or created) YAML to manager map
                ApplyToManager();
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error initializing AnimationReplacementConfig: {ex.Message}");
            }
        }

        // Check if weapon types config should be created or regenerated
        private static bool ShouldCreateOrRegenerateWeaponTypesConfig()
        {
            if (!File.Exists(WeaponTypesConfigFilePath))
            {
                ExtraAttackPlugin.LogInfo("Config", "AnimationReplacement_WeaponTypes.yaml not found, will create");
                return true;
            }

            // Check if file is empty or has no content
            try
            {
                string content = File.ReadAllText(WeaponTypesConfigFilePath, Encoding.UTF8).Trim();
                if (string.IsNullOrEmpty(content))
                {
                    ExtraAttackPlugin.LogInfo("Config", "AnimationReplacement_WeaponTypes.yaml is empty, will regenerate");
                    return true;
                }

                // Check if file has actual AOC type data
                if (!content.Contains("AocTypes:") || !content.Contains("secondary_"))
                {
                    ExtraAttackPlugin.LogInfo("Config", "AnimationReplacement_WeaponTypes.yaml has no AOC type data, will regenerate");
                    return true;
                }

                ExtraAttackPlugin.LogInfo("Config", "AnimationReplacement_WeaponTypes.yaml exists and has content, skipping generation");
                return false;
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error checking AnimationReplacement_WeaponTypes.yaml: {ex.Message}");
                return true; // Regenerate on error
            }
        }

        // Check if individual weapons config should be created or regenerated
        private static bool ShouldCreateOrRegenerateIndividualWeaponsConfig()
        {
            // AnimationReplacement_IndividualWeapons.yaml: ファイルが存在しない場合のみ再生成
            // ファイルが存在する場合は、内容が空でも再生成しない（ユーザー設定維持）
            if (!File.Exists(IndividualWeaponsConfigFilePath))
            {
                ExtraAttackPlugin.LogInfo("Config", "AnimationReplacement_IndividualWeapons.yaml: File does not exist, will create default");
                return true;
            }
            
            ExtraAttackPlugin.LogInfo("Config", "AnimationReplacement_IndividualWeapons.yaml: File exists, will not regenerate (user settings preserved)");
            return false;
        }

        // Load weapon types YAML
        public static void LoadWeaponTypesConfig()
        {
            ExtraAttackPlugin.LogInfo("System", "LoadWeaponTypesConfig: Starting to load weapon types config");
            try
            {
                ExtraAttackPlugin.LogInfo("System", $"LoadWeaponTypesConfig: ConfigFolderPath: {ConfigFolderPath}");
                ExtraAttackPlugin.LogInfo("System", $"LoadWeaponTypesConfig: WeaponTypesConfigFilePath: {WeaponTypesConfigFilePath}");
                ExtraAttackPlugin.LogInfo("System", $"LoadWeaponTypesConfig: File exists: {File.Exists(WeaponTypesConfigFilePath)}");
                ExtraAttackPlugin.LogInfo("System", $"LoadWeaponTypesConfig: Reading file: {WeaponTypesConfigFilePath}");
                
                if (!File.Exists(WeaponTypesConfigFilePath))
                {
                    ExtraAttackPlugin.LogWarning("System", $"LoadWeaponTypesConfig: File does not exist: {WeaponTypesConfigFilePath}");
                    return;
                }

                string yaml = File.ReadAllText(WeaponTypesConfigFilePath, Encoding.UTF8);
                
                if (string.IsNullOrEmpty(yaml))
                {
                    ExtraAttackPlugin.LogWarning("System", "LoadWeaponTypesConfig: YAML content is empty");
                    return;
                }
                
                
                // YAML content validation (debug info removed for cleaner logs)
                
                ExtraAttackPlugin.LogInfo("System", "LoadWeaponTypesConfig: Creating deserializer");
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(UnderscoredNamingConvention.Instance)
                    .IgnoreUnmatchedProperties()
                    .Build();

                // Clean up YAML content to handle malformed structure
                try
                {
                    yaml = CleanupYamlContent(yaml);
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.LogError("System", $"ERROR in CleanupYamlContent: {ex.Message}");
                    ExtraAttackPlugin.LogError("System", $"ERROR in CleanupYamlContent stack trace: {ex.StackTrace}");
                }

                var weaponTypesConfig = deserializer.Deserialize<ReplacementYaml>(yaml) ?? new ReplacementYaml();
                
                // Debug: Log deserialization result
                ExtraAttackPlugin.LogInfo("System", $"LoadWeaponTypesConfig: After deserialization - AocTypes is null: {weaponTypesConfig.AocTypes == null}, Count: {weaponTypesConfig.AocTypes?.Count ?? -1}");
                if (weaponTypesConfig.AocTypes != null && weaponTypesConfig.AocTypes.Count > 0)
                {
                    ExtraAttackPlugin.LogInfo("System", $"LoadWeaponTypesConfig: AocTypes keys: {string.Join(", ", weaponTypesConfig.AocTypes.Keys)}");
                }
                else
                {
                    ExtraAttackPlugin.LogWarning("System", $"LoadWeaponTypesConfig: AocTypes is empty after deserialization. YAML length: {yaml.Length}");
                    ExtraAttackPlugin.LogWarning("System", $"LoadWeaponTypesConfig: YAML preview (first 500 chars): {yaml.Substring(0, Math.Min(500, yaml.Length))}");
                }
                
                // YAML is already in 2-layer format, no conversion needed
                
                current.AocTypes = weaponTypesConfig.AocTypes;
                
                // Note: ApplyToManager() will be called by the caller to avoid infinite loop
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error in LoadWeaponTypesConfig: {ex.Message}");
                ExtraAttackPlugin.LogError("System", $"Stack trace: {ex.StackTrace}");
                
                // Ensure current.AocTypes is reset on error
                current.AocTypes = new Dictionary<string, Dictionary<string, string>>();
                
                // Re-throw to ensure the error is not silently ignored
                throw;
            }
        }

        // Create default weapon types configuration for YAML generation
        private static ReplacementYaml CreateDefaultWeaponTypesConfigForYaml()
        {
            var config = new ReplacementYaml
            {
                AocTypes = new Dictionary<string, Dictionary<string, string>>()
            };

            // Define default mappings with 2-layer structure
            var defaultMappings = new Dictionary<string, Dictionary<string, string>>
            {
                ["Swords"] = new Dictionary<string, string>
                {
                    ["secondary_Q"] = "Sw-Ma-GS-Up_Attack_A_1External",
                    ["secondary_T"] = "Sw-Ma-GS-Up_Attack_A_2External",
                    ["secondary_G"] = "Sw-Ma-GS-Up_Attack_A_3External"
                },
                ["Axes"] = new Dictionary<string, string>
                {
                    ["secondary_Q"] = "OneHand_Up_Attack_B_1External",
                    ["secondary_T"] = "OneHand_Up_Attack_B_2External",
                    ["secondary_G"] = "OneHand_Up_Attack_B_3External"
                },
                ["Clubs"] = new Dictionary<string, string>
                {
                    ["secondary_Q"] = "0MWA_DualWield_Attack02External",
                    ["secondary_T"] = "MWA_RightHand_Attack03External",
                    ["secondary_G"] = "Shield@ShieldAttack01External"
                },
                ["Spears"] = new Dictionary<string, string>
                {
                    ["secondary_Q"] = "Shield@ShieldAttack02External",
                    ["secondary_T"] = "Attack04External",
                    ["secondary_G"] = "0MGSA_Attack_Dash01External"
                },
                ["GreatSwords"] = new Dictionary<string, string>
                {
                    ["secondary_Q"] = "2Hand-Sword-Attack8External",
                    ["secondary_T"] = "2Hand_Skill01_WhirlWindExternal",
                    ["secondary_G"] = "Eas_GreatSword_Combo1External"
                },
                ["BattleAxes"] = new Dictionary<string, string>
                {
                    ["secondary_Q"] = "0MGSA_Attack_Dash02External",
                    ["secondary_T"] = "0MGSA_Attack_Ground01External",
                    ["secondary_G"] = "0MGSA_Attack_Ground02External"
                },
                ["Polearms"] = new Dictionary<string, string>
                {
                    ["secondary_Q"] = "Pa_1handShiled_attack02External",
                    ["secondary_T"] = "Attack_ShieldExternal",
                    ["secondary_G"] = "0DS_Attack_07External"
                },
                ["Knives"] = new Dictionary<string, string>
                {
                    ["secondary_Q"] = "ChargeAttkExternal",
                    ["secondary_T"] = "HardAttkExternal",
                    ["secondary_G"] = "StrongAttk3External"
                },
                ["Fists"] = new Dictionary<string, string>
                {
                    ["secondary_Q"] = "Flying Knee Punch ComboExternal",
                    ["secondary_T"] = "Eas_GreatSword_SlideAttackExternal",
                    ["secondary_G"] = "Eas_GreatSwordSlash_01External"
                }
            };

            // Only add mappings for clips that actually exist
            foreach (var weaponType in defaultMappings.Keys)
            {
                var weaponMappings = new Dictionary<string, string>();
                
                foreach (var clipMapping in defaultMappings[weaponType])
                {
                    var clipKey = clipMapping.Key; // secondary_Q, secondary_T, secondary_G
                    var clipName = clipMapping.Value;
                    
                    // Check if the clip actually exists
                    if (AnimationManager.ExternalAnimations.ContainsKey(clipName))
                    {
                        weaponMappings[clipKey] = clipName;
                        ExtraAttackPlugin.LogInfo("System", $"CreateDefaultWeaponTypesConfigForYaml: Added {weaponType}.{clipKey} = {clipName} (length: {AnimationManager.ExternalAnimations[clipName].length:F3}s)");
                    }
                    else
                    {
                        ExtraAttackPlugin.LogWarning("System", $"CreateDefaultWeaponTypesConfigForYaml: Clip not found: {clipName} for {weaponType}.{clipKey}");
                    }
                }
                
                if (weaponMappings.Count > 0)
                {
                    config.AocTypes[weaponType] = weaponMappings;
                    ExtraAttackPlugin.LogInfo("System", $"CreateDefaultWeaponTypesConfigForYaml: Added {weaponType} with {weaponMappings.Count} available clips");
                }
            }

            ExtraAttackPlugin.LogInfo("System", $"CreateDefaultWeaponTypesConfigForYaml: Generated config with {config.AocTypes.Count} weapon types");
            return config;
        }

        // Create default weapon types configuration
        private static void CreateDefaultWeaponTypesConfig()
        {
            try
            {
                var defaultConfig = new ReplacementYaml
                {
                    AocTypes = new Dictionary<string, Dictionary<string, string>>
                    {
                        ["Swords"] = new Dictionary<string, string>
                        {
                            ["secondary_Q"] = "Sw-Ma-GS-Up_Attack_A_1External", // Q mode
                            ["secondary_T"] = "Sw-Ma-GS-Up_Attack_A_2External", // T mode
                            ["secondary_G"] = "Sw-Ma-GS-Up_Attack_A_3External" // G mode
                        },
                        ["Axes"] = new Dictionary<string, string>
                        {
                            ["secondary_Q"] = "OneHand_Up_Attack_B_1External", // Q mode
                            ["secondary_T"] = "OneHand_Up_Attack_B_2External", // T mode
                            ["secondary_G"] = "OneHand_Up_Attack_B_3External" // G mode
                        },
                        ["Clubs"] = new Dictionary<string, string>
                        {
                            ["secondary_Q"] = "0MWA_DualWield_Attack02External", // Q mode
                            ["secondary_T"] = "MWA_RightHand_Attack03External", // T mode
                            ["secondary_G"] = "Shield@ShieldAttack01External" // G mode
                        },
                        ["Spears"] = new Dictionary<string, string>
                        {
                            ["secondary_Q"] = "Shield@ShieldAttack02External", // Q mode
                            ["secondary_T"] = "Attack04External", // T mode
                            ["secondary_G"] = "0MGSA_Attack_Dash01External" // G mode
                        },
                        ["GreatSwords"] = new Dictionary<string, string>
                        {
                            ["secondary_Q"] = "2Hand-Sword-Attack8External", // Q mode
                            ["secondary_T"] = "2Hand_Skill01_WhirlWindExternal", // T mode
                            ["secondary_G"] = "Eas_GreatSword_Combo1External" // G mode
                        },
                        ["BattleAxes"] = new Dictionary<string, string>
                        {
                            ["secondary_Q"] = "0MGSA_Attack_Dash02External", // Q mode
                            ["secondary_T"] = "0MGSA_Attack_Ground01External", // T mode
                            ["secondary_G"] = "0MGSA_Attack_Ground02External" // G mode
                        },
                        ["Polearms"] = new Dictionary<string, string>
                        {
                            ["secondary_Q"] = "Pa_1handShiled_attack02External", // Q mode
                            ["secondary_T"] = "Attack_ShieldExternal", // T mode
                            ["secondary_G"] = "0DS_Attack_07External" // G mode
                        },
                        ["Knives"] = new Dictionary<string, string>
                        {
                            ["secondary_Q"] = "ChargeAttkExternal", // Q mode
                            ["secondary_T"] = "HardAttkExternal", // T mode
                            ["secondary_G"] = "StrongAttk3External" // G mode
                        },
                        ["Fists"] = new Dictionary<string, string>
                        {
                            ["secondary_Q"] = "Flying Knee Punch ComboExternal", // Q mode
                            ["secondary_T"] = "Eas_GreatSword_SlideAttackExternal", // T mode
                            ["secondary_G"] = "Eas_GreatSwordSlash_01External" // G mode
                        }
                    }
                };

                // Set the current config and save
                current = defaultConfig;
                SaveWeaponTypesConfig();
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error creating default weapon types config: {ex.Message}");
            }
        }

        // Create default individual weapons configuration
        private static void CreateDefaultIndividualWeaponsConfig()
        {
            try
            {
                // Initialize empty individual weapons configuration
                current.AocItems = new Dictionary<string, Dictionary<string, string>>();
                
                // Save the empty configuration
                SaveIndividualWeaponsConfig();
                ExtraAttackPlugin.LogInfo("System", "Created default individual weapons configuration");
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error creating default individual weapons config: {ex.Message}");
            }
        }

        // Load individual weapons YAML
        private static void LoadIndividualWeaponsConfig()
        {
            try
            {
                if (!File.Exists(IndividualWeaponsConfigFilePath))
                {
                    ExtraAttackPlugin.LogInfo("System", $"Individual weapons config file not found at {IndividualWeaponsConfigFilePath} - This is normal, user must create this file manually");
                    return;
                }

                string yaml = File.ReadAllText(IndividualWeaponsConfigFilePath, Encoding.UTF8);
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .IgnoreUnmatchedProperties()
                    .Build();

                var individualWeaponsConfig = deserializer.Deserialize<ReplacementYaml>(yaml) ?? new ReplacementYaml();
                if (individualWeaponsConfig.AocItems != null)
                {
                    current.AocItems = individualWeaponsConfig.AocItems;
                    ExtraAttackPlugin.LogInfo("System", $"Loaded {individualWeaponsConfig.AocItems.Count} individual weapon mappings from YAML");
                }
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error loading AnimationReplacement_IndividualWeapons.yaml: {ex.Message}");
            }
        }

        // Reload at runtime (e.g., F6 hotkey handled elsewhere)
        public static void Reload()
        {
            ExtraAttackPlugin.LogInfo("System", $"F6: AnimationReplacementConfig.Reload() - Checking files...");
            ExtraAttackPlugin.LogInfo("System", $"F6: WeaponTypesConfigFilePath: {WeaponTypesConfigFilePath}");
            ExtraAttackPlugin.LogInfo("System", $"F6: IndividualWeaponsConfigFilePath: {IndividualWeaponsConfigFilePath}");
            ExtraAttackPlugin.LogInfo("System", $"F6: WeaponTypes file exists: {File.Exists(WeaponTypesConfigFilePath)}");
            ExtraAttackPlugin.LogInfo("System", $"F6: IndividualWeapons file exists: {File.Exists(IndividualWeaponsConfigFilePath)}");
            
            if (File.Exists(WeaponTypesConfigFilePath))
            {
                ExtraAttackPlugin.LogInfo("System", "F6: WeaponTypes config exists, loading...");
                LoadWeaponTypesConfig();
                
                // IndividualWeapons config is optional (user-managed)
                if (File.Exists(IndividualWeaponsConfigFilePath))
                {
                    ExtraAttackPlugin.LogInfo("System", "F6: IndividualWeapons config exists, loading...");
                LoadIndividualWeaponsConfig();
                }
                else
                {
                    ExtraAttackPlugin.LogInfo("System", "F6: IndividualWeapons config not found - This is normal, user must create this file manually");
                }
                
                ApplyToManager();
                ExtraAttackPlugin.LogInfo("System", "F6: AnimationReplacementConfig reload completed");
            }
            else
            {
                ExtraAttackPlugin.LogWarning("System", "F6: WeaponTypes YAML file missing, skipping reload");
            }
        }


        // Create YAML from current AnimationManager.ReplacementMap and save with bilingual comments
        private static void CreateDefaultFromManager()
        {
            // Copy data from manager into our YAML structure
            SyncFromManagerToYaml();
            SaveWeaponTypesConfig();
            SaveIndividualWeaponsConfig();
        }

        // Copy AnimationManager.ReplacementMap -> current.Maps
        private static void SyncFromManagerToYaml()
        {
            // Ensure Maps is available
            if (current.Maps == null)
            {
                current.Maps = new Dictionary<string, Dictionary<string, string>>();
            }
            current.Maps.Clear();
            // Ensure new sections exist and clear
            if (current.AocTypes == null)
            {
                current.AocTypes = new Dictionary<string, Dictionary<string, string>>();
            }
            if (current.AocItems == null)
            {
                current.AocItems = new Dictionary<string, Dictionary<string, string>>();
            }
            current.AocTypes.Clear();
            current.AocItems.Clear();
            
            
            foreach (var style in AnimationManager.ReplacementMap.Keys)
            {
                var src = AnimationManager.ReplacementMap[style];
                current.Maps[style] = src.ToDictionary(k => k.Key, v => v.Value);
                
                // Categorize into types/items based on new naming convention
                // Weapon type keys: secondary_Q/T/G_{
                // Individual weapon keys: secondary_Q/T/G_{å€‹åˆ¥æ­¦å™¨å}
                bool isIndividual = IsIndividualWeaponKey(style);
                
                if (isIndividual)
                {
                    current.AocItems[style] = src.ToDictionary(k => k.Key, v => v.Value);
                }
                else
                {
                    current.AocTypes[style] = src.ToDictionary(k => k.Key, v => v.Value);
                }
            }
            
        }

        // Check if a key represents an individual weapon (not weapon type)
        private static bool IsIndividualWeaponKey(string key)
        {
            // Individual weapon keys: secondary_Q/T/G_
            // Weapon type keys: secondary_Q/T/G_{æ­¦å™¨ç¨®åˆ¥}
            
            if (!key.StartsWith("secondary_"))
                return false;
                
            // Known weapon types that should go to AocTypes
            var weaponTypes = new[] { "Swords", "Axes", "Clubs", "Spears", "GreatSwords", "BattleAxes", "Polearms", "Knives", "Fists", "Unarmed" };
            
            // Check if it's a weapon type key
            foreach (var weaponType in weaponTypes)
            {
                if (key.EndsWith($"_{weaponType}"))
                    return false;
            }
            
            // Check for base keys (secondary_Q, secondary_T, secondary_G)
            if (key == "secondary_Q" || key == "secondary_T" || key == "secondary_G")
                return false;
                
            // Everything else is considered individual weapon
            return true;
        }

        // Apply YAML mappings back to AnimationManager.ReplacementMap (override/merge)
        private static void ApplyToManager()
        {
            // Prevent infinite loop
            if (s_isApplyingToManager)
            {
                ExtraAttackPlugin.LogWarning("System", "ApplyToManager: Already applying, preventing infinite loop");
                return;
            }
            s_isApplyingToManager = true;
            
            try
            {
                // If AocTypes is empty, only use default mappings if no YAML file exists or is empty
                if (current.AocTypes == null || current.AocTypes.Count == 0)
            {
                // Check if YAML file exists and has content
                bool yamlExists = File.Exists(WeaponTypesConfigFilePath);
                bool yamlHasContent = false;
                
                if (yamlExists)
                {
                    try
                    {
                        string content = File.ReadAllText(WeaponTypesConfigFilePath, Encoding.UTF8).Trim();
                        yamlHasContent = !string.IsNullOrEmpty(content) && (content.Contains("AocTypes:") || content.Contains("Swords:") || content.Contains("Axes:") || content.Contains("GreatSwords:"));
                        ExtraAttackPlugin.LogInfo("System", $"ApplyToManager: YAML content check - isEmpty: {string.IsNullOrEmpty(content)}, containsAocTypes: {content.Contains("AocTypes:")}, containsSwords: {content.Contains("Swords:")}, yamlHasContent: {yamlHasContent}");
                    }
                    catch (Exception ex)
                    {
                        ExtraAttackPlugin.LogError("System", $"Error checking YAML content: {ex.Message}");
                    }
                }
                
                if (!yamlExists || !yamlHasContent)
                {
                    if (!yamlExists)
                    {
                        ExtraAttackPlugin.LogInfo("System", "ApplyToManager: YAML file does not exist, creating default config");
                        CreateDefaultWeaponTypesConfig();
                    }
                    else
                    {
                        ExtraAttackPlugin.LogInfo("System", "ApplyToManager: YAML file exists but has no content, regenerating");
                        SaveWeaponTypesConfig();
                    }
                    
                    // Note: LoadWeaponTypesConfig() will be called by the caller to avoid infinite loop
                    ExtraAttackPlugin.LogInfo("System", $"ApplyToManager: Generated YAML, will be loaded by caller");
                }
                else
                {
                    ExtraAttackPlugin.LogInfo("System", "ApplyToManager: AocTypes is empty but YAML file exists with content - attempting to reload");
                    try
                    {
                        LoadWeaponTypesConfig();
                        if (current.AocTypes != null && current.AocTypes.Count > 0)
                        {
                            ExtraAttackPlugin.LogInfo("System", $"ApplyToManager: Successfully reloaded YAML with {current.AocTypes.Count} weapon types");
                        }
                        else
                        {
                            ExtraAttackPlugin.LogWarning("System", "ApplyToManager: YAML reload failed, using default mappings");
                            var defaultConfig = CreateDefaultWeaponTypesConfigForYaml();
                            current.AocTypes = defaultConfig.AocTypes;
                        }
                    }
                    catch (Exception ex)
                    {
                        ExtraAttackPlugin.LogError("System", $"ApplyToManager: Error reloading YAML: {ex.Message}");
                        var defaultConfig = CreateDefaultWeaponTypesConfigForYaml();
                        current.AocTypes = defaultConfig.AocTypes;
                    }
                }
            }
            
            // Process AocTypes (weapon types) first
            if (current.AocTypes != null && current.AocTypes.Count > 0)
            {
                foreach (var weaponType in current.AocTypes)
                {
                    if (!AnimationManager.ReplacementMap.ContainsKey(weaponType.Key))
                    {
                        AnimationManager.ReplacementMap[weaponType.Key] = new Dictionary<string, string>();
                    }

                    var target = AnimationManager.ReplacementMap[weaponType.Key];
                    if (weaponType.Value != null)
                    {
                        foreach (var kvp in weaponType.Value)
                        {
                            var vanillaName = kvp.Key ?? string.Empty;
                            var externalName = kvp.Value ?? string.Empty;
                            if (!string.IsNullOrWhiteSpace(vanillaName) && !string.IsNullOrWhiteSpace(externalName))
                            {
                                target[vanillaName] = externalName;
                                ExtraAttackPlugin.LogInfo("System", $"ApplyToManager: Set ReplacementMap[{weaponType.Key}][{vanillaName}] = {externalName}");
                            }
                        }
                    }
                }
            }
            else
            {
                ExtraAttackPlugin.LogInfo("System", "ApplyToManager: current.AocTypes is null or empty - will be handled by AnimationManager");
            }
            
            // Process Maps (legacy format) if available
            if (current.Maps != null && current.Maps.Count > 0)
            {
            foreach (var style in current.Maps)
            {
                if (!AnimationManager.ReplacementMap.ContainsKey(style.Key))
                {
                    AnimationManager.ReplacementMap[style.Key] = new Dictionary<string, string>();
                }

                var target = AnimationManager.ReplacementMap[style.Key];
                // Guard: skip null style map values
                if (style.Value == null)
                {
                    continue;
                }
                foreach (var kvp in style.Value)
                {
                    var vanillaName = kvp.Key ?? string.Empty;
                    var externalName = kvp.Value ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(vanillaName) || string.IsNullOrWhiteSpace(externalName))
                    {
                        // Skip invalid entries
                            continue;
                        }
                        target[vanillaName] = externalName;
                    }
                }
            }
            if (current.AocItems != null)
            {
                foreach (var style in current.AocItems)
                {
                    if (!AnimationManager.ReplacementMap.ContainsKey(style.Key))
                    {
                        AnimationManager.ReplacementMap[style.Key] = new Dictionary<string, string>();
                    }
                    var target = AnimationManager.ReplacementMap[style.Key];
                    if (style.Value == null) continue;
                    foreach (var kvp in style.Value)
                    {
                        var vanillaName = kvp.Key ?? string.Empty;
                        var externalName = kvp.Value ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(vanillaName) || string.IsNullOrWhiteSpace(externalName))
                        {
                            continue;
                        }
                        target[vanillaName] = externalName;
                    }
                }
            }
            }
            finally
            {
                s_isApplyingToManager = false; // Reset flag
            }
        }

        // Save current AnimationManager.ReplacementMap back to YAML for user editing
        public static void SaveFromManager()
        {
            // Copy manager map into YAML structure and persist with comments
            SyncFromManagerToYaml();
            SaveWeaponTypesConfig();
            SaveIndividualWeaponsConfig();
        }

        // Save weapon types YAML with Q/T/G ordering
        private static void SaveWeaponTypesConfig()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("# ============================================================================");
                sb.AppendLine("# Extra Attack System - Weapon Types Animation Replacement");
                sb.AppendLine("# ============================================================================");
                sb.AppendLine("# Format: WeaponType -> Q/T/G -> Vanilla -> External");
                sb.AppendLine("# ============================================================================");
                sb.AppendLine();

                // Get weapon type data directly from AnimationManager.ReplacementMap
                var weaponTypeGroups = new Dictionary<string, Dictionary<string, string>>();
                
                // Ensure the weapon types used in validation conditions are always included (alphabetical order)
                string[] weaponTypes = { "Axes", "BattleAxes", "Clubs", "Fists", "GreatSwords", "Knives", "Polearms", "Spears", "Swords" };
                
                // Check if ReplacementMap has data, if not use default mappings
                bool hasReplacementMapData = AnimationManager.ReplacementMap.Count > 0;
                ExtraAttackPlugin.LogInfo("System", $"SaveWeaponTypesConfig: ReplacementMap.Count = {AnimationManager.ReplacementMap.Count}, hasData = {hasReplacementMapData}");
                ExtraAttackPlugin.LogInfo("System", $"SaveWeaponTypesConfig: ReplacementMap keys: {string.Join(", ", AnimationManager.ReplacementMap.Keys)}");
                
                ExtraAttackPlugin.LogInfo("System", $"SaveWeaponTypesConfig: About to check hasReplacementMapData = {hasReplacementMapData}");
                
                if (!hasReplacementMapData)
                {
                    ExtraAttackPlugin.LogInfo("System", "SaveWeaponTypesConfig: ReplacementMap is empty, using default mappings");
                    // Use default mappings from CreateDefaultWeaponTypesConfig
                    var defaultConfig = CreateDefaultWeaponTypesConfigForYaml();
                    
                    foreach (var weaponType in weaponTypes)
                    {
                        weaponTypeGroups[weaponType] = new Dictionary<string, string>();
                        
                        if (defaultConfig.AocTypes.ContainsKey(weaponType))
                        {
                            var weaponMap = defaultConfig.AocTypes[weaponType];
                            
                            // Use 2-layer structure: secondary_Q/T/G: external
                            foreach (var kvp in weaponMap)
                            {
                                weaponTypeGroups[weaponType][kvp.Key] = kvp.Value;
                            }
                        }
                        else
                        {
                            // Add empty entry if weapon type doesn't exist
                            weaponTypeGroups[weaponType] = new Dictionary<string, string>();
                        }
                    }
                }
                else
                {
                    ExtraAttackPlugin.LogInfo("System", "SaveWeaponTypesConfig: ReplacementMap has data, processing weapon types");
                foreach (var weaponType in weaponTypes)
                {
                    if (AnimationManager.ReplacementMap.ContainsKey(weaponType))
                    {
                        var weaponMap = AnimationManager.ReplacementMap[weaponType];
                            ExtraAttackPlugin.LogInfo("System", $"SaveWeaponTypesConfig: Processing {weaponType}, weaponMap keys: {string.Join(", ", weaponMap.Keys)}");
                            ExtraAttackPlugin.LogInfo("System", $"SaveWeaponTypesConfig: Processing {weaponType}, weaponMap values: {string.Join(", ", weaponMap.Values)}");
                        // Use 2-layer structure: secondary_Q/T/G: external
                        weaponTypeGroups[weaponType] = new Dictionary<string, string>();
                        foreach (var kvp in weaponMap)
                        {
                            weaponTypeGroups[weaponType][kvp.Key] = kvp.Value;
                            ExtraAttackPlugin.LogInfo("System", $"SaveWeaponTypesConfig: Found {weaponType}.{kvp.Key} = {kvp.Value}");
                        }
                        }
                        else
                        {
                            // Add empty entry if weapon type doesn't exist
                            weaponTypeGroups[weaponType] = new Dictionary<string, string>();
                        }
                    }
                }

                // Log weaponTypeGroups for debugging
                ExtraAttackPlugin.LogInfo("System", $"SaveWeaponTypesConfig: weaponTypeGroups.Count = {weaponTypeGroups.Count}");
                foreach (var wt in weaponTypeGroups)
                {
                    ExtraAttackPlugin.LogInfo("System", $"SaveWeaponTypesConfig: weaponTypeGroups[{wt.Key}].Count = {wt.Value.Count}");
                    foreach (var mode in wt.Value)
                    {
                        ExtraAttackPlugin.LogInfo("System", $"SaveWeaponTypesConfig: weaponTypeGroups[{wt.Key}][{mode.Key}] = {mode.Value}");
                    }
                }

                // Output AocTypes section
                sb.AppendLine("AocTypes:");

                // Output in weapon type order
                foreach (var weaponType in weaponTypeGroups.Keys.OrderBy(k => k))
                {
                    sb.AppendLine($"  # {weaponType}");
                    sb.AppendLine($"  {weaponType}:");
                    
                    var weaponMap = weaponTypeGroups[weaponType];
                    if (weaponMap != null && weaponMap.Count > 0)
                    {
                        // Output in Q/T/G order
                        var modes = new[] { "secondary_Q", "secondary_T", "secondary_G" };
                        foreach (var mode in modes)
                        {
                            if (weaponMap.ContainsKey(mode))
                            {
                                sb.AppendLine($"    {mode}: {weaponMap[mode]}  # Vanilla: {mode} | Replacement: {weaponMap[mode]}");
                            }
                        }
                    }
                    else
                    {
                        sb.AppendLine($"    # No mappings defined for {weaponType}");
                    }
                    sb.AppendLine();
                }

                File.WriteAllText(WeaponTypesConfigFilePath, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error saving AnimationReplacement_WeaponTypes.yaml: {ex.Message}");
            }
        }

        // Save individual weapons YAML with Q/T/G ordering
        private static void SaveIndividualWeaponsConfig()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("# ============================================================================");
                sb.AppendLine("# Extra Attack System - Individual Weapons Animation Replacement");
                sb.AppendLine("# ============================================================================");
                sb.AppendLine("# Format: IndividualWeapon -> Q/T/G -> Vanilla -> External");
                sb.AppendLine("# ============================================================================");
                sb.AppendLine();

                var itemsRef = current.AocItems ?? new Dictionary<string, Dictionary<string, string>>();
                
                // Group by individual weapon and sort by Q/T/G
                var individualWeaponGroups = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>();
                
                foreach (var style in itemsRef.Keys)
                {
                    if (style.StartsWith("secondary_"))
                    {
                        string? weaponName = ExtractWeaponNameFromKey(style);
                        string? mode = ExtractModeFromKey(style);
                        
                        // Skip if this is a weapon type (not individual weapon) or invalid
                        if (string.IsNullOrEmpty(weaponName) || string.IsNullOrEmpty(mode))
                    {
                        continue;
                    }
                        
                        if (!string.IsNullOrEmpty(weaponName) && weaponName != null && !individualWeaponGroups.ContainsKey(weaponName))
                        {
                            individualWeaponGroups[weaponName] = new Dictionary<string, Dictionary<string, string>>();
                        }
                        
                        if (itemsRef.ContainsKey(style) && !string.IsNullOrEmpty(weaponName) && !string.IsNullOrEmpty(mode) && weaponName != null && mode != null)
                        {
                            individualWeaponGroups[weaponName][mode] = itemsRef[style];
                        }
                    }
                }

                // Output AocItems section
                sb.AppendLine("AocItems:");

                // Output in individual weapon order: Q, T, G
                foreach (var weaponName in individualWeaponGroups.Keys.OrderBy(k => k))
                {
                    sb.AppendLine($"  # {weaponName}");
                    sb.AppendLine($"  {weaponName}:");
                    
                    var modes = new[] { "Q", "T", "G" };
                    foreach (var mode in modes)
                    {
                        if (individualWeaponGroups[weaponName].ContainsKey(mode))
                        {
                            sb.AppendLine($"    {mode}:");
                            var map = individualWeaponGroups[weaponName][mode];
                            if (map != null)
                            {
                                foreach (var kvp in map.OrderBy(k => k.Key))
                                {
                                    sb.AppendLine($"      {kvp.Key}: {kvp.Value}  # Vanilla: {kvp.Key} | Replacement: {kvp.Value}");
                                }
                            }
                        }
                    }
                    sb.AppendLine();
                }

                File.WriteAllText(IndividualWeaponsConfigFilePath, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error saving AnimationReplacement_IndividualWeapons.yaml: {ex.Message}");
            }
        }

        // Get vanilla clip name for weapon type and mode
        private static string GetVanillaClipName(string weaponType, string mode)
        {
            return weaponType switch
            {
                "Swords" => "sword_secondary",
                "Axes" => "axe_secondary",
                "Clubs" => "mace_secondary",
                "Spears" => "spear_secondary",
                "GreatSwords" => "greatsword_secondary",
                "BattleAxes" => "battleaxe_secondary",
                "Polearms" => "atgeir_secondary",
                "Knives" => "knife_secondary",
                "Fists" => "fist_secondary",
                _ => "sword_secondary"
            };
        }

        // Helper methods for extracting weapon type, weapon name, and mode from keys
        private static string? ExtractWeaponTypeFromKey(string key)
        {
            // secondary_Q_Swords -> Swords
            if (key.StartsWith("secondary_"))
            {
                var parts = key.Split('_');
                if (parts.Length >= 4)
                {
                    return parts[3]; // Swords, Axes, etc.
                }
            }
            return null; // Not a valid weapon type key
        }

        private static string? ExtractWeaponNameFromKey(string key)
        {
            // secondary_Q_SwordBlackmetal -> SwordBlackmetal
            if (key.StartsWith("secondary_"))
            {
                var parts = key.Split('_');
                if (parts.Length >= 4)
                {
                    string weaponName = parts[3]; // SwordBlackmetal, etc.
                    
                    // Check if this is an individual weapon (contains specific weapon name)
                    // vs weapon type (generic names like Swords, Axes, etc.)
                    string[] weaponTypes = { "Swords", "Axes", "Clubs", "Spears", "Polearms", "Knives", "Fists", "BattleAxes", "GreatSwords", "Unarmed", "DualAxes", "DualKnives", "Sledges", "Torch" };
                    
                    if (weaponTypes.Contains(weaponName))
                    {
                        // This is a weapon type, not an individual weapon
                        return null; // Skip weapon types in individual weapons YAML
                    }
                    
                    return weaponName; // Individual weapon name
                }
            }
            return null; // Not a valid individual weapon key
        }

        private static string? ExtractModeFromKey(string key)
        {
            // secondary_Q_Swords -> Q
            if (key.StartsWith("secondary_"))
            {
                var parts = key.Split('_');
                if (parts.Length >= 3)
                {
                    return parts[2]; // Q, T, G
                }
            }
            return null; // Not a valid key
        }
    }
}