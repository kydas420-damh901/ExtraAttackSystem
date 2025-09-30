using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ExtraAttackSystem
{
    public static class AnimationManager
    {
        // AssetBundle and Animation system
        private static AssetBundle? asset;
        public static readonly Dictionary<string, Dictionary<string, string>> ReplacementMap = new();
        public static readonly Dictionary<string, AnimationClip> ExternalAnimations = new();
        public static readonly Dictionary<string, RuntimeAnimatorController> CustomRuntimeControllers = new();

        // Cache the field info for better performance
        private static FieldInfo? animatorFieldCache;

        public static void LoadAssets()
        {
            try
            {
                // Load AssetBundle
                asset = GetAssetBundle("extraattack");
                if (asset != null)
                {
                    // Load animation clips
                    var animationClips = asset.LoadAllAssets<AnimationClip>();
                    ExtraAttackPlugin.ExtraAttackLogger.LogInfo($"Found {animationClips.Length} animation clips in AssetBundle");

                    foreach (var clip in animationClips)
                    {
                        string externalName = clip.name + "External";
                        ExternalAnimations[externalName] = clip;
                        ExtraAttackPlugin.ExtraAttackLogger.LogInfo($"Loaded animation: {clip.name} -> {externalName}");
                    }

                    ExtraAttackPlugin.ExtraAttackLogger.LogInfo($"Successfully loaded {animationClips.Length} animations from AssetBundle");
                    ExtraAttackPlugin.ExtraAttackLogger.LogInfo($"ExternalAnimations dictionary now has {ExternalAnimations.Count} entries");
                    AnimationEventManager.AddEventsToExternalAnimations();
                }
                else
                {
                    ExtraAttackPlugin.ExtraAttackLogger.LogWarning("AssetBundle not found, using fallback animations");
                }

                // Initialize animation replacement maps
                InitializeAnimationMaps();
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.ExtraAttackLogger.LogError($"Error loading assets: {ex.Message}");
                ExtraAttackPlugin.ExtraAttackLogger.LogError($"Stack trace: {ex.StackTrace}");
            }
        }

        public static void UnloadAssets()
        {
            if (asset != null)
            {
                asset.Unload(false);
                asset = null;
            }
        }

        private static AssetBundle? GetAssetBundle(string filename)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string? resourceName = assembly.GetManifestResourceNames().FirstOrDefault(str => str.EndsWith(filename));

            if (resourceName != null)
            {
                using Stream? stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    return AssetBundle.LoadFromStream(stream);
                }
            }

            // Try to load from plugin directory
            string? pluginPath = Path.GetDirectoryName(assembly.Location);
            if (pluginPath != null)
            {
                string assetBundlePath = Path.Combine(pluginPath, filename);

                if (File.Exists(assetBundlePath))
                {
                    return AssetBundle.LoadFromFile(assetBundlePath);
                }
            }

            return null;
        }

        private static void InitializeAnimationMaps()
        {
            // Initialize replacement map for extra attacks
            ReplacementMap["ExtraAttack"] = new Dictionary<string, string>();

            // Only add mappings if we have external animations loaded
            if (ExternalAnimations.Count > 0)
            {
                // Debug: Log all loaded animation names
                ExtraAttackPlugin.ExtraAttackLogger.LogInfo("Available animations:");
                foreach (var anim in ExternalAnimations.Keys)
                {
                    ExtraAttackPlugin.ExtraAttackLogger.LogInfo($"  - {anim}");
                }

                // Map AnimationClip NAMES (not state names!) from DualWield reference
                if (ExternalAnimations.ContainsKey("Great Sword Slash_40External"))
                {
                    // Swords - AnimationClip name from DualWield: "Sword-Attack-R4"
                    ReplacementMap["ExtraAttack"]["Sword-Attack-R4"] = "Great Sword Slash_40External";
                    // Greatswords - AnimationClip name: "Greatsword Secondary Attack"
                    ReplacementMap["ExtraAttack"]["Greatsword Secondary Attack"] = "Great Sword Slash_40External";
                    // Axes - AnimationClip name from DualWield: "Axe Secondary Attack"
                    ReplacementMap["ExtraAttack"]["Axe Secondary Attack"] = "Great Sword Slash_40External";
                    // Maces/Clubs - AnimationClip name from DualWield: "MaceAltAttack"
                    ReplacementMap["ExtraAttack"]["MaceAltAttack"] = "Great Sword Slash_40External";
                    // Knives - AnimationClip name from DualWield: "Knife JumpAttack"
                    ReplacementMap["ExtraAttack"]["Knife JumpAttack"] = "Great Sword Slash_40External";

                    ExtraAttackPlugin.ExtraAttackLogger.LogInfo("Mapped AnimationClip names for weapon types");
                }
                else if (ExternalAnimations.ContainsKey("Great Sword Jump AttackExternal"))
                {
                    // Fallback
                    ReplacementMap["ExtraAttack"]["Sword-Attack-R4"] = "Great Sword Jump AttackExternal";
                    ReplacementMap["ExtraAttack"]["Greatsword Secondary Attack"] = "Great Sword Jump AttackExternal";
                    ReplacementMap["ExtraAttack"]["Axe Secondary Attack"] = "Great Sword Jump AttackExternal";
                    ReplacementMap["ExtraAttack"]["MaceAltAttack"] = "Great Sword Jump AttackExternal";
                    ReplacementMap["ExtraAttack"]["Knife JumpAttack"] = "Great Sword Jump AttackExternal";

                    ExtraAttackPlugin.ExtraAttackLogger.LogInfo("Mapped fallback AnimationClip names");
                }

                ExtraAttackPlugin.ExtraAttackLogger.LogInfo($"Animation replacement maps initialized with {ReplacementMap["ExtraAttack"].Count} mappings");
            }
            else
            {
                ExtraAttackPlugin.ExtraAttackLogger.LogInfo("No external animations found - using fallback mode");
            }
        }

        // AnimatorOverrideController creation (DualWield style) - with detailed debug
        public static RuntimeAnimatorController MakeAOC(Dictionary<string, string> replacement, RuntimeAnimatorController original)
        {
            AnimatorOverrideController aoc = new(original);
            List<KeyValuePair<AnimationClip, AnimationClip>> anims = new();

            ExtraAttackPlugin.ExtraAttackLogger.LogInfo($"MakeAOC: Processing {aoc.animationClips.Length} clips");
            ExtraAttackPlugin.ExtraAttackLogger.LogInfo($"MakeAOC: Replacement map has {replacement.Count} entries");

            // DEBUG: Animation Events
            if (ExtraAttackPlugin.DebugAnimationEvents.Value && replacement.Count > 0)
            {
                ExtraAttackPlugin.ExtraAttackLogger.LogInfo("=== DEBUG: ANIMATION EVENTS (ALL CLIPS) ===");
                foreach (AnimationClip animation in aoc.animationClips)
                {
                    string name = animation.name;
                    AnimationEvent[] events = animation.events;
                    if (events != null && events.Length > 0)
                    {
                        ExtraAttackPlugin.ExtraAttackLogger.LogInfo($"--- [{name}] has {events.Length} event(s) ---");
                        foreach (var evt in events)
                        {
                            ExtraAttackPlugin.ExtraAttackLogger.LogInfo(
                                $"  Event: time={evt.time:F3}, function={evt.functionName}, " +
                                $"int={evt.intParameter}, float={evt.floatParameter:F3}, string={evt.stringParameter}");
                        }
                    }
                }
                ExtraAttackPlugin.ExtraAttackLogger.LogInfo("=== END ANIMATION EVENTS ===");
            }

            // DEBUG: Animation Clips
            if (ExtraAttackPlugin.DebugAnimationClips.Value && replacement.Count > 0)
            {
                ExtraAttackPlugin.ExtraAttackLogger.LogInfo("=== DEBUG: ANIMATION CLIPS (ALL) ===");
                foreach (AnimationClip animation in aoc.animationClips)
                {
                    ExtraAttackPlugin.ExtraAttackLogger.LogInfo(
                        $"  Clip: [{animation.name}] - Length: {animation.length:F3}s, " +
                        $"FrameRate: {animation.frameRate}, Legacy: {animation.legacy}, Events: {animation.events.Length}");
                }
                ExtraAttackPlugin.ExtraAttackLogger.LogInfo("=== END ANIMATION CLIPS ===");
            }

            foreach (AnimationClip animation in aoc.animationClips)
            {
                string name = animation.name;
                if (replacement.TryGetValue(name, out string value) && ExternalAnimations.ContainsKey(value))
                {
                    AnimationClip newClip = UnityEngine.Object.Instantiate(ExternalAnimations[value]);
                    newClip.name = name;
                    anims.Add(new KeyValuePair<AnimationClip, AnimationClip>(animation, newClip));
                    ExtraAttackPlugin.ExtraAttackLogger.LogInfo($"Animation override SUCCESS: {name} -> {value}");
                }
                else
                {
                    anims.Add(new KeyValuePair<AnimationClip, AnimationClip>(animation, animation));
                    if (replacement.ContainsKey(name))
                    {
                        ExtraAttackPlugin.ExtraAttackLogger.LogWarning($"Animation override FAILED: {name} -> {replacement[name]} (External animation not found)");
                    }
                    else if (name.Contains("Secondary") || name.Contains("secondary"))
                    {
                        // DEBUG: Log secondary clips that are NOT in replacement map
                        ExtraAttackPlugin.ExtraAttackLogger.LogInfo($"Secondary clip NOT in replacement map: [{name}]");
                    }
                }
            }

            aoc.ApplyOverrides(anims);
            ExtraAttackPlugin.ExtraAttackLogger.LogInfo($"Applied {anims.Count} animation overrides to AnimatorOverrideController");
            return aoc;
        }

        // Direct Animator access - Using Reflection for runtime compatibility
        public static Animator? GetPlayerAnimator(Player player)
        {
            try
            {
                // Cache the field info for better performance
                if (animatorFieldCache == null)
                {
                    animatorFieldCache = typeof(Character).GetField("m_animator",
                        BindingFlags.NonPublic | BindingFlags.Instance);

                    if (animatorFieldCache == null)
                    {
                        ExtraAttackPlugin.ExtraAttackLogger.LogError("Could not find m_animator field via reflection");
                        return null;
                    }
                }

                return animatorFieldCache.GetValue(player) as Animator;
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.ExtraAttackLogger.LogError($"Failed to get animator via reflection: {ex.Message}");
                return null;
            }
        }

        // Fast RuntimeAnimatorController replacement (safe approach)
        public static void FastReplaceRAC(Player player, RuntimeAnimatorController replace)
        {
            try
            {
                var animator = GetPlayerAnimator(player);
                if (animator == null || replace == null)
                {
                    ExtraAttackPlugin.ExtraAttackLogger.LogWarning("Animator or replacement controller is null");
                    return;
                }

                if (animator.runtimeAnimatorController == replace)
                {
                    return;
                }

                animator.runtimeAnimatorController = replace;
                animator.Update(Time.deltaTime);
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.ExtraAttackLogger.LogError($"Error in FastReplaceRAC: {ex.Message}");
            }
        }

        public static string GetExtraAttackAnimationTrigger(ItemDrop.ItemData weapon)
        {
            // Return dedicated animation trigger per weapon type (expandable for future)
            return weapon.m_shared.m_skillType switch
            {
                Skills.SkillType.Swords => "sword_secondary",      // Sword specific extra attack
                Skills.SkillType.Axes => "axe_secondary",         // Axe specific extra attack
                Skills.SkillType.Clubs => "mace_secondary",       // Mace specific extra attack
                Skills.SkillType.Knives => "knife_secondary",     // Knife specific extra attack
                Skills.SkillType.Spears => "spear_secondary",     // Spear specific extra attack
                Skills.SkillType.Bows => "sword_secondary",       // Bow fallback
                _ => "sword_secondary"                             // Default
            };
        }

        // Output debug info on demand (when config is changed)
        public static void OutputDebugInfoOnDemand(Animator animator)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                ExtraAttackPlugin.ExtraAttackLogger.LogError("Cannot output debug info: Invalid animator");
                return;
            }

            AnimatorOverrideController? aoc = animator.runtimeAnimatorController as AnimatorOverrideController;
            if (aoc == null)
            {
                // If not an override controller, create a temporary one to access clips
                aoc = new AnimatorOverrideController(animator.runtimeAnimatorController);
            }

            // Output Animation Parameters
            if (ExtraAttackPlugin.DebugAnimationParameters.Value)
            {
                ExtraAttackPlugin.ExtraAttackLogger.LogInfo("=== DEBUG: ANIMATOR PARAMETERS (ON DEMAND) ===");
                AnimatorControllerParameter[] parameters = animator.parameters;
                ExtraAttackPlugin.ExtraAttackLogger.LogInfo($"Total parameters: {parameters.Length}");

                foreach (var param in parameters)
                {
                    string typeStr = param.type switch
                    {
                        AnimatorControllerParameterType.Float => "Float",
                        AnimatorControllerParameterType.Int => "Int",
                        AnimatorControllerParameterType.Bool => "Bool",
                        AnimatorControllerParameterType.Trigger => "Trigger",
                        _ => "Unknown"
                    };

                    ExtraAttackPlugin.ExtraAttackLogger.LogInfo($"  {typeStr.PadRight(10)} | {param.name}");
                }
                ExtraAttackPlugin.ExtraAttackLogger.LogInfo("=== END ANIMATOR PARAMETERS ===");
            }

            // Output Animation Events
            if (ExtraAttackPlugin.DebugAnimationEvents.Value)
            {
                ExtraAttackPlugin.ExtraAttackLogger.LogInfo("=== DEBUG: ANIMATION EVENTS (ON DEMAND) ===");
                foreach (AnimationClip animation in aoc.animationClips)
                {
                    string name = animation.name;
                    AnimationEvent[] events = animation.events;
                    if (events != null && events.Length > 0)
                    {
                        ExtraAttackPlugin.ExtraAttackLogger.LogInfo($"--- [{name}] has {events.Length} event(s) ---");
                        foreach (var evt in events)
                        {
                            ExtraAttackPlugin.ExtraAttackLogger.LogInfo(
                                $"  Event: time={evt.time:F3}, function={evt.functionName}, " +
                                $"int={evt.intParameter}, float={evt.floatParameter:F3}, string={evt.stringParameter}");
                        }
                    }
                }
                ExtraAttackPlugin.ExtraAttackLogger.LogInfo("=== END ANIMATION EVENTS ===");
            }

            // Output Animation Clips
            if (ExtraAttackPlugin.DebugAnimationClips.Value)
            {
                ExtraAttackPlugin.ExtraAttackLogger.LogInfo("=== DEBUG: ANIMATION CLIPS (ON DEMAND) ===");
                ExtraAttackPlugin.ExtraAttackLogger.LogInfo($"Total clips: {aoc.animationClips.Length}");
                foreach (AnimationClip animation in aoc.animationClips)
                {
                    ExtraAttackPlugin.ExtraAttackLogger.LogInfo(
                        $"  Clip: [{animation.name}] - Length: {animation.length:F3}s, " +
                        $"FrameRate: {animation.frameRate}, Legacy: {animation.legacy}, Events: {animation.events.Length}");
                }
                ExtraAttackPlugin.ExtraAttackLogger.LogInfo("=== END ANIMATION CLIPS ===");
            }
        }
    }
}