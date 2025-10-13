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
            // Animation Event Timing (0.0 ~ 1.0) - calculated from vanilla data
            public float HitTiming { get; set; }
            public float TrailOnTiming { get; set; }
            public float TrailOffTiming { get; set; }
            public float ChainTiming { get; set; }
            public float SpeedTiming { get; set; }
            public float SpeedMultiplier { get; set; }
            public float DodgeMortalTiming { get; set; }

            // Attack Detection Parameters - calculated from vanilla data per weapon type
            public float AttackRange { get; set; }
            public float AttackHeight { get; set; }
            public float AttackOffset { get; set; }
            public float AttackAngle { get; set; }
            public float AttackRayWidth { get; set; }
            public float AttackRayWidthCharExtra { get; set; }
            public float AttackHeightChar1 { get; set; }
            public float AttackHeightChar2 { get; set; }
            public float MaxYAngle { get; set; }
            public bool EnableHit { get; set; }
            public bool EnableSound { get; set; }
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
                
                // Force generation if file doesn't exist or is empty
                if (!File.Exists(WeaponTypesConfigFilePath) || ShouldCreateOrRegenerateWeaponTypesConfig())
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
                // Calculate timing based on actual clip length (ratio-based)
                timing.HitTiming = CalculateHitTiming(clipLength, weaponType);
                timing.TrailOnTiming = CalculateTrailOnTiming(clipLength, weaponType);
                timing.TrailOffTiming = CalculateTrailOffTiming(clipLength, weaponType);
                timing.ChainTiming = CalculateChainTiming(clipLength, weaponType);
                timing.SpeedTiming = CalculateSpeedTiming(clipLength, weaponType);
                timing.DodgeMortalTiming = CalculateDodgeMortalTiming(clipLength, weaponType);
            }
            else
            {
                // Fallback to default ratios if no clip length available
                timing.HitTiming = 0.45f;
                timing.TrailOnTiming = 0.35f;
                timing.TrailOffTiming = 0.70f;
                timing.ChainTiming = 0.85f;
                timing.SpeedTiming = 0.50f;
                timing.DodgeMortalTiming = 0.70f;
            }

            // Set attack detection parameters from vanilla data (no ratio calculation needed)
            timing.AttackRange = GetVanillaAttackRangeForWeaponType(weaponType);
            timing.AttackHeight = GetVanillaAttackHeightForWeaponType(weaponType);
            timing.AttackAngle = GetVanillaAttackAngleForWeaponType(weaponType);
            timing.AttackOffset = 0.0f;
            timing.AttackRayWidth = 0.0f;
            timing.AttackRayWidthCharExtra = 0.0f;
            timing.AttackHeightChar1 = 0.0f;
            timing.AttackHeightChar2 = 0.0f;
            timing.MaxYAngle = 0.0f;
            timing.EnableHit = true;
            timing.EnableSound = true;

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
                    ["T"] = "Swords",
                    ["G"] = "Swords"
                },
                ["Axes"] = new Dictionary<string, string>
                {
                    ["Q"] = "Axes",
                    ["T"] = "Axes",
                    ["G"] = "Axes"
                },
                ["Clubs"] = new Dictionary<string, string>
                {
                    ["Q"] = "Clubs",
                    ["T"] = "Clubs",
                    ["G"] = "Clubs"
                },
                ["Spears"] = new Dictionary<string, string>
                {
                    ["Q"] = "Spears",
                    ["T"] = "Spears",
                    ["G"] = "Spears"
                },
                ["GreatSwords"] = new Dictionary<string, string>
                {
                    ["Q"] = "GreatSwords",
                    ["T"] = "GreatSwords",
                    ["G"] = "GreatSwords"
                },
                ["BattleAxes"] = new Dictionary<string, string>
                {
                    ["Q"] = "BattleAxes",
                    ["T"] = "BattleAxes",
                    ["G"] = "BattleAxes"
                },
                ["Polearms"] = new Dictionary<string, string>
                {
                    ["Q"] = "Polearms",
                    ["T"] = "Polearms",
                    ["G"] = "Polearms"
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
                // Parse key to get weapon type and mode
                // key format: {weaponType}_secondary_{mode}
                var parts = key.Split('_');
                if (parts.Length >= 3)
                {
                    var weaponType = parts[0]; // Swords, Axes, etc.
                    var mode = parts[2]; // Q, T, G
                    
                    if (AnimationManager.ReplacementMap.TryGetValue(weaponType, out var weaponMap) && 
                        weaponMap != null && weaponMap.Count > 0)
                    {
                        // Look for secondary_{mode} in the weapon map
                        var secondaryKey = $"secondary_{mode}";
                        if (weaponMap.TryGetValue(secondaryKey, out var externalClipName) && 
                            !string.IsNullOrEmpty(externalClipName))
                    {
                        // Get actual clip length from AnimationManager (with smart caching)
                            float clipLength = AnimationManager.GetExternalClipLengthSmart(externalClipName);
                        if (clipLength > 0)
                        {
                            ExtraAttackPlugin.LogInfo("Config", $"Using external clip length for {key}: {clipLength:F3}s");
                            return clipLength;
                        }
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
        
        // Get vanilla clip length for weapon type
        private static float GetVanillaClipLengthForWeaponType(string weaponType)
        {
            // Actual vanilla secondary attack clip lengths
            return weaponType switch
            {
                "Swords" => 1.400f,      // sword_secondary → [Sword-Attack-R4]
                "Axes" => 1.400f,        // axe_secondary → [Axe Secondary Attack]
                "GreatSwords" => 1.400f, // greatsword_secondary → [Greatsword Secondary Attack]
                "BattleAxes" => 0.857f,  // battleaxe_secondary → [BattleAxeAltAttack]
                "Clubs" => 1.400f,       // mace_secondary → [MaceAltAttack]
                "Spears" => 1.0f,        // spear_secondary → no specific clip (default)
                "Polearms" => 2.167f,    // atgeir_secondary → [Atgeir360Attack]
                "Knives" => 1.400f,      // knife_secondary → [Knife JumpAttack]
                "Fists" => 1.0f,         // fist_secondary → no specific clip (default)
                _ => 1.0f
            };
        }
        
        // Get vanilla hit timing for weapon type (from List_AnimationEvent.txt)
        private static float GetVanillaHitTimingForWeaponType(string weaponType)
        {
            return weaponType switch
            {
                "Axes" => 0.610f,        // axe_swing: OnAttackTrigger=0.610
                "BattleAxes" => 0.464f,  // BattleAxeAltAttack: Hit=0.464
                "GreatSwords" => 0.604f, // Greatsword BaseAttack (1): Hit=0.604
                "Knives" => 0.839f,      // Knife Attack Leap: Hit=0.839
                "Spears" => 0.470f,      // 2Hand-Spear-Attack1: OnAttackTrigger=0.470
                "Polearms" => 1.124f,    // Atgeir360Attack: Hit=1.124
                "Fists" => 0.840f,       // Punchstep 1: Hit=0.840
                "Swords" => 0.472f,      // Attack1: Hit=0.472 (fallback for secondary)
                "Clubs" => 0.472f,       // Attack1: Hit=0.472 (fallback for secondary)
                _ => 0.45f
            };
        }
        
        // Get vanilla trail on timing for weapon type (from List_AnimationEvent.txt)
        private static float GetVanillaTrailOnTimingForWeaponType(string weaponType)
        {
            return weaponType switch
            {
                "Axes" => 0.442f,        // axe_swing: TrailOn=0.442
                "BattleAxes" => 0.222f,  // BattleAxeAltAttack: TrailOn=0.222
                "GreatSwords" => 0.461f, // Greatsword BaseAttack (1): TrailOn=0.461
                "Knives" => 0.478f,      // Knife Attack Leap: TrailOn=0.478
                "Spears" => 0.369f,      // 2Hand-Spear-Attack1: TrailOn=0.369
                "Polearms" => 1.011f,    // Atgeir360Attack: TrailOn=1.011
                "Fists" => 0.702f,       // Punchstep 1: TrailOn=0.702
                "Swords" => 0.404f,      // Attack1: TrailOn=0.404 (fallback for secondary)
                "Clubs" => 0.404f,       // Attack1: TrailOn=0.404 (fallback for secondary)
                _ => 0.35f
            };
        }
        
        // Get vanilla trail off timing for weapon type (from List_AnimationEvent.txt)
        private static float GetVanillaTrailOffTimingForWeaponType(string weaponType)
        {
            return weaponType switch
            {
                "Axes" => 0.772f,        // axe_swing: TrailOff=0.772
                "BattleAxes" => 0.521f,  // BattleAxeAltAttack: TrailOff=0.521
                "GreatSwords" => 0.771f, // Greatsword BaseAttack (1): TrailOff=0.771
                "Knives" => 0.0f,        // Knife Attack Leap: no TrailOff event
                "Spears" => 0.526f,      // 2Hand-Spear-Attack1: TrailOff=0.526
                "Polearms" => 1.602f,    // Atgeir360Attack: TrailOff=1.602
                "Fists" => 0.0f,         // Punchstep 1: no TrailOff
                "Swords" => 0.714f,      // Attack1: TrailOff=0.714 (fallback for secondary)
                "Clubs" => 0.714f,       // Attack1: TrailOff=0.714 (fallback for secondary)
                _ => 0.70f
            };
        }
        
        // Calculate hit timing based on clip length and weapon type
        private static float CalculateHitTiming(float clipLength, string weaponType)
        {
            // Get vanilla clip length and hit timing for this weapon type
            float vanillaClipLength = GetVanillaClipLengthForWeaponType(weaponType);
            float vanillaHitTiming = GetVanillaHitTimingForWeaponType(weaponType);
            
            if (vanillaClipLength > 0 && vanillaHitTiming > 0)
            {
                // Calculate vanilla ratio and apply to custom clip length
                float ratio = vanillaHitTiming / vanillaClipLength;
                return Math.Min(clipLength, clipLength * ratio); // Apply ratio to custom clip length, cap at clipLength
            }
            
            // Fallback to weapon-specific default ratios applied to clip length
            float fallbackRatio = weaponType switch
            {
                "Axes" => 0.436f,        // 0.610/1.400
                "BattleAxes" => 0.541f,  // 0.464/0.857
                "GreatSwords" => 0.431f, // 0.604/1.400
                "Knives" => 0.183f,      // 0.256/1.400
                "Spears" => 0.470f,      // 0.470/1.000
                "Polearms" => 0.519f,    // 1.124/2.167
                "Fists" => 0.840f,       // 0.840/1.000
                _ => 0.45f
            };
            return Math.Min(clipLength, clipLength * fallbackRatio);
        }
        
        // Calculate trail on timing based on clip length and weapon type
        private static float CalculateTrailOnTiming(float clipLength, string weaponType)
        {
            float vanillaClipLength = GetVanillaClipLengthForWeaponType(weaponType);
            float vanillaTrailOnTiming = GetVanillaTrailOnTimingForWeaponType(weaponType);
            
            if (vanillaClipLength > 0 && vanillaTrailOnTiming > 0)
            {
                float ratio = vanillaTrailOnTiming / vanillaClipLength;
                return Math.Min(clipLength, clipLength * ratio);
            }
            
            // Fallback to weapon-specific default ratios applied to clip length
            float fallbackRatio = weaponType switch
            {
                "Axes" => 0.316f,        // 0.442/1.400
                "BattleAxes" => 0.259f,  // 0.222/0.857
                "GreatSwords" => 0.329f, // 0.461/1.400
                "Knives" => 0.096f,      // 0.134/1.400
                "Spears" => 0.369f,      // 0.369/1.000
                "Polearms" => 0.467f,    // 1.011/2.167
                "Fists" => 0.702f,       // 0.702/1.000
                _ => 0.32f
            };
            return Math.Min(clipLength, clipLength * fallbackRatio);
        }
        
        // Calculate trail off timing based on clip length and weapon type
        private static float CalculateTrailOffTiming(float clipLength, string weaponType)
        {
            float vanillaClipLength = GetVanillaClipLengthForWeaponType(weaponType);
            float vanillaTrailOffTiming = GetVanillaTrailOffTimingForWeaponType(weaponType);
            
            if (vanillaClipLength > 0 && vanillaTrailOffTiming > 0)
            {
                float ratio = vanillaTrailOffTiming / vanillaClipLength;
                return Math.Min(clipLength, clipLength * ratio);
            }
            
            // Fallback to weapon-specific default ratios applied to clip length
            float fallbackRatio = weaponType switch
            {
                "Axes" => 0.551f,        // 0.772/1.400
                "BattleAxes" => 0.608f,  // 0.521/0.857
                "GreatSwords" => 0.551f, // 0.771/1.400
                "Knives" => 0.314f,      // 0.439/1.400
                "Spears" => 0.526f,      // 0.526/1.000
                "Polearms" => 0.739f,    // 1.602/2.167
                "Fists" => 0.0f,         // no TrailOff
                _ => 0.55f
            };
            return Math.Min(clipLength, clipLength * fallbackRatio);
        }
        
        // Calculate chain timing based on clip length and weapon type
        private static float CalculateChainTiming(float clipLength, string weaponType)
        {
            // Secondary attacks don't have Chain events - return 0.0
            return 0.0f;
        }
        
        // Calculate speed timing based on clip length and weapon type
        private static float CalculateSpeedTiming(float clipLength, string weaponType)
        {
            float vanillaClipLength = GetVanillaClipLengthForWeaponType(weaponType);
            float vanillaSpeedTiming = GetVanillaSpeedTimingForWeaponType(weaponType);
            
            if (vanillaClipLength > 0 && vanillaSpeedTiming > 0)
            {
                float ratio = vanillaSpeedTiming / vanillaClipLength;
                return Math.Min(clipLength, clipLength * ratio);
            }
            
            // Fallback to weapon-specific default ratios applied to clip length
            float fallbackRatio = weaponType switch
            {
                "Axes" => 0.325f,        // 0.456/1.400
                "BattleAxes" => 0.350f,  // 0.300/0.857
                "GreatSwords" => 0.329f, // 0.461/1.400
                "Knives" => 0.179f,      // 0.250/1.400
                "Spears" => 0.470f,      // 0.470/1.000
                "Polearms" => 0.230f,    // 0.500/2.167
                "Fists" => 0.600f,       // 0.600/1.000
                _ => 0.45f
            };
            return Math.Min(clipLength, clipLength * fallbackRatio);
        }
        
        // Calculate dodge mortal timing based on clip length and weapon type
        private static float CalculateDodgeMortalTiming(float clipLength, string weaponType)
        {
            float vanillaClipLength = GetVanillaClipLengthForWeaponType(weaponType);
            float vanillaDodgeMortalTiming = GetVanillaDodgeMortalTimingForWeaponType(weaponType);
            
            if (vanillaClipLength > 0 && vanillaDodgeMortalTiming > 0)
            {
                float ratio = vanillaDodgeMortalTiming / vanillaClipLength;
                return Math.Min(clipLength, clipLength * ratio);
            }
            
            // Fallback to weapon-specific default ratios applied to clip length
            float fallbackRatio = weaponType switch
            {
                "Axes" => 0.621f,        // 0.870/1.400
                "BattleAxes" => 0.980f,  // 0.840/0.857
                "GreatSwords" => 0.607f, // 0.850/1.400
                "Knives" => 0.607f,      // 0.850/1.400
                "Spears" => 0.900f,      // 0.900/1.000
                "Polearms" => 0.415f,    // 0.900/2.167
                "Fists" => 0.900f,       // 0.900/1.000
                _ => 0.85f
            };
            return Math.Min(clipLength, clipLength * fallbackRatio);
        }

        // Vanilla attack detection parameters (no ratio calculation needed)
        private static float GetVanillaAttackRangeForWeaponType(string weaponType)
        {
            return weaponType switch
            {
                "Swords" => 2.0f,
                "Axes" => 2.0f,
                "GreatSwords" => 2.5f,
                "BattleAxes" => 2.0f,
                "Clubs" => 2.0f,
                "Spears" => 3.0f,
                "Polearms" => 2.5f,
                "Knives" => 1.5f,
                "Fists" => 1.5f,
                _ => 2.0f
            };
        }

        private static float GetVanillaAttackHeightForWeaponType(string weaponType)
        {
            return weaponType switch
            {
                "Swords" => 0.6f,
                "Axes" => 0.6f,
                "GreatSwords" => 0.7f,
                "BattleAxes" => 0.6f,
                "Clubs" => 0.6f,
                "Spears" => 0.8f,
                "Polearms" => 0.7f,
                "Knives" => 0.5f,
                "Fists" => 0.5f,
                _ => 0.6f
            };
        }

        private static float GetVanillaAttackAngleForWeaponType(string weaponType)
        {
            return weaponType switch
            {
                "Swords" => 90.0f,
                "Axes" => 90.0f,
                "GreatSwords" => 120.0f,
                "BattleAxes" => 90.0f,
                "Clubs" => 90.0f,
                "Spears" => 45.0f,
                "Polearms" => 360.0f,
                "Knives" => 90.0f,
                "Fists" => 90.0f,
                _ => 90.0f
            };
        }
        
        // Get vanilla speed timing for weapon type
        private static float GetVanillaSpeedTimingForWeaponType(string weaponType)
        {
            return weaponType switch
            {
                "Axes" => 0.456f,        // axe_swing: Speed=0.456
                "BattleAxes" => 0.300f,  // BattleAxeAltAttack: Speed=0.300
                "GreatSwords" => 0.461f, // Greatsword BaseAttack (1): Speed=0.461
                "Knives" => 0.250f,      // knife_slash0: Speed=0.250
                "Spears" => 0.470f,      // 2Hand-Spear-Attack1: Speed=0.470
                "Polearms" => 0.500f,    // Atgeir360Attack: Speed=0.500
                "Fists" => 0.600f,       // Punchstep 1: Speed=0.600
                "Swords" => 0.472f,      // Attack1: Speed=0.472 (fallback for secondary)
                "Clubs" => 0.472f,       // Attack1: Speed=0.472 (fallback for secondary)
                _ => 0.45f
            };
        }
        
        // Get vanilla dodge mortal timing for weapon type
        private static float GetVanillaDodgeMortalTimingForWeaponType(string weaponType)
        {
            return weaponType switch
            {
                "Axes" => 0.870f,        // axe_swing: DodgeMortal=0.870
                "BattleAxes" => 0.840f,  // BattleAxeAltAttack: DodgeMortal=0.840
                "GreatSwords" => 0.850f, // Greatsword BaseAttack (1): DodgeMortal=0.850
                "Knives" => 0.850f,      // knife_slash0: DodgeMortal=0.850
                "Spears" => 0.900f,      // 2Hand-Spear-Attack1: DodgeMortal=0.900
                "Polearms" => 0.900f,    // Atgeir360Attack: DodgeMortal=0.900
                "Fists" => 0.900f,       // Punchstep 1: DodgeMortal=0.900
                "Swords" => 0.850f,      // Attack1: DodgeMortal=0.850 (fallback for secondary)
                "Clubs" => 0.850f,       // Attack1: DodgeMortal=0.850 (fallback for secondary)
                _ => 0.85f
            };
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

                // Create timing based on actual animation clip length
                ExtraAttackPlugin.LogInfo("Config", $"Creating timing for: {weaponType}_{attackMode}");
                return CreateTimingForWeaponType(weaponType, attackMode);
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