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
                       StaminaCost = 20.0f,  // バニラのセカンダリ攻撃と同じ値
                       EitrCost = 0.0f,      // デフォルト0
                       CooldownSec = 0.0f    // テスト中暫定でデフォルト0
                   };

            // Weapon type specific costs and cooldowns
            var weaponTypes = new[] { "Sword", "Axe", "Club", "Spear", "Greatsword", "Battleaxe", "Polearm", "Knife", "Fist" };
            var modes = new[] { "secondary_Q", "secondary_T", "secondary_G" };

            foreach (var weaponType in weaponTypes)
            {
                config.WeaponTypes[weaponType] = new Dictionary<string, AttackCost>();
                
                foreach (var mode in modes)
                {
                           // Different costs per weapon type (affected by skill bonuses)
                           var cost = weaponType switch
                           {
                               "Greatsword" => new AttackCost { StaminaCost = 20.0f, EitrCost = 0.0f, CooldownSec = 0.0f },  // バニラセカンダリと同じ、エイトル0、クールダウン0
                               "Battleaxe" => new AttackCost { StaminaCost = 20.0f, EitrCost = 0.0f, CooldownSec = 0.0f },   // バニラセカンダリと同じ、エイトル0、クールダウン0
                               "Polearm" => new AttackCost { StaminaCost = 20.0f, EitrCost = 0.0f, CooldownSec = 0.0f },     // バニラセカンダリと同じ、エイトル0、クールダウン0
                               "Knife" => new AttackCost { StaminaCost = 20.0f, EitrCost = 0.0f, CooldownSec = 0.0f },       // バニラセカンダリと同じ、エイトル0、クールダウン0
                               "Fist" => new AttackCost { StaminaCost = 20.0f, EitrCost = 0.0f, CooldownSec = 0.0f },        // バニラセカンダリと同じ、エイトル0、クールダウン0
                               _ => new AttackCost { StaminaCost = 20.0f, EitrCost = 0.0f, CooldownSec = 0.0f }                // バニラセカンダリと同じ、エイトル0、クールダウン0
                           };
                    
                    config.WeaponTypes[weaponType][mode] = cost;
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
                           ExtraAttackPlugin.LogWarning("Config", $"Using default cost for {weaponType}_{attackMode}: Stamina={defaultCost?.StaminaCost ?? 20.0f}, Eitr={defaultCost?.EitrCost ?? 0.0f}");
                    return defaultCost;
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.LogError("System", $"Error getting attack cost for {weaponType}_{attackMode}: {ex.Message}");
                    return costConfig?.Default;
                }
            }
        }

        public static float GetAttackCooldown(string weaponType, string attackMode)
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
                            ExtraAttackPlugin.LogInfo("Config", $"Found specific cooldown for {mode} (unified from {attackMode}): Cooldown={cost.CooldownSec}");
                            return cost.CooldownSec;
                        }
                        
                        // Try direct lookup as fallback
                        if (weaponTypeDict?.TryGetValue(attackMode, out cost) == true)
                        {
                            ExtraAttackPlugin.LogInfo("Config", $"Found specific cooldown for {weaponType}_{attackMode}: Cooldown={cost.CooldownSec}");
                            return cost.CooldownSec;
                        }
                        
                        ExtraAttackPlugin.LogWarning("Config", $"No specific cooldown found for {mode} or {weaponType}_{attackMode}");
                    }
                    
                    // Fallback to default if no specific cooldown found
                    var defaultCost = costConfig?.Default;
                    float defaultCooldown = defaultCost?.CooldownSec ?? 0.0f;
                    ExtraAttackPlugin.LogWarning("Config", $"Using default cooldown for {weaponType}_{attackMode}: Cooldown={defaultCooldown}");
                    return defaultCooldown;
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.LogError("System", $"Error getting attack cooldown for {weaponType}_{attackMode}: {ex.Message}");
                    return costConfig?.Default?.CooldownSec ?? 0.0f;
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
            else if (attackMode.Contains("_secondary_"))
            {
                // Extract mode from WeaponType_secondary_Mode format
                var parts = attackMode.Split('_');
                if (parts.Length >= 3)
                {
                    return $"secondary_{parts[parts.Length - 1]}";
                }
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
