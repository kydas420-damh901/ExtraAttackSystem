using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using HarmonyLib;

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
            ExtraAttackPlugin.LogInfo("System", "AnimationTimingConfig.Initialize: Method entry point reached");
            try
            {
                ExtraAttackPlugin.LogInfo("System", "AnimationTimingConfig.Initialize: Starting initialization");
                
                // Ensure AnimationManager is initialized before we try to use ReplacementMap
                ExtraAttackPlugin.LogInfo("System", $"AnimationTimingConfig.Initialize: ReplacementMap.Count = {AnimationManager.AnimationReplacementMap.Count}");
                // Note: AnimationManager.InitializeAnimationMaps() is called by ExtraAttackPlugin.cs
                // No need to call it again here
                
                if (!Directory.Exists(ConfigFolderPath))
                {
                    Directory.CreateDirectory(ConfigFolderPath);
                }

                // Check and create weapon types config if needed, then load it
                ExtraAttackPlugin.LogInfo("System", $"AnimationTimingConfig.Initialize: WeaponTypesConfigFilePath = {WeaponTypesConfigFilePath}");
                
                // Force generation if file doesn't exist or is empty
                if (!File.Exists(WeaponTypesConfigFilePath) || ShouldCreateOrRegenerateWeaponTypesConfig())
                {
                    ExtraAttackPlugin.LogInfo("System", "AnimationTimingConfig.Initialize: Creating default weapon type config");
                    CreateDefaultWeaponTypeConfig();
                }
                
                ExtraAttackPlugin.LogInfo("System", "AnimationTimingConfig.Initialize: Loading weapon type config");
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
                 ExtraAttackPlugin.LogInfo("System", "WeaponTypes config file not found, will create");
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
            // This method is called for custom animations (External animations)
            // We need to determine which weapon type and mode this animation belongs to
            
            // Check if this is a secondary animation with _secondary_Q/T/G suffix
            if (animationName.Contains("_secondary_Q"))
            {
                string weaponType = CapitalizeFirstLetter(animationName.Replace("_secondary_Q", ""));
                return GetWeaponTypeTiming(weaponType, "Q");
            }
            else if (animationName.Contains("_secondary_T"))
            {
                string weaponType = CapitalizeFirstLetter(animationName.Replace("_secondary_T", ""));
                return GetWeaponTypeTiming(weaponType, "T");
            }
            else if (animationName.Contains("_secondary_G"))
            {
                string weaponType = CapitalizeFirstLetter(animationName.Replace("_secondary_G", ""));
                return GetWeaponTypeTiming(weaponType, "G");
            }
            
            // For custom animations without _secondary suffix, calculate timing based on actual clip length
            // Get the actual length of the custom animation
            float clipLength = AnimationManager.GetExternalClipLengthSmart(animationName);
            
            if (clipLength > 0)
            {
                // Calculate timing based on clip length with reasonable ratios
                return new AnimationTiming
                {
                    HitTiming = clipLength * 0.6f,        // Hit at 60% of animation
                    TrailOnTiming = clipLength * 0.2f,    // Trail starts at 20%
                    TrailOffTiming = clipLength * 0.9f,   // Trail ends at 90%
                    AttackRange = 2.0f,
                    AttackHeight = 1.0f,
                    SpeedTiming = clipLength,
                    ChainTiming = clipLength * 0.85f,
                    DodgeMortalTiming = clipLength,
                    SpeedMultiplier = 1.0f
                };
            }
            else
            {
                // Fallback to default timing if clip length not found
                return new AnimationTiming
                {
                    HitTiming = 0.5f,
                    TrailOnTiming = 0.3f,
                    TrailOffTiming = 0.8f,
                    AttackRange = 2.0f,
                    AttackHeight = 1.0f,
                    SpeedTiming = 1.0f,
                    ChainTiming = 0.85f,
                    DodgeMortalTiming = 1.0f,
                    SpeedMultiplier = 1.0f
                };
            }
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
                    if (AnimationManager.AnimationReplacementMap.ContainsKey(weaponType))
                    {
                        var weaponMap = AnimationManager.AnimationReplacementMap[weaponType];
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
                    
                    // Ensure WeaponTypes is initialized
                    if (weaponTypeConfig?.WeaponTypes == null)
                    {
                        weaponTypeConfig = weaponTypeConfig ?? new WeaponTypeConfig();
                        weaponTypeConfig.WeaponTypes = new Dictionary<string, Dictionary<string, AnimationTiming>>();
                    }
                    
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
                ExtraAttackPlugin.LogInfo("System", "Generating default WeaponTypes config");
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
                ExtraAttackPlugin.LogInfo("System", "GenerateWeaponTypeConfig: Starting generation");
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
                        
                        // Create timing based on actual weapon type and mode combination
                        var timing = CreateTimingForWeaponType(targetWeaponType, mode);
                        weaponSettings[key] = timing;
                    }

                    config.WeaponTypes[weaponType] = weaponSettings;
                    ExtraAttackPlugin.LogInfo("Config", $"GenerateWeaponTypeConfig: Added {weaponType} with {weaponSettings.Count} modes");
                }

                // Create individual weapon settings
                CreateIndividualWeaponSettings(config);

                // Save the generated config
                ExtraAttackPlugin.LogInfo("System", $"GenerateWeaponTypeConfig: Final config has {config.WeaponTypes.Count} weapon types");
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
                // Calculate absolute timings based on actual clip length
                float hitTiming = CalculateHitTiming(clipLength, weaponType, mode);
                float trailOnTiming = CalculateTrailOnTiming(clipLength, weaponType, mode);
                float trailOffTiming = CalculateTrailOffTiming(clipLength, weaponType, mode);
                float chainTiming = CalculateChainTiming(clipLength, weaponType, mode);
                float speedTiming = CalculateSpeedTiming(clipLength, weaponType, mode);
                float dodgeMortalTiming = CalculateDodgeMortalTiming(clipLength, weaponType, mode);
                
                // Store absolute timings (will be converted to ratios in YAML output)
                timing.HitTiming = hitTiming;
                timing.TrailOnTiming = trailOnTiming;
                timing.TrailOffTiming = trailOffTiming;
                timing.ChainTiming = chainTiming;
                timing.SpeedTiming = speedTiming;
                timing.DodgeMortalTiming = dodgeMortalTiming;
            }
            else
            {
                ExtraAttackPlugin.LogInfo("System", $"CreateTimingForWeaponType: {key} -> No clip length, using fallback ratios");
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
            timing.SpeedMultiplier = 1.0f; // Default speed multiplier

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
        
        // Get adjusted clip length using AnimationManager's working logic
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
                    
                    // Use AnimationManager's working GetExternalClipForWeaponType method
                    string externalClipName = GetExternalClipForWeaponType(weaponType, mode);
                    
                    if (!string.IsNullOrEmpty(externalClipName))
                    {
                        // Get actual clip length from AnimationManager (with smart caching)
                        float clipLength = AnimationManager.GetExternalClipLengthSmart(externalClipName);
                        
                        if (clipLength > 0)
                        {
                            ExtraAttackPlugin.LogInfo("System", $"GetAdjustedClipLength: {key} -> {externalClipName} = {clipLength:F3}s");
                            return clipLength;
                        }
                        else
                        {
                            // If external clip length is not found, try to get it from CustomAnimationClips directly
                            if (AnimationManager.CustomAnimationClips.TryGetValue(externalClipName, out var clip))
                            {
                                float directClipLength = clip.length;
                                ExtraAttackPlugin.LogInfo("System", $"GetAdjustedClipLength: Direct clip length for {externalClipName} = {directClipLength:F3}s");
                                return directClipLength;
                            }
                            else
                            {
                                ExtraAttackPlugin.LogWarning("System", $"GetAdjustedClipLength: External clip not found in CustomAnimationClips: {externalClipName}");
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
        
        // Get external clip for weapon type and mode (integrated from AnimationManager)
        public static string GetExternalClipForWeaponType(string weaponType, string mode)
        {
            // Q/T/G modes use completely different animation clips
            switch (weaponType)
            {
                case "Swords":
                    return mode == "Q" ? "2Hand-Sword-Attack8External" :
                           mode == "T" ? "2Hand_Skill01_WhirlWindExternal" :
                           "Eas_GreatSword_JumpAttackExternal";
                case "Axes":
                    return mode == "Q" ? "OneHand_Up_Attack_B_1External" :
                           mode == "T" ? "2Hand-Sword-Attack8External" :
                           "Eas_GreatSword_JumpAttackExternal";
                case "Clubs":
                    return mode == "Q" ? "Eas_GreatSword_CastingExternal" :
                           mode == "T" ? "2Hand_Skill01_WhirlWindExternal" :
                           "2Hand-Sword-Attack8External";
                case "Spears":
                    return mode == "Q" ? "Eas_GreatSword_JumpAttackExternal" :
                           mode == "T" ? "2Hand_Skill01_WhirlWindExternal" :
                           "2Hand-Sword-Attack8External";
                case "GreatSwords":
                    return mode == "Q" ? "2Hand-Sword-Attack8External" :
                           mode == "T" ? "2Hand_Skill01_WhirlWindExternal" :
                           "Eas_GreatSword_JumpAttackExternal";
                case "BattleAxes":
                    return mode == "Q" ? "2Hand_Skill01_WhirlWindExternal" :
                           mode == "T" ? "2Hand-Sword-Attack8External" :
                           "Eas_GreatSword_JumpAttackExternal";
                case "Polearms":
                    return mode == "Q" ? "Eas_GreatSword_JumpAttackExternal" :
                           mode == "T" ? "2Hand-Sword-Attack8External" :
                           "2Hand_Skill01_WhirlWindExternal";
                case "Knives":
                    return mode == "Q" ? "ChargeAttkExternal" :
                           mode == "T" ? "Eas_GreatSword_JumpAttackExternal" :
                           "2Hand-Sword-Attack8External";
                case "Fists":
                    return mode == "Q" ? "Flying Knee Punch ComboExternal" :
                           mode == "T" ? "2Hand_Skill01_WhirlWindExternal" :
                           "Eas_GreatSword_JumpAttackExternal";
                default:
                    return mode == "Q" ? "2Hand-Sword-Attack8External" :
                           mode == "T" ? "2Hand-Sword-Attack8External" :
                           "2Hand-Sword-Attack8External";
            }
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
                "Spears" => 1.133f,      // spear_secondary → [throw_spear]
                "Polearms" => 2.167f,    // atgeir_secondary → [Atgeir360Attack]
                "Knives" => 1.400f,      // knife_secondary → [Knife JumpAttack]
                "Fists" => 1.833f,       // fist_secondary → [Kickstep]
                _ => 1.0f
            };
        }
        
        // Get vanilla hit timing for weapon type (from List_AnimationEvent.txt)
        private static float GetVanillaHitTimingForWeaponType(string weaponType)
        {
            return weaponType switch
            {
                "Axes" => 1.122f,       // Axe Secondary Attack: Hit=1.122s
                "BattleAxes" => 0.464f,  // BattleAxeAltAttack: Hit=0.464s
                "GreatSwords" => 0.959f, // Greatsword Secondary Attack: Hit=0.959s
                "Knives" => 0.802f,      // Knife JumpAttack: Hit=0.802s
                "Spears" => 0.739f,     // throw_spear: OnAttackTrigger=0.739s
                "Polearms" => 1.124f,   // Atgeir360Attack: Hit=1.124s
                "Fists" => 0.593f,       // Kickstep: Hit=0.593s
                "Swords" => 0.244f,     // Sword-Attack-R4: Hit=0.244s
                "Clubs" => 1.223f,      // MaceAltAttack: Hit=1.223s
                _ => 0.45f
            };
        }
        
        // Get vanilla trail on timing for weapon type (from List_AnimationEvent.txt)
        private static float GetVanillaTrailOnTimingForWeaponType(string weaponType)
        {
            return weaponType switch
            {
                "Axes" => 0.916f,       // Axe Secondary Attack: TrailOn=0.916s
                "BattleAxes" => 0.222f,  // BattleAxeAltAttack: TrailOn=0.222s
                "GreatSwords" => 0.719f, // Greatsword Secondary Attack: TrailOn=0.719s
                "Knives" => 0.608f,      // Knife JumpAttack: TrailOn=0.608s
                "Spears" => 0.509f,     // throw_spear: TrailOn=0.509s
                "Polearms" => 1.011f,   // Atgeir360Attack: TrailOn=1.011s
                "Fists" => 0.486f,       // Kickstep: TrailOn=0.486s
                "Swords" => 0.184f,     // Sword-Attack-R4: TrailOn=0.184s
                "Clubs" => 0.990f,      // MaceAltAttack: TrailOn=0.990s
                _ => 0.35f
            };
        }
        
        // Get vanilla trail off timing for weapon type (from List_AnimationEvent.txt)
        private static float GetVanillaTrailOffTimingForWeaponType(string weaponType)
        {
            return weaponType switch
            {
                "Axes" => 1.184f,       // Axe Secondary Attack: TrailOff=1.184s
                "BattleAxes" => 0.521f, // BattleAxeAltAttack: TrailOff=0.521s
                "GreatSwords" => 0.980f, // Greatsword Secondary Attack: TrailOff=0.980s
                "Knives" => 0.900f,     // Knife JumpAttack: TrailOff=0.900s
                "Spears" => 0.0f,       // throw_spear: no TrailOff event
                "Polearms" => 1.602f,   // Atgeir360Attack: TrailOff=1.602s
                "Fists" => 0.0f,         // Kickstep: no TrailOff
                "Swords" => 0.270f,     // Sword-Attack-R4: TrailOff=0.270s
                "Clubs" => 1.445f,      // MaceAltAttack: TrailOff=1.445s
                _ => 0.70f
            };
        }
        
        // ###### Calculate hit timing based on clip length and weapon type
        private static float CalculateHitTiming(float clipLength, string weaponType, string mode)
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
            
            // Vanilla HitTiming ratios for each weapon type (same for Q/T/G modes)
            float AxeHitTimingRatio = 0.801f; // Axe Secondary Attack (Hit: 1.122s/1.400s)
            float BattleAxesHitTimingRatio = 0.541f; // BattleAxeAltAttack (Hit: 0.464s/0.857s)
            float GreatSwordsHitTimingRatio = 0.685f; // Greatsword Secondary Attack (Hit: 0.959s/1.400s)
            float KnivesHitTimingRatio = 0.573f; // Knife JumpAttack (Hit: 0.802s/1.400s)
            float SpearsHitTimingRatio = 0.652f; // throw_spear (Hit: 0.739s/1.133s)
            float PolearmsHitTimingRatio = 0.519f; // Atgeir360Attack (Hit: 1.124s/2.167s)
            float FistsHitTimingRatio = 0.324f; // Kickstep (Hit: 0.593s/1.833s)
            
            // Fallback to weapon-specific and mode-specific default ratios applied to clip length
            // Each mode uses different animation clips, so different ratios should be used
            float fallbackRatio = (weaponType, mode) switch
            {
                // Q: own weapon type, T: different clip, G: different clip
                ("Axes", "Q") => AxeHitTimingRatio,        // Axe Secondary AttackExternal
                ("Axes", "T") => AxeHitTimingRatio,        // 2Hand-Sword-Attack8External
                ("Axes", "G") => AxeHitTimingRatio,        // Eas_GreatSword_JumpAttackExternal
                ("BattleAxes", "Q") => BattleAxesHitTimingRatio,  // 2Hand_Skill01_WhirlWindExternal
                ("BattleAxes", "T") => BattleAxesHitTimingRatio,  // 2Hand-Sword-Attack8External
                ("BattleAxes", "G") => BattleAxesHitTimingRatio,  // Eas_GreatSword_JumpAttackExternal
                ("GreatSwords", "Q") => GreatSwordsHitTimingRatio, // 2Hand-Sword-Attack8External
                ("GreatSwords", "T") => GreatSwordsHitTimingRatio, // 2Hand_Skill01_WhirlWindExternal
                ("GreatSwords", "G") => GreatSwordsHitTimingRatio, // Eas_GreatSword_JumpAttackExternal
                ("Knives", "Q") => KnivesHitTimingRatio,      // ChargeAttkExternal
                ("Knives", "T") => KnivesHitTimingRatio,      // Eas_GreatSword_JumpAttackExternal
                ("Knives", "G") => KnivesHitTimingRatio,      // 2Hand-Sword-Attack8External
                ("Spears", "Q") => SpearsHitTimingRatio,      // Eas_GreatSword_JumpAttackExternal
                ("Spears", "T") => SpearsHitTimingRatio,      // 2Hand_Skill01_WhirlWindExternal
                ("Spears", "G") => SpearsHitTimingRatio,      // 2Hand-Sword-Attack8External
                ("Polearms", "Q") => PolearmsHitTimingRatio,    // Eas_GreatSword_JumpAttackExternal
                ("Polearms", "T") => PolearmsHitTimingRatio,    // 2Hand-Sword-Attack8External
                ("Polearms", "G") => PolearmsHitTimingRatio,    // 2Hand_Skill01_WhirlWindExternal
                ("Fists", "Q") => FistsHitTimingRatio,       // Flying Knee Punch ComboExternal
                ("Fists", "T") => FistsHitTimingRatio,       // 2Hand_Skill01_WhirlWindExternal
                ("Fists", "G") => FistsHitTimingRatio,       // Eas_GreatSword_JumpAttackExternal
                _ => 0.45f
            };
            return Math.Min(clipLength, clipLength * fallbackRatio);
        }
        
        // Calculate trail on timing based on clip length and weapon type
        private static float CalculateTrailOnTiming(float clipLength, string weaponType, string mode)
        {
            float vanillaClipLength = GetVanillaClipLengthForWeaponType(weaponType);
            float vanillaTrailOnTiming = GetVanillaTrailOnTimingForWeaponType(weaponType);
            
            if (vanillaClipLength > 0 && vanillaTrailOnTiming > 0)
            {
                float ratio = vanillaTrailOnTiming / vanillaClipLength;
                return Math.Min(clipLength, clipLength * ratio);
            }
            
            // Fallback to weapon-specific and mode-specific default ratios applied to clip length
            // Each mode uses different animation clips, so different ratios should be used
            float fallbackRatio = (weaponType, mode) switch
            {
                // Q: own weapon type, T: different clip, G: different clip
                ("Axes", "Q") => 0.654f,        // Axe Secondary Attack (TrailOn: 0.916s/1.400s)
                ("Axes", "T") => 0.0f,          // 2Hand-Sword-Attack8 (TrailOn: なし)
                ("Axes", "G") => 0.491f,        // Eas_GreatSword_JumpAttack (TrailOn: 0.688s/1.400s)
                ("BattleAxes", "Q") => 0.259f,  // 2Hand_Skill01_WhirlWind (TrailOn: 0.222s/0.857s)
                ("BattleAxes", "T") => 0.0f,    // 2Hand-Sword-Attack8 (TrailOn: なし)
                ("BattleAxes", "G") => 0.491f,  // Eas_GreatSword_JumpAttack (TrailOn: 0.688s/1.400s)
                ("GreatSwords", "Q") => 0.0f,   // 2Hand-Sword-Attack8 (TrailOn: なし)
                ("GreatSwords", "T") => 0.259f, // 2Hand_Skill01_WhirlWind (TrailOn: 0.222s/0.857s)
                ("GreatSwords", "G") => 0.491f, // Eas_GreatSword_JumpAttack (TrailOn: 0.688s/1.400s)
                ("Knives", "Q") => 1.388f,      // ChargeAttk (TrailOn: 0.648s/0.467s) - 異常値のため調整
                ("Knives", "T") => 0.491f,      // Eas_GreatSword_JumpAttack (TrailOn: 0.688s/1.400s)
                ("Knives", "G") => 0.0f,       // 2Hand-Sword-Attack8 (TrailOn: なし)
                ("Spears", "Q") => 0.491f,      // Eas_GreatSword_JumpAttack (TrailOn: 0.688s/1.400s)
                ("Spears", "T") => 0.259f,      // 2Hand_Skill01_WhirlWind (TrailOn: 0.222s/0.857s)
                ("Spears", "G") => 0.0f,        // 2Hand-Sword-Attack8 (TrailOn: なし)
                ("Polearms", "Q") => 0.491f,    // Eas_GreatSword_JumpAttack (TrailOn: 0.688s/1.400s)
                ("Polearms", "T") => 0.0f,      // 2Hand-Sword-Attack8 (TrailOn: なし)
                ("Polearms", "G") => 0.259f,    // 2Hand_Skill01_WhirlWind (TrailOn: 0.222s/0.857s)
                ("Fists", "Q") => 0.510f,       // Flying Knee Punch Combo (TrailOn: 0.595s/1.167s)
                ("Fists", "T") => 0.259f,       // 2Hand_Skill01_WhirlWind (TrailOn: 0.222s/0.857s)
                ("Fists", "G") => 0.491f,       // Eas_GreatSword_JumpAttack (TrailOn: 0.688s/1.400s)
                _ => 0.32f
            };
            return Math.Min(clipLength, clipLength * fallbackRatio);
        }
        
        // Calculate trail off timing based on clip length and weapon type
        private static float CalculateTrailOffTiming(float clipLength, string weaponType, string mode)
        {
            float vanillaClipLength = GetVanillaClipLengthForWeaponType(weaponType);
            float vanillaTrailOffTiming = GetVanillaTrailOffTimingForWeaponType(weaponType);
            
            if (vanillaClipLength > 0 && vanillaTrailOffTiming > 0)
            {
                float ratio = vanillaTrailOffTiming / vanillaClipLength;
                return Math.Min(clipLength, clipLength * ratio);
            }
            
            // Fallback to weapon-specific default ratios applied to clip length
            // All modes (Q/T/G) use the same weapon type's secondary animation, so same ratio
            float fallbackRatio = weaponType switch
            {
                "Axes" => 0.551f,        // Axe Secondary Attack (same for Q/T/G)
                "BattleAxes" => 0.608f,  // 2Hand_Skill01_WhirlWind (same for Q/T/G)
                "GreatSwords" => 0.551f, // 2Hand-Sword-Attack8 (same for Q/T/G)
                "Knives" => 0.314f,      // ChargeAttk (same for Q/T/G)
                "Spears" => 0.526f,      // Eas_GreatSword_JumpAttack (same for Q/T/G)
                "Polearms" => 0.739f,    // Eas_GreatSword_JumpAttack (same for Q/T/G)
                "Fists" => 0.0f,         // Flying Knee Punch Combo (no TrailOff)
                _ => 0.55f
            };
            return Math.Min(clipLength, clipLength * fallbackRatio);
        }
        
        // Calculate chain timing based on clip length and weapon type
        private static float CalculateChainTiming(float clipLength, string weaponType, string mode)
        {
            // Secondary attacks don't have Chain events - return 0.0
            return 0.0f;
        }
        
        // Calculate speed timing based on clip length and weapon type
        private static float CalculateSpeedTiming(float clipLength, string weaponType, string mode)
        {
            float vanillaClipLength = GetVanillaClipLengthForWeaponType(weaponType);
            float vanillaSpeedTiming = GetVanillaSpeedTimingForWeaponType(weaponType);
            
            if (vanillaClipLength > 0 && vanillaSpeedTiming > 0)
            {
                float ratio = vanillaSpeedTiming / vanillaClipLength;
                return Math.Min(clipLength, clipLength * ratio);
            }
            
            // Fallback to weapon-specific default ratios applied to clip length
            // All modes (Q/T/G) use the same weapon type's secondary animation, so same ratio
            float fallbackRatio = weaponType switch
            {
                "Axes" => 0.0f,        // Axe Secondary Attack (same for Q/T/G)
                "BattleAxes" => 0.0f,  // 2Hand_Skill01_WhirlWind (same for Q/T/G)
                "GreatSwords" => 0.0f, // 2Hand-Sword-Attack8 (same for Q/T/G)
                "Knives" => 0.0f,      // ChargeAttk (same for Q/T/G)
                "Spears" => 0.0f,      // Eas_GreatSword_JumpAttack (same for Q/T/G)
                "Polearms" => 0.0f,    // Eas_GreatSword_JumpAttack (same for Q/T/G)
                "Fists" => 0.0f,       // Flying Knee Punch Combo (same for Q/T/G)
                _ => 0.00f
            };
            return Math.Min(clipLength, clipLength * fallbackRatio);
        }
        
        // Calculate dodge mortal timing based on clip length and weapon type
        private static float CalculateDodgeMortalTiming(float clipLength, string weaponType, string mode)
        {
            float vanillaClipLength = GetVanillaClipLengthForWeaponType(weaponType);
            float vanillaDodgeMortalTiming = GetVanillaDodgeMortalTimingForWeaponType(weaponType);
            
            if (vanillaClipLength > 0 && vanillaDodgeMortalTiming > 0)
            {
                float ratio = vanillaDodgeMortalTiming / vanillaClipLength;
                return Math.Min(clipLength, clipLength * ratio);
            }
            
            // Fallback to weapon-specific default ratios applied to clip length
            // All modes (Q/T/G) use the same weapon type's secondary animation, so same ratio
            float fallbackRatio = weaponType switch
            {
                "Axes" => 0.621f,        // Axe Secondary Attack (same for Q/T/G)
                "BattleAxes" => 0.980f,  // 2Hand_Skill01_WhirlWind (same for Q/T/G)
                "GreatSwords" => 0.607f, // 2Hand-Sword-Attack8 (same for Q/T/G)
                "Knives" => 0.607f,      // ChargeAttk (same for Q/T/G)
                "Spears" => 0.900f,      // Eas_GreatSword_JumpAttack (same for Q/T/G)
                "Polearms" => 0.415f,    // Eas_GreatSword_JumpAttack (same for Q/T/G)
                "Fists" => 0.900f,       // Flying Knee Punch Combo (same for Q/T/G)
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
                "Axes" => 0.0f,         // Axe Secondary Attack: no Speed event
                "BattleAxes" => 0.216f,  // BattleAxeAltAttack: Speed=0.216s
                "GreatSwords" => 0.0f,  // Greatsword Secondary Attack: no Speed event
                "Knives" => 0.0f,       // Knife JumpAttack: Speed=0.000s (first event)
                "Spears" => 0.0f,       // throw_spear: Speed=0.000s (first event)
                "Polearms" => 0.0f,     // Atgeir360Attack: Speed=0.000s (first event)
                "Fists" => 0.459f,      // Kickstep: Speed=0.459s
                "Swords" => 0.0f,       // Sword-Attack-R4: Speed=0.000s (first event)
                "Clubs" => 0.976f,      // MaceAltAttack: Speed=0.976s
                _ => 0.0f
            };
        }
        
        // Get vanilla dodge mortal timing for weapon type
        private static float GetVanillaDodgeMortalTimingForWeaponType(string weaponType)
        {
            return weaponType switch
            {
                "Axes" => 0.0f,         // Axe Secondary Attack: no DodgeMortal event
                "BattleAxes" => 0.0f,    // BattleAxeAltAttack: no DodgeMortal event
                "GreatSwords" => 0.0f,  // Greatsword Secondary Attack: no DodgeMortal event
                "Knives" => 0.0f,       // Knife JumpAttack: no DodgeMortal event
                "Spears" => 0.0f,       // throw_spear: no DodgeMortal event
                "Polearms" => 0.0f,     // Atgeir360Attack: no DodgeMortal event
                "Fists" => 0.0f,       // Kickstep: no DodgeMortal event
                "Swords" => 0.0f,      // Sword-Attack-R4: no DodgeMortal event
                "Clubs" => 0.0f,       // MaceAltAttack: no DodgeMortal event
                _ => 0.0f
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
            
            return baseRange;
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
            
            return baseHeight;
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
            if (AnimationManager.AnimationReplacementMap.ContainsKey(weaponType))
            {
                var weaponMap = AnimationManager.AnimationReplacementMap[weaponType];
                
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
                sb.AppendLine("#   WeaponType                  - Fallback (no suffix)");
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
                        
                        sb.AppendLine($"    # secondary_{modeKey} - ExtraAttack 1-> {replacementAnimation}");
                        sb.AppendLine($"    {mode.Key}:");
                        sb.AppendLine($"      # Animation Event Timing (0.0 ~ 1.0) - Zero means OFF");
                        
                        // Convert absolute timings to normalized ratios (0.0-1.0)
                        float clipLength = GetAdjustedClipLength($"{weaponType.Key}_{mode.Key}");
                        ExtraAttackPlugin.LogInfo("System", $"SaveWeaponTypeConfig: {weaponType.Key}_{mode.Key} -> clipLength={clipLength:F3}s, timing.HitTiming={timing.HitTiming:F3}s");
                        
                        float hitRatio = clipLength > 0 ? Math.Min(1.0f, timing.HitTiming / clipLength) : 0.0f;
                        float trailOnRatio = clipLength > 0 ? Math.Min(1.0f, timing.TrailOnTiming / clipLength) : 0.0f;
                        float trailOffRatio = clipLength > 0 ? Math.Min(1.0f, timing.TrailOffTiming / clipLength) : 0.0f;
                        float speedRatio = clipLength > 0 ? Math.Min(1.0f, timing.SpeedTiming / clipLength) : 0.0f;
                        float chainRatio = clipLength > 0 ? Math.Min(1.0f, timing.ChainTiming / clipLength) : 0.0f;
                        float dodgeMortalRatio = clipLength > 0 ? Math.Min(1.0f, timing.DodgeMortalTiming / clipLength) : 0.0f;
                        
                        sb.AppendLine($"      # Animation Event Timing (0.0 ~ 1.0 ratio based on clip length) - Zero means OFF");
                        sb.AppendLine($"      HitTiming: {hitRatio:F2}");
                        sb.AppendLine($"      TrailOnTiming: {trailOnRatio:F2}");
                        sb.AppendLine($"      TrailOffTiming: {trailOffRatio:F2}");
                        sb.AppendLine($"      SpeedTiming: {speedRatio:F2}");
                        sb.AppendLine($"      ChainTiming: {chainRatio:F2}");
                        sb.AppendLine($"      DodgeMortalTiming: {dodgeMortalRatio:F2}");
                        sb.AppendLine($"      SpeedMultiplier: {timing.SpeedMultiplier:F2}");
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