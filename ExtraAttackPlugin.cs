#nullable enable
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn;
using Jotunn.Utils;
using System;
using System.Collections.Generic;

namespace ExtraAttackSystem
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibilityAttribute(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class ExtraAttackPlugin : BaseUnityPlugin
    {
        internal const string PluginGUID = "Dyju420.ExtraAttackSystem";
        internal const string PluginName = "Extra Attack System";
        internal const string PluginVersion = "1.0.0";

        internal static readonly ManualLogSource ExtraAttackLogger = BepInEx.Logging.Logger.CreateLogSource(PluginName);

        // Configuration - Shortcut Keys
        public static ConfigEntry<KeyboardShortcut> ExtraAttackKey = null!;
        public static ConfigEntry<KeyboardShortcut> TestButton1 = null!;
        public static ConfigEntry<KeyboardShortcut> TestButton2 = null!;

        // Configuration - Cooldowns
        public static ConfigEntry<float> ExtraAttackQCooldown = null!;
        public static ConfigEntry<float> ExtraAttackTCooldown = null!;
        public static ConfigEntry<float> ExtraAttackGCooldown = null!;

        // Debug configurations - Master switch
        public static ConfigEntry<bool> DebugMasterSwitch = null!;

        // Debug configurations - Individual categories
        public static ConfigEntry<bool> DebugClipNames = null!;
        public static ConfigEntry<bool> DebugAnimationEvents = null!;
        public static ConfigEntry<bool> DebugAnimationClips = null!;
        public static ConfigEntry<bool> DebugAnimationParameters = null!;
        public static ConfigEntry<bool> DebugAttackTriggers = null!;
        public static ConfigEntry<bool> DebugAOCOperations = null!;
        public static ConfigEntry<bool> DebugDamageCalculation = null!;
        public static ConfigEntry<bool> DebugSystemMessages = null!;

        // Cached debug settings for performance
        private static bool cachedDebugMaster = false;
        private static bool cachedDebugClipNames = false;
        private static bool cachedDebugAnimationEvents = false;
        private static bool cachedDebugAnimationClips = false;
        private static bool cachedDebugAnimationParameters = false;
        private static bool cachedDebugAttackTriggers = false;
        private static bool cachedDebugAOCOperations = false;
        private static bool cachedDebugDamageCalculation = false;
        private static bool cachedDebugSystemMessages = false;

        // Weapon balancing configurations
        public static readonly Dictionary<Skills.SkillType, WeaponBalancingConfig> BalancingMap = new();

        public struct WeaponBalancingConfig
        {
            public ConfigEntry<float> damageMultiplier;
            public ConfigEntry<float> staminaCost;
            public ConfigEntry<float> animationSpeed;
        }

        // Default balancing values
        private static readonly Dictionary<Skills.SkillType, WeaponBalancing> defaultBalancing = new()
        {
            { Skills.SkillType.Swords, new WeaponBalancing { damageMultiplier = 1.5f, staminaCost = 15f, animationSpeed = 1.3f } },
            { Skills.SkillType.Axes, new WeaponBalancing { damageMultiplier = 1.8f, staminaCost = 20f, animationSpeed = 1.1f } },
            { Skills.SkillType.Clubs, new WeaponBalancing { damageMultiplier = 1.6f, staminaCost = 18f, animationSpeed = 1.2f } },
            { Skills.SkillType.Knives, new WeaponBalancing { damageMultiplier = 1.3f, staminaCost = 12f, animationSpeed = 1.5f } },
            { Skills.SkillType.Spears, new WeaponBalancing { damageMultiplier = 1.4f, staminaCost = 16f, animationSpeed = 1.2f } },
            { Skills.SkillType.Bows, new WeaponBalancing { damageMultiplier = 1.2f, staminaCost = 10f, animationSpeed = 1.1f } },
        };

        private struct WeaponBalancing
        {
            public float damageMultiplier;
            public float staminaCost;
            public float animationSpeed;
        }

        private readonly Harmony harmony = new(PluginGUID);

        public void Awake()
        {
            ExtraAttackLogger.LogInfo($"{PluginName} v{PluginVersion} initializing...");

            // Configuration setup
            SetupConfiguration();

            // Load assets
            AnimationManager.LoadAssets();

            // Initialize animation timing config (load or create YAML)
            AnimationTimingConfig.Initialize();

            // Apply patches
            ApplyPatches();
            ExtraAttackLogger.LogInfo($"{PluginName} has loaded!");
        }

        private void SetupConfiguration()
        {
            // ========================================================================
            // 1 - SHORTCUT KEYS
            // ========================================================================

            ExtraAttackKey = Config.Bind("1 - Shortcut Keys", "Q Key - Extra Attack",
                new KeyboardShortcut(UnityEngine.KeyCode.Q),
                "Key to trigger extra attack (Secondary attack animation)");

            TestButton2 = Config.Bind("1 - Shortcut Keys", "T Key - Custom Attack 1",
                new KeyboardShortcut(UnityEngine.KeyCode.T),
                "Key to trigger custom attack animation 1 (Primary attack animation)");

            TestButton1 = Config.Bind("1 - Shortcut Keys", "G Key - Custom Attack 2",
                new KeyboardShortcut(UnityEngine.KeyCode.G),
                "Key to trigger custom attack animation 2 (Primary attack animation)");

            // ========================================================================
            // 2 - BALANCING
            // ========================================================================

            // Initialize weapon type balancing configurations
            foreach (var kvp in defaultBalancing)
            {
                var skillType = kvp.Key;
                var defaults = kvp.Value;

                BalancingMap[skillType] = new WeaponBalancingConfig
                {
                    damageMultiplier = Config.Bind("2 - Balancing", $"{skillType} Damage Multiplier", defaults.damageMultiplier,
                        $"Damage multiplier for {skillType} extra attacks"),
                    staminaCost = Config.Bind("2 - Balancing", $"{skillType} Stamina Cost", defaults.staminaCost,
                        $"Stamina cost for {skillType} extra attacks"),
                    animationSpeed = Config.Bind("2 - Balancing", $"{skillType} Animation Speed", defaults.animationSpeed,
                        $"Animation speed multiplier for {skillType} extra attacks")
                };
            }

            // ========================================================================
            // 3 - COOLDOWNS
            // ========================================================================

            ExtraAttackQCooldown = Config.Bind("3 - Cooldowns", "Q Key Cooldown", 3f,
                "Cooldown for Q key extra attack (seconds)");

            ExtraAttackTCooldown = Config.Bind("3 - Cooldowns", "T Key Cooldown", 2f,
                "Cooldown for T key extra attack (seconds)");

            ExtraAttackGCooldown = Config.Bind("3 - Cooldowns", "G Key Cooldown", 2f,
                "Cooldown for G key extra attack (seconds)");

            // ========================================================================
            // 4 - DEBUG
            // ========================================================================

            // Debug master switch
            DebugMasterSwitch = Config.Bind("4 - Debug", "0. Debug Master Switch", false,
                "Enable/Disable ALL debug logging at once");
            cachedDebugMaster = DebugMasterSwitch.Value;
            DebugMasterSwitch.SettingChanged += (s, e) => cachedDebugMaster = DebugMasterSwitch.Value;

            // Debug options - Individual categories
            DebugClipNames = Config.Bind("4 - Debug", "1. Debug Clip Names", false,
                "Log the actual clip names being played when pressing Q/T/G keys");
            cachedDebugClipNames = DebugClipNames.Value;
            DebugClipNames.SettingChanged += (s, e) => cachedDebugClipNames = DebugClipNames.Value;

            DebugAnimationEvents = Config.Bind("4 - Debug", "2. Debug Animation Events", false,
                "Log detailed AnimationEvent information when creating AOC");
            cachedDebugAnimationEvents = DebugAnimationEvents.Value;
            DebugAnimationEvents.SettingChanged += (s, e) => cachedDebugAnimationEvents = DebugAnimationEvents.Value;

            DebugAnimationClips = Config.Bind("4 - Debug", "3. Debug Animation Clips", false,
                "Log detailed AnimationClip information when creating AOC");
            cachedDebugAnimationClips = DebugAnimationClips.Value;
            DebugAnimationClips.SettingChanged += (s, e) => cachedDebugAnimationClips = DebugAnimationClips.Value;

            DebugAnimationParameters = Config.Bind("4 - Debug", "4. Debug Animation Parameters", false,
                "Log all Animator parameters during initialization");
            cachedDebugAnimationParameters = DebugAnimationParameters.Value;
            DebugAnimationParameters.SettingChanged += (s, e) => cachedDebugAnimationParameters = DebugAnimationParameters.Value;

            DebugAttackTriggers = Config.Bind("4 - Debug", "5. Debug Attack Triggers", false,
                "Log attack trigger detection (primary/secondary)");
            cachedDebugAttackTriggers = DebugAttackTriggers.Value;
            DebugAttackTriggers.SettingChanged += (s, e) => cachedDebugAttackTriggers = DebugAttackTriggers.Value;

            DebugAOCOperations = Config.Bind("4 - Debug", "6. Debug AOC Operations", false,
                "Log AOC creation, switching, and animation override operations");
            cachedDebugAOCOperations = DebugAOCOperations.Value;
            DebugAOCOperations.SettingChanged += (s, e) => cachedDebugAOCOperations = DebugAOCOperations.Value;

            DebugDamageCalculation = Config.Bind("4 - Debug", "7. Debug Damage Calculation", false,
                "Log damage multiplier application and restoration");
            cachedDebugDamageCalculation = DebugDamageCalculation.Value;
            DebugDamageCalculation.SettingChanged += (s, e) => cachedDebugDamageCalculation = DebugDamageCalculation.Value;

            DebugSystemMessages = Config.Bind("4 - Debug", "8. Debug System Messages", false,
                "Log initialization, caching, cleanup, and other system operations");
            cachedDebugSystemMessages = DebugSystemMessages.Value;
            DebugSystemMessages.SettingChanged += (s, e) => cachedDebugSystemMessages = DebugSystemMessages.Value;

            ExtraAttackLogger.LogInfo("Configuration initialized");
        }

        private void ApplyPatches()
        {
            try
            {
                harmony.PatchAll();
                LogInfo("System", "Harmony patches applied successfully");
            }
            catch (Exception ex)
            {
                LogError("System", $"Failed to apply Harmony patches: {ex.Message}");
            }
        }

        public void OnDestroy()
        {
            harmony?.UnpatchSelf();
            AnimationManager.UnloadAssets();
        }

        // Public utility methods
        public static bool IsExtraAttackKeyPressed() => ExtraAttackKey.Value.IsDown();
        public static bool IsTestButton1Pressed() => TestButton1.Value.IsDown();
        public static bool IsTestButton2Pressed() => TestButton2.Value.IsDown();

        public static float GetCooldown(ExtraAttackUtils.AttackMode mode)
        {
            return mode switch
            {
                ExtraAttackUtils.AttackMode.ExtraQ => ExtraAttackQCooldown.Value,
                ExtraAttackUtils.AttackMode.ExtraT => ExtraAttackTCooldown.Value,
                ExtraAttackUtils.AttackMode.ExtraG => ExtraAttackGCooldown.Value,
                _ => 2f
            };
        }

        public static float GetDamageMultiplier(Skills.SkillType skillType)
        {
            return BalancingMap.TryGetValue(skillType, out var config) ? config.damageMultiplier.Value : 1.5f;
        }

        public static float GetStaminaCost(Skills.SkillType skillType)
        {
            return BalancingMap.TryGetValue(skillType, out var config) ? config.staminaCost.Value : 15f;
        }

        public static float GetAnimationSpeed(Skills.SkillType skillType)
        {
            return BalancingMap.TryGetValue(skillType, out var config) ? config.animationSpeed.Value : 1.3f;
        }

        // Debug logging helper methods - Using cached values for performance
        public static void LogInfo(string category, string message)
        {
            if (!cachedDebugMaster) return;

            bool shouldLog = category switch
            {
                "ClipNames" => cachedDebugClipNames,
                "AnimationEvents" => cachedDebugAnimationEvents,
                "AnimationClips" => cachedDebugAnimationClips,
                "AnimationParameters" => cachedDebugAnimationParameters,
                "AttackTriggers" => cachedDebugAttackTriggers,
                "AOC" => cachedDebugAOCOperations,
                "Damage" => cachedDebugDamageCalculation,
                "System" => cachedDebugSystemMessages,
                _ => false
            };

            if (shouldLog)
            {
                ExtraAttackLogger.LogInfo(message);
            }
        }

        public static void LogWarning(string category, string message)
        {
            if (!cachedDebugMaster) return;

            bool shouldLog = category switch
            {
                "ClipNames" => cachedDebugClipNames,
                "AnimationEvents" => cachedDebugAnimationEvents,
                "AnimationClips" => cachedDebugAnimationClips,
                "AnimationParameters" => cachedDebugAnimationParameters,
                "AttackTriggers" => cachedDebugAttackTriggers,
                "AOC" => cachedDebugAOCOperations,
                "Damage" => cachedDebugDamageCalculation,
                "System" => cachedDebugSystemMessages,
                _ => true // Warnings always show if master is on
            };

            if (shouldLog)
            {
                ExtraAttackLogger.LogWarning(message);
            }
        }

        public static void LogError(string category, string message)
        {
            // Errors always log regardless of debug settings
            ExtraAttackLogger.LogError(message);
        }
    }
}