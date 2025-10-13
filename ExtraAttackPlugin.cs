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

        // Cached debug toggles for performance (all debug categories)
        private static bool cachedDebugSystemMessages;
        private static bool cachedDebugAttackTriggers;
        private static bool cachedDebugDamageCalculation;
        private static bool cachedDebugPerformanceMetrics;
        private static bool cachedDebugAOCOperations;
        private static bool cachedDebugAnimationParameters;
        private static bool cachedDebugClipNames;

        // General config - keys & cooldowns
        // Q/T/G keys for extra attacks
        public static ConfigEntry<KeyboardShortcut> ExtraAttackKey_Q = null!;
        public static ConfigEntry<KeyboardShortcut> ExtraAttackKey_T = null!;
        public static ConfigEntry<KeyboardShortcut> ExtraAttackKey_G = null!;

        // Debug configurations - All debug categories
        public static ConfigEntry<bool> DebugSystemMessages = null!;
        public static ConfigEntry<bool> DebugAttackTriggers = null!;
        public static ConfigEntry<bool> DebugDamageCalculation = null!;
        public static ConfigEntry<bool> DebugPerformanceMetrics = null!;
        public static ConfigEntry<bool> DebugAOCOperations = null!;
        public static ConfigEntry<bool> DebugAnimationEventsList = null!;
        public static ConfigEntry<bool> DebugAnimationClipsList = null!;
        public static ConfigEntry<bool> DebugAnimationParameters = null!;
        public static ConfigEntry<bool> DebugClipNames = null!;
        
        // Cached debug flags for performance (all categories)
        public static bool IsDebugSystemMessagesEnabled => cachedDebugSystemMessages;
        public static bool IsDebugAttackTriggersEnabled => cachedDebugAttackTriggers;
        public static bool IsDebugDamageCalculationEnabled => cachedDebugDamageCalculation;
        public static bool IsDebugPerformanceMetricsEnabled => cachedDebugPerformanceMetrics;
        public static bool IsDebugAOCOperationsEnabled => cachedDebugAOCOperations;
        public static bool IsDebugAnimationParametersEnabled => cachedDebugAnimationParameters;
        public static bool IsDebugClipNamesEnabled => cachedDebugClipNames;
        
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
                    if (IsDebugSystemMessagesEnabled)
                    {
                        LogInfo("System", "Dual Wield or Dual Wielder mod detection: PluginInfos not ready");
                    }
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
                        if (IsDebugSystemMessagesEnabled)
                        {
                            LogInfo("System", $"Dual-wield mod detected: {detectedDualWieldModName} - {foundReason}");
                        }
                        break;
                    }
                }

                if (!dualWieldModDetected)
                {
                    if (IsDebugSystemMessagesEnabled)
                    {
                        LogInfo("System", "Dual-wield mod not detected");
                    }
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
            // SYSTEM MESSAGES
            // ========================================================================
            
            // System messages
            DebugSystemMessages = Config.Bind("1 - System Messages", "System Messages", false,
                "Enable/disable system messages");
            cachedDebugSystemMessages = DebugSystemMessages.Value;
            DebugSystemMessages.SettingChanged += (s, e) => {
                cachedDebugSystemMessages = DebugSystemMessages.Value;
                LogInfo("System", $"DebugSystemMessages changed to: {cachedDebugSystemMessages}");
                if (cachedDebugSystemMessages)
                {
                    LogInfo("System", "System Messages debug enabled - will log system messages during gameplay");
                }
                else
                {
                    LogInfo("System", "System Messages debug disabled - no more system message logs will be output");
                }
            };

            // ========================================================================
            // ATTACK SYSTEM DEBUG
            // ========================================================================
            
            // Attack trigger detection and processing
            DebugAttackTriggers = Config.Bind("2 - Attack System", "Attack Triggers", false,
                "Output attack trigger detection and processing to debug console");
            cachedDebugAttackTriggers = DebugAttackTriggers.Value;
            DebugAttackTriggers.SettingChanged += (s, e) => {
                cachedDebugAttackTriggers = DebugAttackTriggers.Value;
                LogInfo("System", $"DebugAttackTriggers changed to: {cachedDebugAttackTriggers}");
                if (cachedDebugAttackTriggers)
                {
                    LogInfo("System", "Attack Triggers debug enabled - will log attack triggers during gameplay");
                }
                else
                {
                    LogInfo("System", "Attack Triggers debug disabled - no more attack trigger logs will be output");
                }
            };

            // Damage calculation and restoration
            DebugDamageCalculation = Config.Bind("2 - Attack System", "Damage Calculation", false,
                "Output damage multiplier application and restoration to debug console");
            cachedDebugDamageCalculation = DebugDamageCalculation.Value;
            DebugDamageCalculation.SettingChanged += (s, e) => {
                cachedDebugDamageCalculation = DebugDamageCalculation.Value;
                LogInfo("System", $"DebugDamageCalculation changed to: {cachedDebugDamageCalculation}");
                if (cachedDebugDamageCalculation)
                {
                    LogInfo("System", "Damage Calculation debug enabled - will log damage calculations during attacks");
                }
                else
                {
                    LogInfo("System", "Damage Calculation debug disabled - no more damage calculation logs will be output");
                }
            };

            // ========================================================================
            // ANIMATION SYSTEM DEBUG
            // ========================================================================

            // AOC (Animator Override Controller) operations
            DebugAOCOperations = Config.Bind("3 - Animation System", "AOC Operations", false,
                "Output AOC creation, switching, and animation overrides to debug console");
            cachedDebugAOCOperations = DebugAOCOperations.Value;
            DebugAOCOperations.SettingChanged += (s, e) => {
                cachedDebugAOCOperations = DebugAOCOperations.Value;
                LogInfo("System", $"DebugAOCOperations changed to: {cachedDebugAOCOperations}");
                if (cachedDebugAOCOperations)
                {
                    LogInfo("System", "AOC Operations debug enabled - will log AOC operations during gameplay");
                }
                else
                {
                    LogInfo("System", "AOC Operations debug disabled - no more AOC operation logs will be output");
                }
            };

            // Animation Events List (one-time output)
            DebugAnimationEventsList = Config.Bind("4 - Debug Lists", "Animation Events List", false,
                "Output all animation events from clips to debug console for timing analysis");
            DebugAnimationEventsList.SettingChanged += (s, e) => {
                LogInfo("System", $"DebugAnimationEventsList changed to: {DebugAnimationEventsList.Value}");
                if (DebugAnimationEventsList.Value)
                {
                    LogInfo("System", "Animation Events list output requested");
                    EAS_Debug.LogAllAnimationEvents();
                    LogInfo("System", "Animation Events list output completed");
                }
                else
                {
                    // Simple log for OFF to prevent freeze
                    UnityEngine.Debug.Log("[Extra Attack System] Animation Events list output disabled");
                }
            };

            // Animation Clips List (one-time output)
            DebugAnimationClipsList = Config.Bind("4 - Debug Lists", "Animation Clips List", false,
                "Output all animation clips and their properties to debug console");
            DebugAnimationClipsList.SettingChanged += (s, e) => {
                LogInfo("System", $"DebugAnimationClipsList changed to: {DebugAnimationClipsList.Value}");
                if (DebugAnimationClipsList.Value)
                {
                    LogInfo("System", "Animation Clips list output requested");
                    EAS_Debug.LogAllAnimationClips();
                    LogInfo("System", "Animation Clips list output completed");
                }
                else
                {
                    // Simple log for OFF to prevent freeze
                    UnityEngine.Debug.Log("[Extra Attack System] Animation Clips list output disabled");
                }
            };


            // Animation Parameters Debug
            DebugAnimationParameters = Config.Bind("3 - Animation System", "Animation Parameters", false,
                "Output animator parameters and their current values during operations");
            cachedDebugAnimationParameters = DebugAnimationParameters.Value;
            DebugAnimationParameters.SettingChanged += (s, e) => {
                cachedDebugAnimationParameters = DebugAnimationParameters.Value;
                LogInfo("System", $"DebugAnimationParameters changed to: {cachedDebugAnimationParameters}");
                if (cachedDebugAnimationParameters)
                {
                    LogInfo("System", "Animation Parameters debug enabled - will log animator parameters during operations");
                }
                else
                {
                    LogInfo("System", "Animation Parameters debug disabled - no more animator parameter logs will be output");
                }
            };

            // Clip Names Debug
            DebugClipNames = Config.Bind("3 - Animation System", "Clip Names", false,
                "Log clip names during operations for debugging");
            cachedDebugClipNames = DebugClipNames.Value;
            DebugClipNames.SettingChanged += (s, e) => {
                cachedDebugClipNames = DebugClipNames.Value;
                LogInfo("System", $"DebugClipNames changed to: {cachedDebugClipNames}");
                if (cachedDebugClipNames)
                {
                    LogInfo("System", "Clip Names debug enabled - will log clip names during operations");
                }
                else
                {
                    LogInfo("System", "Clip Names debug disabled - no more clip name logs will be output");
                }
            };
            
            
            
            
            // Initialize EAS_Debug system
            EAS_Debug.Initialize();

            // ========================================================================
            // ADVANCED DEBUG
            // ========================================================================
            
            // Performance metrics
            DebugPerformanceMetrics = Config.Bind("5 - Advanced Debug", "Performance Metrics", false,
                "Log performance measurements (timings, allocations)");
            cachedDebugPerformanceMetrics = DebugPerformanceMetrics.Value;
            DebugPerformanceMetrics.SettingChanged += (s, e) => {
                cachedDebugPerformanceMetrics = DebugPerformanceMetrics.Value;
                LogInfo("System", $"DebugPerformanceMetrics changed to: {cachedDebugPerformanceMetrics}");
                if (cachedDebugPerformanceMetrics)
                {
                    LogInfo("System", "Performance Metrics debug enabled - will log performance measurements during gameplay");
                }
                else
                {
                    LogInfo("System", "Performance Metrics debug disabled - no more performance metric logs will be output");
                }
            };
            

            if (IsDebugSystemMessagesEnabled)
            {
                LogInfo("System", "Debug configuration initialized");
            }
        }

        // Generate all 6 YAML config files in one unified process
        private void GenerateAllYamlConfigs()
        {
            try
            {
                if (IsDebugSystemMessagesEnabled)
                {
                    LogInfo("System", "GenerateAllYamlConfigs: Starting unified YAML generation for all 6 config files");
                }
                
                // 1. AnimationReplacementConfig (2 files)
                if (IsDebugSystemMessagesEnabled)
                {
                    LogInfo("System", "Generating AnimationReplacementConfig files...");
                }
                AnimationReplacementConfig.Initialize();
                
                // 2. AnimationTimingConfig (4 files)
                if (IsDebugSystemMessagesEnabled)
                {
                    LogInfo("System", "Generating AnimationTimingConfig files...");
                }
                if (IsDebugSystemMessagesEnabled)
                {
                    LogInfo("System", "About to call AnimationTimingConfig.Initialize()");
                }
                try
                {
                    AnimationTimingConfig.Initialize();
                    if (IsDebugSystemMessagesEnabled)
                    {
                        LogInfo("System", "AnimationTimingConfig.Initialize completed successfully");
                    }
                }
                catch (Exception ex)
                {
                    LogError("System", $"AnimationTimingConfig.Initialize failed: {ex.Message}");
                    LogError("System", $"Stack trace: {ex.StackTrace}");
                }
                
                // 3. ExtraAttackExclusionConfig (1 file)
                if (IsDebugSystemMessagesEnabled)
                {
                    LogInfo("System", "Generating ExtraAttackExclusionConfig file...");
                }
                ExtraAttackExclusionConfig.Initialize();
                
                if (IsDebugSystemMessagesEnabled)
                {
                    LogInfo("System", "GenerateAllYamlConfigs: Successfully generated all 6 YAML config files");
                }
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
            if (IsDebugSystemMessagesEnabled)
            {
                LogInfo("System", "Harmony patches applied successfully");
            }
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