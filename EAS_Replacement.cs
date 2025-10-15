using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ExtraAttackSystem
{
    // Manage YAML for animation replacement maps
    public static class EAS_Replacement
    {
        private static string ConfigFolderPath => Path.Combine(BepInEx.Paths.ConfigPath, "ExtraAttackSystem");
        private static string WeaponTypesConfigFilePath => Path.Combine(ConfigFolderPath, "AnimationReplacement_WeaponTypes.yaml");

        // YAML structure
        public class ReplacementYaml
        {
            public Dictionary<string, Dictionary<string, string>> WeaponTypes { get; set; } = new();
        }

        // Initialize: create or load YAML, then apply to AnimationManager.AnimationReplacementMap
        public static void Initialize()
        {
            try
            {
                if (!Directory.Exists(ConfigFolderPath))
                {
                    Directory.CreateDirectory(ConfigFolderPath);
                }

                // Create default YAML if not exists
                if (!File.Exists(WeaponTypesConfigFilePath))
                {
                    ExtraAttackSystemPlugin.LogInfo("System", "AnimationReplacement_WeaponTypes.yaml not found, creating default");
                    CreateDefaultConfig();
                }

                // Load YAML and apply to manager
                LoadWeaponTypesConfig();
                ApplyToManager();

                ExtraAttackSystemPlugin.LogInfo("System", "EAS_Replacement initialized successfully");
            }
            catch (Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error initializing EAS_Replacement: {ex.Message}");
            }
        }

        // Create default weapon types configuration
        private static void CreateDefaultConfig()
        {
            try
            {
                var config = new ReplacementYaml
                {
                    WeaponTypes = new Dictionary<string, Dictionary<string, string>>
                    {
                        // Greatsword (両手剣)
                        ["Greatsword"] = new Dictionary<string, string>
                        {
                            ["secondary_Q"] = "Eas_GreatSword_Combo1External",
                            ["secondary_T"] = "Eas_GreatSword_JumpAttackExternal",
                            ["secondary_G"] = "Eas_GreatSword_SlideAttackExternal"
                        },
                        // Sword (片手剣)
                        ["Sword"] = new Dictionary<string, string>
                        {
                            ["secondary_Q"] = "OneHand_Up_Attack_B_1External",
                            ["secondary_T"] = "OneHand_Up_Attack_B_2External",
                            ["secondary_G"] = "OneHand_Up_Attack_B_3External"
                        },
                        // Axe (片手斧)
                        ["Axe"] = new Dictionary<string, string>
                        {
                            ["secondary_Q"] = "MWA_RightHand_Attack03External",
                            ["secondary_T"] = "Attack04External",
                            ["secondary_G"] = "0DS_Attack_07External"
                        },
                        // Battleaxe (両手斧)
                        ["Battleaxe"] = new Dictionary<string, string>
                        {
                            ["secondary_Q"] = "2Hand_Combo01External",
                            ["secondary_T"] = "2Hand_Combo02External",
                            ["secondary_G"] = "2Hand_Combo03External"
                        },
                        // Club (棍棒)
                        ["Club"] = new Dictionary<string, string>
                        {
                            ["secondary_Q"] = "StrongAttk1External",
                            ["secondary_T"] = "StrongAttk3External",
                            ["secondary_G"] = "StrongAttk4External"
                        },
                        // Spear (槍)
                        ["Spear"] = new Dictionary<string, string>
                        {
                            ["secondary_Q"] = "ChargeAttkExternal",
                            ["secondary_T"] = "HardAttkExternal",
                            ["secondary_G"] = "0MGSA_Attack_Dash01External"
                        },
                        // Knife (ナイフ)
                        ["Knife"] = new Dictionary<string, string>
                        {
                            ["secondary_Q"] = "Sw-Ma-GS-Up_Attack_A_1External",
                            ["secondary_T"] = "Sw-Ma-GS-Up_Attack_A_2External",
                            ["secondary_G"] = "Sw-Ma-GS-Up_Attack_A_3External"
                        },
                        // Fist (素手)
                        ["Fist"] = new Dictionary<string, string>
                        {
                            ["secondary_Q"] = "Flying Knee Punch ComboExternal",
                            ["secondary_T"] = "Standing Melee Attack 360 LowExternal",
                            ["secondary_G"] = "Standing Melee Combo AttackExternal"
                        },
                        // Shield (盾)
                        ["Shield"] = new Dictionary<string, string>
                        {
                            ["secondary_Q"] = "Shield@ShieldAttack01External",
                            ["secondary_T"] = "Shield@ShieldAttack02External",
                            ["secondary_G"] = "Attack_ShieldExternal"
                        }
                    }
                };

                // Serialize to YAML
                var serializer = new SerializerBuilder()
                    .WithNamingConvention(NullNamingConvention.Instance)
                    .Build();

                string yaml = serializer.Serialize(config);
                File.WriteAllText(WeaponTypesConfigFilePath, yaml, Encoding.UTF8);

                ExtraAttackSystemPlugin.LogInfo("System", $"Created default AnimationReplacement_WeaponTypes.yaml with {config.WeaponTypes.Count} weapon types");
            }
            catch (Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error creating default config: {ex.Message}");
            }
        }

        // Load weapon types YAML
        private static void LoadWeaponTypesConfig()
        {
            try
            {
                if (!File.Exists(WeaponTypesConfigFilePath))
                {
                    ExtraAttackSystemPlugin.LogWarning("System", $"AnimationReplacement_WeaponTypes.yaml not found at {WeaponTypesConfigFilePath}");
                    return;
                }

                string yaml = File.ReadAllText(WeaponTypesConfigFilePath, Encoding.UTF8);
                
                if (string.IsNullOrWhiteSpace(yaml))
                {
                    ExtraAttackSystemPlugin.LogWarning("System", "AnimationReplacement_WeaponTypes.yaml is empty");
                    return;
                }

                // Deserialize YAML
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(NullNamingConvention.Instance)
                    .Build();

                var config = deserializer.Deserialize<ReplacementYaml>(yaml);
                
                if (config?.WeaponTypes != null && config.WeaponTypes.Count > 0)
                {
                    // Apply to AnimationManager.AnimationReplacementMap
                    foreach (var weaponType in config.WeaponTypes)
                    {
                        EAS_AnimationManager.AnimationReplacementMap[weaponType.Key] = weaponType.Value;
                    }
                    
                    ExtraAttackSystemPlugin.LogInfo("System", $"Loaded {config.WeaponTypes.Count} weapon types from YAML");
                }
                else
                {
                    ExtraAttackSystemPlugin.LogWarning("System", "No weapon types found in YAML");
                }
            }
            catch (Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error loading weapon types config: {ex.Message}");
            }
        }

        // Apply loaded YAML to AnimationManager.AnimationReplacementMap
        private static void ApplyToManager()
        {
            try
            {
                ExtraAttackSystemPlugin.LogInfo("System", $"Applied {EAS_AnimationManager.AnimationReplacementMap.Count} weapon types to AnimationManager");
            }
            catch (Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error applying to manager: {ex.Message}");
            }
        }

        // Reload configuration
        public static void Reload()
        {
            try
            {
                EAS_AnimationManager.AnimationReplacementMap.Clear();
                LoadWeaponTypesConfig();
                ApplyToManager();
                ExtraAttackSystemPlugin.LogInfo("System", "EAS_Replacement reloaded successfully");
            }
            catch (Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error reloading EAS_Replacement: {ex.Message}");
            }
        }
    }
}

