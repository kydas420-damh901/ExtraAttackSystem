using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ExtraAttackSystem
{
    public class EAS_CostConfig
    {
        public class AttackCost
        {
            // Base costs (affected by skill bonuses)
            public float StaminaCost { get; set; }
            public float EitrCost { get; set; }
            // Fixed cooldown duration (not affected by skill bonuses)
            public float CooldownSec { get; set; }
        }

        public class CostConfig
        {
            public AttackCost Default { get; set; } = new AttackCost();
            public Dictionary<string, Dictionary<string, AttackCost>> WeaponTypes { get; set; } = new Dictionary<string, Dictionary<string, AttackCost>>();
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
                        ExtraAttackSystemPlugin.LogInfo("System", "Cost config file not found, creating default");
                        costConfig = CreateDefaultCostConfig();
                        SaveCostConfig();
                    }
                }
                catch (Exception ex)
                {
                    ExtraAttackSystemPlugin.LogError("System", $"Error initializing cost config: {ex.Message}");
                    costConfig = CreateDefaultCostConfig();
                    SaveCostConfig();
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
                    ExtraAttackSystemPlugin.LogInfo("System", "Cost config reloaded successfully");
                }
                catch (Exception ex)
                {
                    ExtraAttackSystemPlugin.LogError("System", $"Error reloading cost config: {ex.Message}");
                }
            }
        }

        private static void LoadCostConfig()
        {
            try
            {
                if (!File.Exists(CostConfigFilePath))
                {
                    ExtraAttackSystemPlugin.LogInfo("System", $"Cost config file not found: {CostConfigFilePath}");
                    return;
                }

                string content = File.ReadAllText(CostConfigFilePath, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(content))
                {
                    ExtraAttackSystemPlugin.LogInfo("System", "Cost config file is empty");
                    return;
                }

                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .IgnoreUnmatchedProperties()
                    .Build();

                costConfig = deserializer.Deserialize<CostConfig>(content);
                
                // Null check before accessing properties
                if (costConfig != null && costConfig.WeaponTypes != null)
                {
                    ExtraAttackSystemPlugin.LogInfo("System", $"Loaded cost config: {costConfig.WeaponTypes.Count} weapon types");
                }
                else
                {
                    ExtraAttackSystemPlugin.LogInfo("System", "Cost config deserialized as null or invalid");
                }
            }
            catch (Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error loading cost config: {ex.Message}");
                costConfig = null; // Ensure costConfig is null on error
            }
        }

        private static void SaveCostConfig()
        {
            try
            {
                if (!Directory.Exists(ConfigFolderPath))
                {
                    Directory.CreateDirectory(ConfigFolderPath);
                }

                var serializer = new SerializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
                    .Build();

                // Create a copy without the Default property for serialization
                var configToSave = new CostConfig
                {
                    WeaponTypes = costConfig?.WeaponTypes ?? new Dictionary<string, Dictionary<string, AttackCost>>()
                    // Default property is intentionally omitted
                };

                string yaml = serializer.Serialize(configToSave);
                File.WriteAllText(CostConfigFilePath, yaml, Encoding.UTF8);
                ExtraAttackSystemPlugin.LogInfo("Config", $"Saved cost config to {CostConfigFilePath}");
            }
            catch (Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error saving cost config: {ex.Message}");
            }
        }


        private static CostConfig CreateDefaultCostConfig()
        {
            var config = new CostConfig();
            
            // Default costs
            config.Default = new AttackCost
            {
                StaminaCost = 10.0f,
                EitrCost = 0.0f,
                CooldownSec = 2.0f
            };

            // Weapon type specific costs
            string[] weaponTypes = { "Sword", "Axe", "Club", "Spear", "Knife", "Greatsword", "Battleaxe", "Polearm", "Fist" };
            string[] modes = { "secondary_Q", "secondary_T", "secondary_G" };

            foreach (var weaponType in weaponTypes)
            {
                config.WeaponTypes[weaponType] = new Dictionary<string, AttackCost>();
                
                foreach (var mode in modes)
                {
                    config.WeaponTypes[weaponType][mode] = new AttackCost
                    {
                        StaminaCost = 10.0f,
                        EitrCost = 0.0f,
                        CooldownSec = 2.0f
                    };
                }
            }

            return config;
        }

        // Get attack cooldown for weapon type and mode
        public static float GetAttackCooldown(string weaponType, string mode)
        {
            lock (configLock)
            {
                if (costConfig == null) return 2.0f;

                try
                {
                    // Try weapon type specific
                    if (costConfig.WeaponTypes.TryGetValue(weaponType, out var weaponSettings))
                    {
                        if (weaponSettings.TryGetValue(mode, out var cost))
                        {
                            return cost.CooldownSec;
                        }
                    }

                    // Fallback to default
                    return costConfig.Default.CooldownSec;
                }
                catch (Exception ex)
                {
                    ExtraAttackSystemPlugin.LogError("System", $"Error getting attack cooldown: {ex.Message}");
                    return 2.0f;
                }
            }
        }

        // Get stamina cost for weapon type and mode
        public static float GetStaminaCost(string weaponType, string mode)
        {
            lock (configLock)
            {
                if (costConfig == null) return 10.0f;

                try
                {
                    // Try weapon type specific
                    if (costConfig.WeaponTypes.TryGetValue(weaponType, out var weaponSettings))
                    {
                        if (weaponSettings.TryGetValue(mode, out var cost))
                        {
                            return cost.StaminaCost;
                        }
                    }

                    // Fallback to default
                    return costConfig.Default.StaminaCost;
                }
                catch (Exception ex)
                {
                    ExtraAttackSystemPlugin.LogError("System", $"Error getting stamina cost: {ex.Message}");
                    return 10.0f;
                }
            }
        }

        // Get eitr cost for weapon type and mode
        public static float GetEitrCost(string weaponType, string mode)
        {
            lock (configLock)
            {
                if (costConfig == null) return 0.0f;

                try
                {
                    // Try weapon type specific
                    if (costConfig.WeaponTypes.TryGetValue(weaponType, out var weaponSettings))
                    {
                        if (weaponSettings.TryGetValue(mode, out var cost))
                        {
                            return cost.EitrCost;
                        }
                    }

                    // Fallback to default
                    return costConfig.Default.EitrCost;
                }
                catch (Exception ex)
                {
                    ExtraAttackSystemPlugin.LogError("System", $"Error getting eitr cost: {ex.Message}");
                    return 0.0f;
                }
            }
        }
    }
}
