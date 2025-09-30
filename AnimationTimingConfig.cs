using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ExtraAttackSystem
{
    public static class AnimationTimingConfig
    {
        // Config file path
        private static string ConfigFolderPath => Path.Combine(BepInEx.Paths.ConfigPath, "Dyju420.ExtraAttackSystem");
        private static string ConfigFilePath => Path.Combine(ConfigFolderPath, "AnimationTiming.yaml");

        // Timing data for each animation
        public class AnimationTiming
        {
            // Animation Event Timing (0.0 ~ 1.0)
            public float HitTiming { get; set; } = 0.45f;
            public float TrailOnTiming { get; set; } = 0.35f;
            public float TrailOffTiming { get; set; } = 0.70f;
            public float ChainTiming { get; set; } = 0.75f;
            public float SpeedMultiplier { get; set; } = 1.0f;

            // Attack Detection Parameters
            public float AttackRange { get; set; } = 1.5f;
            public float AttackHeight { get; set; } = 0.6f;
            public float AttackOffset { get; set; } = 0.0f;
            public float AttackAngle { get; set; } = 90.0f;
            public float AttackRayWidth { get; set; } = 0.0f;
            public float AttackRayWidthCharExtra { get; set; } = 0.0f;
            public float AttackHeightChar1 { get; set; } = 0.0f;
            public float AttackHeightChar2 { get; set; } = 0.0f;
            public float MaxYAngle { get; set; } = 0.0f;
        }

        // Config file structure
        public class TimingConfig
        {
            public AnimationTiming Default { get; set; } = new AnimationTiming();
            public Dictionary<string, AnimationTiming> Animations { get; set; } = new Dictionary<string, AnimationTiming>();
        }

        private static TimingConfig currentConfig = new TimingConfig();

        // Load or create config file
        public static void Initialize()
        {
            try
            {
                if (!Directory.Exists(ConfigFolderPath))
                {
                    Directory.CreateDirectory(ConfigFolderPath);
                }

                if (File.Exists(ConfigFilePath))
                {
                    LoadConfig();
                    ExtraAttackPlugin.LogInfo("System", $"Loaded AnimationTiming.yaml with {currentConfig.Animations.Count} animations");
                }
                else
                {
                    CreateDefaultConfig();
                    ExtraAttackPlugin.LogInfo("System", "Created default AnimationTiming.yaml");
                }
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error initializing AnimationTimingConfig: {ex.Message}");
            }
        }

        // Create default config with mode suffix
        private static void CreateDefaultConfig()
        {
            currentConfig = new TimingConfig();

            // Create entries from ReplacementMap (Vanilla name -> External name)
            var mappings = new Dictionary<string, string>(); // Key: VanillaName_MODE, Value: ExternalName

            // Q Key mappings
            if (AnimationManager.ReplacementMap.TryGetValue("ExtraAttack_Q", out var qMap))
            {
                foreach (var kvp in qMap)
                {
                    string vanillaName = kvp.Key;
                    string externalName = kvp.Value;
                    mappings[$"{vanillaName}_Q"] = externalName;
                }
            }

            // T Key Swords mappings
            if (AnimationManager.ReplacementMap.TryGetValue("ExtraAttack_T_Swords", out var tSwordsMap))
            {
                foreach (var kvp in tSwordsMap)
                {
                    string vanillaName = kvp.Key;
                    string externalName = kvp.Value;
                    mappings[$"{vanillaName}_T"] = externalName;
                }
            }

            // T Key Clubs mappings
            if (AnimationManager.ReplacementMap.TryGetValue("ExtraAttack_T_Clubs", out var tClubsMap))
            {
                foreach (var kvp in tClubsMap)
                {
                    string vanillaName = kvp.Key;
                    string externalName = kvp.Value;
                    // Only add if not already added by Swords (avoid duplicates)
                    string key = $"{vanillaName}_T";
                    if (!mappings.ContainsKey(key))
                    {
                        mappings[key] = externalName;
                    }
                }
            }

            // G Key Swords mappings
            if (AnimationManager.ReplacementMap.TryGetValue("ExtraAttack_G_Swords", out var gSwordsMap))
            {
                foreach (var kvp in gSwordsMap)
                {
                    string vanillaName = kvp.Key;
                    string externalName = kvp.Value;
                    mappings[$"{vanillaName}_G"] = externalName;
                }
            }

            // G Key Clubs mappings
            if (AnimationManager.ReplacementMap.TryGetValue("ExtraAttack_G_Clubs", out var gClubsMap))
            {
                foreach (var kvp in gClubsMap)
                {
                    string vanillaName = kvp.Key;
                    string externalName = kvp.Value;
                    // Only add if not already added by Swords (avoid duplicates)
                    string key = $"{vanillaName}_G";
                    if (!mappings.ContainsKey(key))
                    {
                        mappings[key] = externalName;
                    }
                }
            }

            // Create config entries
            foreach (var kvp in mappings)
            {
                currentConfig.Animations[kvp.Key] = new AnimationTiming
                {
                    HitTiming = 0.45f,
                    TrailOnTiming = 0.35f,
                    TrailOffTiming = 0.70f,
                    ChainTiming = 0.75f,
                    SpeedMultiplier = 1.0f
                };
            }

            SaveConfigWithComments(mappings);
        }

        // Save config with Japanese/English comments and External names
        private static void SaveConfigWithComments(Dictionary<string, string>? mappings = null)
        {
            try
            {
                var sb = new StringBuilder();

                // Header comments
                sb.AppendLine("# ============================================================================");
                sb.AppendLine("# Extra Attack System - Animation Timing Configuration");
                sb.AppendLine("# アニメーションタイミング設定ファイル");
                sb.AppendLine("# ============================================================================");
                sb.AppendLine("# IMPORTANT: Keys use VANILLA clip names with mode suffix (_Q/_T/_G)");
                sb.AppendLine("# 重要: キーはバニラクリップ名 + モードサフィックス (_Q/_T/_G)");
                sb.AppendLine("# ============================================================================");
                sb.AppendLine();
                sb.AppendLine("# Key Format / キー形式:");
                sb.AppendLine("#   VanillaClipName_Q     - Q key (secondary attack)");
                sb.AppendLine("#   VanillaClipName_T     - T key (primary attack 1)");
                sb.AppendLine("#   VanillaClipName_G     - G key (primary attack 2)");
                sb.AppendLine("#   VanillaClipName_Q_hit0 - Q key, first hit (multi-hit)");
                sb.AppendLine("#   VanillaClipName       - Fallback (no mode suffix)");
                sb.AppendLine();
                sb.AppendLine("# ============================================================================");
                sb.AppendLine("# Default Settings");
                sb.AppendLine("# ============================================================================");
                sb.AppendLine("default:");
                sb.AppendLine("  # Animation Event Timing (0.0 ~ 1.0)");
                sb.AppendLine($"  hitTiming: {currentConfig.Default.HitTiming:F2}");
                sb.AppendLine($"  trailOnTiming: {currentConfig.Default.TrailOnTiming:F2}");
                sb.AppendLine($"  trailOffTiming: {currentConfig.Default.TrailOffTiming:F2}");
                sb.AppendLine($"  chainTiming: {currentConfig.Default.ChainTiming:F2}");
                sb.AppendLine($"  speedMultiplier: {currentConfig.Default.SpeedMultiplier:F2}");
                sb.AppendLine();
                sb.AppendLine("  # Attack Detection Parameters");
                sb.AppendLine($"  attackRange: {currentConfig.Default.AttackRange:F2}");
                sb.AppendLine($"  attackHeight: {currentConfig.Default.AttackHeight:F2}");
                sb.AppendLine($"  attackOffset: {currentConfig.Default.AttackOffset:F2}");
                sb.AppendLine($"  attackAngle: {currentConfig.Default.AttackAngle:F2}");
                sb.AppendLine($"  attackRayWidth: {currentConfig.Default.AttackRayWidth:F2}");
                sb.AppendLine($"  attackRayWidthCharExtra: {currentConfig.Default.AttackRayWidthCharExtra:F2}");
                sb.AppendLine($"  attackHeightChar1: {currentConfig.Default.AttackHeightChar1:F2}");
                sb.AppendLine($"  attackHeightChar2: {currentConfig.Default.AttackHeightChar2:F2}");
                sb.AppendLine($"  maxYAngle: {currentConfig.Default.MaxYAngle:F2}");
                sb.AppendLine();
                sb.AppendLine("# ============================================================================");
                sb.AppendLine("# Animation Settings");
                sb.AppendLine("# ============================================================================");
                sb.AppendLine();
                sb.AppendLine("animations:");

                // Sort by mode (Q, T, G), then alphabetically
                var sortedAnims = currentConfig.Animations
                    .OrderBy(kvp => kvp.Key.Contains("_Q") ? 0 : kvp.Key.Contains("_T") ? 1 : kvp.Key.Contains("_G") ? 2 : 3)
                    .ThenBy(kvp => kvp.Key);

                string lastMode = "";
                foreach (var kvp in sortedAnims)
                {
                    string animKey = kvp.Key;
                    var timing = kvp.Value;

                    // Add section header for mode change
                    string currentMode = animKey.Contains("_Q") ? "Q" : animKey.Contains("_T") ? "T" : animKey.Contains("_G") ? "G" : "Other";
                    if (currentMode != lastMode)
                    {
                        sb.AppendLine();
                        sb.AppendLine($"  # ========== {currentMode} Key ==========");
                        lastMode = currentMode;
                    }

                    // Add comment with External name if available
                    if (mappings != null && mappings.TryGetValue(animKey, out string externalName))
                    {
                        sb.AppendLine($"  {animKey}:  # <- {externalName}");
                    }
                    else
                    {
                        sb.AppendLine($"  {animKey}:");
                    }

                    sb.AppendLine($"    hitTiming: {timing.HitTiming:F2}");
                    sb.AppendLine($"    trailOnTiming: {timing.TrailOnTiming:F2}");
                    sb.AppendLine($"    trailOffTiming: {timing.TrailOffTiming:F2}");
                    sb.AppendLine($"    chainTiming: {timing.ChainTiming:F2}");
                    sb.AppendLine($"    speedMultiplier: {timing.SpeedMultiplier:F2}");
                    sb.AppendLine($"    attackRange: {timing.AttackRange:F2}");
                    sb.AppendLine($"    attackHeight: {timing.AttackHeight:F2}");
                    sb.AppendLine($"    attackOffset: {timing.AttackOffset:F2}");
                    sb.AppendLine($"    attackAngle: {timing.AttackAngle:F2}");
                    sb.AppendLine($"    attackRayWidth: {timing.AttackRayWidth:F2}");
                    sb.AppendLine($"    attackRayWidthCharExtra: {timing.AttackRayWidthCharExtra:F2}");
                    sb.AppendLine($"    attackHeightChar1: {timing.AttackHeightChar1:F2}");
                    sb.AppendLine($"    attackHeightChar2: {timing.AttackHeightChar2:F2}");
                    sb.AppendLine($"    maxYAngle: {timing.MaxYAngle:F2}");
                }

                File.WriteAllText(ConfigFilePath, sb.ToString(), Encoding.UTF8);
                ExtraAttackPlugin.LogInfo("System", $"Saved AnimationTiming.yaml with {currentConfig.Animations.Count} animations");
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error saving config: {ex.Message}");
            }
        }

        // Load config from YAML
        private static void LoadConfig()
        {
            try
            {
                string yaml = File.ReadAllText(ConfigFilePath, Encoding.UTF8);

                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .IgnoreUnmatchedProperties()
                    .Build();

                currentConfig = deserializer.Deserialize<TimingConfig>(yaml) ?? new TimingConfig();
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error loading config, using defaults: {ex.Message}");
                currentConfig = new TimingConfig();
            }
        }

        // Get timing for specific animation
        public static AnimationTiming GetTiming(string animationName)
        {
            if (currentConfig.Animations.TryGetValue(animationName, out var timing))
            {
                return timing;
            }
            return currentConfig.Default;
        }

        // Check if specific animation config exists
        public static bool HasConfig(string animationName)
        {
            return currentConfig.Animations.ContainsKey(animationName);
        }

        // Reload config (for runtime changes)
        public static void Reload()
        {
            if (File.Exists(ConfigFilePath))
            {
                LoadConfig();
                ExtraAttackPlugin.LogInfo("System", "Reloaded AnimationTiming.yaml");
            }
        }
    }
}