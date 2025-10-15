#nullable enable
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn;
using Jotunn.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx.Bootstrap;
using UnityEngine;

namespace ExtraAttackSystem
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibilityAttribute(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Patch)]
    public class ExtraAttackSystemPlugin : BaseUnityPlugin
    {
        internal const string PluginGUID = "Dyju420.ExtraAttackSystem";
        internal const string PluginName = "Extra Attack System";
        internal const string PluginVersion = "0.8.7";

        internal static readonly ManualLogSource ExtraAttackLogger = BepInEx.Logging.Logger.CreateLogSource(PluginName);

        // Harmony instance
        private readonly Harmony harmony = new Harmony(PluginGUID);

        // General config - keys & cooldowns
        // Q/T/G keys for extra attacks
        public static ConfigEntry<KeyboardShortcut> ExtraAttackKey_Q = null!;
        public static ConfigEntry<KeyboardShortcut> ExtraAttackKey_T = null!;
        public static ConfigEntry<KeyboardShortcut> ExtraAttackKey_G = null!;

        // Toggle for T and G keys
        public static ConfigEntry<bool> EnableTKey = null!;
        public static ConfigEntry<bool> EnableGKey = null!;

        // Debug configurations
        public static ConfigEntry<bool> DebugSystemMessages = null!;
        public static ConfigEntry<bool> DebugAttackTriggers = null!;
        public static ConfigEntry<bool> DebugAOCOperations = null!;
        public static ConfigEntry<bool> DebugClipNames = null!;

        // Debug key bindings
        public static ConfigEntry<KeyboardShortcut> DebugWeaponParamsKey = null!;
        public static ConfigEntry<KeyboardShortcut> DebugAllWeaponsKey = null!;

        // Debug toggles for different data types
        public static ConfigEntry<bool> DebugWeaponAttackParams = null!;
        public static ConfigEntry<bool> DebugWeaponTimingData = null!;
        public static ConfigEntry<bool> DebugWeaponAnimationData = null!;
        public static ConfigEntry<bool> DebugWeaponCostData = null!;

        // Cached debug flags for performance
        private static bool cachedDebugSystemMessages;
        private static bool cachedDebugAttackTriggers;
        private static bool cachedDebugAOCOperations;
        private static bool cachedDebugClipNames;

        // Public properties for checking debug states
        public static bool IsDebugSystemMessagesEnabled => cachedDebugSystemMessages;
        public static bool IsDebugAttackTriggersEnabled => cachedDebugAttackTriggers;
        public static bool IsDebugAOCOperationsEnabled => cachedDebugAOCOperations;
        public static bool IsDebugClipNamesEnabled => cachedDebugClipNames;

        private void Awake()
        {
            try
            {
                // Initialize debug system
                EAS_Debug.Initialize();

                // Load configuration
                LoadConfiguration();

                // Initialize subsystems
                InitializeSubsystems();

                // Apply Harmony patches
                harmony.PatchAll();

                ExtraAttackLogger.LogInfo($"[{PluginName}] Plugin loaded successfully");
            }
            catch (Exception ex)
            {
                ExtraAttackLogger.LogError($"Error initializing {PluginName}: {ex.Message}");
                throw;
            }
        }

        private void LoadConfiguration()
        {
            // General settings
            ExtraAttackKey_Q = Config.Bind("General", "Extra Attack Q Key", new KeyboardShortcut(KeyCode.Q), "Key for Extra Attack Q");
            ExtraAttackKey_T = Config.Bind("General", "Extra Attack T Key", new KeyboardShortcut(KeyCode.T), "Key for Extra Attack T");
            ExtraAttackKey_G = Config.Bind("General", "Extra Attack G Key", new KeyboardShortcut(KeyCode.G), "Key for Extra Attack G");

            EnableTKey = Config.Bind("General", "Enable T Key", true, "Enable T key attacks");
            EnableGKey = Config.Bind("General", "Enable G Key", true, "Enable G key attacks");

            // Debug settings
            DebugSystemMessages = Config.Bind("Debug", "System Messages", false, "Show system debug messages");
            DebugAttackTriggers = Config.Bind("Debug", "Attack Triggers", false, "Show attack trigger debug messages");
            DebugAOCOperations = Config.Bind("Debug", "AOC Operations", false, "Show AOC operation debug messages");
            DebugClipNames = Config.Bind("Debug", "Clip Names", false, "Show animation clip name debug messages");

            // Debug key bindings
            DebugWeaponParamsKey = Config.Bind("Debug", "Debug Weapon Params Key", new KeyboardShortcut(KeyCode.F9), "Key to log current weapon attack parameters");
            DebugAllWeaponsKey = Config.Bind("Debug", "Debug All Weapons Key", new KeyboardShortcut(KeyCode.F10), "Key to log all weapon types attack parameters");

            // Debug toggles for different data types
            DebugWeaponAttackParams = Config.Bind("Debug", "Log Weapon Attack Parameters", true, "Log weapon attack parameters (range, height, angle, etc.)");
            DebugWeaponTimingData = Config.Bind("Debug", "Log Weapon Timing Data", true, "Log weapon timing data (hit timing, trail timing, etc.)");
            DebugWeaponAnimationData = Config.Bind("Debug", "Log Weapon Animation Data", true, "Log weapon animation data (clip names, events, etc.)");
            DebugWeaponCostData = Config.Bind("Debug", "Log Weapon Cost Data", true, "Log weapon cost data (stamina, eitr, health costs)");

            // Cache debug flags
            cachedDebugSystemMessages = DebugSystemMessages.Value;
            cachedDebugAttackTriggers = DebugAttackTriggers.Value;
            cachedDebugAOCOperations = DebugAOCOperations.Value;
            cachedDebugClipNames = DebugClipNames.Value;
        }

        private void InitializeSubsystems()
        {
            // Initialize animation manager and get asset bundle path
            string assetBundlePath = EAS_AnimationManager.Initialize();

            // Initialize animation replacement from YAML
            EAS_Replacement.Initialize();

            // Initialize animation cache with the same asset bundle path
            EAS_AnimationCache.Initialize(assetBundlePath);

            // Pre-cache all AOCs for all weapon types
            EAS_AnimationManager.PreCacheAllAOCs();

            // Initialize cost config
            EAS_CostConfig.Initialize();

            // Initialize exclusion config
            EAS_ExclusionConfig.Initialize();

            // Initialize animation timing
            EAS_AnimationTiming.Initialize();

            ExtraAttackLogger.LogInfo("All subsystems initialized successfully");
        }

        // Logging helper methods
        public static void LogInfo(string category, string message)
        {
            ExtraAttackLogger.LogInfo($"[{category}] {message}");
        }

        public static void LogWarning(string category, string message)
        {
            ExtraAttackLogger.LogWarning($"[{category}] {message}");
        }

        public static void LogError(string category, string message)
        {
            ExtraAttackLogger.LogError($"[{category}] {message}");
        }

        private void Update()
        {
            // Check debug key bindings
            if (DebugWeaponParamsKey.Value.IsDown())
            {
                EAS_Debug.LogWeaponAttackParameters();
            }
            
            if (DebugAllWeaponsKey.Value.IsDown())
            {
                EAS_Debug.LogAllWeaponTypes();
            }
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
        }
    }
}
