using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using Jotunn.Managers;
using HarmonyLib;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ExtraAttackSystem
{
    /// <summary>
    /// 共通処理をまとめるユーティリティクラス
    /// 重複した処理を統一し、コードの保守性を向上させる
    /// </summary>
    public static class EAS_CommonUtils
    {
        // Attack mode enum
        public enum AttackMode
        {
            Normal,         // Left click (vanilla animation)
            secondary_Q, // Q key - secondary_Q attack
            secondary_T, // T key - secondary_T attack
            secondary_G, // G key - secondary_G attack
        }

        // Attack mode tracking
        private static readonly Dictionary<Player, AttackMode> playerAttackModes = new();
        private static readonly Dictionary<Player, Dictionary<AttackMode, float>> playerCooldowns = new();

        // Static message dictionary for localization
        private static readonly Dictionary<string, string> MessageDictionary = new()
        {
            { "extra_attack_triggered", "Extra Attack!" },
            { "extra_attack_cooldown", "Extra Attack on cooldown: {0}s" },
            { "extra_attack_no_stamina", "Not enough stamina for Extra Attack" },
            { "extra_attack_no_weapon", "No weapon equipped" },
            { "extra_attack_blocked", "Cannot use Extra Attack right now" },
            { "extra_attack_no_secondary", "Secondary attack is not defined for the equipped weapon" },
            { "extra_attack_tool_bomb_blocked", "This item type does not support extra attacks" },
            { "extra_attack_ac", "Animator Controller for {0}: {1}" }
        };

        
        // Attack mode management
        public static void SetAttackMode(Player player, AttackMode mode)
        {
            if (player != null)
            {
                playerAttackModes[player] = mode;
            }
        }

        public static AttackMode GetAttackMode(Player player)
        {
            if (player != null && playerAttackModes.TryGetValue(player, out var mode))
            {
                return mode;
            }
            return AttackMode.Normal;
        }

        public static bool IsPlayerInExtraAttack(Player player)
        {
            var mode = GetAttackMode(player);
            return mode != AttackMode.Normal;
        }

        // Cooldown management - per button
        public static bool IsPlayerOnCooldown(Player player, AttackMode mode)
        {
            if (player == null || mode == AttackMode.Normal)
                return false;

            if (playerCooldowns.TryGetValue(player, out var cooldowns))
            {
                if (cooldowns.TryGetValue(mode, out float cooldownTime))
                {
                    return cooldownTime > Time.time;
                }
            }
            return false;
        }

        public static float GetPlayerCooldownRemaining(Player player, AttackMode mode)
        {
            if (player == null || mode == AttackMode.Normal)
                return 0f;

            if (playerCooldowns.TryGetValue(player, out var cooldowns))
            {
                if (cooldowns.TryGetValue(mode, out float cooldownTime))
                {
                    return Mathf.Max(0f, cooldownTime - Time.time);
                }
            }
            return 0f;
        }

        public static void SetPlayerCooldown(Player player, AttackMode mode)
        {
            if (player == null || mode == AttackMode.Normal)
                return;

            if (!playerCooldowns.ContainsKey(player))
            {
                playerCooldowns[player] = new Dictionary<AttackMode, float>();
            }

            // Try to get cooldown from CostConfig based on current weapon and mode
            float cooldownDuration = 0f;
            try
            {
                if (player.GetCurrentWeapon() != null)
                {
                    string weaponType = GetWeaponTypeFromSkill(player.GetCurrentWeapon().m_shared.m_skillType, player.GetCurrentWeapon());
                    string modeString = mode.ToString();
                    
               float attackCooldown = ExtraAttackCostConfig.GetAttackCooldown(weaponType, modeString);
               if (attackCooldown > 0f)
               {
                   cooldownDuration = attackCooldown;
               }
                }
                
                // Fallback to config if CostConfig not available
                if (cooldownDuration <= 0f)
                {
                    cooldownDuration = 0f; // No cooldown
                }
            }
            catch (System.Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error getting CostConfig cooldown, falling back to config: {ex.Message}");
                cooldownDuration = 0f; // No cooldown
            }
            
            playerCooldowns[player][mode] = Time.time + cooldownDuration;
        }

        public static void CleanupPlayer(Player player)
        {
            if (player != null)
            {
                playerAttackModes.Remove(player);
                playerCooldowns.Remove(player);
            }
        }




        // Localization helper methods
        public static string GetLocalizedString(string key, params object[] args)
        {
            // Try Jotunn localization first; fallback to built-in messages if not found
            string translated = string.Empty;
            try
            {
                translated = LocalizationManager.Instance.TryTranslate(key);
            }
            catch
            {
                translated = string.Empty;
            }

            if (!string.IsNullOrEmpty(translated) && translated != key)
            {
                return args.Length > 0 ? string.Format(translated, args) : translated;
            }

            if (MessageDictionary.TryGetValue(key, out string message))
            {
                return args.Length > 0 ? string.Format(message, args) : message;
            }
            return key; // Fallback to key if message not found
        }


        public static void ShowMessage(Player player, string messageKey, params object[] args)
        {
            if (player != null)
            {
                string message = GetLocalizedString(messageKey, args);
                player.Message(MessageHud.MessageType.Center, message);
            }
        }

        public static float GetEffectiveStaminaCost(Player player, ItemDrop.ItemData weapon, AttackMode mode)
        {
            // Simplified: Just return base cost from YAML
            // The actual stamina calculation with all modifiers is now handled by Attack_GetAttackStamina_Prefix
            if (player == null || weapon == null)
            {
                return 0f;
            }

            float baseCost = 0f;
            
            // Get base stamina cost from CostConfig
            try
            {
                string weaponType = GetWeaponTypeFromSkill(weapon.m_shared.m_skillType, weapon);
                string modeString = mode.ToString();
                
                var attackCost = ExtraAttackCostConfig.GetAttackCost(weaponType, modeString);
                if (attackCost != null && attackCost.StaminaCost > 0f)
                {
                    baseCost = attackCost.StaminaCost;
                    if (ExtraAttackPlugin.IsDebugAOCOperationsEnabled)
                    {
                        ExtraAttackPlugin.LogInfo("System", $"Using CostConfig base stamina cost: {weaponType}_{modeString} = {baseCost}");
                    }
                }
                
               // Use default if CostConfig not available
                if (baseCost <= 0f)
                {
                   baseCost = 20f; // Default base stamina cost (バニラのセカンダリ攻撃と同じ値)
                }
            }
            catch (System.Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error getting CostConfig stamina cost, using default: {ex.Message}");
                baseCost = 20f; // Default base stamina cost (バニラのセカンダリ攻撃と同じ値)
            }
            
            return baseCost;
        }

        public static float GetEffectiveStaminaCost(Attack attack, Player player, ItemDrop.ItemData weapon, AttackMode mode)
        {
            // Simplified: Just return base cost from YAML
            // The actual stamina calculation with all modifiers is now handled by Attack_GetAttackStamina_Prefix
            return GetEffectiveStaminaCost(player, weapon, mode);
        }


        /// <summary>
        /// 武器タイプ判定の統一ロジック
        /// </summary>
        public static string GetWeaponTypeFromSkill(Skills.SkillType skillType, ItemDrop.ItemData? weapon = null)
        {
            // First determine base weapon type from skill
            string baseType = skillType switch
            {
                Skills.SkillType.Swords => "Swords",
                Skills.SkillType.Axes => "Axes", // Both regular axes and battle axes use Axes skill
                Skills.SkillType.Clubs => "Clubs",
                Skills.SkillType.Spears => "Spears",
                Skills.SkillType.Polearms => "Polearms",
                Skills.SkillType.Knives => "Knives",
                Skills.SkillType.Unarmed => "Fists",
                _ => "Swords" // Default fallback
            };

            // Refine weapon type based on two-handed status for GreatSwords and BattleAxes
            if (weapon != null && weapon.m_shared != null)
            {
                bool isTwoHanded = weapon.m_shared.m_itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon;
                
                // Check for GreatSwords (two-handed swords)
                if (baseType == "Swords" && isTwoHanded)
                {
                    return "GreatSwords";
                }
                
                // Check for BattleAxes (two-handed axes)
                if (baseType == "Axes" && isTwoHanded)
                {
                    return "BattleAxes";
                }
            }

            return baseType;
        }

        // Legacy method for backward compatibility
        public static string GetWeaponTypeFromSkill(Skills.SkillType skillType)
        {
            return GetWeaponTypeFromSkill(skillType, null);
        }

        // Get effective eitr cost for attack
        public static float GetEffectiveEitrCost(Attack attack, Player player, ItemDrop.ItemData weapon, AttackMode mode)
        {
            // Simplified: Just return base cost from YAML
            // The actual eitr calculation with skill modifiers is now handled by Attack_GetAttackEitr_Prefix
            if (attack == null || player == null || weapon == null)
            {
                return 0f;
            }

            float baseCost = 0f;
            
            // Get base eitr cost from CostConfig
            try
            {
                string weaponType = GetWeaponTypeFromSkill(weapon.m_shared.m_skillType, weapon);
                string modeString = mode.ToString();
                
                var attackCost = ExtraAttackCostConfig.GetAttackCost(weaponType, modeString);
                if (attackCost != null && attackCost.EitrCost > 0f)
                {
                    baseCost = attackCost.EitrCost;
                    if (ExtraAttackPlugin.IsDebugAOCOperationsEnabled)
                    {
                        ExtraAttackPlugin.LogInfo("System", $"Using CostConfig base eitr cost: {weaponType}_{modeString} = {baseCost}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error getting CostConfig eitr cost: {ex.Message}");
                baseCost = 0f; // Default eitr cost is 0
            }

            return baseCost;
        }

        

        // Attack bypass management
        private static Dictionary<Player, bool> bypassNextStartAttack = new Dictionary<Player, bool>();
        
        public static void MarkBypassNextStartAttack(Player player)
        {
            if (player != null)
            {
                bypassNextStartAttack[player] = true;
            }
        }
        
        public static bool ConsumeBypassNextStartAttack(Player player)
        {
            if (player != null && bypassNextStartAttack.TryGetValue(player, out bool shouldBypass))
            {
                bypassNextStartAttack.Remove(player);
                return shouldBypass;
            }
            return false;
        }

        // Block management during chain window
        private static Dictionary<Player, bool> blockPrimaryDuringChainWindow = new Dictionary<Player, bool>();
        
        public static void MarkBlockPrimaryDuringChainWindow(Player player)
        {
            if (player != null)
            {
                blockPrimaryDuringChainWindow[player] = true;
            }
        }
        
        public static bool HasBlockPrimaryDuringChainWindow(Player player)
        {
            return player != null && blockPrimaryDuringChainWindow.TryGetValue(player, out bool shouldBlock) && shouldBlock;
        }
        
        public static void ClearBlockPrimaryDuringChainWindow(Player player)
        {
            if (player != null)
            {
                blockPrimaryDuringChainWindow.Remove(player);
            }
        }
        
        public static bool ConsumeBlockNextPrimary(Player player)
        {
            if (player != null && blockPrimaryDuringChainWindow.TryGetValue(player, out bool shouldBlock))
            {
                blockPrimaryDuringChainWindow.Remove(player);
                return shouldBlock;
            }
            return false;
        }

        public static bool ConsumeBlockNextSecondary(Player player)
        {
            // Secondary attacks are not blocked by default
            return false;
        }

        /// <summary>
        /// アニメーションクリップ長取得の統一ロジック
        /// </summary>
        public static float GetClipLength(string clipName, bool isExternal = true)
        {
            try
            {
                if (isExternal)
                {
                    // External animation clip
                    if (AnimationManager.ExternalAnimations.TryGetValue(clipName, out AnimationClip clip) && clip != null)
                    {
                        return clip.length;
                    }
                }
                else
                {
                    // Vanilla animation clip - use default values
                    if (AnimationManager.DefaultClipLengths.TryGetValue(clipName, out float defaultLength))
                    {
                        return defaultLength;
                    }
                }
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error getting clip length for {clipName}: {ex.Message}");
            }
            
            return -1f; // Indicate no valid clip found
        }

        /// <summary>
        /// YAMLファイルの読み込み処理の統一
        /// </summary>
        public static T LoadYamlConfig<T>(string filePath, T defaultConfig) where T : new()
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    ExtraAttackPlugin.LogInfo("Config", $"Config file not found: {filePath}");
                    return defaultConfig;
                }

                string yamlContent = File.ReadAllText(filePath, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(yamlContent))
                {
                    ExtraAttackPlugin.LogWarning("Config", $"Config file is empty: {filePath}");
                    return defaultConfig;
                }

                // Clean up YAML content
                yamlContent = CleanupYamlContent(yamlContent);

                var deserializer = new DeserializerBuilder()
                    .IgnoreUnmatchedProperties()
                    .Build();

                var config = deserializer.Deserialize<T>(yamlContent);
                return config ?? defaultConfig;
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error loading YAML config from {filePath}: {ex.Message}");
                return defaultConfig;
            }
        }

        /// <summary>
        /// YAMLファイルの保存処理の統一
        /// </summary>
        public static void SaveYamlConfig<T>(T config, string filePath, string header = "")
        {
            try
            {
                var serializer = new SerializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();

                var yaml = serializer.Serialize(config);
                
                var sb = new StringBuilder();
                if (!string.IsNullOrEmpty(header))
                {
                    sb.AppendLine(header);
                    sb.AppendLine();
                }
                sb.Append(yaml);

                File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
                ExtraAttackPlugin.LogInfo("Config", $"Saved YAML config to: {filePath}");
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error saving YAML config to {filePath}: {ex.Message}");
            }
        }

        /// <summary>
        /// YAMLコンテンツのクリーンアップ処理
        /// </summary>
        public static string CleanupYamlContent(string yamlContent)
        {
            if (string.IsNullOrEmpty(yamlContent))
                return yamlContent;

            var lines = yamlContent.Split('\n');
            var cleanedLines = new List<string>();
            
            foreach (var line in lines)
            {
                // Skip lines that start with random characters (like "8e36e9a1")
                if (line.Trim().Length > 0 && !line.Trim().StartsWith("#") && 
                    !line.Trim().StartsWith("Default:") && !line.Trim().StartsWith("WeaponTypes:"))
                {
                    // Check if line looks like a random string (contains only alphanumeric characters and is short)
                    if (line.Trim().Length < 10 && 
                        System.Text.RegularExpressions.Regex.IsMatch(line.Trim(), "^[a-zA-Z0-9]+$"))
                    {
                        ExtraAttackPlugin.LogInfo("Config", $"Skipping malformed line: {line.Trim()}");
                        continue;
                    }
                }
                cleanedLines.Add(line);
            }
            
            return string.Join("\n", cleanedLines);
        }

        /// <summary>
        /// 設定ファイルの存在確認と生成判定の統一ロジック
        /// </summary>
        public static bool ShouldCreateOrRegenerateConfig(string filePath, string requiredContent = "")
        {
            if (!File.Exists(filePath))
            {
                ExtraAttackPlugin.LogInfo("Config", $"Config file not found: {filePath}");
                return true;
            }

            try
            {
                string content = File.ReadAllText(filePath, Encoding.UTF8).Trim();
                if (string.IsNullOrEmpty(content))
                {
                    ExtraAttackPlugin.LogInfo("Config", $"Config file is empty: {filePath}");
                    return true;
                }

                // Check if file has required content
                if (!string.IsNullOrEmpty(requiredContent) && !content.Contains(requiredContent))
                {
                    ExtraAttackPlugin.LogInfo("Config", $"Config file missing required content '{requiredContent}': {filePath}");
                    return true;
                }

                ExtraAttackPlugin.LogInfo("Config", $"Config file exists and has content: {filePath}");
                return false;
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error checking config file {filePath}: {ex.Message}");
                return true; // Regenerate on error
            }
        }

        /// <summary>
        /// デバッグログの統一出力
        /// </summary>
        public static void LogDebugInfo(string category, string context, Dictionary<string, object> data)
        {
            if (!ExtraAttackPlugin.IsDebugSystemMessagesEnabled)
                return;

            var sb = new StringBuilder();
            sb.AppendLine($"[{category}] {context}");
            
            foreach (var kvp in data)
            {
                sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
            }
            
            ExtraAttackPlugin.LogInfo(category, sb.ToString());
        }

        /// <summary>
        /// 設定ディレクトリのパス取得
        /// </summary>
        public static string GetConfigFolderPath()
        {
            return Path.Combine(BepInEx.Paths.ConfigPath, "ExtraAttackSystem");
        }

        /// <summary>
        /// 設定ディレクトリの初期化
        /// </summary>
        public static void EnsureConfigDirectoryExists()
        {
            string configPath = GetConfigFolderPath();
            if (!Directory.Exists(configPath))
            {
                Directory.CreateDirectory(configPath);
                ExtraAttackPlugin.LogInfo("Config", $"Created config directory: {configPath}");
            }
        }

    }
}
