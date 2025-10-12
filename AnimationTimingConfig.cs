using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ExtraAttackSystem
{
    public static class AnimationTimingConfig
    {
        // Config file path
        private static string ConfigFolderPath => Path.Combine(BepInEx.Paths.ConfigPath, "ExtraAttackSystem");
        private static string ConfigFilePath => Path.Combine(ConfigFolderPath, "eas_attackconfig.yaml");
        private static string WeaponTypesConfigFilePath => Path.Combine(ConfigFolderPath, "eas_attackconfig_WeaponTypes.yaml");
        private static string IndividualWeaponsConfigFilePath => Path.Combine(ConfigFolderPath, "eas_attackconfig_IndividualWeapons.yaml");

        // Timing data for each animation
        public class AnimationTiming
        {
            // Animation Event Timing (0.0 ~ 1.0)
            public float HitTiming { get; set; } = 0.45f;
            public float TrailOnTiming { get; set; } = 0.35f;
            public float TrailOffTiming { get; set; } = 0.70f;
            public float ChainTiming { get; set; } = 0.0f;
            public float SpeedMultiplier { get; set; } = 1.0f;

            // Attack Detection Parameters
            public float AttackRange { get; set; } = 1.5f;
            public float AttackHeight { get; set; } = 0.6f;
            public float AttackOffset { get; set; } = 0.0f;
            public float AttackAngle { get; set; } = 90.0f;
            public float AttackRayWidth { get; set; } = 0.0f;
            public float AttackRayWidthCharExtra { get; set; } = 0.0f;
            public float AttackHeightChar1 { get; set; } = 0.0f;
            public float AttackHeightChar2 { get; set; } = 0.0f;
            public float MaxYAngle { get; set; } = 0.0f;
            public bool EnableHit { get; set; } = true;
            public bool EnableSound { get; set; } = true;
            public bool EnableVFX { get; set; } = true;

            // NEW: Costs and cooldown (can be overridden per animation)
            public float StaminaCost { get; set; } = 0.0f;
            public float EitrCost { get; set; } = 0.0f;
            public float CooldownSec { get; set; } = 0.0f;
        }

        // Config file structure
        public class TimingConfig
        {
            public AnimationTiming Default { get; set; } = new AnimationTiming();
            public Dictionary<string, AnimationTiming> Animations { get; set; } = new Dictionary<string, AnimationTiming>();
        }

        // Weapon type specific config structure
        public class WeaponTypeConfig
        {
            public AnimationTiming Default { get; set; } = new AnimationTiming();
            public Dictionary<string, Dictionary<string, AnimationTiming>> WeaponTypes { get; set; } = new Dictionary<string, Dictionary<string, AnimationTiming>>();
            public Dictionary<string, AnimationTiming> IndividualWeapons { get; set; } = new Dictionary<string, AnimationTiming>();
        }

        private static TimingConfig currentConfig = new TimingConfig();
        private static WeaponTypeConfig weaponTypeConfig = new WeaponTypeConfig();

        // Load or create config file
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
                    CreateDefaultWeaponTypeConfig();
                }
                LoadWeaponTypeConfig();

                // Check and create individual weapons config if needed, then load it
                if (ShouldCreateOrRegenerateIndividualWeaponsConfig())
                {
                    CreateDefaultIndividualWeaponsConfig();
                }
                LoadIndividualWeaponsConfig();
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error initializing AnimationTimingConfig: {ex.Message}");
            }
        }

        // Check if weapon types config should be created or regenerated
        private static bool ShouldCreateOrRegenerateWeaponTypesConfig()
        {
            if (!File.Exists(WeaponTypesConfigFilePath))
            {
                ExtraAttackPlugin.LogInfo("Config", "WeaponTypes config file not found, will create");
                return true;
            }

            // Check if file is empty or has no content
            try
            {
                string content = File.ReadAllText(WeaponTypesConfigFilePath, Encoding.UTF8).Trim();
                if (string.IsNullOrEmpty(content))
                {
                    ExtraAttackPlugin.LogInfo("Config", "WeaponTypes config file is empty, will regenerate");
                    return true;
                }

                // Check if file has actual weapon type data (not just comments/headers)
                if (!content.Contains("WeaponTypes:") || !content.Contains("_secondary_"))
                {
                    ExtraAttackPlugin.LogInfo("Config", "WeaponTypes config file has no weapon type data, will regenerate");
                    return true;
                }

                ExtraAttackPlugin.LogInfo("Config", "WeaponTypes config file exists and has content, skipping generation");
                return false;
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error checking WeaponTypes config file: {ex.Message}");
                return true; // Regenerate on error
            }
        }

        // Check if individual weapons config should be created or regenerated
        private static bool ShouldCreateOrRegenerateIndividualWeaponsConfig()
        {
            if (!File.Exists(IndividualWeaponsConfigFilePath))
            {
                ExtraAttackPlugin.LogInfo("Config", "IndividualWeapons config file not found, will create");
                return true;
            }

            // Check if file is empty or has no content
            try
            {
                string content = File.ReadAllText(IndividualWeaponsConfigFilePath, Encoding.UTF8).Trim();
                if (string.IsNullOrEmpty(content))
                {
                    ExtraAttackPlugin.LogInfo("Config", "IndividualWeapons config file is empty, will regenerate");
                    return true;
                }

                // Check if file has actual individual weapon data
                if (!content.Contains("IndividualWeapons:") || !content.Contains("_secondary_"))
                {
                    ExtraAttackPlugin.LogInfo("Config", "IndividualWeapons config file has no individual weapon data, will regenerate");
                    return true;
                }

                ExtraAttackPlugin.LogInfo("Config", "IndividualWeapons config file exists and has content, skipping generation");
                return false;
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error checking IndividualWeapons config file: {ex.Message}");
                return true; // Regenerate on error
            }
        }

        // Get timing for specific animation using weapon type config
        public static AnimationTiming GetTiming(string animationName)
        {
            // Use weapon type config instead of old config
            // Extract weapon type and attack mode from animation name
            string weaponType = "Swords"; // Default fallback
            string attackMode = "Q"; // Default fallback
            
            if (animationName.Contains("_secondary_Q"))
            {
                attackMode = "Q";
                weaponType = animationName.Replace("_secondary_Q", "").Replace("ea_", "");
            }
            else if (animationName.Contains("_secondary_T"))
            {
                attackMode = "T";
                weaponType = animationName.Replace("_secondary_T", "").Replace("ea_", "");
            }
            else if (animationName.Contains("_secondary_G"))
            {
                attackMode = "G";
                weaponType = animationName.Replace("_secondary_G", "").Replace("ea_", "");
            }
            
            return GetWeaponTypeTiming(weaponType, attackMode);
        }

        // Check if specific animation config exists using weapon type config
        public static bool HasConfig(string animationName)
        {
            // Use weapon type config instead of old config
            return GetTiming(animationName) != null;
        }

        // Extract mode from various attack mode formats
        private static string ExtractModeFromAttackMode(string attackMode)
        {
            if (string.IsNullOrEmpty(attackMode))
            {
                return attackMode;
            }

            // Handle format: ea_secondary_{Mode}
            if (attackMode.StartsWith("ea_secondary_"))
            {
                return attackMode.Replace("ea_secondary_", "");
            }
            // Handle format: secondary_{Mode}
            else if (attackMode.StartsWith("secondary_"))
            {
                return attackMode.Replace("secondary_", "");
            }
            // Handle format: {Mode} (Q/T/G only)
            else if (attackMode == "Q" || attackMode == "T" || attackMode == "G")
            {
                return attackMode;
            }

            return attackMode; // Return original if can't parse
        }

        // Append missing animation timing entries from current ReplacementMap and save
        public static void SaveAppendFromReplacementMap()
        {
            try
            {
                // Ensure collection
                if (currentConfig.Animations == null)
                {
                    currentConfig.Animations = new Dictionary<string, AnimationTiming>();
                }

                // Build mapping: Vanilla clip name + secondary suffix -> External clip name
                var mappings = new Dictionary<string, string>();

                // Helper to append from weapon type maps by suffix (Q/T/G)
                void AppendFromWeaponType(string weaponType, string mode, string suffix)
                {
                    if (AnimationManager.ReplacementMap.ContainsKey(weaponType))
                    {
                        var weaponMap = AnimationManager.ReplacementMap[weaponType];
                        if (weaponMap.ContainsKey(mode))
                        {
                            string externalName = weaponMap[mode];
                            string key = $"{weaponType}_{suffix}";
                            if (!currentConfig.Animations.ContainsKey(key))
                            {
                                currentConfig.Animations[key] = new AnimationTiming();
                            }
                            if (!mappings.ContainsKey(key))
                            {
                                mappings[key] = externalName;
                            }
                        }
                    }
                }

                // Weapon type maps: Q/T/G for each weapon type
                string[] weaponTypes = { "Swords", "Axes", "Clubs", "Spears", "GreatSwords", "BattleAxes", "Polearms", "Knives", "Fists" };
                foreach (var weaponType in weaponTypes)
                {
                    AppendFromWeaponType(weaponType, "ea_secondary_Q", "secondary_Q");
                    AppendFromWeaponType(weaponType, "ea_secondary_T", "secondary_T");
                    AppendFromWeaponType(weaponType, "ea_secondary_G", "secondary_G");
                }

                // DEPRECATED: Persist with comments including replacement names
                // SaveConfigWithComments(mappings); // Disabled - use weapon type config instead
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error appending timing entries from ReplacementMap: {ex.Message}");
            }
        }

        // Reload config (for runtime changes)
        public static void Reload()
        {
            ExtraAttackPlugin.LogInfo("System", $"F6: AnimationTimingConfig.Reload() - Checking files...");
            ExtraAttackPlugin.LogInfo("System", $"F6: WeaponTypesConfigFilePath: {WeaponTypesConfigFilePath}");
            ExtraAttackPlugin.LogInfo("System", $"F6: IndividualWeaponsConfigFilePath: {IndividualWeaponsConfigFilePath}");
            ExtraAttackPlugin.LogInfo("System", $"F6: WeaponTypes file exists: {File.Exists(WeaponTypesConfigFilePath)}");
            ExtraAttackPlugin.LogInfo("System", $"F6: IndividualWeapons file exists: {File.Exists(IndividualWeaponsConfigFilePath)}");
            
            if (File.Exists(WeaponTypesConfigFilePath))
            {
                ExtraAttackPlugin.LogInfo("System", "F6: Loading WeaponTypes config...");
                LoadWeaponTypeConfig();
            }
            else
            {
                ExtraAttackPlugin.LogWarning("System", "F6: WeaponTypes YAML file missing, skipping reload");
            }
            
            if (File.Exists(IndividualWeaponsConfigFilePath))
            {
                ExtraAttackPlugin.LogInfo("System", "F6: Loading IndividualWeapons config...");
                LoadIndividualWeaponsConfig();
            }
            else
            {
                ExtraAttackPlugin.LogWarning("System", "F6: IndividualWeapons YAML file missing, skipping reload");
            }
            
            // Apply the reloaded settings to the manager
            ExtraAttackPlugin.LogInfo("System", "F6: Applying reloaded settings to manager...");
            AnimationManager.ApplyWeaponTypeSettings();
            ExtraAttackPlugin.LogInfo("System", "F6: AnimationTimingConfig reload completed");
        }

        // Load weapon type specific config
        private static void LoadWeaponTypeConfig()
        {
            try
            {
                var deserializer = new DeserializerBuilder()
                    .IgnoreUnmatchedProperties()
                    .Build();

                var yamlContent = File.ReadAllText(WeaponTypesConfigFilePath);
                ExtraAttackPlugin.LogInfo("Config", $"YAML content length: {yamlContent.Length}");
                ExtraAttackPlugin.LogInfo("Config", $"YAML content preview (first 500 chars): {yamlContent.Substring(0, Math.Min(500, yamlContent.Length))}");
                
                // Try to deserialize with better error handling
                try
                {
                    weaponTypeConfig = deserializer.Deserialize<WeaponTypeConfig>(yamlContent) ?? new WeaponTypeConfig();
                }
                catch (Exception deserializeEx)
                {
                    ExtraAttackPlugin.LogError("System", $"Deserialization failed: {deserializeEx.Message}");
                    ExtraAttackPlugin.LogError("System", $"YAML content causing error: {yamlContent}");
                    throw; // Re-throw to be caught by outer catch
                }
                
                ExtraAttackPlugin.LogInfo("Config", $"Loaded weapon type config: {weaponTypeConfig?.WeaponTypes?.Count ?? 0} weapon types");
                ExtraAttackPlugin.LogInfo("Config", $"weaponTypeConfig is null: {weaponTypeConfig == null}");
                ExtraAttackPlugin.LogInfo("Config", $"WeaponTypes is null: {weaponTypeConfig?.WeaponTypes == null}");
                
                if (weaponTypeConfig?.WeaponTypes != null)
                {
                    foreach (var weaponType in weaponTypeConfig.WeaponTypes)
                    {
                        ExtraAttackPlugin.LogInfo("Config", $"  {weaponType.Key}: {weaponType.Value.Count} attack modes");
                    }
                }
                else
                {
                    ExtraAttackPlugin.LogWarning("Config", "WeaponTypes is null or empty after deserialization");
                }
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error loading weapon type config: {ex.Message}");
                ExtraAttackPlugin.LogError("System", $"Stack trace: {ex.StackTrace}");
                weaponTypeConfig = new WeaponTypeConfig();
            }
        }

        // Load individual weapons config
        private static void LoadIndividualWeaponsConfig()
        {
            try
            {
                var deserializer = new DeserializerBuilder()
                    .IgnoreUnmatchedProperties()
                    .Build();

                var yamlContent = File.ReadAllText(IndividualWeaponsConfigFilePath);
                var individualConfig = deserializer.Deserialize<WeaponTypeConfig>(yamlContent) ?? new WeaponTypeConfig();
                
                // Merge individual weapons into main config
                if (weaponTypeConfig.IndividualWeapons == null)
                {
                    weaponTypeConfig.IndividualWeapons = new Dictionary<string, AnimationTiming>();
                }
                
                if (individualConfig.IndividualWeapons != null)
                {
                    foreach (var kvp in individualConfig.IndividualWeapons)
                    {
                        weaponTypeConfig.IndividualWeapons[kvp.Key] = kvp.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error loading individual weapons config: {ex.Message}");
            }
        }

        // Create default weapon type config
        private static void CreateDefaultWeaponTypeConfig()
        {
            try
            {
                ExtraAttackPlugin.LogInfo("Config", "Generating default WeaponTypes config");
                GenerateWeaponTypeConfig();
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error creating default weapon type config: {ex.Message}");
            }
        }

        // Create default individual weapons config
        private static void CreateDefaultIndividualWeaponsConfig()
        {
            try
            {
                ExtraAttackPlugin.LogInfo("Config", "Generating default IndividualWeapons config");
                var config = new WeaponTypeConfig();
                CreateIndividualWeaponSettings(config);
                SaveIndividualWeaponsConfig(config);
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error creating default individual weapons config: {ex.Message}");
            }
        }

        // Save individual weapons config
        private static void SaveIndividualWeaponsConfig(WeaponTypeConfig config)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("# ============================================================================");
                sb.AppendLine("# Extra Attack System - Individual Weapons Attack Config");
                sb.AppendLine("# å€‹åˆ¥æ­¦å™¨æ”»æ’ƒè¨­å®šãƒ•ã‚¡ã‚¤ãƒ«");
                sb.AppendLine("# ============================================================================");
                sb.AppendLine("# Format: IndividualWeapon -> Q/T/G -> Timing Settings");
                sb.AppendLine("# å½¢å¼: å€‹åˆ¥æ­¦å™¨ -> Q/T/G -> ã‚¿ã‚¤ãƒŸãƒ³ã‚°è¨­å®š");
                sb.AppendLine("# ============================================================================");
                sb.AppendLine();

                if (config.IndividualWeapons != null && config.IndividualWeapons.Count > 0)
                {
                    sb.AppendLine("IndividualWeapons:");
                    
                    // Group by individual weapon name and sort by Q/T/G
                    var individualWeaponGroups = new Dictionary<string, Dictionary<string, AnimationTiming>>();
                    
                    foreach (var kvp in config.IndividualWeapons)
                    {
                        string key = kvp.Key;
                        AnimationTiming timing = kvp.Value;
                        
                        // Extract weapon name and mode from key (e.g., "SwordBlackmetal_secondary_Q" -> "SwordBlackmetal", "Q")
                        string weaponName = key;
                        string mode = "Q";
                        
                        if (key.Contains("_secondary_Q"))
                        {
                            weaponName = key.Replace("_secondary_Q", "");
                            mode = "Q";
                        }
                        else if (key.Contains("_secondary_T"))
                        {
                            weaponName = key.Replace("_secondary_T", "");
                            mode = "T";
                        }
                        else if (key.Contains("_secondary_G"))
                        {
                            weaponName = key.Replace("_secondary_G", "");
                            mode = "G";
                        }
                        
                        // Skip weapon types (only process individual weapons)
                        string[] weaponTypes = { "Swords", "Axes", "Clubs", "Spears", "Polearms", "Knives", "Fists", "BattleAxes", "GreatSwords", "Unarmed", "DualAxes", "DualKnives", "Sledges", "Torch" };
                        if (weaponTypes.Contains(weaponName))
                        {
                            continue; // Skip weapon types
                        }
                        
                        if (!individualWeaponGroups.ContainsKey(weaponName))
                        {
                            individualWeaponGroups[weaponName] = new Dictionary<string, AnimationTiming>();
                        }
                        
                        individualWeaponGroups[weaponName][mode] = timing;
                    }
                    
                    // Output in weapon name order, then Q/T/G order
                    foreach (var weaponName in individualWeaponGroups.Keys.OrderBy(k => k))
                    {
                        sb.AppendLine($"  # {weaponName} / {weaponName}");
                        sb.AppendLine($"  {weaponName}:");
                        
                        var modes = new[] { "Q", "T", "G" };
                        foreach (var mode in modes)
                        {
                            if (individualWeaponGroups[weaponName].ContainsKey(mode))
                            {
                                var timing = individualWeaponGroups[weaponName][mode];
                                sb.AppendLine($"    {mode}:");
                                sb.AppendLine($"      HitTiming: {timing.HitTiming:F3}");
                                sb.AppendLine($"      TrailOnTiming: {timing.TrailOnTiming:F3}");
                                sb.AppendLine($"      TrailOffTiming: {timing.TrailOffTiming:F3}");
                                sb.AppendLine($"      AttackRange: {timing.AttackRange:F3}");
                                sb.AppendLine($"      AttackHeight: {timing.AttackHeight:F3}");
                                sb.AppendLine();
                            }
                        }
                        sb.AppendLine();
                    }
                }

                File.WriteAllText(IndividualWeaponsConfigFilePath, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error saving individual weapons config: {ex.Message}");
            }
        }

        // Helper method to get replacement name for comments
        private static string GetReplacementName(string key)
        {
            // Extract replacement name from key
            if (key.Contains("_secondary_Q"))
            {
                return key.Replace("_secondary_Q", "_Q");
            }
            else if (key.Contains("_secondary_T"))
            {
                return key.Replace("_secondary_T", "_T");
            }
            else if (key.Contains("_secondary_G"))
            {
                return key.Replace("_secondary_G", "_G");
            }
            return key;
        }

        // Generate weapon type config programmatically
        private static void GenerateWeaponTypeConfig()
        {
            try
            {
                ExtraAttackPlugin.LogInfo("Config", "GenerateWeaponTypeConfig: Starting generation");
                var config = new WeaponTypeConfig();
                config.Default = new AnimationTiming();

                // Define weapon types and their Q/T/G mappings
                var weaponTypeMappings = new Dictionary<string, Dictionary<string, string>>
                {
                    ["Swords"] = new Dictionary<string, string>
                    {
                        ["Q"] = "Swords",      // Sword secondary
                        ["T"] = "GreatSwords",  // Great Sword secondary
                        ["G"] = "BattleAxes"    // Battle Axe secondary
                    },
                    ["Axes"] = new Dictionary<string, string>
                    {
                        ["Q"] = "Axes",        // Axe secondary
                        ["T"] = "GreatSwords",  // Great Sword secondary
                        ["G"] = "Polearms"      // Polearm secondary
                    },
                    ["Clubs"] = new Dictionary<string, string>
                    {
                        ["Q"] = "Clubs",       // Club secondary
                        ["T"] = "GreatSwords", // Great Sword secondary
                        ["G"] = "BattleAxes"   // Battle Axe secondary
                    },
                    ["Spears"] = new Dictionary<string, string>
                    {
                        ["Q"] = "Spears",      // Spear secondary
                        ["T"] = "GreatSwords", // Great Sword secondary
                        ["G"] = "BattleAxes"   // Battle Axe secondary
                    },
                    ["GreatSwords"] = new Dictionary<string, string>
                    {
                        ["Q"] = "GreatSwords", // Great Sword secondary
                        ["T"] = "BattleAxes",  // Battle Axe secondary
                        ["G"] = "Polearms"     // Polearm secondary
                    },
                    ["BattleAxes"] = new Dictionary<string, string>
                    {
                        ["Q"] = "BattleAxes",  // Battle Axe secondary
                        ["T"] = "GreatSwords", // Great Sword secondary
                        ["G"] = "Polearms"     // Polearm secondary
                    },
                    ["Polearms"] = new Dictionary<string, string>
                    {
                        ["Q"] = "Polearms",    // Polearm secondary
                        ["T"] = "GreatSwords", // Great Sword secondary
                        ["G"] = "BattleAxes"   // Battle Axe secondary
                    }
                };

                // Generate settings for each weapon type
                foreach (var weaponType in weaponTypeMappings.Keys)
                {
                    ExtraAttackPlugin.LogInfo("Config", $"GenerateWeaponTypeConfig: Processing weapon type: {weaponType}");
                    var weaponSettings = new Dictionary<string, AnimationTiming>();
                    var mappings = weaponTypeMappings[weaponType];

                    foreach (var mode in new[] { "Q", "T", "G" })
                    {
                        var targetWeaponType = mappings[mode];
                        var key = $"{weaponType}_secondary_{mode}";
                        
                        ExtraAttackPlugin.LogInfo("Config", $"GenerateWeaponTypeConfig: Creating timing for {key} -> {targetWeaponType}_{mode}");
                        
                        // Create timing based on target weapon type
                        var timing = CreateTimingForWeaponType(targetWeaponType, mode);
                        weaponSettings[key] = timing;
                    }

                    config.WeaponTypes[weaponType] = weaponSettings;
                    ExtraAttackPlugin.LogInfo("Config", $"GenerateWeaponTypeConfig: Added {weaponType} with {weaponSettings.Count} modes");
                }

                // Create individual weapon settings
                CreateIndividualWeaponSettings(config);

                // Save the generated config
                ExtraAttackPlugin.LogInfo("Config", $"GenerateWeaponTypeConfig: Final config has {config.WeaponTypes.Count} weapon types");
                SaveWeaponTypeConfig(config);
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error generating weapon type config: {ex.Message}");
            }
        }

        // Create timing settings for specific weapon type and mode based on actual animation clip lengths
        private static AnimationTiming CreateTimingForWeaponType(string weaponType, string mode)
        {
            var timing = new AnimationTiming();
            
            // Try to get actual animation clip length from ReplacementMap using unified key format
            string key = $"{weaponType}_Secondary_{mode}";
            float clipLength = GetAdjustedClipLength(key);
            
            if (clipLength > 0)
            {
                // Calculate timing based on actual clip length
                timing.HitTiming = CalculateHitTiming(clipLength, weaponType);
                timing.TrailOnTiming = CalculateTrailOnTiming(clipLength, weaponType);
                timing.TrailOffTiming = CalculateTrailOffTiming(clipLength, weaponType);
                timing.AttackRange = CalculateAttackRange(weaponType, mode);
                timing.AttackHeight = CalculateAttackHeight(weaponType, mode);
            }
            else
            {
                // Fallback to base settings for different weapon types
                ApplyBaseSettingsForWeaponType(timing, weaponType);
            }

            return timing;
        }
        
        // Get target weapon type for the given mode
        private static string GetTargetWeaponTypeForMode(string weaponType, string mode)
        {
            // Define weapon type mappings for Q/T/G modes
            var weaponTypeMappings = new Dictionary<string, Dictionary<string, string>>
            {
                ["Swords"] = new Dictionary<string, string>
                {
                    ["Q"] = "Swords",
                    ["T"] = "GreatSwords",
                    ["G"] = "BattleAxes"
                },
                ["Axes"] = new Dictionary<string, string>
                {
                    ["Q"] = "Axes",
                    ["T"] = "GreatSwords",
                    ["G"] = "Polearms"
                },
                ["Clubs"] = new Dictionary<string, string>
                {
                    ["Q"] = "Clubs",
                    ["T"] = "GreatSwords",
                    ["G"] = "BattleAxes"
                },
                ["Spears"] = new Dictionary<string, string>
                {
                    ["Q"] = "Spears",
                    ["T"] = "GreatSwords",
                    ["G"] = "BattleAxes"
                },
                ["GreatSwords"] = new Dictionary<string, string>
                {
                    ["Q"] = "GreatSwords",
                    ["T"] = "BattleAxes",
                    ["G"] = "Polearms"
                },
                ["BattleAxes"] = new Dictionary<string, string>
                {
                    ["Q"] = "BattleAxes",
                    ["T"] = "GreatSwords",
                    ["G"] = "Polearms"
                },
                ["Polearms"] = new Dictionary<string, string>
                {
                    ["Q"] = "Polearms",
                    ["T"] = "GreatSwords",
                    ["G"] = "BattleAxes"
                }
            };
            
            if (weaponTypeMappings.TryGetValue(weaponType, out var mappings) && 
                mappings.TryGetValue(mode, out var targetType))
            {
                return targetType;
            }
            
            return weaponType; // Fallback to same weapon type
        }
        
        // Get adjusted clip length from ReplacementMap, fallback to vanilla if external not found
        private static float GetAdjustedClipLength(string key)
        {
            try
            {
                if (AnimationManager.ReplacementMap.TryGetValue(key, out var replacementMap) && 
                    replacementMap != null && replacementMap.Count > 0)
                {
                    // Get the first replacement clip name
                    var firstReplacement = replacementMap.Values.First();
                    if (!string.IsNullOrEmpty(firstReplacement))
                    {
                        // Get actual clip length from AnimationManager (with smart caching)
                        float clipLength = AnimationManager.GetExternalClipLengthSmart(firstReplacement);
                        if (clipLength > 0)
                        {
                            ExtraAttackPlugin.LogInfo("Config", $"Using external clip length for {key}: {clipLength:F3}s");
                            return clipLength;
                        }
                    }
                    
                    // If external clip not found, try to get vanilla clip length
                    var firstVanilla = replacementMap.Keys.First();
                    if (!string.IsNullOrEmpty(firstVanilla))
                    {
                        float vanillaLength = AnimationManager.GetVanillaClipLength(firstVanilla);
                        if (vanillaLength > 0)
                        {
                            ExtraAttackPlugin.LogInfo("Config", $"Using vanilla clip length for {key}: {vanillaLength:F3}s (external not found)");
                            return vanillaLength;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error getting adjusted clip length for {key}: {ex.Message}");
            }
            
            return -1f; // Indicate no valid clip found
        }
        
        // Calculate hit timing based on clip length and weapon type
        private static float CalculateHitTiming(float clipLength, string weaponType)
        {
            // Base hit timing ratio (0.45 for 1.0s clip)
            float baseRatio = 0.45f;
            
            // Adjust based on weapon type
            float weaponMultiplier = weaponType switch
            {
                "GreatSwords" => 1.1f,
                "BattleAxes" => 1.05f,
                "Polearms" => 1.15f,
                _ => 1.0f
            };
            
            return Math.Min(0.8f, baseRatio * weaponMultiplier); // Cap at 0.8
        }
        
        // Calculate trail on timing based on clip length and weapon type
        private static float CalculateTrailOnTiming(float clipLength, string weaponType)
        {
            // Base trail on timing ratio (0.35 for 1.0s clip)
            float baseRatio = 0.35f;
            
            // Adjust based on weapon type
            float weaponMultiplier = weaponType switch
            {
                "GreatSwords" => 1.1f,
                "BattleAxes" => 1.05f,
                "Polearms" => 1.15f,
                _ => 1.0f
            };
            
            return Math.Min(0.7f, baseRatio * weaponMultiplier); // Cap at 0.7
        }
        
        // Calculate trail off timing based on clip length and weapon type
        private static float CalculateTrailOffTiming(float clipLength, string weaponType)
        {
            // Base trail off timing ratio (0.70 for 1.0s clip)
            float baseRatio = 0.70f;
            
            // Adjust based on weapon type
            float weaponMultiplier = weaponType switch
            {
                "GreatSwords" => 1.05f,
                "BattleAxes" => 1.1f,
                "Polearms" => 1.15f,
                _ => 1.0f
            };
            
            return Math.Min(0.95f, baseRatio * weaponMultiplier); // Cap at 0.95
        }
        
        // Calculate attack range based on weapon type and mode
        private static float CalculateAttackRange(string weaponType, string mode)
        {
            float baseRange = weaponType switch
            {
                "Swords" => 1.50f,
                "Axes" => 1.50f,
                "Clubs" => 1.50f,
                "Spears" => 1.50f,
                "GreatSwords" => 2.00f,
                "BattleAxes" => 1.80f,
                "Polearms" => 2.20f,
                _ => 1.50f
            };
            
            // Adjust based on mode
            float modeMultiplier = mode switch
            {
                "Q" => 1.0f,
                "T" => 1.1f,
                "G" => 1.2f,
                _ => 1.0f
            };
            
            return baseRange * modeMultiplier;
        }
        
        // Calculate attack height based on weapon type and mode
        private static float CalculateAttackHeight(string weaponType, string mode)
        {
            float baseHeight = weaponType switch
            {
                "Swords" => 0.60f,
                "Axes" => 0.60f,
                "Clubs" => 0.60f,
                "Spears" => 0.60f,
                "GreatSwords" => 0.80f,
                "BattleAxes" => 0.70f,
                "Polearms" => 0.70f,
                _ => 0.60f
            };
            
            // Adjust based on mode
            float modeMultiplier = mode switch
            {
                "Q" => 1.0f,
                "T" => 1.1f,
                "G" => 1.2f,
                _ => 1.0f
            };
            
            return baseHeight * modeMultiplier;
        }
        
        // Apply base settings for weapon type (fallback)
        private static void ApplyBaseSettingsForWeaponType(AnimationTiming timing, string weaponType)
        {
            switch (weaponType)
            {
                case "Swords":
                    timing.HitTiming = 0.45f;
                    timing.TrailOnTiming = 0.35f;
                    timing.TrailOffTiming = 0.70f;
                    timing.AttackRange = 1.50f;
                    timing.AttackHeight = 0.60f;
                    break;
                case "Axes":
                    timing.HitTiming = 0.45f;
                    timing.TrailOnTiming = 0.35f;
                    timing.TrailOffTiming = 0.70f;
                    timing.AttackRange = 1.50f;
                    timing.AttackHeight = 0.60f;
                    break;
                case "Clubs":
                    timing.HitTiming = 0.45f;
                    timing.TrailOnTiming = 0.35f;
                    timing.TrailOffTiming = 0.70f;
                    timing.AttackRange = 1.50f;
                    timing.AttackHeight = 0.60f;
                    break;
                case "Spears":
                    timing.HitTiming = 0.45f;
                    timing.TrailOnTiming = 0.35f;
                    timing.TrailOffTiming = 0.70f;
                    timing.AttackRange = 1.50f;
                    timing.AttackHeight = 0.60f;
                    break;
                case "GreatSwords":
                    timing.HitTiming = 0.50f;
                    timing.TrailOnTiming = 0.40f;
                    timing.TrailOffTiming = 0.75f;
                    timing.AttackRange = 2.00f;
                    timing.AttackHeight = 0.80f;
                    break;
                case "BattleAxes":
                    timing.HitTiming = 0.55f;
                    timing.TrailOnTiming = 0.45f;
                    timing.TrailOffTiming = 0.80f;
                    timing.AttackRange = 1.80f;
                    timing.AttackHeight = 0.70f;
                    break;
                case "Polearms":
                    timing.HitTiming = 0.60f;
                    timing.TrailOnTiming = 0.50f;
                    timing.TrailOffTiming = 0.85f;
                    timing.AttackRange = 2.20f;
                    timing.AttackHeight = 0.70f;
                    break;
                default:
                    // Use default values
                    break;
            }
        }

        // Create individual weapon settings
        private static void CreateIndividualWeaponSettings(WeaponTypeConfig config)
        {
            // Individual weapon overrides - only create sample entries
            var individualWeapons = new Dictionary<string, AnimationTiming>();

            // Sample: Wooden Greatsword custom settings (demonstration only)
            var woodenGreatswordQ = new AnimationTiming();
            woodenGreatswordQ.HitTiming = 0.55f;
            woodenGreatswordQ.TrailOnTiming = 0.35f;
            woodenGreatswordQ.TrailOffTiming = 0.70f;
            woodenGreatswordQ.AttackRange = 2.20f;
            woodenGreatswordQ.AttackHeight = 0.90f;
            individualWeapons["ea_secondary_Q_THSwordWood"] = woodenGreatswordQ;

            var woodenGreatswordT = new AnimationTiming();
            woodenGreatswordT.HitTiming = 0.60f;
            woodenGreatswordT.TrailOnTiming = 0.35f;
            woodenGreatswordT.TrailOffTiming = 0.70f;
            woodenGreatswordT.AttackRange = 1.90f;
            woodenGreatswordT.AttackHeight = 0.80f;
            individualWeapons["ea_secondary_T_THSwordWood"] = woodenGreatswordT;

            var woodenGreatswordG = new AnimationTiming();
            woodenGreatswordG.HitTiming = 0.65f;
            woodenGreatswordG.TrailOnTiming = 0.35f;
            woodenGreatswordG.TrailOffTiming = 0.70f;
            woodenGreatswordG.AttackRange = 2.40f;
            woodenGreatswordG.AttackHeight = 0.80f;
            individualWeapons["ea_secondary_G_THSwordWood"] = woodenGreatswordG;

            config.IndividualWeapons = individualWeapons;
        }

        // Get weapon type from weapon identifier and skill type
        // æ­¦å™¨åã¨ã‚¹ã‚­ãƒ«ã‚¿ã‚¤ãƒ—ã‹ã‚‰æ­£ã—ã„æ­¦å™¨ã‚¿ã‚¤ãƒ—ã‚’åˆ¤å®šã™ã‚‹
        private static string GetWeaponTypeFromIdent(string weaponIdent, Skills.SkillType skillType)
        {
            if (string.IsNullOrEmpty(weaponIdent)) return skillType.ToString();
            
            // Check if it's a 2H weapon by looking for 2H indicators in the name
            bool is2H = weaponIdent.Contains("2H") || weaponIdent.Contains("2h") || 
                       weaponIdent.Contains("TwoHand") || weaponIdent.Contains("twohand") ||
                       weaponIdent.Contains("battleaxe") || weaponIdent.Contains("BattleAxe") ||
                       weaponIdent.Contains("greatsword") || weaponIdent.Contains("GreatSword");
            
            // Battle Axe is Axes skill type + 2H, Great Sword is Swords skill type + 2H
            if (skillType == Skills.SkillType.Axes)
            {
                if (is2H)
                    return "BattleAxes";
                else
                    return "Axes";
            }
            else if (skillType == Skills.SkillType.Swords)
            {
                if (is2H)
                    return "GreatSwords";
                else
                    return "Swords";
            }
            
            return skillType.ToString();
        }

        // Get vanilla clip name for weapon type and mode
        // è£…å‚™ã—ã¦ã„ã‚‹æ­¦å™¨ã®å®Ÿéš›ã®ã‚»ã‚«ãƒ³ãƒ€ãƒªã‚¢ãƒ‹ãƒ¡ãƒ¼ã‚·ãƒ§ãƒ³ã‚¯ãƒªãƒƒãƒ—åã‚’è¿”ã™
        private static string GetVanillaClipName(string weaponType, string mode)
        {
            // Always return the equipped weapon's secondary trigger name
            return weaponType switch
            {
                "Swords" => "Sword-Attack-R4", // sword_secondary trigger
                "GreatSwords" => "Greatsword Secondary Attack", // greatsword_secondary trigger
                "Axes" => "Axe Secondary Attack", // axe_secondary trigger
                "Clubs" => "MaceAltAttack", // club_secondary trigger
                "Spears" => "throw_spear", // spear_throw trigger
                "BattleAxes" => "BattleAxeAltAttack", // battleaxe_secondary trigger
                "Polearms" => "Atgeir360Attack", // polearm_secondary trigger
                "Knives" => "Knife JumpAttack", // knife_secondary trigger
                "Fists" => "Kickstep", // fist_secondary trigger
                _ => "Sword-Attack-R4"
            };
        }

        // Get replacement animation name for weapon type and mode
        private static string GetReplacementAnimationName(string weaponType, string mode)
        {
            // Check if we have the mapping in AnimationManager
            if (AnimationManager.ReplacementMap.ContainsKey(weaponType))
            {
                var weaponMap = AnimationManager.ReplacementMap[weaponType];
                
                // NEW: ãƒ¢ãƒ¼ãƒ‰ã‚­ãƒ¼ (ea_secondary_Q/T/G) ã§ç›´æŽ¥æ¤œç´¢
                string modeKey = $"ea_secondary_{mode}";
                if (weaponMap.ContainsKey(modeKey))
                {
                    string actualAnimation = weaponMap[modeKey];
                    return actualAnimation;
                }
                else
                {
                }
            }
            else
            {
            }
            
            // Fallback to default names (matching AnimationManager.cs assignments)
            string fallbackAnimation = weaponType switch
            {
                "Swords" => mode == "Q" ? "2Hand-Sword-Attack8External" : mode == "T" ? "2Hand_Skill01_WhirlWindExternal" : "Eas_GreatSword_JumpAttackExternal",
                "Axes" => mode == "Q" ? "Axe Secondary AttackExternal" : mode == "T" ? "2Hand-Sword-Attack8External" : "Eas_GreatSword_JumpAttackExternal",
                "Clubs" => mode == "Q" ? "Eas_GreatSword_CastingExternal" : mode == "T" ? "2Hand_Skill01_WhirlWindExternal" : "2Hand-Sword-Attack8External",
                "Spears" => mode == "Q" ? "Eas_GreatSword_JumpAttackExternal" : mode == "T" ? "2Hand_Skill01_WhirlWindExternal" : "2Hand-Sword-Attack8External",
                "GreatSwords" => mode == "Q" ? "2Hand-Sword-Attack8External" : mode == "T" ? "2Hand_Skill01_WhirlWindExternal" : "Eas_GreatSword_JumpAttackExternal",
                "BattleAxes" => mode == "Q" ? "2Hand_Skill01_WhirlWindExternal" : mode == "T" ? "2Hand-Sword-Attack8External" : "Eas_GreatSword_JumpAttackExternal",
                "Polearms" => mode == "Q" ? "Eas_GreatSword_JumpAttackExternal" : mode == "T" ? "2Hand-Sword-Attack8External" : "2Hand_Skill01_WhirlWindExternal",
                "Knives" => mode == "Q" ? "ChargeAttkExternal" : mode == "T" ? "Eas_GreatSword_JumpAttackExternal" : "2Hand-Sword-Attack8External",
                "Fists" => mode == "Q" ? "Flying Knee Punch ComboExternal" : mode == "T" ? "2Hand_Skill01_WhirlWindExternal" : "Eas_GreatSword_JumpAttackExternal",
                _ => "Unknown"
            };
            
            return fallbackAnimation;
        }

        // Save weapon type config to file with custom formatting
        private static void SaveWeaponTypeConfig(WeaponTypeConfig config)
        {
            try
            {
                var sb = new StringBuilder();
                
                // Header
                sb.AppendLine("# ==============================================================================");
                sb.AppendLine("# Extra Attack System - Weapon Type Specific Configuration");
                sb.AppendLine("# æ­¦å™¨ç¨®åˆ¥ã”ã¨ã®è¨­å®šãƒ•ã‚¡ã‚¤ãƒ«");
                sb.AppendLine("# ==============================================================================");
                sb.AppendLine("# IMPORTANT: Configuration is organized by weapon types (Swords, Axes, Clubs, etc.)");
                sb.AppendLine("# é‡è¦: è¨­å®šã¯æ­¦å™¨ç¨®åˆ¥ï¼ˆå‰£ã€æ–§ã€æ£æ£’ãªã©ï¼‰ã”ã¨ã«æ•´ç†ã•ã‚Œã¦ã„ã¾ã™");
                sb.AppendLine("# ==============================================================================");
                sb.AppendLine();
                sb.AppendLine("# Key Format / ã‚­ãƒ¼å½¢å¼:");
                sb.AppendLine("#   WeaponType_secondary_Q      - Q key ( secondary_Q secondary attack)");
                sb.AppendLine("#   WeaponType_secondary_T      - T key ( secondary_T secondary attack)  ");
                sb.AppendLine("#   WeaponType_secondary_G      - G key ( secondary_G secondary attack)");
                sb.AppendLine("#   WeaponType_secondary_Q_hit0 - Q key, first hit (multi-hit)");
                sb.AppendLine("#   WeaponType                  - Fallback (no suffix)");
                sb.AppendLine();
                sb.AppendLine("# ==============================================================================");
                sb.AppendLine("# Default Settings");
                sb.AppendLine("# ==============================================================================");
                sb.AppendLine("Default:");
                sb.AppendLine("  # Animation Event Timing (0.0 ~ 1.0)");
                sb.AppendLine($"  HitTiming: {config.Default.HitTiming:F2}");
                sb.AppendLine($"  TrailOnTiming: {config.Default.TrailOnTiming:F2}");
                sb.AppendLine($"  TrailOffTiming: {config.Default.TrailOffTiming:F2}");
                sb.AppendLine($"  ChainTiming: {config.Default.ChainTiming:F2}  # defaults = 0.00 (disabled), set per-clip if needed");
                sb.AppendLine($"  SpeedMultiplier: {config.Default.SpeedMultiplier:F2}");
                sb.AppendLine();
                sb.AppendLine("  # Attack Detection Parameters");
                sb.AppendLine($"  AttackRange: {config.Default.AttackRange:F2}");
                sb.AppendLine($"  AttackHeight: {config.Default.AttackHeight:F2}");
                sb.AppendLine($"  AttackOffset: {config.Default.AttackOffset:F2}");
                sb.AppendLine($"  AttackAngle: {config.Default.AttackAngle:F2}");
                sb.AppendLine($"  AttackRayWidth: {config.Default.AttackRayWidth:F2}");
                sb.AppendLine($"  AttackRayWidthCharExtra: {config.Default.AttackRayWidthCharExtra:F2}");
                sb.AppendLine($"  AttackHeightChar1: {config.Default.AttackHeightChar1:F2}");
                sb.AppendLine($"  AttackHeightChar2: {config.Default.AttackHeightChar2:F2}");
                sb.AppendLine($"  MaxYAngle: {config.Default.MaxYAngle:F2}");
                sb.AppendLine("  # Enable Flags");
                sb.AppendLine($"  EnableHit: {(config.Default.EnableHit ? "true" : "false")}");
                sb.AppendLine($"  EnableSound: {(config.Default.EnableSound ? "true" : "false")}");
                sb.AppendLine($"  EnableVFX: {(config.Default.EnableVFX ? "true" : "false")}");
                sb.AppendLine();
                sb.AppendLine("# ==============================================================================");
                sb.AppendLine("# Weapon Type Specific Settings");
                sb.AppendLine("# ==============================================================================");
                sb.AppendLine();
                sb.AppendLine("WeaponTypes:");
                
                // Filter only weapon types (not individual weapons)
                string[] validWeaponTypes = { "Swords", "Axes", "Clubs", "Spears", "Polearms", "Knives", "Fists", "BattleAxes", "GreatSwords", "Unarmed", "DualAxes", "DualKnives", "Sledges", "Torch" };
                
                var filteredWeaponTypes = config.WeaponTypes
                    .Where(kvp => validWeaponTypes.Contains(kvp.Key))
                    .OrderBy(kvp => kvp.Key);
                
                foreach (var weaponType in filteredWeaponTypes)
                {
                    sb.AppendLine($"  # ========== {weaponType.Key} ==========");
                    sb.AppendLine($"  {weaponType.Key}:");
                    
                    // Sort by Q, T, G order (unified format: {WeaponType}_Secondary_{Mode})
                    var sortedModes = weaponType.Value.OrderBy(kvp => 
                        kvp.Key.Contains("_Secondary_Q") ? 0 : 
                        kvp.Key.Contains("_Secondary_T") ? 1 : 
                        kvp.Key.Contains("_Secondary_G") ? 2 : 3);
                    
                    foreach (var mode in sortedModes)
                    {
                        var timing = mode.Value;
                        
                        // Extract mode (Q/T/G) and get replacement animation name
                        string modeKey = mode.Key.Replace($"{weaponType.Key}_Secondary_", "");
                        string replacementAnimation = GetReplacementAnimationName(weaponType.Key, modeKey);
                        
                        sb.AppendLine($"    # ea_secondary_{modeKey} - ExtraAttack 1 -> {replacementAnimation}");
                        sb.AppendLine($"    {mode.Key}:");
                        sb.AppendLine($"      HitTiming: {timing.HitTiming:F2}  # Hit event timing");
                        sb.AppendLine($"      TrailOnTiming: {timing.TrailOnTiming:F2}  # TrailOn event timing");
                        sb.AppendLine($"      TrailOffTiming: {timing.TrailOffTiming:F2}  # TrailOff event timing");
                        sb.AppendLine($"      # Attack Parameters (from vanilla Attack class)");
                        sb.AppendLine($"      AttackRange: {timing.AttackRange:F2}  # m_attackRange");
                        sb.AppendLine($"      AttackHeight: {timing.AttackHeight:F2}  # m_attackHeight");
                        sb.AppendLine($"      AttackOffset: {timing.AttackOffset:F2}  # m_attackOffset");
                        sb.AppendLine($"      AttackAngle: {timing.AttackAngle:F2}  # m_attackAngle");
                        sb.AppendLine($"      AttackHeightChar1: {timing.AttackHeightChar1:F2}  # m_attackHeightChar1");
                        sb.AppendLine($"      AttackHeightChar2: {timing.AttackHeightChar2:F2}  # m_attackHeightChar2");
                        sb.AppendLine($"      MaxYAngle: {timing.MaxYAngle:F2}  # m_maxYAngle");
                        sb.AppendLine($"      # Attack Control Flags");
                        sb.AppendLine($"      EnableHit: {(timing.EnableHit ? "true" : "false")}  # Enable hit detection");
                        sb.AppendLine($"      EnableSound: {(timing.EnableSound ? "true" : "false")}  # Enable attack sound");
                    }
                    sb.AppendLine();
                }

                File.WriteAllText(WeaponTypesConfigFilePath, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error saving weapon type config: {ex.Message}");
            }
        }
        

        // Get timing for weapon type and attack mode
        public static AnimationTiming GetWeaponTypeTiming(string weaponType, string attackMode)
        {
            try
            {
                // Try individual weapon first
                if (weaponTypeConfig.IndividualWeapons.TryGetValue($"{weaponType}_{attackMode}", out var individualTiming))
                {
                    ExtraAttackPlugin.LogInfo("Config", $"Using individual weapon setting: {weaponType}_{attackMode}");
                    return individualTiming;
                }

                // Try weapon type specific using unified key format
                if (weaponTypeConfig.WeaponTypes.TryGetValue(weaponType, out var weaponTypeSettings))
                {
                    string unifiedKey = $"{weaponType}_Secondary_{attackMode}";
                    if (weaponTypeSettings.TryGetValue(unifiedKey, out var typeTiming))
                    {
                        ExtraAttackPlugin.LogInfo("Config", $"Using weapon type setting: {unifiedKey}");
                        return typeTiming;
                    }
                }

                // Fallback to default
                ExtraAttackPlugin.LogInfo("Config", $"Using default setting for: {weaponType}_{attackMode}");
                return weaponTypeConfig.Default;
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error getting weapon type timing: {ex.Message}");
                return new AnimationTiming();
            }
        }

        // Get timing for individual weapon and attack mode
        public static AnimationTiming GetIndividualWeaponTiming(string weaponName, string attackMode)
        {
            try
            {
                // Try individual weapon first
                if (weaponTypeConfig.IndividualWeapons.TryGetValue($"{weaponName}_{attackMode}", out var individualTiming))
                {
                    ExtraAttackPlugin.LogInfo("Config", $"Using individual weapon setting: {weaponName}_{attackMode}");
                    return individualTiming;
                }

                // Fallback to default
                ExtraAttackPlugin.LogInfo("Config", $"Using default setting for individual weapon: {weaponName}_{attackMode}");
                return weaponTypeConfig.Default;
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error getting individual weapon timing: {ex.Message}");
                return new AnimationTiming();
            }
        }

        // Get weapon type config for external access
        public static WeaponTypeConfig GetWeaponTypeConfig()
        {
            return weaponTypeConfig;
        }
        
        // Get attack cost for weapon type and attack mode
        public static AnimationTiming? GetAttackCost(string weaponType, string attackMode)
        {
            try
            {
                ExtraAttackPlugin.LogInfo("Config", $"GetAttackCost called: weaponType={weaponType}, attackMode={attackMode}");
                ExtraAttackPlugin.LogInfo("Config", $"WeaponTypes count: {weaponTypeConfig?.WeaponTypes?.Count ?? 0}");
                
                if (weaponTypeConfig?.WeaponTypes?.Count > 0)
                {
                    ExtraAttackPlugin.LogInfo("Config", $"WeaponTypes keys: {string.Join(", ", weaponTypeConfig.WeaponTypes.Keys)}");
                }
                
                if (weaponTypeConfig?.WeaponTypes?.TryGetValue(weaponType, out var weaponTypeDict) == true)
                {
                    ExtraAttackPlugin.LogInfo("Config", $"Found weaponType '{weaponType}', modes count: {weaponTypeDict?.Count ?? 0}");
                    if (weaponTypeDict?.Count > 0)
                    {
                        ExtraAttackPlugin.LogInfo("Config", $"Available modes: {string.Join(", ", weaponTypeDict.Keys)}");
                    }
                    
                    // Try unified key format: {WeaponType}_Secondary_{Mode}
                    string mode = ExtractModeFromAttackMode(attackMode);
                    string unifiedKey = $"{weaponType}_Secondary_{mode}";
                    if (weaponTypeDict?.TryGetValue(unifiedKey, out AnimationTiming timing) == true)
                    {
                        ExtraAttackPlugin.LogInfo("Config", $"Found specific timing for {unifiedKey} (unified from {attackMode}): StaminaCost={timing.StaminaCost}, EitrCost={timing.EitrCost}");
                        return timing;
                    }
                    
                    // Try direct lookup as fallback
                    if (weaponTypeDict?.TryGetValue(attackMode, out timing) == true)
                    {
                        ExtraAttackPlugin.LogInfo("Config", $"Found specific timing for {weaponType}_{attackMode}: StaminaCost={timing.StaminaCost}, EitrCost={timing.EitrCost}");
                        return timing;
                    }
                    
                    ExtraAttackPlugin.LogWarning("Config", $"No specific timing found for {unifiedKey} or {weaponType}_{attackMode}");
                }
                
                // Fallback to default if no specific timing found
                var defaultTiming = weaponTypeConfig?.Default;
                ExtraAttackPlugin.LogWarning("Config", $"Using default timing for {weaponType}_{attackMode}: StaminaCost={defaultTiming?.StaminaCost ?? 0f}, EitrCost={defaultTiming?.EitrCost ?? 0f}");
                return defaultTiming;
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error getting attack cost for {weaponType}_{attackMode}: {ex.Message}");
                return null;
            }
        }
    }
}