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

        // Configuration
        public static ConfigEntry<KeyboardShortcut> ExtraAttackKey = null!;
        public static ConfigEntry<float> ExtraAttackCooldown = null!;

        // Test buttons for animation testing
        public static ConfigEntry<KeyboardShortcut> TestButton1 = null!;
        public static ConfigEntry<KeyboardShortcut> TestButton2 = null!;

        // Debug configurations
        public static ConfigEntry<bool> DebugAnimationEvents = null!;
        public static ConfigEntry<bool> DebugAnimationParameters = null!;
        public static ConfigEntry<bool> DebugAnimationClips = null!;

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

            // Apply patches
            ApplyPatches();

            ExtraAttackLogger.LogInfo($"{PluginName} has loaded!");
        }

        private void SetupConfiguration()
        {
            ExtraAttackKey = Config.Bind("1 - General", "Extra Attack Key", new KeyboardShortcut(UnityEngine.KeyCode.Q),
                "Key to trigger extra attack");

            ExtraAttackCooldown = Config.Bind("1 - General", "Extra Attack Cooldown", 2f,
                "Cooldown between extra attacks in seconds");

            // Test buttons
            TestButton1 = Config.Bind("3 - Test", "Test Animation Button 1",
                new KeyboardShortcut(UnityEngine.KeyCode.G),
                "Key to test custom animation 1 (no gameplay effects)");

            TestButton2 = Config.Bind("3 - Test", "Test Animation Button 2",
                new KeyboardShortcut(UnityEngine.KeyCode.T),
                "Key to test custom animation 2 (no gameplay effects)");

            // Debug options
            DebugAnimationEvents = Config.Bind("4 - Debug", "Debug Animation Events", false,
                "Log detailed AnimationEvent information for secondary attack clips");

            DebugAnimationParameters = Config.Bind("4 - Debug", "Debug Animation Parameters", false,
                "Log all Animator parameters");

            DebugAnimationClips = Config.Bind("4 - Debug", "Debug Animation Clips", false,
                "Log detailed AnimationClip information");

            // Subscribe to setting changes for immediate output
            DebugAnimationEvents.SettingChanged += OnDebugSettingChanged;
            DebugAnimationParameters.SettingChanged += OnDebugSettingChanged;
            DebugAnimationClips.SettingChanged += OnDebugSettingChanged;

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

            ExtraAttackLogger.LogInfo("Configuration initialized");
        }

        private static void OnDebugSettingChanged(object sender, EventArgs e)
        {
            // Get local player and animator
            if (Player.m_localPlayer == null)
            {
                ExtraAttackLogger.LogWarning("Cannot output debug info: No local player found");
                return;
            }

            var animator = AnimationManager.GetPlayerAnimator(Player.m_localPlayer);
            if (animator == null)
            {
                ExtraAttackLogger.LogWarning("Cannot output debug info: Animator not found");
                return;
            }

            // Output debug info based on current settings
            ExtraAttackLogger.LogInfo("Debug setting changed - outputting debug info...");
            AnimationManager.OutputDebugInfoOnDemand(animator);
        }

        private void ApplyPatches()
        {
            try
            {
                harmony.PatchAll();
                ExtraAttackLogger.LogInfo("Harmony patches applied successfully");
            }
            catch (Exception ex)
            {
                ExtraAttackLogger.LogError($"Failed to apply Harmony patches: {ex.Message}");
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
    }
}