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
    /// <summary>
    /// AnimationTimingConfig - manages YAML configuration and timing calculations
    /// </summary>
    public static class AnimationTimingConfig
    {
        // Config file paths
        private static string ConfigFolderPath => Path.Combine(BepInEx.Paths.ConfigPath, "ExtraAttackSystem");
        private static string WeaponTypesConfigFilePath => Path.Combine(ConfigFolderPath, "eas_attackconfig_WeaponTypes.yaml");
        private static string IndividualWeaponsConfigFilePath => Path.Combine(ConfigFolderPath, "eas_attackconfig_IndividualWeapons.yaml");

        // Timing data for each animation
        public class AnimationTiming
        {
            public float HitTiming { get; set; }
            public float TrailOnTiming { get; set; }
            public float TrailOffTiming { get; set; }
            public float ChainTiming { get; set; }
            public float SpeedTiming { get; set; }
            public float SpeedMultiplier { get; set; }
            public float DodgeMortalTiming { get; set; }
            public float ClipLength { get; set; }
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
        public class WeaponTypeConfig
        {
            public AnimationTiming Default { get; set; } = new AnimationTiming();
            public Dictionary<string, Dictionary<string, AnimationTiming>> WeaponTypes { get; set; } = new Dictionary<string, Dictionary<string, AnimationTiming>>();
            public Dictionary<string, AnimationTiming> IndividualWeapons { get; set; } = new Dictionary<string, AnimationTiming>();
        }

        private static WeaponTypeConfig weaponTypeConfig = new WeaponTypeConfig();

        // Initialize configuration
        public static void Initialize()
        {
            try
            {
                if (!Directory.Exists(ConfigFolderPath))
                    Directory.CreateDirectory(ConfigFolderPath);

                // Load or create weapon types config
                if (!File.Exists(WeaponTypesConfigFilePath) || ShouldCreateOrRegenerateWeaponTypesConfig())
                {
                    CreateDefaultWeaponTypeConfig();
                }
                LoadWeaponTypeConfig();

                // Load or create individual weapons config
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
                return true;

            try
            {
                string content = File.ReadAllText(WeaponTypesConfigFilePath, Encoding.UTF8).Trim();
                return string.IsNullOrEmpty(content) || !content.Contains("WeaponTypes:") || !content.Contains("secondary_");
                }
            catch
                {
                    return true;
            }
        }

        // Check if individual weapons config should be created or regenerated
        private static bool ShouldCreateOrRegenerateIndividualWeaponsConfig()
        {
            if (!File.Exists(IndividualWeaponsConfigFilePath))
                return true;

            try
            {
                string content = File.ReadAllText(IndividualWeaponsConfigFilePath, Encoding.UTF8).Trim();
                return string.IsNullOrEmpty(content) || !content.Contains("IndividualWeapons:");
            }
            catch
                {
                    return true;
            }
        }

        // Get timing for specific animation
        public static AnimationTiming GetTiming(string animationName)
        {
            // Get custom animation length
            float customClipLength = AnimationManager.GetExternalClipLengthSmart(animationName);
            
            if (customClipLength <= 0)
            {
                // Fallback if clip length not found
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
                    SpeedMultiplier = 1.0f,
                    EnableHit = true,
                    EnableSound = true
                };
            }
            
            // Get vanilla timing for this weapon type
            string weaponType = GetWeaponTypeFromAnimationName(animationName);
            var vanillaTiming = GetVanillaTimingForWeaponType(weaponType);
            
            // Calculate ratio: customLength / vanillaLength
            float ratio = customClipLength / vanillaTiming.ClipLength;
            
            // Apply ratio to timing events, keep attack parameters as-is
            return new AnimationTiming
            {
                HitTiming = vanillaTiming.HitTiming * ratio,
                TrailOnTiming = vanillaTiming.TrailOnTiming * ratio,
                TrailOffTiming = vanillaTiming.TrailOffTiming * ratio,
                ChainTiming = vanillaTiming.ChainTiming * ratio,
                SpeedTiming = vanillaTiming.SpeedTiming * ratio,
                DodgeMortalTiming = vanillaTiming.DodgeMortalTiming * ratio,
                ClipLength = customClipLength,
                SpeedMultiplier = vanillaTiming.SpeedMultiplier,
                // Attack parameters from vanilla (unchanged)
                AttackRange = vanillaTiming.AttackRange,
                AttackHeight = vanillaTiming.AttackHeight,
                AttackAngle = vanillaTiming.AttackAngle,
                AttackOffset = vanillaTiming.AttackOffset,
                AttackRayWidth = vanillaTiming.AttackRayWidth,
                AttackRayWidthCharExtra = vanillaTiming.AttackRayWidthCharExtra,
                AttackHeightChar1 = vanillaTiming.AttackHeightChar1,
                AttackHeightChar2 = vanillaTiming.AttackHeightChar2,
                MaxYAngle = vanillaTiming.MaxYAngle,
                EnableHit = vanillaTiming.EnableHit,
                EnableSound = vanillaTiming.EnableSound
            };
        }

        // Get timing for weapon type and attack mode
        public static AnimationTiming GetWeaponTypeTiming(string weaponType, string attackMode)
        {
            try
            {
                // Try individual weapon first
                if (weaponTypeConfig.IndividualWeapons.TryGetValue($"{weaponType}_{attackMode}", out var individualTiming))
                {
                    ExtraAttackPlugin.LogInfo("System", $"Using individual weapon setting: {weaponType}_{attackMode}");
                    return individualTiming;
                }

                // Try weapon type specific using unified key format
                if (weaponTypeConfig.WeaponTypes.TryGetValue(weaponType, out var weaponTypeSettings))
                {
                    string modeKey = $"secondary_{attackMode}";
                    if (weaponTypeSettings.TryGetValue(modeKey, out var typeTiming))
                    {
                        ExtraAttackPlugin.LogInfo("System", $"Using YAML weapon type setting: {weaponType}_{modeKey} (HitTiming={typeTiming.HitTiming})");
                        return typeTiming;
            }
            else
            {
                        ExtraAttackPlugin.LogWarning("System", $"YAML weapon type setting not found: {weaponType}_{modeKey}");
            }
            }
            else
            {
                    ExtraAttackPlugin.LogWarning("System", $"YAML weapon type not found: {weaponType}");
                }

                // Create timing based on actual animation clip length
                ExtraAttackPlugin.LogInfo("System", $"Creating timing for: {weaponType}_{attackMode}");
                return CreateTimingForWeaponType(weaponType, attackMode);
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error getting weapon type timing: {ex.Message}");
                return new AnimationTiming();
            }
        }

        // Create timing settings for specific weapon type and mode
        private static AnimationTiming CreateTimingForWeaponType(string weaponType, string mode)
        {
            var timing = new AnimationTiming();
            
            // Get actual animation clip length
            string key = $"{weaponType}_secondary_{mode}";
            float clipLength = GetAdjustedClipLength(key);
            
            if (clipLength > 0)
            {
                // Calculate timings using the new calculator
                timing.HitTiming = AnimationTimingCalculator.CalculateHitTiming(clipLength, weaponType, mode);
                timing.TrailOnTiming = AnimationTimingCalculator.CalculateTrailOnTiming(clipLength, weaponType, mode);
                timing.TrailOffTiming = AnimationTimingCalculator.CalculateTrailOffTiming(clipLength, weaponType, mode);
                timing.ChainTiming = AnimationTimingCalculator.CalculateChainTiming(clipLength, weaponType, mode);
                timing.SpeedTiming = AnimationTimingCalculator.CalculateSpeedTiming(clipLength, weaponType, mode);
                timing.DodgeMortalTiming = AnimationTimingCalculator.CalculateDodgeMortalTiming(clipLength, weaponType, mode);
                timing.ClipLength = clipLength;
            }
            else
            {
                // Fallback to default ratios
                timing.HitTiming = 0.45f;
                timing.TrailOnTiming = 0.35f;
                timing.TrailOffTiming = 0.70f;
                timing.ChainTiming = 0.85f;
                timing.SpeedTiming = 0.50f;
                timing.DodgeMortalTiming = 0.70f;
                timing.ClipLength = -1f;
            }

            // Set attack detection parameters from vanilla data
            var vanillaParams = EAS_VanillaDataProvider.GetAllVanillaParameters(weaponType);
            timing.AttackRange = vanillaParams.attackRange;
            timing.AttackHeight = vanillaParams.attackHeight;
            timing.AttackAngle = vanillaParams.attackAngle;
            timing.AttackOffset = vanillaParams.attackOffset;
            timing.AttackRayWidth = vanillaParams.attackRayWidth;
            timing.AttackRayWidthCharExtra = vanillaParams.attackRayWidthCharExtra;
            timing.AttackHeightChar1 = vanillaParams.attackHeightChar1;
            timing.AttackHeightChar2 = vanillaParams.attackHeightChar2;
            timing.MaxYAngle = vanillaParams.maxYAngle;
            timing.SpeedMultiplier = 1.0f;
            timing.EnableHit = vanillaParams.enableHit;
            timing.EnableSound = vanillaParams.enableSound;

            return timing;
        }
        
        // Get adjusted clip length using AnimationManager's logic
        private static float GetAdjustedClipLength(string key)
        {
            try
            {
                var parts = key.Split('_');
                if (parts.Length >= 3)
                {
                    var weaponType = parts[0];
                    string mode = parts[2]; // Q, T, G
                    
                    string externalClipName = GetExternalClipForWeaponType(weaponType, mode);
                    
                    if (!string.IsNullOrEmpty(externalClipName))
                    {
                        float clipLength = AnimationManager.GetExternalClipLengthSmart(externalClipName);
                        
                        if (clipLength > 0)
                        {
                            return clipLength;
                        }
                        
                            if (AnimationManager.CustomAnimationClips.TryGetValue(externalClipName, out var clip))
                            {
                            return clip.length;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error getting adjusted clip length for {key}: {ex.Message}");
            }
            
            return -1f;
        }
        
        // Get external clip for weapon type and mode from YAML configuration
        public static string GetExternalClipForWeaponType(string weaponType, string mode)
        {
            try
            {
                // Try to get from AnimationManager.AnimationReplacementMap (YAML loaded values)
                if (AnimationManager.AnimationReplacementMap.TryGetValue(weaponType, out var weaponMappings))
                {
                    string clipKey = $"secondary_{mode}";
                    if (weaponMappings.TryGetValue(clipKey, out var externalClip))
                    {
                        return externalClip;
                    }
                }
                
                // Fallback to hardcoded values
                return GetFallbackExternalClipForWeaponType(weaponType, mode);
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error in GetExternalClipForWeaponType: {ex.Message}");
                return GetFallbackExternalClipForWeaponType(weaponType, mode);
            }
        }
        
        // Fallback method with hardcoded values
        private static string GetFallbackExternalClipForWeaponType(string weaponType, string mode)
        {
            return weaponType switch
            {
                "Sword" => mode == "Q" ? "2Hand-Sword-Attack8External" : mode == "T" ? "2Hand_Skill01_WhirlWindExternal" : "Eas_GreatSword_JumpAttackExternal",
                "Axe" => mode == "Q" ? "OneHand_Up_Attack_B_1External" : mode == "T" ? "2Hand-Sword-Attack8External" : "Eas_GreatSword_JumpAttackExternal",
                "Club" => mode == "Q" ? "0MWA_DualWield_Attack02External" : mode == "T" ? "MWA_RightHand_Attack03External" : "Shield@ShieldAttack01External",
                "Spear" => mode == "Q" ? "Shield@ShieldAttack02External" : mode == "T" ? "Attack04External" : "0MGSA_Attack_Dash01External",
                "Greatsword" => mode == "Q" ? "2Hand-Sword-Attack8External" : mode == "T" ? "2Hand_Skill01_WhirlWindExternal" : "Eas_GreatSword_Combo1External",
                "Battleaxe" => mode == "Q" ? "0MGSA_Attack_Dash02External" : mode == "T" ? "0MGSA_Attack_Ground01External" : "0MGSA_Attack_Ground02External",
                "Polearm" => mode == "Q" ? "Pa_1handShiled_attack02External" : mode == "T" ? "Attack_ShieldExternal" : "0DS_Attack_07External",
                "Knife" => mode == "Q" ? "ChargeAttkExternal" : mode == "T" ? "HardAttkExternal" : "StrongAttk3External",
                "Fist" => mode == "Q" ? "Flying Knee Punch ComboExternal" : mode == "T" ? "Eas_GreatSword_SlideAttackExternal" : "Eas_GreatSwordSlash_01External",
                _ => "2Hand-Sword-Attack8External"
            };
        }

        // Check if config exists
        public static bool HasConfig(string configKey)
        {
            return weaponTypeConfig.IndividualWeapons.ContainsKey(configKey) ||
                   weaponTypeConfig.WeaponTypes.Any(wt => wt.Value.ContainsKey(configKey.Split('_').Last()));
        }

        // Helper methods
        private static string CapitalizeFirstLetter(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;
            
            return char.ToUpper(input[0]) + input.Substring(1);
        }

        // Get weapon type from animation name
        private static string GetWeaponTypeFromAnimationName(string animationName)
        {
            // Try to extract weapon type from animation name patterns
            if (animationName.Contains("Sword") || animationName.Contains("sword"))
                return "Sword";
            else if (animationName.Contains("Axe") || animationName.Contains("axe"))
                return "Axe";
            else if (animationName.Contains("Club") || animationName.Contains("club"))
                return "Club";
            else if (animationName.Contains("Spear") || animationName.Contains("spear"))
                return "Spear";
            else if (animationName.Contains("Knife") || animationName.Contains("knife"))
                return "Knife";
            else if (animationName.Contains("Fist") || animationName.Contains("fist"))
                return "Fist";
            else if (animationName.Contains("Great") || animationName.Contains("great"))
                return "Greatsword";
            else if (animationName.Contains("Battle") || animationName.Contains("battle"))
                return "Battleaxe";
            else if (animationName.Contains("Pole") || animationName.Contains("pole"))
                return "Polearm";
            else
                return "Sword"; // Default fallback
        }

        // Get vanilla timing for weapon type
        private static AnimationTiming GetVanillaTimingForWeaponType(string weaponType)
        {
            // Get vanilla clip length
            float vanillaClipLength = EAS_VanillaDataProvider.GetVanillaClipLengthForWeaponType(weaponType);
            
            // Get vanilla timing values
            float vanillaHitTiming = EAS_VanillaDataProvider.GetVanillaHitTimingForWeaponType(weaponType);
            float vanillaTrailOnTiming = EAS_VanillaDataProvider.GetVanillaTrailOnTimingForWeaponType(weaponType);
            float vanillaTrailOffTiming = EAS_VanillaDataProvider.GetVanillaTrailOffTimingForWeaponType(weaponType);
            
            // Get vanilla attack parameters
            var vanillaParams = EAS_VanillaDataProvider.GetAllVanillaParameters(weaponType);
            
            return new AnimationTiming
            {
                HitTiming = vanillaHitTiming,
                TrailOnTiming = vanillaTrailOnTiming,
                TrailOffTiming = vanillaTrailOffTiming,
                ChainTiming = vanillaClipLength * 0.85f, // Default chain timing
                SpeedTiming = vanillaClipLength * 0.5f, // Default speed timing
                DodgeMortalTiming = vanillaClipLength * 0.7f, // Default dodge mortal timing
                ClipLength = vanillaClipLength,
                SpeedMultiplier = 1.0f,
                AttackRange = vanillaParams.attackRange,
                AttackHeight = vanillaParams.attackHeight,
                AttackAngle = vanillaParams.attackAngle,
                AttackOffset = vanillaParams.attackOffset,
                AttackRayWidth = vanillaParams.attackRayWidth,
                AttackRayWidthCharExtra = vanillaParams.attackRayWidthCharExtra,
                AttackHeightChar1 = vanillaParams.attackHeightChar1,
                AttackHeightChar2 = vanillaParams.attackHeightChar2,
                MaxYAngle = vanillaParams.maxYAngle,
                EnableHit = vanillaParams.enableHit,
                EnableSound = vanillaParams.enableSound
            };
        }


        // Placeholder methods for config loading (simplified)
        private static void LoadWeaponTypeConfig()
        {
            try
            {
                var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
                var yamlContent = File.ReadAllText(WeaponTypesConfigFilePath);
                weaponTypeConfig = deserializer.Deserialize<WeaponTypeConfig>(yamlContent) ?? new WeaponTypeConfig();
                
                if (weaponTypeConfig?.WeaponTypes == null)
                {
                    weaponTypeConfig = new WeaponTypeConfig();
                    weaponTypeConfig.WeaponTypes = new Dictionary<string, Dictionary<string, AnimationTiming>>();
                }
                
                ExtraAttackPlugin.LogInfo("System", $"Loaded weapon type config: {weaponTypeConfig?.WeaponTypes?.Count ?? 0} weapon types");
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error loading weapon type config: {ex.Message}");
                weaponTypeConfig = new WeaponTypeConfig();
            }
        }

        private static void LoadIndividualWeaponsConfig()
        {
            try
            {
                if (File.Exists(IndividualWeaponsConfigFilePath))
                {
                    var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
                    var yamlContent = File.ReadAllText(IndividualWeaponsConfigFilePath);
                    var individualConfig = deserializer.Deserialize<WeaponTypeConfig>(yamlContent) ?? new WeaponTypeConfig();
                    
                    if (weaponTypeConfig.IndividualWeapons == null)
                        weaponTypeConfig.IndividualWeapons = new Dictionary<string, AnimationTiming>();
                    
                    if (individualConfig.IndividualWeapons != null)
                    {
                        foreach (var kvp in individualConfig.IndividualWeapons)
                        {
                            weaponTypeConfig.IndividualWeapons[kvp.Key] = kvp.Value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error loading individual weapons config: {ex.Message}");
            }
        }

        private static void CreateDefaultWeaponTypeConfig()
        {
            // Simplified default config creation
            ExtraAttackPlugin.LogInfo("System", "Creating default weapon type config");
        }

        private static void CreateDefaultIndividualWeaponsConfig()
        {
            // Simplified individual weapons config creation
            ExtraAttackPlugin.LogInfo("System", "Creating default individual weapons config");
        }

        // Reload config (for runtime changes)
        public static void Reload()
        {
            ExtraAttackPlugin.LogInfo("System", "F6: AnimationTimingConfig reload");
            
            if (File.Exists(WeaponTypesConfigFilePath))
            {
                LoadWeaponTypeConfig();
            }
            
            if (File.Exists(IndividualWeaponsConfigFilePath))
            {
                LoadIndividualWeaponsConfig();
            }
            
            ExtraAttackPlugin.LogInfo("System", "F6: AnimationTimingConfig reload completed");
        }

        // Get weapon type config for external access
        public static WeaponTypeConfig GetWeaponTypeConfig()
        {
            return weaponTypeConfig;
        }
    }
}
