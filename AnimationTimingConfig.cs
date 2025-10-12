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
        private static string WeaponTypesConfigFilePath => Path.Combine(ConfigFolderPath, "eas_attackconfig_WeaponTypes.yaml");
        private static string IndividualWeaponsConfigFilePath => Path.Combine(ConfigFolderPath, "eas_attackconfig_IndividualWeapons.yaml");

        // Clean up YAML content to handle malformed structure
        private static string CleanupYamlContent(string yamlContent)
        {
            if (string.IsNullOrEmpty(yamlContent))
                return yamlContent;

            var lines = yamlContent.Split('\n');
            var cleanedLines = new List<string>();
            
            foreach (var line in lines)
            {
                // Skip lines that start with random characters (like "8e36e9a1")
                if (line.Trim().Length > 0 && !line.Trim().StartsWith("#") && !line.Trim().StartsWith("Default:") && !line.Trim().StartsWith("WeaponTypes:"))
                {
                    // Check if line looks like a random string (contains only alphanumeric characters and is short)
                    if (line.Trim().Length < 10 && System.Text.RegularExpressions.Regex.IsMatch(line.Trim(), "^[a-zA-Z0-9]+$"))
                    {
                        ExtraAttackPlugin.LogInfo("Config", $"Skipping malformed line: {line.Trim()}");
                        continue;
                    }
                }
                cleanedLines.Add(line);
            }
            
            return string.Join("\n", cleanedLines);
        }

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
            ExtraAttackPlugin.LogInfo("Config", "AnimationTimingConfig.Initialize: Method entry point reached");
            try
            {
                ExtraAttackPlugin.LogInfo("Config", "AnimationTimingConfig.Initialize: Starting initialization");
                
                if (!Directory.Exists(ConfigFolderPath))
                {
                    Directory.CreateDirectory(ConfigFolderPath);
                }

                // Check and create weapon types config if needed, then load it
                ExtraAttackPlugin.LogInfo("Config", $"AnimationTimingConfig.Initialize: WeaponTypesConfigFilePath = {WeaponTypesConfigFilePath}");
                
                if (ShouldCreateOrRegenerateWeaponTypesConfig())
                {
                    ExtraAttackPlugin.LogInfo("Config", "AnimationTimingConfig.Initialize: Creating default weapon type config");
                    CreateDefaultWeaponTypeConfig();
                }
                
                ExtraAttackPlugin.LogInfo("Config", "AnimationTimingConfig.Initialize: Loading weapon type config");
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
                if (!content.Contains("WeaponTypes:") || !content.Contains("secondary_"))
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
                weaponType = CapitalizeFirstLetter(animationName.Replace("_secondary_Q", "").Replace("ea_", ""));
            }
            else if (animationName.Contains("_secondary_T"))
            {
                attackMode = "T";
                weaponType = CapitalizeFirstLetter(animationName.Replace("_secondary_T", "").Replace("ea_", ""));
            }
            else if (animationName.Contains("_secondary_G"))
            {
                attackMode = "G";
                weaponType = CapitalizeFirstLetter(animationName.Replace("_secondary_G", "").Replace("ea_", ""));
            }
            
            return GetWeaponTypeTiming(weaponType, attackMode);
        }

        // Check if specific animation config exists using weapon type config
        public static bool HasConfig(string animationName)
        {
            // Use weapon type config instead of old config
            return GetTiming(animationName) != null;
        }

        // Helper method to capitalize first letter
        private static string CapitalizeFirstLetter(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;
            
            return char.ToUpper(input[0]) + input.Substring(1);
        }

        // Extract mode from various attack mode formats and convert to secondary_{Mode}
        private static string ExtractModeFromAttackMode(string attackMode)
        {
            if (string.IsNullOrEmpty(attackMode))
            {
                return attackMode;
            }

            // Normalize attack mode to secondary_ format
            string normalizedMode = attackMode.Replace("ea_secondary_", "secondary_");
            
            // Handle format: secondary_{Mode}
            if (normalizedMode.StartsWith("secondary_"))
            {
                string mode = normalizedMode.Replace("secondary_", "");
                return $"secondary_{mode}"; // Use lowercase 's' to match YAML format
            }
            // Handle format: {Mode} (Q/T/G only)
            else if (attackMode == "Q" || attackMode == "T" || attackMode == "G")
            {
                return $"secondary_{attackMode}"; // Use lowercase 's' to match YAML format
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
                    AppendFromWeaponType(weaponType, "secondary_Q", "secondary_Q");
                    AppendFromWeaponType(weaponType, "secondary_T", "secondary_T");
                    AppendFromWeaponType(weaponType, "secondary_G", "secondary_G");
                }

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
            
            ExtraAttackPlugin.LogInfo("System", "F6: AnimationTimingConfig reload completed");
        }

        // Load weapon type specific config
        public static void LoadWeaponTypeConfig()
        {
            ExtraAttackPlugin.LogInfo("Config", "LoadWeaponTypeConfig: Method entry point reached");
            try
            {
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .IgnoreUnmatchedProperties()
                    .Build();

                var yamlContent = File.ReadAllText(WeaponTypesConfigFilePath);
                // Clean up YAML content to handle malformed structure
                yamlContent = CleanupYamlContent(yamlContent);
                
                // YAML content loaded successfully
                // YAML content preview removed for cleaner logs
                
                // Try to deserialize with better error handling
                try
                {
                    weaponTypeConfig = deserializer.Deserialize<WeaponTypeConfig>(yamlContent) ?? new WeaponTypeConfig();
                    ExtraAttackPlugin.LogInfo("Config", $"Deserialization successful: {weaponTypeConfig?.WeaponTypes?.Count ?? 0} weapon types loaded");
                    
                    // Debug: Check specific weapon types
                    if (weaponTypeConfig?.WeaponTypes != null)
                    {
                        foreach (var weaponType in weaponTypeConfig.WeaponTypes.Keys.Take(3))
                        {
                            var modes = weaponTypeConfig.WeaponTypes[weaponType];
                            ExtraAttackPlugin.LogInfo("Config", $"  {weaponType}: {modes.Count} modes");
                            foreach (var mode in modes.Keys.Take(3))
                            {
                                var timing = modes[mode];
                                ExtraAttackPlugin.LogInfo("Config", $"    {mode}: HitTiming={timing.HitTiming}, TrailOn={timing.TrailOnTiming}, TrailOff={timing.TrailOffTiming}");
                            }
                        }
                    }
                }
                catch (Exception deserializeEx)
                {
                    ExtraAttackPlugin.LogError("System", $"Deserialization failed: {deserializeEx.Message}");
                    throw; // Re-throw to be caught by outer catch
                }
                
                ExtraAttackPlugin.LogInfo("Config", $"Loaded weapon type config: {weaponTypeConfig?.WeaponTypes?.Count ?? 0} weapon types");
                ExtraAttackPlugin.LogInfo("Config", $"weaponTypeConfig is null: {weaponTypeConfig == null}");
                ExtraAttackPlugin.LogInfo("Config", $"WeaponTypes is null: {weaponTypeConfig?.WeaponTypes == null}");
                
                // Debug: Check if Swords exists and what modes it has
                if (weaponTypeConfig?.WeaponTypes != null && weaponTypeConfig.WeaponTypes.ContainsKey("Swords"))
                {
                    var swordsModes = weaponTypeConfig.WeaponTypes["Swords"];
                    ExtraAttackPlugin.LogInfo("Config", $"Swords modes found: {string.Join(", ", swordsModes.Keys)}");
                }
                else
                {
                    ExtraAttackPlugin.LogInfo("Config", "Swords not found in WeaponTypes - this is normal during initial setup");
                }
                
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
                sb.AppendLine("# ============================================================================");
                sb.AppendLine("# Format: IndividualWeapon -> Q/T/G -> Timing Settings");
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
                        ["T"] = "Swords",      // Sword secondary (same weapon type)
                        ["G"] = "Swords"       // Sword secondary (same weapon type)
                    },
                    ["Axes"] = new Dictionary<string, string>
                    {
                        ["Q"] = "Axes",        // Axe secondary
                        ["T"] = "Axes",        // Axe secondary (same weapon type)
                        ["G"] = "Axes"         // Axe secondary (same weapon type)
                    },
                    ["Clubs"] = new Dictionary<string, string>
                    {
                        ["Q"] = "Clubs",       // Club secondary
                        ["T"] = "Clubs",       // Club secondary (same weapon type)
                        ["G"] = "Clubs"        // Club secondary (same weapon type)
                    },
                    ["Spears"] = new Dictionary<string, string>
                    {
                        ["Q"] = "Spears",      // Spear secondary
                        ["T"] = "Spears",      // Spear secondary (same weapon type)
                        ["G"] = "Spears"       // Spear secondary (same weapon type)
                    },
                    ["GreatSwords"] = new Dictionary<string, string>
                    {
                        ["Q"] = "GreatSwords", // Great Sword secondary
                        ["T"] = "GreatSwords", // Great Sword secondary (same weapon type)
                        ["G"] = "GreatSwords"  // Great Sword secondary (same weapon type)
                    },
                    ["BattleAxes"] = new Dictionary<string, string>
                    {
                        ["Q"] = "BattleAxes",  // Battle Axe secondary
                        ["T"] = "BattleAxes",  // Battle Axe secondary (same weapon type)
                        ["G"] = "BattleAxes"   // Battle Axe secondary (same weapon type)
                    },
                    ["Polearms"] = new Dictionary<string, string>
                    {
                        ["Q"] = "Polearms",    // Polearm secondary
                        ["T"] = "Polearms",    // Polearm secondary (same weapon type)
                        ["G"] = "Polearms"     // Polearm secondary (same weapon type)
                    },
                    ["Knives"] = new Dictionary<string, string>
                    {
                        ["Q"] = "Knives",      // Knife secondary
                        ["T"] = "Knives",      // Knife secondary (same weapon type)
                        ["G"] = "Knives"       // Knife secondary (same weapon type)
                    },
                    ["Fists"] = new Dictionary<string, string>
                    {
                        ["Q"] = "Fists",       // Fist secondary
                        ["T"] = "Fists",       // Fist secondary (same weapon type)
                        ["G"] = "Fists"        // Fist secondary (same weapon type)
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
                        var key = $"secondary_{mode}";
                        
                        ExtraAttackPlugin.LogInfo("Config", $"GenerateWeaponTypeConfig: Creating timing for {weaponType}_{key} -> {targetWeaponType}_{mode}");
                        
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
            string key = $"{weaponType}_secondary_{mode}";
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
            individualWeapons["secondary_Q_THSwordWood"] = woodenGreatswordQ;

            var woodenGreatswordT = new AnimationTiming();
            woodenGreatswordT.HitTiming = 0.60f;
            woodenGreatswordT.TrailOnTiming = 0.35f;
            woodenGreatswordT.TrailOffTiming = 0.70f;
            woodenGreatswordT.AttackRange = 1.90f;
            woodenGreatswordT.AttackHeight = 0.80f;
            individualWeapons["secondary_T_THSwordWood"] = woodenGreatswordT;

            var woodenGreatswordG = new AnimationTiming();
            woodenGreatswordG.HitTiming = 0.65f;
            woodenGreatswordG.TrailOnTiming = 0.35f;
            woodenGreatswordG.TrailOffTiming = 0.70f;
            woodenGreatswordG.AttackRange = 2.40f;
            woodenGreatswordG.AttackHeight = 0.80f;
            individualWeapons["secondary_G_THSwordWood"] = woodenGreatswordG;

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
                
                // NEW: ãƒ¢ãƒ¼ãƒ‰ã‚­ãƒ¼ (secondary_Q/T/G) ã§ç›´æŽ¥æ¤œç´¢
                string modeKey = $"secondary_{mode}";
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
                sb.AppendLine("# ==============================================================================");
                sb.AppendLine("# IMPORTANT: Configuration is organized by weapon types (Swords, Axes, Clubs, etc.)");
                sb.AppendLine("# ==============================================================================");
                sb.AppendLine();
                sb.AppendLine("# Key Format");
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
                sb.AppendLine();
                sb.AppendLine("# ==============================================================================");
                sb.AppendLine("# Weapon Type Specific Settings");
                sb.AppendLine("# ==============================================================================");
                sb.AppendLine();
                sb.AppendLine("WeaponTypes:");
                
                // Filter only weapon types (not individual weapons)
                string[] validWeaponTypes = { "Axes", "BattleAxes", "Clubs", "DualAxes", "DualKnives", "Fists", "GreatSwords", "Knives", "Polearms", "Sledges", "Spears", "Swords", "Torch", "Unarmed" };
                
                var filteredWeaponTypes = config.WeaponTypes
                    .Where(kvp => validWeaponTypes.Contains(kvp.Key))
                    .OrderBy(kvp => kvp.Key);
                
                foreach (var weaponType in filteredWeaponTypes)
                {
                    sb.AppendLine($"  # ========== {weaponType.Key} ==========");
                    sb.AppendLine($"  {weaponType.Key}:");
                    
                    // Sort by Q, T, G order (unified format: secondary_{Mode})
                    var sortedModes = weaponType.Value.OrderBy(kvp => 
                        kvp.Key.Contains("secondary_Q") ? 0 : 
                        kvp.Key.Contains("secondary_T") ? 1 : 
                        kvp.Key.Contains("secondary_G") ? 2 : 3);
                    
                    foreach (var mode in sortedModes)
                    {
                        var timing = mode.Value;
                        
                        // Extract mode (Q/T/G) and get replacement animation name
                        string modeKey = mode.Key.Replace("secondary_", "");
                        string replacementAnimation = GetReplacementAnimationName(weaponType.Key, modeKey);
                        
                        sb.AppendLine($"    # secondary_{modeKey} - ExtraAttack 1 -> {replacementAnimation}");
                        sb.AppendLine($"    {mode.Key}:");
                        sb.AppendLine($"      HitTiming: {timing.HitTiming:F2}");
                        sb.AppendLine($"      TrailOnTiming: {timing.TrailOnTiming:F2}");
                        sb.AppendLine($"      TrailOffTiming: {timing.TrailOffTiming:F2}");
                        sb.AppendLine($"      # Attack Parameters (from vanilla Attack class)");
                        sb.AppendLine($"      AttackRange: {timing.AttackRange:F2}");
                        sb.AppendLine($"      AttackHeight: {timing.AttackHeight:F2}");
                        sb.AppendLine($"      AttackOffset: {timing.AttackOffset:F2}");
                        sb.AppendLine($"      AttackAngle: {timing.AttackAngle:F2}");
                        sb.AppendLine($"      AttackHeightChar1: {timing.AttackHeightChar1:F2}");
                        sb.AppendLine($"      AttackHeightChar2: {timing.AttackHeightChar2:F2}");
                        sb.AppendLine($"      MaxYAngle: {timing.MaxYAngle:F2}");
                        sb.AppendLine($"      # Attack Control Flags");
                        sb.AppendLine($"      EnableHit: {(timing.EnableHit ? "true" : "false")}");
                        sb.AppendLine($"      EnableSound: {(timing.EnableSound ? "true" : "false")}");
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
                    string modeKey = $"secondary_{attackMode}";
                    if (weaponTypeSettings.TryGetValue(modeKey, out var typeTiming))
                    {
                        ExtraAttackPlugin.LogInfo("Config", $"Using weapon type setting: {weaponType}_{modeKey}");
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
        
    }
}