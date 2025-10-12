using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ExtraAttackSystem
{
    public class ExtraAttackCostConfig
    {
        public class AttackCost
        {
            // Base costs (affected by skill bonuses)
            public float StaminaCost { get; set; } = 15.0f;
            public float EitrCost { get; set; } = 8.0f;
        }

        public class AttackCooldown
        {
            // Fixed cooldown duration (not affected by skill bonuses)
            public float CooldownSec { get; set; } = 2.0f;
        }

        public class CostConfig
        {
            public AttackCost Default { get; set; } = new AttackCost();
            public AttackCooldown DefaultCooldown { get; set; } = new AttackCooldown();
            public Dictionary<string, Dictionary<string, AttackCost>> WeaponTypes { get; set; } = new Dictionary<string, Dictionary<string, AttackCost>>();
            public Dictionary<string, Dictionary<string, AttackCooldown>> Cooldowns { get; set; } = new Dictionary<string, Dictionary<string, AttackCooldown>>();
        }

        private static CostConfig? costConfig = null;
        private static readonly object configLock = new object();

        public static string ConfigFolderPath => Path.Combine(BepInEx.Paths.ConfigPath, "ExtraAttackSystem");
        public static string CostConfigFilePath => Path.Combine(ConfigFolderPath, "eas_attackconfig_cost.yaml");

        public static void Initialize()
        {
            lock (configLock)
            {
                try
                {
                    LoadCostConfig();
                    if (costConfig == null)
                    {
                        costConfig = CreateDefaultCostConfig();
                        SaveCostConfig();
                    }
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.LogError("System", $"Error initializing cost config: {ex.Message}");
                    costConfig = CreateDefaultCostConfig();
                }
            }
        }

        public static void ReloadCostConfig()
        {
            lock (configLock)
            {
                try
                {
                    LoadCostConfig();
                    ExtraAttackPlugin.LogInfo("System", "Cost config reloaded successfully");
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.LogError("System", $"Error reloading cost config: {ex.Message}");
                }
            }
        }

        private static void LoadCostConfig()
        {
            try
            {
                if (!File.Exists(CostConfigFilePath))
                {
                    ExtraAttackPlugin.LogInfo("Config", $"Cost config file not found: {CostConfigFilePath}");
                    return;
                }

                string content = File.ReadAllText(CostConfigFilePath, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(content))
                {
                    ExtraAttackPlugin.LogInfo("Config", "Cost config file is empty");
                    return;
                }

                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .IgnoreUnmatchedProperties()
                    .Build();

                costConfig = deserializer.Deserialize<CostConfig>(content);
                ExtraAttackPlugin.LogInfo("Config", $"Loaded cost config: {costConfig.WeaponTypes.Count} weapon types");
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error loading cost config: {ex.Message}");
                throw;
            }
        }

        private static void SaveCostConfig()
        {
            try
            {
                Directory.CreateDirectory(ConfigFolderPath);

                var serializer = new SerializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();

                string yaml = serializer.Serialize(costConfig);
                File.WriteAllText(CostConfigFilePath, yaml, Encoding.UTF8);
                ExtraAttackPlugin.LogInfo("System", $"Saved cost config to: {CostConfigFilePath}");
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error saving cost config: {ex.Message}");
                throw;
            }
        }

        private static CostConfig CreateDefaultCostConfig()
        {
            var config = new CostConfig();
            
            // Default costs (affected by skill bonuses)
            config.Default = new AttackCost
            {
                StaminaCost = 15.0f,
                EitrCost = 8.0f
            };

            // Default cooldown (fixed, not affected by skill bonuses)
            config.DefaultCooldown = new AttackCooldown
            {
                CooldownSec = 2.0f
            };

            // Weapon type specific costs and cooldowns
            var weaponTypes = new[] { "Swords", "Axes", "Clubs", "Spears", "GreatSwords", "BattleAxes", "Polearms", "Knives", "Fists" };
            var modes = new[] { "secondary_Q", "secondary_T", "secondary_G" };

            foreach (var weaponType in weaponTypes)
            {
                config.WeaponTypes[weaponType] = new Dictionary<string, AttackCost>();
                config.Cooldowns[weaponType] = new Dictionary<string, AttackCooldown>();
                
                foreach (var mode in modes)
                {
                    // Different costs per weapon type (affected by skill bonuses)
                    var cost = weaponType switch
                    {
                        "GreatSwords" => new AttackCost { StaminaCost = 25.0f, EitrCost = 12.0f },
                        "BattleAxes" => new AttackCost { StaminaCost = 30.0f, EitrCost = 15.0f },
                        "Polearms" => new AttackCost { StaminaCost = 20.0f, EitrCost = 10.0f },
                        "Knives" => new AttackCost { StaminaCost = 8.0f, EitrCost = 5.0f },
                        "Fists" => new AttackCost { StaminaCost = 5.0f, EitrCost = 3.0f },
                        _ => new AttackCost { StaminaCost = 15.0f, EitrCost = 8.0f }
                    };
                    
                    // Different cooldowns per weapon type (fixed values)
                    var cooldown = weaponType switch
                    {
                        "GreatSwords" => new AttackCooldown { CooldownSec = 3.0f },
                        "BattleAxes" => new AttackCooldown { CooldownSec = 3.5f },
                        "Polearms" => new AttackCooldown { CooldownSec = 2.5f },
                        "Knives" => new AttackCooldown { CooldownSec = 1.0f },
                        "Fists" => new AttackCooldown { CooldownSec = 0.5f },
                        _ => new AttackCooldown { CooldownSec = 2.0f }
                    };
                    
                    config.WeaponTypes[weaponType][mode] = cost;
                    config.Cooldowns[weaponType][mode] = cooldown;
                }
            }

            ExtraAttackPlugin.LogInfo("System", "Created default cost config");
            return config;
        }

        public static AttackCost? GetAttackCost(string weaponType, string attackMode)
        {
            lock (configLock)
            {
                try
                {
                    if (costConfig?.WeaponTypes?.TryGetValue(weaponType, out var weaponTypeDict) == true)
                    {
                        // Try unified key format: secondary_{Mode}
                        string mode = ExtractModeFromAttackMode(attackMode);
                        if (weaponTypeDict?.TryGetValue(mode, out AttackCost cost) == true)
                        {
                            ExtraAttackPlugin.LogInfo("Config", $"Found specific cost for {mode} (unified from {attackMode}): Stamina={cost.StaminaCost}, Eitr={cost.EitrCost}");
                            return cost;
                        }
                        
                        // Try direct lookup as fallback
                        if (weaponTypeDict?.TryGetValue(attackMode, out cost) == true)
                        {
                            ExtraAttackPlugin.LogInfo("Config", $"Found specific cost for {weaponType}_{attackMode}: Stamina={cost.StaminaCost}, Eitr={cost.EitrCost}");
                            return cost;
                        }
                        
                        ExtraAttackPlugin.LogWarning("Config", $"No specific cost found for {mode} or {weaponType}_{attackMode}");
                    }
                    
                    // Fallback to default if no specific cost found
                    var defaultCost = costConfig?.Default;
                    ExtraAttackPlugin.LogWarning("Config", $"Using default cost for {weaponType}_{attackMode}: Stamina={defaultCost?.StaminaCost ?? 15.0f}, Eitr={defaultCost?.EitrCost ?? 8.0f}");
                    return defaultCost;
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.LogError("System", $"Error getting attack cost for {weaponType}_{attackMode}: {ex.Message}");
                    return costConfig?.Default;
                }
            }
        }

        public static AttackCooldown? GetAttackCooldown(string weaponType, string attackMode)
        {
            lock (configLock)
            {
                try
                {
                    if (costConfig?.Cooldowns?.TryGetValue(weaponType, out var weaponTypeDict) == true)
                    {
                        // Try unified key format: secondary_{Mode}
                        string mode = ExtractModeFromAttackMode(attackMode);
                        if (weaponTypeDict?.TryGetValue(mode, out AttackCooldown cooldown) == true)
                        {
                            ExtraAttackPlugin.LogInfo("Config", $"Found specific cooldown for {mode} (unified from {attackMode}): Cooldown={cooldown.CooldownSec}");
                            return cooldown;
                        }
                        
                        // Try direct lookup as fallback
                        if (weaponTypeDict?.TryGetValue(attackMode, out cooldown) == true)
                        {
                            ExtraAttackPlugin.LogInfo("Config", $"Found specific cooldown for {weaponType}_{attackMode}: Cooldown={cooldown.CooldownSec}");
                            return cooldown;
                        }
                        
                        ExtraAttackPlugin.LogWarning("Config", $"No specific cooldown found for {mode} or {weaponType}_{attackMode}");
                    }
                    
                    // Fallback to default if no specific cooldown found
                    var defaultCooldown = costConfig?.DefaultCooldown;
                    ExtraAttackPlugin.LogWarning("Config", $"Using default cooldown for {weaponType}_{attackMode}: Cooldown={defaultCooldown?.CooldownSec ?? 2.0f}");
                    return defaultCooldown;
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.LogError("System", $"Error getting attack cooldown for {weaponType}_{attackMode}: {ex.Message}");
                    return costConfig?.DefaultCooldown;
                }
            }
        }

        private static string ExtractModeFromAttackMode(string attackMode)
        {
            // Convert ea_secondary_Q, secondary_Q, or Q to secondary_Q
            if (attackMode.StartsWith("ea_secondary_"))
            {
                return attackMode.Replace("ea_secondary_", "secondary_");
            }
            else if (attackMode.StartsWith("secondary_"))
            {
                return attackMode;
            }
            else if (attackMode == "Q" || attackMode == "T" || attackMode == "G")
            {
                return $"secondary_{attackMode}";
            }
            
            return attackMode;
        }

        public static void GenerateCostConfig()
        {
            lock (configLock)
            {
                try
                {
                    if (costConfig == null)
                    {
                        costConfig = CreateDefaultCostConfig();
                    }
                    
                    SaveCostConfig();
                    ExtraAttackPlugin.LogInfo("System", "Generated cost config file");
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.LogError("System", $"Error generating cost config: {ex.Message}");
                }
            }
        }
    }
}
