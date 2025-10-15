using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ExtraAttackSystem
{
    public static class EAS_AnimationManager
    {
        // AssetBundle and Animation system
        private static AssetBundle? asset;
        public static readonly Dictionary<string, Dictionary<string, string>> AnimationReplacementMap = new();
        public static readonly Dictionary<string, AnimationClip> CustomAnimationClips = new();
        public static readonly Dictionary<string, UnityEngine.RuntimeAnimatorController> AnimatorControllerCache = new();
        
        // Cache for external clip lengths
        private static readonly Dictionary<string, float> CustomClipLengthCache = new();
        
        // Default clip lengths for common animations (based on actual vanilla values)
        public static readonly Dictionary<string, float> VanillaClipLengths = new Dictionary<string, float>
        {
            // Sword animations
            { "Sword-Attack-R4", 1.4f },
           
            // Great Sword animations
            { "Greatsword Secondary Attack", 1.4f },
            
            // Axe animations
            { "Axe Secondary Attack", 1.4f },
            
            // Battle Axe animations
            { "BattleAxeAltAttack", 0.857f },
            
            // Club/Mace animations
            { "MaceAltAttack", 1.4f },
            
            // Spear animations
            { "throw_spear", 1.133f },
            
            // Polearm animations
            { "Atgeir360Attack", 2.167f },
            
            // Knife animations
            { "Knife JumpAttack", 1.4f },
            
            // Unarmed animations
            { "Kickstep", 0.9f },
            
            // Dual knives
            { "Knife Attack Leap", 1.5f },
            // Dual axes
            { "DualAxes Attack Cleave", 1.9f },
        };

        public static string Initialize()
        {
            try
            {
                // Clear any existing data
                AnimationReplacementMap.Clear();
                CustomAnimationClips.Clear();
                AnimatorControllerCache.Clear();
                CustomClipLengthCache.Clear();

                string assetBundlePath = LoadAssets();
                CreateDefaultWeaponTypeMappings();
                ExtraAttackSystemPlugin.LogInfo("System", "EAS_AnimationManager initialized successfully");
                return assetBundlePath;
            }
            catch (Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error initializing EAS_AnimationManager: {ex.Message}");
                return "";
            }
        }

        public static string LoadAssets()
        {
            try
            {
                // Get asset bundle path first
                string assetBundlePath = GetAssetBundlePath("extraattacksystem");
                if (string.IsNullOrEmpty(assetBundlePath))
                {
                    ExtraAttackSystemPlugin.LogWarning("System", "AssetBundle path not found");
                    return "";
                }

                // Load AssetBundle
                asset = AssetBundle.LoadFromFile(assetBundlePath);
                if (asset != null)
                {
                    // Load animation clips
                    var animationClips = asset.LoadAllAssets<AnimationClip>();
                    if (ExtraAttackSystemPlugin.IsDebugSystemMessagesEnabled)
                    {
                        ExtraAttackSystemPlugin.LogInfo("System", $"Found {animationClips.Length} animation clips in AssetBundle");
                    }

                    foreach (var clip in animationClips)
                    {
                        string externalName = clip.name + "External";
                        CustomAnimationClips[externalName] = clip;
                        
                        if (ExtraAttackSystemPlugin.IsDebugSystemMessagesEnabled)
                        {
                            ExtraAttackSystemPlugin.LogInfo("System", $"Loaded animation: {clip.name} -> {externalName}");
                        }
                    }

                    ExtraAttackSystemPlugin.LogInfo("System", $"Successfully loaded {CustomAnimationClips.Count} animations from AssetBundle");
                    ExtraAttackSystemPlugin.LogInfo("System", $"CustomAnimationClips dictionary now has {CustomAnimationClips.Count} entries");
                    return assetBundlePath;
                }
                else
                {
                    ExtraAttackSystemPlugin.LogWarning("System", "AssetBundle not found or failed to load");
                    return "";
                }
            }
            catch (Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error loading assets: {ex.Message}");
                return "";
            }
        }

        private static string GetAssetBundlePath(string bundleName)
        {
            string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "Resources", bundleName);
            return File.Exists(path) ? path : "";
        }


        // Create default weapon type mappings
        private static void CreateDefaultWeaponTypeMappings()
        {
            try
            {
                // Define weapon types
                string[] weaponTypes = { "Sword", "Axe", "Club", "Spear", "Knife", "Greatsword", "Battleaxe", "Polearm", "Fist" };
                
                // Get available animations from CustomAnimationClips
                var availableAnimations = CustomAnimationClips.Keys.ToList();
                int animationIndex = 0;
                
                foreach (var weaponType in weaponTypes)
                {
                    if (!AnimationReplacementMap.ContainsKey(weaponType))
                    {
                        AnimationReplacementMap[weaponType] = new Dictionary<string, string>();
                    }
                    
                    // Add Q, T, G mappings for each weapon type using unified key format
                    if (animationIndex < availableAnimations.Count)
                        AnimationReplacementMap[weaponType]["secondary_Q"] = availableAnimations[animationIndex++];
                    if (animationIndex < availableAnimations.Count)
                        AnimationReplacementMap[weaponType]["secondary_T"] = availableAnimations[animationIndex++];
                    if (animationIndex < availableAnimations.Count)
                        AnimationReplacementMap[weaponType]["secondary_G"] = availableAnimations[animationIndex++];
                    
                    ExtraAttackSystemPlugin.LogInfo("System", $"CreateDefaultWeaponTypeMappings: Added {weaponType} with Q/T/G mappings");
                }
                
                ExtraAttackSystemPlugin.LogInfo("System", "CreateDefaultWeaponTypeMappings: Default weapon type mappings created successfully");
            }
            catch (Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error creating default weapon type mappings: {ex.Message}");
            }
        }

        // Get external clip length with smart caching
        public static float GetExternalClipLengthSmart(string clipName)
        {
            if (string.IsNullOrEmpty(clipName))
                return 0f;

            // Check cache first
            if (CustomClipLengthCache.TryGetValue(clipName, out float cachedLength))
            {
                return cachedLength;
            }

            // Try to get from CustomAnimationClips
            if (CustomAnimationClips.TryGetValue(clipName, out var clip))
            {
                float length = clip.length;
                CustomClipLengthCache[clipName] = length;
                return length;
            }

            // Try vanilla clip lengths
            if (VanillaClipLengths.TryGetValue(clipName, out float vanillaLength))
            {
                CustomClipLengthCache[clipName] = vanillaLength;
                return vanillaLength;
            }

            // Fallback
            CustomClipLengthCache[clipName] = 1.0f;
            return 1.0f;
        }

        // Get animation clip by name
        public static AnimationClip? GetAnimationClip(string clipName)
        {
            if (string.IsNullOrEmpty(clipName))
                return null;

            if (CustomAnimationClips.TryGetValue(clipName, out var clip))
            {
                return clip;
            }

            return null;
        }

        // Check if animation exists
        public static bool HasAnimation(string clipName)
        {
            return !string.IsNullOrEmpty(clipName) && CustomAnimationClips.ContainsKey(clipName);
        }

        // Get custom animation name for weapon type and mode
        public static string? GetCustomAnimationName(string weaponType, string mode)
        {
            if (AnimationReplacementMap.TryGetValue(weaponType, out var weaponMappings))
            {
                if (weaponMappings.TryGetValue(mode, out var customAnimName))
                {
                    return customAnimName;
                }
            }
            return null;
        }

        // Apply AOC for weapon type (weapon change only)
        public static void ApplyAOCForWeapon(Player player, string weaponType)
        {
            try
            {
                if (player == null || string.IsNullOrEmpty(weaponType))
                {
                    ExtraAttackSystemPlugin.LogWarning("System", "ApplyAOCForWeapon: player or weaponType is null");
                    return;
                }

                // Try multiple methods to find animator
                var animator = GetPlayerAnimator(player);
                if (animator == null)
                {
                    ExtraAttackSystemPlugin.LogWarning("System", "ApplyAOCForWeapon: animator not found with any method");
                    return;
                }

                // Check if we have animations for this weapon type
                if (!AnimationReplacementMap.TryGetValue(weaponType, out var weaponMappings))
                {
                    ExtraAttackSystemPlugin.LogWarning("System", $"ApplyAOCForWeapon: No animations found for weapon type {weaponType}");
                    return;
                }

                // Get or create AOC from cache
                AnimatorOverrideController? aoc = null;
                if (AnimatorControllerCache.TryGetValue($"{weaponType}_aoc", out var cachedController))
                {
                    aoc = cachedController as AnimatorOverrideController;
                    ExtraAttackSystemPlugin.LogInfo("System", $"ApplyAOCForWeapon: Using cached AOC for {weaponType}");
                }
                
                if (aoc == null)
                {
                    // Create new AOC
                    aoc = CreateAOCForWeapon(animator, weaponType, weaponMappings);
                    if (aoc == null)
                    {
                        ExtraAttackSystemPlugin.LogWarning("System", $"ApplyAOCForWeapon: Failed to create AOC for {weaponType}");
                        return;
                    }
                }

                // Store original controller if not already stored
                if (animator.runtimeAnimatorController != null && 
                    !AnimatorControllerCache.ContainsKey($"{weaponType}_original"))
                {
                    AnimatorControllerCache[$"{weaponType}_original"] = animator.runtimeAnimatorController;
                }

                // Apply AOC (weapon change only)
                animator.runtimeAnimatorController = aoc;
                
                // AOC最適化: 1フレーム表示問題解決
                animator.Play("Idle", 0, 0f);
                
                ExtraAttackSystemPlugin.LogInfo("System", $"ApplyAOCForWeapon: Applied AOC for {weaponType} with optimization");
            }
            catch (Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error applying AOC for weapon {weaponType}: {ex.Message}");
            }
        }

        // Get player animator using multiple methods
        public static Animator? GetPlayerAnimator(Player player)
        {
            try
            {
                // Method 1: Direct component
                var animator = player.GetComponent<Animator>();
                if (animator != null)
                {
                    ExtraAttackSystemPlugin.LogInfo("System", "GetPlayerAnimator: Found animator on player directly");
                    return animator;
                }

                // Method 2: In children
                animator = player.GetComponentInChildren<Animator>();
                if (animator != null)
                {
                    ExtraAttackSystemPlugin.LogInfo("System", "GetPlayerAnimator: Found animator in children");
                    return animator;
                }

                // Method 3: Look for model object
                var modelTransform = player.transform.Find("Model");
                if (modelTransform != null)
                {
                    animator = modelTransform.GetComponent<Animator>();
                    if (animator != null)
                    {
                        ExtraAttackSystemPlugin.LogInfo("System", "GetPlayerAnimator: Found animator on Model");
                        return animator;
                    }
                }

                // Method 4: Search all children recursively
                animator = FindAnimatorRecursive(player.transform);
                if (animator != null)
                {
                    ExtraAttackSystemPlugin.LogInfo("System", "GetPlayerAnimator: Found animator recursively");
                    return animator;
                }

                ExtraAttackSystemPlugin.LogWarning("System", "GetPlayerAnimator: No animator found with any method");
                return null;
            }
            catch (Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error getting player animator: {ex.Message}");
                return null;
            }
        }

        // Recursively find animator in transform hierarchy
        private static Animator? FindAnimatorRecursive(Transform parent)
        {
            try
            {
                // Check current transform
                var animator = parent.GetComponent<Animator>();
                if (animator != null) return animator;

                // Check all children
                for (int i = 0; i < parent.childCount; i++)
                {
                    var childAnimator = FindAnimatorRecursive(parent.GetChild(i));
                    if (childAnimator != null) return childAnimator;
                }

                return null;
            }
            catch (Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error in recursive animator search: {ex.Message}");
                return null;
            }
        }


        // Create AOC for weapon type
        private static AnimatorOverrideController? CreateAOCForWeapon(Animator animator, string weaponType, Dictionary<string, string> weaponMappings)
        {
            try
            {
                if (animator.runtimeAnimatorController == null)
                {
                    ExtraAttackSystemPlugin.LogWarning("System", "CreateAOCForWeapon: No original controller found");
                    return null;
                }

                // Create AnimatorOverrideController
                var aoc = new AnimatorOverrideController(animator.runtimeAnimatorController);
                aoc.name = $"AOC_{weaponType}";

                // AOC created with empty replacementMap - animations will be replaced only on QTG attacks
                ExtraAttackSystemPlugin.LogInfo("System", $"CreateAOCForWeapon: Created empty AOC for {weaponType}");

                // Cache the AOC
                AnimatorControllerCache[$"{weaponType}_aoc"] = aoc;

                ExtraAttackSystemPlugin.LogInfo("System", $"CreateAOCForWeapon: Created AOC for {weaponType}");
                return aoc;
            }
            catch (Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error creating AOC for {weaponType}: {ex.Message}");
                return null;
            }
        }

        // Get replacement animation name
        private static string GetReplacementAnimation(string originalClipName, string weaponType, Dictionary<string, string> weaponMappings)
        {
            try
            {
                // Map vanilla animation names to our custom animations
                if (originalClipName.Contains("Attack") || originalClipName.Contains("Combo") || originalClipName.Contains("Secondary"))
                {
                    // Get current attack mode
                    var currentMode = EAS_InputHandler.GetAttackMode(Player.m_localPlayer);
                    if (currentMode != EAS_InputHandler.AttackMode.Normal)
                    {
                        if (weaponMappings.TryGetValue(currentMode.ToString(), out string customAnimation))
                        {
                            ExtraAttackSystemPlugin.LogInfo("System", $"GetReplacementAnimation: {originalClipName} -> {customAnimation} for {currentMode}");
                            return customAnimation;
                        }
                    }
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error getting replacement animation: {ex.Message}");
                return string.Empty;
            }
        }

        // Get animation clip by name (private version for AOC)
        private static AnimationClip? GetAnimationClipForAOC(string clipName)
        {
            try
            {
                // Check custom clips first
                if (CustomAnimationClips.TryGetValue(clipName, out var customClip))
                {
                    return customClip;
                }

                // Load from resources if not cached
                var clip = Resources.Load<AnimationClip>(clipName);
                if (clip != null)
                {
                    CustomAnimationClips[clipName] = clip;
                    ExtraAttackSystemPlugin.LogInfo("System", $"GetAnimationClipForAOC: Loaded {clipName} from resources");
                    return clip;
                }

                ExtraAttackSystemPlugin.LogWarning("System", $"GetAnimationClipForAOC: Animation clip not found: {clipName}");
                return null;
            }
            catch (Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error getting animation clip {clipName}: {ex.Message}");
                return null;
            }
        }

        // Initialize default animation mappings
        public static void InitializeDefaultMappings()
        {
            try
            {
                ExtraAttackSystemPlugin.LogInfo("System", "InitializeDefaultMappings: Setting up default animation mappings");

                // Sword mappings
                AnimationReplacementMap["Sword"] = new Dictionary<string, string>
                {
                    { "secondary_Q", "Sword_ExtraAttackQ" },
                    { "secondary_T", "Sword_ExtraAttackT" },
                    { "secondary_G", "Sword_ExtraAttackG" }
                };

                // Greatsword mappings
                AnimationReplacementMap["Greatsword"] = new Dictionary<string, string>
                {
                    { "secondary_Q", "Greatsword_ExtraAttackQ" },
                    { "secondary_T", "Greatsword_ExtraAttackT" },
                    { "secondary_G", "Greatsword_ExtraAttackG" }
                };

                // Axe mappings
                AnimationReplacementMap["Axe"] = new Dictionary<string, string>
                {
                    { "secondary_Q", "Axe_ExtraAttackQ" },
                    { "secondary_T", "Axe_ExtraAttackT" },
                    { "secondary_G", "Axe_ExtraAttackG" }
                };

                // Club mappings
                AnimationReplacementMap["Club"] = new Dictionary<string, string>
                {
                    { "secondary_Q", "Club_ExtraAttackQ" },
                    { "secondary_T", "Club_ExtraAttackT" },
                    { "secondary_G", "Club_ExtraAttackG" }
                };

                // Spear mappings
                AnimationReplacementMap["Spear"] = new Dictionary<string, string>
                {
                    { "secondary_Q", "Spear_ExtraAttackQ" },
                    { "secondary_T", "Spear_ExtraAttackT" },
                    { "secondary_G", "Spear_ExtraAttackG" }
                };

                // Knife mappings
                AnimationReplacementMap["Knife"] = new Dictionary<string, string>
                {
                    { "secondary_Q", "Knife_ExtraAttackQ" },
                    { "secondary_T", "Knife_ExtraAttackT" },
                    { "secondary_G", "Knife_ExtraAttackG" }
                };

                ExtraAttackSystemPlugin.LogInfo("System", $"InitializeDefaultMappings: Created mappings for {AnimationReplacementMap.Count} weapon types");
            }
            catch (Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error initializing default mappings: {ex.Message}");
            }
        }


        // Clear AOC cache
        public static void ClearAOCCache()
        {
            try
            {
                foreach (var controller in AnimatorControllerCache.Values)
                {
                    if (controller != null && !(controller is AnimatorOverrideController))
                    {
                        // Don't destroy original controllers
                        continue;
                    }
                }
                
                AnimatorControllerCache.Clear();
                ExtraAttackSystemPlugin.LogInfo("System", "ClearAOCCache: AOC cache cleared");
            }
            catch (Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error clearing AOC cache: {ex.Message}");
            }
        }

        // Get AOC for weapon type
        public static AnimatorOverrideController? GetAOCForWeapon(string weaponType)
        {
            try
            {
                if (AnimatorControllerCache.TryGetValue($"{weaponType}_aoc", out var controller))
                {
                    return controller as AnimatorOverrideController;
                }
                return null;
            }
            catch (Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error getting AOC for weapon {weaponType}: {ex.Message}");
                return null;
            }
        }

        // Check if AOC exists for weapon
        public static bool HasAOCForWeapon(string weaponType)
        {
            try
            {
                return AnimatorControllerCache.ContainsKey($"{weaponType}_aoc");
            }
            catch (Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error checking AOC for weapon {weaponType}: {ex.Message}");
                return false;
            }
        }

        // Force AOC refresh for weapon
        public static void RefreshAOCForWeapon(Player player, string weaponType)
        {
            try
            {
                if (player == null || string.IsNullOrEmpty(weaponType)) return;

                // Clear existing AOC
                if (AnimatorControllerCache.ContainsKey($"{weaponType}_aoc"))
                {
                    AnimatorControllerCache.Remove($"{weaponType}_aoc");
                }

                // Recreate AOC
                ApplyAOCForWeapon(player, weaponType);
                ExtraAttackSystemPlugin.LogInfo("System", $"RefreshAOCForWeapon: Refreshed AOC for {weaponType}");
            }
            catch (Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error refreshing AOC for weapon {weaponType}: {ex.Message}");
            }
        }

        // Restore original controller
        public static void RestoreOriginalController(Player player, string weaponType)
        {
            try
            {
                if (player == null) return;

                var animator = GetPlayerAnimator(player);
                if (animator == null) return;

                // Get original controller
                if (AnimatorControllerCache.TryGetValue($"{weaponType}_original", out var originalController))
                {
                    animator.runtimeAnimatorController = originalController;
                    ExtraAttackSystemPlugin.LogInfo("System", $"RestoreOriginalController: Restored original controller for {weaponType}");
                }
                else
                {
                    ExtraAttackSystemPlugin.LogWarning("System", $"RestoreOriginalController: No original controller found for {weaponType}");
                }
            }
            catch (Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error restoring original controller: {ex.Message}");
            }
        }


        // Pre-cache all AOCs for all weapon types at startup
        public static void PreCacheAllAOCs()
        {
            try
            {
                ExtraAttackSystemPlugin.LogInfo("System", "PreCacheAllAOCs: Starting AOC pre-caching");
                
                // Need to get original controller first - we'll do this when first player is available
                // For now, just log that we're ready to cache
                ExtraAttackSystemPlugin.LogInfo("System", $"PreCacheAllAOCs: Ready to cache AOCs for {AnimationReplacementMap.Count} weapon types");
            }
            catch (Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error pre-caching AOCs: {ex.Message}");
            }
        }

        // Update AOC replacementMap for specific mode (no AOC re-application)
        public static void UpdateAOCForMode(Player player, string weaponType, string mode)
        {
            try
            {
                ExtraAttackSystemPlugin.LogInfo("System", $"UpdateAOCForMode: Called with {weaponType}, {mode}");
                
                if (player == null || string.IsNullOrEmpty(weaponType) || string.IsNullOrEmpty(mode))
                {
                    ExtraAttackSystemPlugin.LogWarning("System", "UpdateAOCForMode: Invalid parameters");
                    return;
                }

                var animator = GetPlayerAnimator(player);
                if (animator == null)
                {
                    ExtraAttackSystemPlugin.LogWarning("System", "UpdateAOCForMode: Animator not found");
                    return;
                }

                ExtraAttackSystemPlugin.LogInfo("System", $"UpdateAOCForMode: Animator found, controller type: {animator.runtimeAnimatorController?.GetType().Name}");

                // Get current AOC
                var aoc = animator.runtimeAnimatorController as AnimatorOverrideController;
                if (aoc == null)
                {
                    ExtraAttackSystemPlugin.LogWarning("System", "UpdateAOCForMode: No AOC currently applied");
                    return;
                }

                ExtraAttackSystemPlugin.LogInfo("System", $"UpdateAOCForMode: AOC found: {aoc.name}");

                // Get animation mappings for this weapon type
                if (!AnimationReplacementMap.TryGetValue(weaponType, out var weaponMappings))
                {
                    ExtraAttackSystemPlugin.LogWarning("System", $"UpdateAOCForMode: No mappings found for {weaponType}");
                    return;
                }

                ExtraAttackSystemPlugin.LogInfo("System", $"UpdateAOCForMode: Found {weaponMappings.Count} mappings for {weaponType}");

                // Get custom animation name for this mode
                if (!weaponMappings.TryGetValue(mode, out var customAnimName))
                {
                    ExtraAttackSystemPlugin.LogWarning("System", $"UpdateAOCForMode: No animation found for {weaponType}.{mode}");
                    ExtraAttackSystemPlugin.LogInfo("System", $"UpdateAOCForMode: Available modes: {string.Join(", ", weaponMappings.Keys)}");
                    return;
                }

                ExtraAttackSystemPlugin.LogInfo("System", $"UpdateAOCForMode: Custom anim name: {customAnimName}");

                // Get custom animation clip
                var customClip = GetAnimationClip(customAnimName);
                if (customClip == null)
                {
                    ExtraAttackSystemPlugin.LogWarning("System", $"UpdateAOCForMode: Custom clip not found: {customAnimName}");
                    return;
                }

                ExtraAttackSystemPlugin.LogInfo("System", $"UpdateAOCForMode: Custom clip found: {customClip.name}, length: {customClip.length:F3}s");

                // Get vanilla secondary attack clip name
                string vanillaClipName = GetVanillaSecondaryClipName(weaponType);
                if (string.IsNullOrEmpty(vanillaClipName))
                {
                    ExtraAttackSystemPlugin.LogWarning("System", $"UpdateAOCForMode: No vanilla clip name for {weaponType}");
                    return;
                }

                ExtraAttackSystemPlugin.LogInfo("System", $"UpdateAOCForMode: Vanilla clip name: {vanillaClipName}");

                // Try to get cached clip with timing first
                var cachedClip = EAS_AnimationCache.GetCachedClip(weaponType, mode);
                if (cachedClip != null)
                {
                    // Use cached clip with timing
                    aoc[vanillaClipName] = cachedClip;
                    ExtraAttackSystemPlugin.LogInfo("System", $"UpdateAOCForMode: Used cached clip with timing for {weaponType}_{mode}");
                }
                else
                {
                    // Fallback to original clip without timing
                    aoc[vanillaClipName] = customClip;
                    ExtraAttackSystemPlugin.LogInfo("System", $"UpdateAOCForMode: Updated {weaponType} {vanillaClipName} -> {customAnimName} for mode {mode}");
                }
            }
            catch (Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error updating AOC for mode: {ex.Message}");
            }
        }

        // Update AOC replacementMap for specific mode (without recreating AOC)
        public static void UpdateAOCReplacementMap(Player player, string weaponType, string mode)
        {
            try
            {
                ExtraAttackSystemPlugin.LogInfo("System", $"UpdateAOCReplacementMap: Called with {weaponType}, {mode}");
                
                if (player == null || string.IsNullOrEmpty(weaponType) || string.IsNullOrEmpty(mode))
                {
                    ExtraAttackSystemPlugin.LogWarning("System", "UpdateAOCReplacementMap: Invalid parameters");
                    return;
                }

                var animator = GetPlayerAnimator(player);
                if (animator == null)
                {
                    ExtraAttackSystemPlugin.LogWarning("System", "UpdateAOCReplacementMap: Animator not found");
                    return;
                }

                // Get current AOC
                var aoc = animator.runtimeAnimatorController as AnimatorOverrideController;
                if (aoc == null)
                {
                    ExtraAttackSystemPlugin.LogWarning("System", "UpdateAOCReplacementMap: No AOC currently applied");
                    return;
                }

                // Get animation mappings for this weapon type
                if (!AnimationReplacementMap.TryGetValue(weaponType, out var weaponMappings))
                {
                    ExtraAttackSystemPlugin.LogWarning("System", $"UpdateAOCReplacementMap: No mappings found for {weaponType}");
                    return;
                }

                // Get custom animation name for this mode
                if (!weaponMappings.TryGetValue(mode, out var customAnimName))
                {
                    ExtraAttackSystemPlugin.LogWarning("System", $"UpdateAOCReplacementMap: No animation found for {weaponType}.{mode}");
                    return;
                }

                // Get custom animation clip
                var customClip = GetAnimationClip(customAnimName);
                if (customClip == null)
                {
                    ExtraAttackSystemPlugin.LogWarning("System", $"UpdateAOCReplacementMap: Custom clip not found: {customAnimName}");
                    return;
                }

                // Get vanilla secondary attack clip name
                string vanillaClipName = GetVanillaSecondaryClipName(weaponType);
                if (string.IsNullOrEmpty(vanillaClipName))
                {
                    ExtraAttackSystemPlugin.LogWarning("System", $"UpdateAOCReplacementMap: No vanilla clip name for {weaponType}");
                    return;
                }

                // Try to get cached clip with timing first
                var cachedClip = EAS_AnimationCache.GetCachedClip(weaponType, mode);
                if (cachedClip != null)
                {
                    // Use cached clip with timing
                    aoc[vanillaClipName] = cachedClip;
                    ExtraAttackSystemPlugin.LogInfo("System", $"UpdateAOCReplacementMap: Used cached clip with timing for {weaponType}_{mode}");
                }
                else
                {
                    // Fallback to original clip without timing
                    aoc[vanillaClipName] = customClip;
                    ExtraAttackSystemPlugin.LogInfo("System", $"UpdateAOCReplacementMap: Updated {weaponType} {vanillaClipName} -> {customAnimName} for mode {mode}");
                }
            }
            catch (Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error updating AOC replacement map: {ex.Message}");
            }
        }

        // Get vanilla secondary attack clip name for weapon type
        public static string GetVanillaSecondaryClipName(string weaponType)
        {
            return weaponType switch
            {
                "Sword" => "Sword-Attack-R4",
                "Greatsword" => "Greatsword Secondary Attack",
                "Axe" => "Axe Secondary Attack",
                "Battleaxe" => "BattleAxeAltAttack",
                "Club" => "MaceAltAttack",
                "Spear" => "throw_spear",
                "Polearm" => "Atgeir360Attack",
                "Knife" => "Knife JumpAttack",
                "Fist" => "Kickstep",
                "Unarmed" => "Kickstep",
                "DualAxe" => "DualAxes Attack Cleave",
                "DualKnife" => "Knife Attack Leap",
                _ => "Sword-Attack-R4" // Default fallback
            };
        }
    }
}
