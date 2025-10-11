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
        }

        private static ReplacementYaml current = new ReplacementYaml();

        // Initialize: create or load YAML, then apply to AnimationManager.ReplacementMap
        public static void Initialize()
        {
            try
            {
                if (!Directory.Exists(ConfigFolderPath))
                {
                    Directory.CreateDirectory(ConfigFolderPath);
                }

                bool weaponTypesExisted = File.Exists(WeaponTypesConfigFilePath);
                bool individualWeaponsExisted = File.Exists(IndividualWeaponsConfigFilePath);
                if (weaponTypesExisted && individualWeaponsExisted)
                {
                    LoadWeaponTypesConfig();
                    LoadIndividualWeaponsConfig();
                }
                else
                {
                    CreateDefaultFromManager();
                }

                // Apply loaded (or created) YAML to manager map
                ApplyToManager();

                // Auto-populate empty existing YAML from manager defaults to help first-run users
                if (weaponTypesExisted && individualWeaponsExisted && (current?.AocTypes?.Count ?? 0) == 0 && (current?.AocItems?.Count ?? 0) == 0)
                {
                    // Only if manager already has entries
                    bool hasManagerEntries = AnimationManager.ReplacementMap != null && AnimationManager.ReplacementMap.Any(kv => kv.Value != null && kv.Value.Count > 0);
                    if (hasManagerEntries)
                    {
                        SaveFromManager();
                    }
                }
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error initializing AnimationReplacementConfig: {ex.Message}");
            }
        }

        // Load weapon types YAML
        public static void LoadWeaponTypesConfig()
        {
            try
            {
                // If YAML file doesn't exist, create default configuration
                if (!File.Exists(WeaponTypesConfigFilePath))
                {
                    CreateDefaultWeaponTypesConfig();
                }

                string yaml = File.ReadAllText(WeaponTypesConfigFilePath, Encoding.UTF8);
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .IgnoreUnmatchedProperties()
                    .Build();

                var weaponTypesConfig = deserializer.Deserialize<ReplacementYaml>(yaml) ?? new ReplacementYaml();
                if (weaponTypesConfig.AocTypes != null)
                {
                    current.AocTypes = weaponTypesConfig.AocTypes;
                }
                // Apply the loaded configuration to ReplacementMap (always call, even if AocTypes is null)
                ApplyToManager();
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error loading AnimationReplacement_WeaponTypes.yaml: {ex.Message}");
            }
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
                            ["ea_secondary_Q"] = "Sw-Ma-GS-Up_Attack_A_1External", // Q mode
                            ["ea_secondary_T"] = "Sw-Ma-GS-Up_Attack_A_2External", // T mode
                            ["ea_secondary_G"] = "Sw-Ma-GS-Up_Attack_A_3External" // G mode
                        },
                        ["Axes"] = new Dictionary<string, string>
                        {
                            ["ea_secondary_Q"] = "OneHand_Up_Attack_B_1External", // Q mode
                            ["ea_secondary_T"] = "OneHand_Up_Attack_B_2External", // T mode
                            ["ea_secondary_G"] = "OneHand_Up_Attack_B_3External" // G mode
                        },
                        ["Clubs"] = new Dictionary<string, string>
                        {
                            ["ea_secondary_Q"] = "0MWA_DualWield_Attack02External", // Q mode
                            ["ea_secondary_T"] = "MWA_RightHand_Attack03External", // T mode
                            ["ea_secondary_G"] = "Shield@ShieldAttack01External" // G mode
                        },
                        ["Spears"] = new Dictionary<string, string>
                        {
                            ["ea_secondary_Q"] = "Shield@ShieldAttack02External", // Q mode
                            ["ea_secondary_T"] = "Attack04External", // T mode
                            ["ea_secondary_G"] = "0MGSA_Attack_Dash01External" // G mode
                        },
                        ["GreatSwords"] = new Dictionary<string, string>
                        {
                            ["ea_secondary_Q"] = "2Hand-Sword-Attack8External", // Q mode
                            ["ea_secondary_T"] = "2Hand_Skill01_WhirlWindExternal", // T mode
                            ["ea_secondary_G"] = "Eas_GreatSword_Combo1External" // G mode
                        },
                        ["BattleAxes"] = new Dictionary<string, string>
                        {
                            ["ea_secondary_Q"] = "0MGSA_Attack_Dash02External", // Q mode
                            ["ea_secondary_T"] = "0MGSA_Attack_Ground01External", // T mode
                            ["ea_secondary_G"] = "0MGSA_Attack_Ground02External" // G mode
                        },
                        ["Polearms"] = new Dictionary<string, string>
                        {
                            ["ea_secondary_Q"] = "Pa_1handShiled_attack02External", // Q mode
                            ["ea_secondary_T"] = "Attack_ShieldExternal", // T mode
                            ["ea_secondary_G"] = "0DS_Attack_07External" // G mode
                        },
                        ["Knives"] = new Dictionary<string, string>
                        {
                            ["ea_secondary_Q"] = "ChargeAttkExternal", // Q mode
                            ["ea_secondary_T"] = "HardAttkExternal", // T mode
                            ["ea_secondary_G"] = "StrongAttk3External" // G mode
                        },
                        ["Fists"] = new Dictionary<string, string>
                        {
                            ["ea_secondary_Q"] = "Flying Knee Punch ComboExternal", // Q mode
                            ["ea_secondary_T"] = "Eas_GreatSword_SlideAttackExternal", // T mode
                            ["ea_secondary_G"] = "Eas_GreatSwordSlash_01External" // G mode
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

        // Load individual weapons YAML
        private static void LoadIndividualWeaponsConfig()
        {
            try
            {
                string yaml = File.ReadAllText(IndividualWeaponsConfigFilePath, Encoding.UTF8);
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .IgnoreUnmatchedProperties()
                    .Build();

                var individualWeaponsConfig = deserializer.Deserialize<ReplacementYaml>(yaml) ?? new ReplacementYaml();
                if (individualWeaponsConfig.AocItems != null)
                {
                    current.AocItems = individualWeaponsConfig.AocItems;
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
            if (File.Exists(WeaponTypesConfigFilePath) && File.Exists(IndividualWeaponsConfigFilePath))
            {
                LoadWeaponTypesConfig();
                LoadIndividualWeaponsConfig();
                ApplyToManager();
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
                // Weapon type keys: ea_secondary_Q/T/G_{武器種別}
                // Individual weapon keys: ea_secondary_Q/T/G_{個別武器名}
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
            // Individual weapon keys: ea_secondary_Q/T/G_{個別武器名}
            // Weapon type keys: ea_secondary_Q/T/G_{武器種別}
            
            if (!key.StartsWith("ea_secondary_"))
                return false;
                
            // Known weapon types that should go to AocTypes
            var weaponTypes = new[] { "Swords", "Axes", "Clubs", "Spears", "GreatSwords", "BattleAxes", "Polearms", "Knives", "Fists", "Unarmed" };
            
            // Check if it's a weapon type key
            foreach (var weaponType in weaponTypes)
            {
                if (key.EndsWith($"_{weaponType}"))
                    return false;
            }
            
            // Check for base keys (ea_secondary_Q, ea_secondary_T, ea_secondary_G)
            if (key == "ea_secondary_Q" || key == "ea_secondary_T" || key == "ea_secondary_G")
                return false;
                
            // Everything else is considered individual weapon
            return true;
        }

        // Apply YAML mappings back to AnimationManager.ReplacementMap (override/merge)
        private static void ApplyToManager()
        {
            
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
                            }
                        }
                    }
                }
            }
            else
            {
                ExtraAttackPlugin.LogWarning("System", "ApplyToManager: current.AocTypes is null or empty");
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
                sb.AppendLine("# 武器種別アニメーション置換設定ファイル");
                sb.AppendLine("# ============================================================================");
                sb.AppendLine("# Format: WeaponType -> Q/T/G -> Vanilla -> External");
                sb.AppendLine("# 適応順序: 個別武器 -> 武器種 -> バニラ");
                sb.AppendLine("# ============================================================================");
                sb.AppendLine();

                // Get weapon type data directly from AnimationManager.ReplacementMap
                var weaponTypeGroups = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>();
                
                string[] weaponTypes = { "Swords", "Axes", "Clubs", "Spears", "GreatSwords", "BattleAxes", "Polearms", "Knives", "Fists" };
                
                foreach (var weaponType in weaponTypes)
                {
                    if (AnimationManager.ReplacementMap.ContainsKey(weaponType))
                    {
                        var weaponMap = AnimationManager.ReplacementMap[weaponType];
                        weaponTypeGroups[weaponType] = new Dictionary<string, Dictionary<string, string>>();
                        
                        // Check for Q/T/G modes
                        var modes = new[] { "Q", "T", "G" };
                        var modeKeys = new[] { "ea_secondary_Q", "ea_secondary_T", "ea_secondary_G" };
                        
                        for (int i = 0; i < modes.Length; i++)
                        {
                            if (weaponMap.ContainsKey(modeKeys[i]))
                            {
                                string externalClip = weaponMap[modeKeys[i]];
                                string triggerName = GetVanillaClipName(weaponType, modes[i]);
                                
                                weaponTypeGroups[weaponType][modes[i]] = new Dictionary<string, string>
                                {
                                    { triggerName, externalClip }
                                };
                            }
                        }
                    }
                }

                // Output in weapon type order: Q, T, G
                foreach (var weaponType in weaponTypeGroups.Keys.OrderBy(k => k))
                {
                    sb.AppendLine($"# {weaponType}");
                    sb.AppendLine($"{weaponType}:");
                    
                    var modes = new[] { "Q", "T", "G" };
                    foreach (var mode in modes)
                    {
                        if (weaponTypeGroups[weaponType].ContainsKey(mode))
                        {
                            sb.AppendLine($"  {mode}:");
                            var map = weaponTypeGroups[weaponType][mode];
                            if (map != null)
                            {
                    foreach (var kvp in map.OrderBy(k => k.Key))
                    {
                                    sb.AppendLine($"    {kvp.Key}: {kvp.Value}  # Vanilla: {kvp.Key} | Replacement: {kvp.Value}");
                                }
                            }
                        }
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
                sb.AppendLine("# 個別武器アニメーション置換設定ファイル");
                sb.AppendLine("# ============================================================================");
                sb.AppendLine("# Format: IndividualWeapon -> Q/T/G -> Vanilla -> External");
                sb.AppendLine("# 適応順序: 個別武器 -> 武器種 -> バニラ");
                sb.AppendLine("# ============================================================================");
                sb.AppendLine();

                var itemsRef = current.AocItems ?? new Dictionary<string, Dictionary<string, string>>();
                
                // Group by individual weapon and sort by Q/T/G
                var individualWeaponGroups = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>();
                
                foreach (var style in itemsRef.Keys)
                {
                    if (style.StartsWith("ea_secondary_"))
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

                // Output in individual weapon order: Q, T, G
                foreach (var weaponName in individualWeaponGroups.Keys.OrderBy(k => k))
                {
                    sb.AppendLine($"# {weaponName} / {weaponName}");
                    sb.AppendLine($"{weaponName}:");
                    
                    var modes = new[] { "Q", "T", "G" };
                    foreach (var mode in modes)
                    {
                        if (individualWeaponGroups[weaponName].ContainsKey(mode))
                        {
                            sb.AppendLine($"  {mode}:");
                            var map = individualWeaponGroups[weaponName][mode];
                            if (map != null)
                            {
                                foreach (var kvp in map.OrderBy(k => k.Key))
                                {
                                    sb.AppendLine($"    {kvp.Key}: {kvp.Value}  # Vanilla: {kvp.Key} | Replacement: {kvp.Value}");
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
            // ea_secondary_Q_Swords -> Swords
            if (key.StartsWith("ea_secondary_"))
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
            // ea_secondary_Q_SwordBlackmetal -> SwordBlackmetal
            if (key.StartsWith("ea_secondary_"))
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
            // ea_secondary_Q_Swords -> Q
            if (key.StartsWith("ea_secondary_"))
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