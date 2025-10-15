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
    public static class EAS_AnimationTiming
    {
        // YAML設定データ（比率とパラメータ）
        public class AnimationTimingConfig
        {
            // タイミング比率（0～1.0で調整可能）
            public float HitTimingRatio { get; set; } = 1.0f;
            public float TrailOnRatio { get; set; } = 1.0f;
            public float TrailOffRatio { get; set; } = 1.0f;
            public float ChainRatio { get; set; } = 1.0f;
            public float SpeedRatio { get; set; } = 1.0f;
            public float DodgeMortalRatio { get; set; } = 1.0f;
            
            // 攻撃パラメータ（現実的な値）
            public float AttackRange { get; set; } = 3.5f;
            public float AttackHeight { get; set; } = 2.0f;
            public float AttackOffset { get; set; } = 0.0f;
            public float AttackAngle { get; set; } = 180.0f;
            public float AttackRayWidth { get; set; } = 0.8f;
            public float AttackRayWidthCharExtra { get; set; } = 0.2f;
            public float AttackHeightChar1 { get; set; } = 1.5f;
            public float AttackHeightChar2 { get; set; } = 1.2f;
            public float MaxYAngle { get; set; } = 60.0f;
            public bool EnableHit { get; set; } = true;
            public bool EnableSound { get; set; } = true;
        }

        // 最終的なタイミングデータ（計算済み）
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
            public AnimationTimingConfig Default { get; set; } = new AnimationTimingConfig();
            public Dictionary<string, Dictionary<string, AnimationTimingConfig>> WeaponTypes { get; set; } = new Dictionary<string, Dictionary<string, AnimationTimingConfig>>();
            public Dictionary<string, AnimationTimingConfig> IndividualWeapons { get; set; } = new Dictionary<string, AnimationTimingConfig>();
        }

        private static WeaponTypeConfig weaponTypeConfig = new WeaponTypeConfig();

        // バニラのタイミング値（固定値）
        private static readonly Dictionary<string, AnimationTiming> VanillaTimings = new Dictionary<string, AnimationTiming>
        {
            ["Sword"] = new AnimationTiming
            {
                HitTiming = 0.5f, TrailOnTiming = 0.3f, TrailOffTiming = 0.8f,
                ChainTiming = 0.85f, SpeedTiming = 1.0f, DodgeMortalTiming = 1.0f,
                ClipLength = 1.2f, AttackRange = 2.0f, AttackHeight = 1.0f,
                AttackOffset = 0.0f, AttackAngle = 120.0f, AttackRayWidth = 0.5f,
                AttackRayWidthCharExtra = 0.1f, AttackHeightChar1 = 1.0f, AttackHeightChar2 = 0.8f,
                MaxYAngle = 45.0f, EnableHit = true, EnableSound = true
            },
            ["Greatsword"] = new AnimationTiming
            {
                HitTiming = 0.6f, TrailOnTiming = 0.4f, TrailOffTiming = 0.9f,
                ChainTiming = 0.9f, SpeedTiming = 1.0f, DodgeMortalTiming = 1.0f,
                ClipLength = 1.8f, AttackRange = 2.5f, AttackHeight = 1.2f,
                AttackOffset = 0.0f, AttackAngle = 140.0f, AttackRayWidth = 0.6f,
                AttackRayWidthCharExtra = 0.15f, AttackHeightChar1 = 1.1f, AttackHeightChar2 = 0.9f,
                MaxYAngle = 50.0f, EnableHit = true, EnableSound = true
            },
            ["Axe"] = new AnimationTiming
            {
                HitTiming = 0.4f, TrailOnTiming = 0.2f, TrailOffTiming = 0.7f,
                ChainTiming = 0.8f, SpeedTiming = 1.0f, DodgeMortalTiming = 1.0f,
                ClipLength = 1.0f, AttackRange = 1.8f, AttackHeight = 0.9f,
                AttackOffset = 0.0f, AttackAngle = 100.0f, AttackRayWidth = 0.4f,
                AttackRayWidthCharExtra = 0.1f, AttackHeightChar1 = 0.9f, AttackHeightChar2 = 0.7f,
                MaxYAngle = 40.0f, EnableHit = true, EnableSound = true
            },
            ["Club"] = new AnimationTiming
            {
                HitTiming = 0.7f, TrailOnTiming = 0.5f, TrailOffTiming = 1.0f,
                ChainTiming = 0.95f, SpeedTiming = 1.0f, DodgeMortalTiming = 1.0f,
                ClipLength = 2.0f, AttackRange = 2.2f, AttackHeight = 1.1f,
                AttackOffset = 0.0f, AttackAngle = 130.0f, AttackRayWidth = 0.55f,
                AttackRayWidthCharExtra = 0.12f, AttackHeightChar1 = 1.05f, AttackHeightChar2 = 0.85f,
                MaxYAngle = 47.0f, EnableHit = true, EnableSound = true
            },
            ["Spear"] = new AnimationTiming
            {
                HitTiming = 0.3f, TrailOnTiming = 0.1f, TrailOffTiming = 0.6f,
                ChainTiming = 0.7f, SpeedTiming = 1.0f, DodgeMortalTiming = 1.0f,
                ClipLength = 0.8f, AttackRange = 3.0f, AttackHeight = 0.8f,
                AttackOffset = 0.0f, AttackAngle = 60.0f, AttackRayWidth = 0.3f,
                AttackRayWidthCharExtra = 0.05f, AttackHeightChar1 = 0.8f, AttackHeightChar2 = 0.6f,
                MaxYAngle = 30.0f, EnableHit = true, EnableSound = true
            },
            ["Knife"] = new AnimationTiming
            {
                HitTiming = 0.2f, TrailOnTiming = 0.1f, TrailOffTiming = 0.4f,
                ChainTiming = 0.6f, SpeedTiming = 1.0f, DodgeMortalTiming = 1.0f,
                ClipLength = 0.6f, AttackRange = 1.5f, AttackHeight = 0.7f,
                AttackOffset = 0.0f, AttackAngle = 90.0f, AttackRayWidth = 0.3f,
                AttackRayWidthCharExtra = 0.08f, AttackHeightChar1 = 0.7f, AttackHeightChar2 = 0.5f,
                MaxYAngle = 35.0f, EnableHit = true, EnableSound = true
            },
            ["Battleaxe"] = new AnimationTiming
            {
                HitTiming = 0.8f, TrailOnTiming = 0.6f, TrailOffTiming = 1.1f,
                ChainTiming = 1.0f, SpeedTiming = 1.0f, DodgeMortalTiming = 1.0f,
                ClipLength = 2.2f, AttackRange = 2.8f, AttackHeight = 1.3f,
                AttackOffset = 0.0f, AttackAngle = 150.0f, AttackRayWidth = 0.7f,
                AttackRayWidthCharExtra = 0.18f, AttackHeightChar1 = 1.2f, AttackHeightChar2 = 1.0f,
                MaxYAngle = 55.0f, EnableHit = true, EnableSound = true
            },
            ["Polearm"] = new AnimationTiming
            {
                HitTiming = 0.9f, TrailOnTiming = 0.7f, TrailOffTiming = 1.2f,
                ChainTiming = 1.1f, SpeedTiming = 1.0f, DodgeMortalTiming = 1.0f,
                ClipLength = 2.5f, AttackRange = 3.2f, AttackHeight = 1.4f,
                AttackOffset = 0.0f, AttackAngle = 160.0f, AttackRayWidth = 0.8f,
                AttackRayWidthCharExtra = 0.2f, AttackHeightChar1 = 1.3f, AttackHeightChar2 = 1.1f,
                MaxYAngle = 60.0f, EnableHit = true, EnableSound = true
            },
            ["Fist"] = new AnimationTiming
            {
                HitTiming = 0.15f, TrailOnTiming = 0.05f, TrailOffTiming = 0.3f,
                ChainTiming = 0.5f, SpeedTiming = 1.0f, DodgeMortalTiming = 1.0f,
                ClipLength = 0.5f, AttackRange = 1.2f, AttackHeight = 0.6f,
                AttackOffset = 0.0f, AttackAngle = 80.0f, AttackRayWidth = 0.25f,
                AttackRayWidthCharExtra = 0.05f, AttackHeightChar1 = 0.6f, AttackHeightChar2 = 0.4f,
                MaxYAngle = 30.0f, EnableHit = true, EnableSound = true
            }
        };

        public static void Initialize()
        {
            try
            {
                if (!Directory.Exists(ConfigFolderPath))
                    Directory.CreateDirectory(ConfigFolderPath);

                LoadWeaponTypeConfig();
                ExtraAttackSystemPlugin.LogInfo("System", "EAS_AnimationTiming initialized successfully");
            }
            catch (Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error initializing EAS_AnimationTiming: {ex.Message}");
            }
        }

        private static string ConfigFolderPath => Path.Combine(BepInEx.Paths.ConfigPath, "ExtraAttackSystem");
        private static string WeaponTypesConfigFilePath => Path.Combine(ConfigFolderPath, "eas_attackconfig_WeaponTypes.yaml");

        // Get timing for specific animation
        public static AnimationTiming GetTiming(string animationName)
        {
            // Get custom animation length
            float customClipLength = EAS_AnimationManager.GetExternalClipLengthSmart(animationName);
            
            // Get weapon type and mode
            string weaponType = GetWeaponTypeFromAnimationName(animationName);
            string mode = GetModeFromAnimationName(animationName);
            
            // Get vanilla timing for this weapon type
            var vanillaTiming = GetVanillaTimingForWeaponType(weaponType);
            
            // Get YAML config for this weapon type and mode
            var yamlConfig = GetYAMLConfig(weaponType, mode);
            
            if (customClipLength <= 0)
            {
                // Fallback if clip length not found - use vanilla values
                return new AnimationTiming
                {
                    HitTiming = vanillaTiming.HitTiming,
                    TrailOnTiming = vanillaTiming.TrailOnTiming,
                    TrailOffTiming = vanillaTiming.TrailOffTiming,
                    ChainTiming = vanillaTiming.ChainTiming,
                    SpeedTiming = vanillaTiming.SpeedTiming,
                    DodgeMortalTiming = vanillaTiming.DodgeMortalTiming,
                    ClipLength = vanillaTiming.ClipLength,
                    SpeedMultiplier = vanillaTiming.SpeedMultiplier,
                    AttackRange = yamlConfig.AttackRange,
                    AttackHeight = yamlConfig.AttackHeight,
                    AttackAngle = yamlConfig.AttackAngle,
                    AttackOffset = yamlConfig.AttackOffset,
                    AttackRayWidth = yamlConfig.AttackRayWidth,
                    AttackRayWidthCharExtra = yamlConfig.AttackRayWidthCharExtra,
                    AttackHeightChar1 = yamlConfig.AttackHeightChar1,
                    AttackHeightChar2 = yamlConfig.AttackHeightChar2,
                    MaxYAngle = yamlConfig.MaxYAngle,
                    EnableHit = yamlConfig.EnableHit,
                    EnableSound = yamlConfig.EnableSound
                };
            }
            
            // Calculate ratio: customLength / vanillaLength
            float lengthRatio = customClipLength / vanillaTiming.ClipLength;
            
            // Apply YAML ratio and length ratio to timing events
            return new AnimationTiming
            {
                HitTiming = vanillaTiming.HitTiming * yamlConfig.HitTimingRatio * lengthRatio,
                TrailOnTiming = vanillaTiming.TrailOnTiming * yamlConfig.TrailOnRatio * lengthRatio,
                TrailOffTiming = vanillaTiming.TrailOffTiming * yamlConfig.TrailOffRatio * lengthRatio,
                ChainTiming = vanillaTiming.ChainTiming * yamlConfig.ChainRatio * lengthRatio,
                SpeedTiming = vanillaTiming.SpeedTiming * yamlConfig.SpeedRatio * lengthRatio,
                DodgeMortalTiming = vanillaTiming.DodgeMortalTiming * yamlConfig.DodgeMortalRatio * lengthRatio,
                ClipLength = customClipLength,
                SpeedMultiplier = vanillaTiming.SpeedMultiplier,
                // Attack parameters from YAML config
                AttackRange = yamlConfig.AttackRange,
                AttackHeight = yamlConfig.AttackHeight,
                AttackAngle = yamlConfig.AttackAngle,
                AttackOffset = yamlConfig.AttackOffset,
                AttackRayWidth = yamlConfig.AttackRayWidth,
                AttackRayWidthCharExtra = yamlConfig.AttackRayWidthCharExtra,
                AttackHeightChar1 = yamlConfig.AttackHeightChar1,
                AttackHeightChar2 = yamlConfig.AttackHeightChar2,
                MaxYAngle = yamlConfig.MaxYAngle,
                EnableHit = yamlConfig.EnableHit,
                EnableSound = yamlConfig.EnableSound
            };
        }

        // Get weapon type from animation name
        private static string GetWeaponTypeFromAnimationName(string animationName)
        {
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
            float vanillaClipLength = GetVanillaClipLengthForWeaponType(weaponType);
            
            // Get vanilla timing values
            float vanillaHitTiming = GetVanillaHitTimingForWeaponType(weaponType);
            float vanillaTrailOnTiming = GetVanillaTrailOnTimingForWeaponType(weaponType);
            float vanillaTrailOffTiming = GetVanillaTrailOffTimingForWeaponType(weaponType);
            
            return new AnimationTiming
            {
                HitTiming = vanillaHitTiming,
                TrailOnTiming = vanillaTrailOnTiming,
                TrailOffTiming = vanillaTrailOffTiming,
                ChainTiming = vanillaClipLength * 0.85f,
                SpeedTiming = vanillaClipLength * 0.5f,
                DodgeMortalTiming = vanillaClipLength * 0.7f,
                ClipLength = vanillaClipLength,
                SpeedMultiplier = 1.0f,
                AttackRange = 2.0f,
                AttackHeight = 1.0f,
                AttackAngle = 90.0f,
                AttackOffset = 0.0f,
                AttackRayWidth = 0.0f,
                AttackRayWidthCharExtra = 0.0f,
                AttackHeightChar1 = 0.0f,
                AttackHeightChar2 = 0.0f,
                MaxYAngle = 0.0f,
                EnableHit = true,
                EnableSound = true
            };
        }

        // Vanilla data methods
        private static float GetVanillaClipLengthForWeaponType(string weaponType)
        {
            return weaponType switch
            {
                "Sword" => 1.400f,
                "Axe" => 1.400f,
                "Greatsword" => 1.400f,
                "Battleaxe" => 0.857f,
                "Club" => 1.400f,
                "Spear" => 1.133f,
                "Polearm" => 2.167f,
                "Knife" => 1.400f,
                "Fist" => 1.833f,
                _ => 1.0f
            };
        }

        private static float GetVanillaHitTimingForWeaponType(string weaponType)
        {
            return weaponType switch
            {
                "Axe" => 1.122f,
                "Battleaxe" => 0.464f,
                "Greatsword" => 0.959f,
                "Knife" => 0.802f,
                "Spear" => 0.739f,
                "Polearm" => 1.124f,
                "Fist" => 0.593f,
                "Sword" => 0.244f,
                "Club" => 1.223f,
                _ => 0.45f
            };
        }

        private static float GetVanillaTrailOnTimingForWeaponType(string weaponType)
        {
            return weaponType switch
            {
                "Axe" => 0.916f,
                "Battleaxe" => 0.222f,
                "Greatsword" => 0.719f,
                "Knife" => 0.608f,
                "Spear" => 0.509f,
                "Polearm" => 1.011f,
                "Fist" => 0.486f,
                "Sword" => 0.184f,
                "Club" => 0.990f,
                _ => 0.35f
            };
        }

        private static float GetVanillaTrailOffTimingForWeaponType(string weaponType)
        {
            return weaponType switch
            {
                "Axe" => 1.184f,
                "Battleaxe" => 0.521f,
                "Greatsword" => 0.980f,
                "Knife" => 0.900f,
                "Spear" => 0.0f,
                "Polearm" => 1.602f,
                "Fist" => 0.0f,
                "Sword" => 0.270f,
                "Club" => 1.445f,
                _ => 0.70f
            };
        }

        private static void LoadWeaponTypeConfig()
        {
            try
            {
                if (!File.Exists(WeaponTypesConfigFilePath))
                {
                    ExtraAttackSystemPlugin.LogInfo("System", "Weapon types config file not found, creating default");
                    CreateDefaultWeaponTypeConfig();
                    return;
                }

                var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
                var yamlContent = File.ReadAllText(WeaponTypesConfigFilePath);
                
                if (string.IsNullOrWhiteSpace(yamlContent))
                {
                    ExtraAttackSystemPlugin.LogInfo("System", "Weapon types config file is empty, creating default");
                    CreateDefaultWeaponTypeConfig();
                    return;
                }
                
                weaponTypeConfig = deserializer.Deserialize<WeaponTypeConfig>(yamlContent) ?? new WeaponTypeConfig();
                
                if (weaponTypeConfig?.WeaponTypes == null || weaponTypeConfig.WeaponTypes.Count == 0)
                {
                    ExtraAttackSystemPlugin.LogInfo("System", "Weapon types config is empty, creating default");
                    CreateDefaultWeaponTypeConfig();
                    return;
                }
                
                ExtraAttackSystemPlugin.LogInfo("System", $"Loaded weapon type config: {weaponTypeConfig?.WeaponTypes?.Count ?? 0} weapon types");
            }
            catch (Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error loading weapon type config: {ex.Message}");
                CreateDefaultWeaponTypeConfig();
            }
        }


        private static void CreateDefaultWeaponTypeConfig()
        {
            try
            {
                ExtraAttackSystemPlugin.LogInfo("System", "Creating default weapon type config...");
                
                weaponTypeConfig = new WeaponTypeConfig();
                
                // Create default config for each weapon type
                var weaponTypes = new[] { "Sword", "Greatsword", "Axe", "Club", "Spear", "Knife", "Battleaxe", "Polearm", "Fist" };
                var modes = new[] { "secondary_Q", "secondary_T", "secondary_G" };
                
                foreach (var weaponType in weaponTypes)
                {
                    var weaponConfigs = new Dictionary<string, AnimationTimingConfig>();
                    
                    // Get weapon-specific base configuration
                    var weaponSpecificConfig = GetWeaponSpecificConfig(weaponType);
                    
                    foreach (var mode in modes)
                    {
                        // Use weapon-specific configuration as base, then apply mode-specific overrides if needed
                        weaponConfigs[mode] = new AnimationTimingConfig
                        {
                            // Copy weapon-specific values
                            AttackRange = weaponSpecificConfig.AttackRange,
                            AttackHeight = weaponSpecificConfig.AttackHeight,
                            AttackOffset = weaponSpecificConfig.AttackOffset,
                            AttackAngle = weaponSpecificConfig.AttackAngle,
                            AttackRayWidth = weaponSpecificConfig.AttackRayWidth,
                            AttackRayWidthCharExtra = weaponSpecificConfig.AttackRayWidthCharExtra,
                            AttackHeightChar1 = weaponSpecificConfig.AttackHeightChar1,
                            AttackHeightChar2 = weaponSpecificConfig.AttackHeightChar2,
                            MaxYAngle = weaponSpecificConfig.MaxYAngle,
                            EnableHit = weaponSpecificConfig.EnableHit,
                            EnableSound = weaponSpecificConfig.EnableSound,
                            
                            // Keep default timing ratios
                            HitTimingRatio = 1.0f,
                            TrailOnRatio = 1.0f,
                            TrailOffRatio = 1.0f,
                            ChainRatio = 1.0f,
                            SpeedRatio = 1.0f,
                            DodgeMortalRatio = 1.0f
                        };
                    }
                    
                    weaponTypeConfig.WeaponTypes[weaponType] = weaponConfigs;
                }
                
                ExtraAttackSystemPlugin.LogInfo("System", $"Created config for {weaponTypeConfig.WeaponTypes.Count} weapon types");
                
                // Save default config to file
                var serializer = new SerializerBuilder().Build();
                var yamlContent = serializer.Serialize(weaponTypeConfig);
                
                ExtraAttackSystemPlugin.LogInfo("System", $"Config folder path: {ConfigFolderPath}");
                ExtraAttackSystemPlugin.LogInfo("System", $"Config file path: {WeaponTypesConfigFilePath}");
                
                Directory.CreateDirectory(ConfigFolderPath);
                File.WriteAllText(WeaponTypesConfigFilePath, yamlContent);
                
                ExtraAttackSystemPlugin.LogInfo("System", "Created default weapon type config file successfully");
            }
            catch (Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error creating default weapon type config: {ex.Message}");
                ExtraAttackSystemPlugin.LogError("System", $"Stack trace: {ex.StackTrace}");
            }
        }

        // Get mode from animation name
        private static string GetModeFromAnimationName(string animationName)
        {
            if (animationName.Contains("_Q") || animationName.EndsWith("_Q"))
                return "secondary_Q";
            else if (animationName.Contains("_T") || animationName.EndsWith("_T"))
                return "secondary_T";
            else if (animationName.Contains("_G") || animationName.EndsWith("_G"))
                return "secondary_G";
            
            return "secondary_Q"; // fallback
        }

        // Get weapon-specific configuration (based on vanilla secondary attack values from List_timing_param.log)
        private static AnimationTimingConfig GetWeaponSpecificConfig(string weaponType)
        {
            return weaponType switch
            {
                "Sword" => new AnimationTimingConfig  // From $item_sword_blackmetal
                {
                    AttackRange = 2.7f,
                    AttackHeight = 1.0f,
                    AttackAngle = 10.0f,
                    AttackRayWidth = 0.7f,
                    AttackRayWidthCharExtra = 0.0f,
                    AttackHeightChar1 = 0.0f,
                    AttackHeightChar2 = 0.0f,
                    MaxYAngle = 0.0f
                },
                "Greatsword" => new AnimationTimingConfig  // From $item_sword_krom
                {
                    AttackRange = 3.0f,
                    AttackHeight = 1.0f,
                    AttackAngle = 30.0f,
                    AttackRayWidth = 0.5f,
                    AttackRayWidthCharExtra = 0.0f,
                    AttackHeightChar1 = 0.0f,
                    AttackHeightChar2 = 0.0f,
                    MaxYAngle = 0.0f
                },
                "Axe" => new AnimationTimingConfig  // From $item_axe_bronze
                {
                    AttackRange = 2.2f,
                    AttackHeight = 0.6f,
                    AttackAngle = 90.0f,
                    AttackRayWidth = 0.0f,
                    AttackRayWidthCharExtra = 0.0f,
                    AttackHeightChar1 = 0.0f,
                    AttackHeightChar2 = 0.0f,
                    MaxYAngle = 0.0f
                },
                "Club" => new AnimationTimingConfig  // From $item_mace_iron
                {
                    AttackRange = 2.5f,
                    AttackHeight = 0.8f,
                    AttackAngle = 30.0f,
                    AttackRayWidth = 0.3f,
                    AttackRayWidthCharExtra = 0.0f,
                    AttackHeightChar1 = 0.0f,
                    AttackHeightChar2 = 0.0f,
                    MaxYAngle = 0.0f
                },
                "Spear" => new AnimationTimingConfig  // Spear uses projectile, use default melee values
                {
                    AttackRange = 2.5f,
                    AttackHeight = 1.0f,
                    AttackAngle = 60.0f,
                    AttackRayWidth = 0.3f,
                    AttackRayWidthCharExtra = 0.0f,
                    AttackHeightChar1 = 0.0f,
                    AttackHeightChar2 = 0.0f,
                    MaxYAngle = 0.0f
                },
                "Knife" => new AnimationTimingConfig  // From $item_knife_copper
                {
                    AttackRange = 1.8f,
                    AttackHeight = 0.5f,
                    AttackAngle = 45.0f,
                    AttackRayWidth = 0.3f,
                    AttackRayWidthCharExtra = 0.0f,
                    AttackHeightChar1 = 0.34f,
                    AttackHeightChar2 = -0.34f,
                    MaxYAngle = 0.0f
                },
                "Battleaxe" => new AnimationTimingConfig  // From $item_battleaxe_crystal
                {
                    AttackRange = 2.5f,
                    AttackHeight = 1.0f,
                    AttackAngle = 30.0f,
                    AttackRayWidth = 0.5f,
                    AttackRayWidthCharExtra = 0.0f,
                    AttackHeightChar1 = 0.0f,
                    AttackHeightChar2 = 0.0f,
                    MaxYAngle = 0.0f
                },
                "Polearm" => new AnimationTimingConfig  // From $item_atgeir_bronze
                {
                    AttackRange = 3.0f,
                    AttackHeight = 1.0f,
                    AttackAngle = 360.0f,
                    AttackRayWidth = 0.3f,
                    AttackRayWidthCharExtra = 0.0f,
                    AttackHeightChar1 = 0.0f,
                    AttackHeightChar2 = 0.0f,
                    MaxYAngle = 0.0f
                },
                "Fist" => new AnimationTimingConfig  // From Unarmed skill
                {
                    AttackRange = 1.5f,
                    AttackHeight = 0.6f,
                    AttackAngle = 90.0f,
                    AttackRayWidth = 0.0f,
                    AttackRayWidthCharExtra = 0.0f,
                    AttackHeightChar1 = 0.0f,
                    AttackHeightChar2 = 0.0f,
                    MaxYAngle = 0.0f
                },
                "Unarmed" => new AnimationTimingConfig  // From Unarmed skill
                {
                    AttackRange = 1.5f,
                    AttackHeight = 0.6f,
                    AttackAngle = 90.0f,
                    AttackRayWidth = 0.0f,
                    AttackRayWidthCharExtra = 0.0f,
                    AttackHeightChar1 = 0.0f,
                    AttackHeightChar2 = 0.0f,
                    MaxYAngle = 0.0f
                },
                _ => new AnimationTimingConfig() // Default fallback
            };
        }

        // Get YAML config for weapon type and mode
        private static AnimationTimingConfig GetYAMLConfig(string weaponType, string mode)
        {
            // Try to get specific weapon type and mode config
            if (weaponTypeConfig.WeaponTypes.TryGetValue(weaponType, out var weaponConfigs) &&
                weaponConfigs.TryGetValue(mode, out var config))
            {
                return config;
            }
            
            // Fallback to default config
            return weaponTypeConfig.Default;
        }
    }
}
