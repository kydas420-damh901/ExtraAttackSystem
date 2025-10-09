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
    [NetworkCompatibilityAttribute(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class ExtraAttackPlugin : BaseUnityPlugin
    {
        internal const string PluginGUID = "Dyju420.ExtraAttackSystem";
        internal const string PluginName = "Extra Attack System";
        internal const string PluginVersion = "1.0.0";

        internal static readonly ManualLogSource ExtraAttackLogger = BepInEx.Logging.Logger.CreateLogSource(PluginName);
        // NEW: Dual-wield mod detection flag
        private static bool dualWieldModDetected = false;
        private static string detectedDualWieldModName = string.Empty;

        // Harmony instance
        private readonly Harmony harmony = new Harmony(PluginGUID);

        // Cached debug toggles for performance
        private static bool cachedDebugMaster;
        private static bool cachedDebugClipNames;
        private static bool cachedDebugAnimationEvents;
        private static bool cachedDebugAnimationClips;
        private static bool cachedDebugAnimationParameters;
        private static bool cachedDebugAttackTriggers;
        private static bool cachedDebugAOCOperations;
        private static bool cachedDebugDamageCalculation;
        private static bool cachedDebugSystemMessages;
        private static bool cachedDebugPerformanceMetrics;
        private static bool cachedDebugDisableSetTriggerOverride;
        private static bool cachedDebugSkipGAOCApply;

        // General config - keys & cooldowns
        public static ConfigEntry<KeyboardShortcut> ExtraAttackKey = null!;
        public static ConfigEntry<KeyboardShortcut> TestButton1 = null!;
        public static ConfigEntry<KeyboardShortcut> TestButton2 = null!;
        // NEW: Separate keys for Q/T/G
        public static ConfigEntry<KeyboardShortcut> ExtraAttackKey_Q = null!;
        public static ConfigEntry<KeyboardShortcut> ExtraAttackKey_T = null!;
        public static ConfigEntry<KeyboardShortcut> ExtraAttackKey_G = null!;
        public static ConfigEntry<float> ExtraAttackQCooldown = null!;
        public static ConfigEntry<float> ExtraAttackTCooldown = null!;
        public static ConfigEntry<float> ExtraAttackGCooldown = null!;
        public static ConfigEntry<bool> EnableCrouchGuard = null!;
        public static ConfigEntry<bool> EnablePostAttackEmoteStopGuard = null!;
        public static ConfigEntry<float> PostAttackEmoteStopGuardSeconds = null!;
        public static ConfigEntry<bool> EnableInsufficientStaminaEmoteStopGuard = null!;
        public static ConfigEntry<float> InsufficientStaminaEmoteStopGuardSeconds = null!;
public static ConfigEntry<bool> DisableAllGuardsChecks = null!;
private static bool cachedDisableAllGuardsChecks;

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
        public static ConfigEntry<bool> DebugPerformanceMetrics = null!;
        public static ConfigEntry<bool> DebugDisableSetTriggerOverride = null!;
        public static ConfigEntry<bool> DebugSkipGAOCApply = null!;
        
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
            // Debug master switch
            DebugMasterSwitch = Config.Bind("5 - Debug", "0. Debug Master Switch", false,
                "Enable/Disable ALL debug logging at once");
            cachedDebugMaster = DebugMasterSwitch.Value;
            DebugMasterSwitch.SettingChanged += (s, e) => cachedDebugMaster = DebugMasterSwitch.Value;

            // ========================================================================
            // CORE SYSTEM LOGGING
            // ========================================================================
            
            // Essential system messages (always available)
            DebugSystemMessages = Config.Bind("5 - Debug", "1. System Messages", false,
                "Core system logs (initialization, errors, warnings)");
            cachedDebugSystemMessages = DebugSystemMessages.Value;
            DebugSystemMessages.SettingChanged += (s, e) => {
                cachedDebugSystemMessages = DebugSystemMessages.Value;
                LogInfo("Config", $"DebugSystemMessages changed to: {cachedDebugSystemMessages}");
            };

            // ========================================================================
            // ATTACK SYSTEM LOGGING
            // ========================================================================
            
            // Attack trigger detection and processing
            DebugAttackTriggers = Config.Bind("5 - Debug", "2. Attack Triggers", false,
                "Log attack trigger detection and processing");
            cachedDebugAttackTriggers = DebugAttackTriggers.Value;
            DebugAttackTriggers.SettingChanged += (s, e) => {
                cachedDebugAttackTriggers = DebugAttackTriggers.Value;
                if (cachedDebugSystemMessages) LogInfo("Config", $"DebugAttackTriggers changed to: {cachedDebugAttackTriggers}");
            };

            // Damage calculation and restoration
            DebugDamageCalculation = Config.Bind("5 - Debug", "3. Damage Calculation", false,
                "Log damage multiplier application and restoration");
            cachedDebugDamageCalculation = DebugDamageCalculation.Value;
            DebugDamageCalculation.SettingChanged += (s, e) => {
                cachedDebugDamageCalculation = DebugDamageCalculation.Value;
                if (cachedDebugSystemMessages) LogInfo("Config", $"DebugDamageCalculation changed to: {cachedDebugDamageCalculation}");
            };

            // ========================================================================
            // ANIMATION SYSTEM LOGGING
            // ========================================================================
            
            // Animation clip information
            DebugAnimationClips = Config.Bind("5 - Debug", "4. Animation Clips", false,
                "Log AnimationClip information during AOC creation");
            cachedDebugAnimationClips = DebugAnimationClips.Value;
            DebugAnimationClips.SettingChanged += (s, e) => {
                cachedDebugAnimationClips = DebugAnimationClips.Value;
                if (cachedDebugSystemMessages) LogInfo("Config", $"DebugAnimationClips changed to: {cachedDebugAnimationClips}");
            };

            // Animation events
            DebugAnimationEvents = Config.Bind("5 - Debug", "5. Animation Events", false,
                "Log AnimationEvent information during AOC creation");
            cachedDebugAnimationEvents = DebugAnimationEvents.Value;
            DebugAnimationEvents.SettingChanged += (s, e) => {
                cachedDebugAnimationEvents = DebugAnimationEvents.Value;
                if (cachedDebugSystemMessages) LogInfo("Config", $"DebugAnimationEvents changed to: {cachedDebugAnimationEvents}");
            };

            // Animator parameters
            DebugAnimationParameters = Config.Bind("5 - Debug", "6. Animation Parameters", false,
                "Log Animator parameters during initialization");
            cachedDebugAnimationParameters = DebugAnimationParameters.Value;
            DebugAnimationParameters.SettingChanged += (s, e) => {
                cachedDebugAnimationParameters = DebugAnimationParameters.Value;
                if (cachedDebugSystemMessages) LogInfo("Config", $"DebugAnimationParameters changed to: {cachedDebugAnimationParameters}");
            };

            // AOC (Animator Override Controller) operations
            DebugAOCOperations = Config.Bind("5 - Debug", "7. AOC Operations", false,
                "Log AOC creation, switching, and animation overrides");
            cachedDebugAOCOperations = DebugAOCOperations.Value;
            DebugAOCOperations.SettingChanged += (s, e) => {
                cachedDebugAOCOperations = DebugAOCOperations.Value;
                if (cachedDebugSystemMessages) LogInfo("Config", $"DebugAOCOperations changed to: {cachedDebugAOCOperations}");
            };

            // ========================================================================
            // PERFORMANCE & DIAGNOSTICS
            // ========================================================================
            
            // Performance metrics
            DebugPerformanceMetrics = Config.Bind("5 - Debug", "8. Performance Metrics", false,
                "Log performance measurements (timings, allocations)");
            cachedDebugPerformanceMetrics = DebugPerformanceMetrics.Value;
            DebugPerformanceMetrics.SettingChanged += (s, e) => {
                cachedDebugPerformanceMetrics = DebugPerformanceMetrics.Value;
                if (cachedDebugSystemMessages) LogInfo("Config", $"DebugPerformanceMetrics changed to: {cachedDebugPerformanceMetrics}");
            };

            // Clip names during gameplay
            DebugClipNames = Config.Bind("5 - Debug", "9. Clip Names", false,
                "Log actual clip names being played when pressing Q/T/G keys");
            cachedDebugClipNames = DebugClipNames.Value;
            DebugClipNames.SettingChanged += (s, e) => {
                cachedDebugClipNames = DebugClipNames.Value;
                if (cachedDebugSystemMessages) LogInfo("Config", $"DebugClipNames changed to: {cachedDebugClipNames}");
            };

            // ========================================================================
            // ADVANCED DIAGNOSTICS (Crash Isolation)
            // ========================================================================
            
            // Disable animation trigger overrides
            DebugDisableSetTriggerOverride = Config.Bind("5 - Debug", "10. Disable SetTrigger Override", false,
                "Disable animation trigger overrides (for crash isolation)");
            cachedDebugDisableSetTriggerOverride = DebugDisableSetTriggerOverride.Value;
            DebugDisableSetTriggerOverride.SettingChanged += (s, e) => {
                cachedDebugDisableSetTriggerOverride = DebugDisableSetTriggerOverride.Value;
                if (cachedDebugSystemMessages) LogInfo("Config", $"DebugDisableSetTriggerOverride changed to: {cachedDebugDisableSetTriggerOverride}");
            };

            // Skip G attack AOC application
            DebugSkipGAOCApply = Config.Bind("5 - Debug", "11. Skip G AOC Apply", false,
                "Skip applying AOC for G attacks (for crash isolation)");
            cachedDebugSkipGAOCApply = DebugSkipGAOCApply.Value;
            DebugSkipGAOCApply.SettingChanged += (s, e) => {
                cachedDebugSkipGAOCApply = DebugSkipGAOCApply.Value;
                if (cachedDebugSystemMessages) LogInfo("Config", $"DebugSkipGAOCApply changed to: {cachedDebugSkipGAOCApply}");
            };

            LogInfo("System", "Debug configuration initialized");
        }

        private void Awake()
        {
            try
            {
                // Bind general keys
                ExtraAttackKey = Config.Bind("1 - General", "Extra Attack Key (Q)", new KeyboardShortcut(KeyCode.Q), "Trigger Q extra attack.");
                TestButton1 = Config.Bind("1 - General", "T Key (T)", new KeyboardShortcut(KeyCode.T), "Trigger T extra attack.");
                TestButton2 = Config.Bind("1 - General", "G Key (G)", new KeyboardShortcut(KeyCode.G), "Trigger G extra attack.");
                // NEW: Separate keys for Q/T/G
                ExtraAttackKey_Q = Config.Bind("1 - General", "Extra Attack Key Q", new KeyboardShortcut(KeyCode.Q), "Trigger Q extra attack.");
                ExtraAttackKey_T = Config.Bind("1 - General", "Extra Attack Key T", new KeyboardShortcut(KeyCode.T), "Trigger T extra attack.");
                ExtraAttackKey_G = Config.Bind("1 - General", "Extra Attack Key G", new KeyboardShortcut(KeyCode.G), "Trigger G extra attack.");

                // Bind cooldowns
                ExtraAttackQCooldown = Config.Bind("1 - General", "Q (Q) Cooldown", 2f, "Cooldown seconds for Q extra attack.");
                ExtraAttackTCooldown = Config.Bind("1 - General", "T (T) Cooldown", 2f, "Cooldown seconds for T extra attack.");
                ExtraAttackGCooldown = Config.Bind("1 - General", "G (G) Cooldown", 2f, "Cooldown seconds for G extra attack.");

                // PostAttack emote_stop guard config
                EnablePostAttackEmoteStopGuard = Config.Bind("2 - Guards & Safety", "Enable PostAttack Emote Stop Guard", false, "Suppress stand-up (emote_stop) for a short window after extra attack.");
                PostAttackEmoteStopGuardSeconds = Config.Bind("2 - Guards & Safety", "PostAttack Emote Stop Guard Seconds", 0.5f, "Length of guard window in seconds after extra attack.");

                // Insufficient stamina guard config
                EnableInsufficientStaminaEmoteStopGuard = Config.Bind("2 - Guards & Safety", "Enable Insufficient Stamina Emote Stop Guard", false, "Suppress stand-up (emote_stop) when extra attack fails due to insufficient stamina.");
                InsufficientStaminaEmoteStopGuardSeconds = Config.Bind("2 - Guards & Safety", "Insufficient Stamina Emote Stop Guard Seconds", 0.6f, "Length of guard window in seconds when attack is blocked.");

                // Crouch guard
                EnableCrouchGuard = Config.Bind("2 - Guards & Safety", "Enable Crouch Guard", false, "Prevent forced stand-up (crouch=false) during extra attack flow.");

                // Inside Awake() after EnableCrouchGuard and timing configs
                DisableAllGuardsChecks = Config.Bind("3 - Debug & Troubleshooting", "Disable All Guards & Checks", false,
                    "Turns off all guard/check logic: emote_stop guard, crouch guards, stamina guards, SetTrigger guard override, etc. Use for troubleshooting.");
                cachedDisableAllGuardsChecks = DisableAllGuardsChecks.Value;
                DisableAllGuardsChecks.SettingChanged += (s, e) => cachedDisableAllGuardsChecks = DisableAllGuardsChecks.Value;

                // Compatibility detection (DualWield/DualWielder)
                DetectDualWieldMods();

                // Initialize debug configs
                InitializeDebugConfigs();

                // Initialize stamina balancing configs per skill type
                // After InitializeBalancingConfigs(); insert runtime config/asset initialization
                InitializeBalancingConfigs();
                
                // Load external animation assets first to populate ReplacementMap
                AnimationManager.LoadAssets();
                // Initialize YAML configs after assets to generate default mappings properly
                AnimationReplacementConfig.Initialize();
                AnimationTimingConfig.Initialize();
                ExtraAttackExclusionConfig.Initialize();
                
                // Apply Harmony patches
                harmony.PatchAll();
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
        // Guard: DebugMasterSwitch may be null during early Awake before config binding.
        bool master = DebugMasterSwitch != null && DebugMasterSwitch.Value;
        if (master || category is "System" or "AOC" or "AttackTriggers" or "COMBO" or "AnimationParameters" or "Diag")
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

    // NEW: Key input helpers for Q/T/G
    public static bool IsExtraAttackKeyPressed()
    {
        return ExtraAttackKey.Value.IsDown();
    }
    public static bool IsTestButton1Pressed()
    {
        return TestButton1.Value.IsDown();
    }
    public static bool IsTestButton2Pressed()
    {
        return TestButton2.Value.IsDown();
    }
    // NEW: Separate key input helpers for Q/T/G
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

    // NEW: Stamina cost resolver per skill type and style
    public static float GetStaminaCost(Skills.SkillType skill, ExtraAttackUtils.AttackMode mode)
    {
        if (!BalancingMap.TryGetValue(skill, out var cfg))
        {
            return 0f;
        }
        switch (mode)
        {
            case ExtraAttackUtils.AttackMode.ea_secondary_Q:
                return cfg.staminaCostQ.Value;
            case ExtraAttackUtils.AttackMode.ea_secondary_T:
                return cfg.staminaCostT.Value;
            case ExtraAttackUtils.AttackMode.ea_secondary_G:
                return cfg.staminaCostG.Value;
            default:
                return cfg.staminaCost.Value;
        }
    }

    // NEW: Cooldown resolver per style
    public static float GetCooldown(ExtraAttackUtils.AttackMode mode)
    {
        switch (mode)
        {
            case ExtraAttackUtils.AttackMode.ea_secondary_Q:
                return ExtraAttackQCooldown.Value;
            case ExtraAttackUtils.AttackMode.ea_secondary_T:
                return ExtraAttackTCooldown.Value;
            case ExtraAttackUtils.AttackMode.ea_secondary_G:
                return ExtraAttackGCooldown.Value;
            default:
                return 0f;
        }
    }

    // Helper method inside class
    public static bool AreGuardsDisabled()
    {
        return cachedDisableAllGuardsChecks;
    }

    // Stamina balancing map per skill type
    private static readonly Dictionary<Skills.SkillType, ExtraAttackBalancingConfig> BalancingMap = new();

    // Holder for stamina cost config entries per style
    private sealed class ExtraAttackBalancingConfig
    {
        public ConfigEntry<float> staminaCost = null!;
        public ConfigEntry<float> staminaCostQ = null!;
        public ConfigEntry<float> staminaCostT = null!;
        public ConfigEntry<float> staminaCostG = null!;
    }

    // Initialize stamina balancing configs per skill type (inside class)
    private void InitializeBalancingConfigs()
    {
        // Create default entries for supported weapon skills
        Skills.SkillType[] skills = new[]
        {
            Skills.SkillType.Swords,
            Skills.SkillType.Axes,
            Skills.SkillType.Clubs,
            Skills.SkillType.Knives,
            Skills.SkillType.Spears,
        };

        foreach (var skill in skills)
        {
            var cfg = new ExtraAttackBalancingConfig();
            cfg.staminaCost = Config.Bind("2 - Balancing", $"{skill} Stamina Cost (Base)", 20f, "Base stamina cost for extra attacks (fallback).");
            cfg.staminaCostQ = Config.Bind("2 - Balancing", $"{skill} Stamina Cost Q (Q)", 20f, "Stamina cost for Q.");
            cfg.staminaCostT = Config.Bind("2 - Balancing", $"{skill} Stamina Cost T (T)", 22f, "Stamina cost for T.");
            cfg.staminaCostG = Config.Bind("2 - Balancing", $"{skill} Stamina Cost G (G)", 24f, "Stamina cost for G.");
            BalancingMap[skill] = cfg;
        }
    }
}
}