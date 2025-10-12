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
using BepInEx.Bootstrap;
using UnityEngine;

namespace ExtraAttackSystem
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibilityAttribute(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Patch)]
    public class ExtraAttackPlugin : BaseUnityPlugin
    {
        internal const string PluginGUID = "Dyju420.ExtraAttackSystem";
        internal const string PluginName = "Extra Attack System";
        internal const string PluginVersion = VersionInfo.FullVersion;

        internal static readonly ManualLogSource ExtraAttackLogger = BepInEx.Logging.Logger.CreateLogSource(PluginName);
        // NEW: Dual-wield mod detection flag
        private static bool dualWieldModDetected = false;
        private static string detectedDualWieldModName = string.Empty;

        // Harmony instance
        private readonly Harmony harmony = new Harmony(PluginGUID);

        // Cached debug toggles for performance
        private static bool cachedDebugClipNames;
        private static bool cachedDebugAnimationEvents;
        private static bool cachedDebugAnimationClips;
        private static bool cachedDebugAnimationParameters;
        private static bool cachedDebugAttackTriggers;
        private static bool cachedDebugDamageCalculation;
        private static bool cachedDebugSystemMessages;
        private static bool cachedDebugPerformanceMetrics;
        private static bool cachedDebugAOCOperations;

        // General config - keys & cooldowns
        // Q/T/G keys for extra attacks
        public static ConfigEntry<KeyboardShortcut> ExtraAttackKey_Q = null!;
        public static ConfigEntry<KeyboardShortcut> ExtraAttackKey_T = null!;
        public static ConfigEntry<KeyboardShortcut> ExtraAttackKey_G = null!;


        // Debug configurations - Individual categories
        public static ConfigEntry<bool> DebugClipNames = null!;
        public static ConfigEntry<bool> DebugAnimationEvents = null!;
        public static ConfigEntry<bool> DebugAnimationClips = null!;
        public static ConfigEntry<bool> DebugAnimationParameters = null!;
        public static ConfigEntry<bool> DebugAttackTriggers = null!;
        public static ConfigEntry<bool> DebugDamageCalculation = null!;
        public static ConfigEntry<bool> DebugSystemMessages = null!;
        public static ConfigEntry<bool> DebugPerformanceMetrics = null!;
        public static ConfigEntry<bool> DebugAOCOperations = null!;
        public static ConfigEntry<bool> DebugDisableSetTriggerOverride = null!;
        
        // Cached debug flags for performance (all controlled by System Messages)
        public static bool IsDebugSystemMessagesEnabled => cachedDebugSystemMessages;
        public static bool IsDebugAttackTriggersEnabled => cachedDebugSystemMessages && cachedDebugAttackTriggers;
        public static bool IsDebugDamageCalculationEnabled => cachedDebugSystemMessages && cachedDebugDamageCalculation;
        public static bool IsDebugAnimationClipsEnabled => cachedDebugSystemMessages && cachedDebugAnimationClips;
        public static bool IsDebugAnimationEventsEnabled => cachedDebugSystemMessages && cachedDebugAnimationEvents;
        public static bool IsDebugAnimationParametersEnabled => cachedDebugSystemMessages && cachedDebugAnimationParameters;
        public static bool IsDebugPerformanceMetricsEnabled => cachedDebugSystemMessages && cachedDebugPerformanceMetrics;
        public static bool IsDebugClipNamesEnabled => cachedDebugSystemMessages && cachedDebugClipNames;
        public static bool IsDebugAOCOperationsEnabled => cachedDebugSystemMessages && cachedDebugAOCOperations;
        
        // ========================================================================
        // 3 - COMPAT
        // ========================================================================
        private void DetectDualWieldMods()
        {
            try
            {
                // Guard: Chainloader.PluginInfos may be null/empty early in lifecycle
                if (Chainloader.PluginInfos == null || Chainloader.PluginInfos.Count == 0)
                {
                    LogInfo("System", "Dual-wield mod detection: PluginInfos not ready");
                    return;
                }

                bool found = false;
                string foundReason = string.Empty;
                string foundName = string.Empty;

                foreach (var kv in Chainloader.PluginInfos)
                {
                    var guid = kv.Key ?? string.Empty;
                    var info = kv.Value;
                    // Guard: info or metadata can be null
                    var name = info?.Metadata?.Name ?? guid;
                    var asmName = info?.Instance?.GetType()?.Assembly?.GetName()?.Name ?? string.Empty;
                    var asmFullName = info?.Instance?.GetType()?.Assembly?.FullName ?? string.Empty;

                    // Stage 1: GUID/Name contains known tokens
                    if ((guid.IndexOf("DualWield", StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (guid.IndexOf("DualWielder", StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (name.IndexOf("DualWield", StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (name.IndexOf("DualWielder", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        found = true;
                        foundReason = "Plugin GUID/Name match";
                        foundName = name;
                    }
                    // Stage 2: Assembly name match
                    else if ((asmName.IndexOf("DualWield", StringComparison.OrdinalIgnoreCase) >= 0) ||
                             (asmName.IndexOf("DualWielder", StringComparison.OrdinalIgnoreCase) >= 0) ||
                             (asmFullName.IndexOf("DualWield", StringComparison.OrdinalIgnoreCase) >= 0) ||
                             (asmFullName.IndexOf("DualWielder", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        found = true;
                        foundReason = $"Assembly match ({asmName})";
                        foundName = name;
                    }
                    // Stage 3: Type-level scan in assembly
                    else
                    {
                        var asm = info?.Instance?.GetType()?.Assembly;
                        if (asm != null)
                        {
                            try
                            {
                                foreach (var t in asm.GetTypes())
                                {
                                    string tn = t.FullName ?? t.Name;
                                    string ns = t.Namespace ?? string.Empty;
                                    if ((tn.IndexOf("DualWield", StringComparison.OrdinalIgnoreCase) >= 0) ||
                                        (tn.IndexOf("DualWielder", StringComparison.OrdinalIgnoreCase) >= 0) ||
                                        (ns.IndexOf("DualWield", StringComparison.OrdinalIgnoreCase) >= 0) ||
                                        (ns.IndexOf("DualWielder", StringComparison.OrdinalIgnoreCase) >= 0))
                                    {
                                        found = true;
                                        foundReason = $"Type match ({tn})";
                                        foundName = name;
                                        break;
                                    }
                                }
                            }
                            catch { /* Assembly may restrict reflection; ignore */ }
                        }
                    }

                    if (found)
                    {
                        dualWieldModDetected = true;
                        detectedDualWieldModName = foundName;
                        LogInfo("System", $"Dual-wield mod detected: {detectedDualWieldModName} - {foundReason}");
                        break;
                    }
                }

                if (!dualWieldModDetected)
                {
                    LogInfo("System", "Dual-wield mod not detected");
                }
            }
            catch (Exception ex)
            {
                LogError("System", $"Dual-wield mod detection error: {ex.Message}");
            }
        }

        // ========================================================================
        // 4 - DEBUG
        // ========================================================================
        private void InitializeDebugConfigs()
        {
            // ========================================================================
            // MASTER CONTROL
            // ========================================================================
            
            // Essential system messages (master control for all debug messages)
            DebugSystemMessages = Config.Bind("1 - Master Control", "System Messages (Master Switch)", false,
                "Master control: Enable/disable all debug messages at once");
            cachedDebugSystemMessages = DebugSystemMessages.Value;
            DebugSystemMessages.SettingChanged += (s, e) => {
                cachedDebugSystemMessages = DebugSystemMessages.Value;
                LogInfo("Config", $"DebugSystemMessages (Master Control) changed to: {cachedDebugSystemMessages}");
            };

            // ========================================================================
            // ATTACK SYSTEM DEBUG
            // ========================================================================
            
            // Attack trigger detection and processing
            DebugAttackTriggers = Config.Bind("2 - Attack System", "Attack Triggers", false,
                "Log attack trigger detection and processing (requires System Messages enabled)");
            cachedDebugAttackTriggers = DebugAttackTriggers.Value;
            DebugAttackTriggers.SettingChanged += (s, e) => {
                cachedDebugAttackTriggers = DebugAttackTriggers.Value;
                if (cachedDebugSystemMessages) LogInfo("Config", $"DebugAttackTriggers changed to: {cachedDebugAttackTriggers}");
            };

            // Damage calculation and restoration
            DebugDamageCalculation = Config.Bind("2 - Attack System", "Damage Calculation", false,
                "Log damage multiplier application and restoration (requires System Messages enabled)");
            cachedDebugDamageCalculation = DebugDamageCalculation.Value;
            DebugDamageCalculation.SettingChanged += (s, e) => {
                cachedDebugDamageCalculation = DebugDamageCalculation.Value;
                if (cachedDebugSystemMessages) LogInfo("Config", $"DebugDamageCalculation changed to: {cachedDebugDamageCalculation}");
            };

            // ========================================================================
            // ANIMATION SYSTEM DEBUG
            // ========================================================================

            // AOC (Animator Override Controller) operations
            DebugAOCOperations = Config.Bind("3 - Animation System", "AOC Operations", false,
                "Log AOC creation, switching, and animation overrides (requires System Messages enabled)");
            cachedDebugAOCOperations = DebugAOCOperations.Value;
            DebugAOCOperations.SettingChanged += (s, e) => {
                cachedDebugAOCOperations = DebugAOCOperations.Value;
                if (cachedDebugSystemMessages) LogInfo("Config", $"DebugAOCOperations changed to: {cachedDebugAOCOperations}");
            };
            
            // Animation clip information
            DebugAnimationClips = Config.Bind("3 - Animation System", "Animation Clips", false,
                "Log AnimationClip information during AOC creation (requires System Messages enabled)");
            cachedDebugAnimationClips = DebugAnimationClips.Value;
            DebugAnimationClips.SettingChanged += (s, e) => {
                cachedDebugAnimationClips = DebugAnimationClips.Value;
                if (cachedDebugSystemMessages) LogInfo("Config", $"DebugAnimationClips changed to: {cachedDebugAnimationClips}");
            };

            // Animation events
            DebugAnimationEvents = Config.Bind("3 - Animation System", "Animation Events", false,
                "Log AnimationEvent information during AOC creation (requires System Messages enabled)");
            cachedDebugAnimationEvents = DebugAnimationEvents.Value;
            DebugAnimationEvents.SettingChanged += (s, e) => {
                cachedDebugAnimationEvents = DebugAnimationEvents.Value;
                if (cachedDebugSystemMessages) LogInfo("Config", $"DebugAnimationEvents changed to: {cachedDebugAnimationEvents}");
            };

            // Animator parameters
            DebugAnimationParameters = Config.Bind("3 - Animation System", "Animation Parameters", false,
                "Log Animator parameters during initialization (requires System Messages enabled)");
            cachedDebugAnimationParameters = DebugAnimationParameters.Value;
            DebugAnimationParameters.SettingChanged += (s, e) => {
                cachedDebugAnimationParameters = DebugAnimationParameters.Value;
                if (cachedDebugSystemMessages) LogInfo("Config", $"DebugAnimationParameters changed to: {cachedDebugAnimationParameters}");
            };

            // Current playing animation names
            DebugClipNames = Config.Bind("3 - Animation System", "Current Playing Animations", false,
                "Log which animation clips are currently playing when pressing Q/T/G keys (requires System Messages enabled)");
            cachedDebugClipNames = DebugClipNames.Value;
            DebugClipNames.SettingChanged += (s, e) => {
                cachedDebugClipNames = DebugClipNames.Value;
                if (cachedDebugSystemMessages) LogInfo("Config", $"DebugClipNames changed to: {cachedDebugClipNames}");
            };

            // ========================================================================
            // ADVANCED DEBUG
            // ========================================================================
            
            // Performance metrics
            DebugPerformanceMetrics = Config.Bind("4 - Advanced Debug", "Performance Metrics", false,
                "Log performance measurements (timings, allocations) (requires System Messages enabled)");
            cachedDebugPerformanceMetrics = DebugPerformanceMetrics.Value;
            DebugPerformanceMetrics.SettingChanged += (s, e) => {
                cachedDebugPerformanceMetrics = DebugPerformanceMetrics.Value;
                if (cachedDebugSystemMessages) LogInfo("Config", $"DebugPerformanceMetrics changed to: {cachedDebugPerformanceMetrics}");
            };
            
            // SetTrigger override debug
            DebugDisableSetTriggerOverride = Config.Bind("4 - Advanced Debug", "Disable SetTrigger Override", false,
                "Disable SetTrigger override for debugging (requires System Messages enabled)");
            DebugDisableSetTriggerOverride.SettingChanged += (s, e) => {
                if (cachedDebugSystemMessages) LogInfo("Config", $"DebugDisableSetTriggerOverride changed to: {DebugDisableSetTriggerOverride.Value}");
            };

            LogInfo("System", "Debug configuration initialized");
        }

        // Generate all 6 YAML config files in one unified process
        private void GenerateAllYamlConfigs()
        {
            try
            {
                LogInfo("System", "GenerateAllYamlConfigs: Starting unified YAML generation for all 6 config files");
                
                // 1. AnimationReplacementConfig (2 files)
                LogInfo("System", "Generating AnimationReplacementConfig files...");
                AnimationReplacementConfig.Initialize();
                
                // 2. AnimationTimingConfig (4 files)
                LogInfo("System", "Generating AnimationTimingConfig files...");
                LogInfo("System", "About to call AnimationTimingConfig.Initialize()");
                try
                {
                    AnimationTimingConfig.Initialize();
                    LogInfo("System", "AnimationTimingConfig.Initialize completed successfully");
                }
                catch (Exception ex)
                {
                    LogError("System", $"AnimationTimingConfig.Initialize failed: {ex.Message}");
                    LogError("System", $"Stack trace: {ex.StackTrace}");
                }
                
                // 3. ExtraAttackExclusionConfig (1 file)
                LogInfo("System", "Generating ExtraAttackExclusionConfig file...");
                ExtraAttackExclusionConfig.Initialize();
                
                LogInfo("System", "GenerateAllYamlConfigs: Successfully generated all 6 YAML config files");
            }
            catch (Exception ex)
            {
                LogError("System", $"Error in GenerateAllYamlConfigs: {ex.Message}");
            }
        }

        private void Awake()
        {
            try
            {
                // Bind general keys
                // Q/T/G keys for extra attacks (ordered Q/T/G)
                ExtraAttackKey_Q = Config.Bind("1 - General", "1. Extra Attack Key Q", new KeyboardShortcut(KeyCode.Q), "Trigger Q extra attack.");
                ExtraAttackKey_T = Config.Bind("1 - General", "2. Extra Attack Key T", new KeyboardShortcut(KeyCode.T), "Trigger T extra attack.");
                ExtraAttackKey_G = Config.Bind("1 - General", "3. Extra Attack Key G", new KeyboardShortcut(KeyCode.G), "Trigger G extra attack.");



                // Compatibility detection (DualWield/DualWielder)
                DetectDualWieldMods();

                // Initialize debug configs
                InitializeDebugConfigs();

                // Stamina costs are now managed by YAML CostConfig
                
                // Initialize cost configuration
                ExtraAttackCostConfig.Initialize();
                
                // Load external animation assets first to populate ReplacementMap
                AnimationManager.LoadAssets();
                
                // Apply weapon type settings to populate ReplacementMap before generating YAML
                AnimationManager.ApplyWeaponTypeSettings();
                
                // Generate all 6 YAML configs in one unified process
                GenerateAllYamlConfigs();
                
                // Generate cost config file
                ExtraAttackCostConfig.GenerateCostConfig();
                
                // Only save YAML files if they don't exist or are empty
                // (existing YAML files should not be overwritten)
                
                // Apply Harmony patches
                ApplyPatches();
            }
            catch (Exception ex)
            {
                LogError("System", $"Awake error: {ex.Message}");
            }
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

    public static void LogInfo(string category, string message)
    {
        // Always log essential system messages
        if (category is "System" or "AOC" or "AttackTriggers" or "COMBO" or "AnimationParameters" or "Diag")
        {
            ExtraAttackLogger.LogInfo($"[{category}] {message}");
        }
    }

    public static void LogError(string category, string message)
    {
        ExtraAttackLogger.LogError($"[{category}] {message}");
    }

    // NEW: Warning logger wrapper for consistency
    public static void LogWarning(string category, string message)
    {
        ExtraAttackLogger.LogWarning($"[{category}] {message}");
    }

    // Key input helpers for Q/T/G
    public static bool IsExtraAttackKey_QPressed()
    {
        return ExtraAttackKey_Q.Value.IsDown();
    }
    public static bool IsExtraAttackKey_TPressed()
    {
        return ExtraAttackKey_T.Value.IsDown();
    }
    public static bool IsExtraAttackKey_GPressed()
    {
        return ExtraAttackKey_G.Value.IsDown();
    }
}
}