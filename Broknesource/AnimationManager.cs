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
        public static readonly Dictionary<string, Dictionary<string, string>> AnimationReplacementMap = new();
        public static readonly Dictionary<string, AnimationClip> CustomAnimationClips = new();
        public static readonly Dictionary<string, UnityEngine.RuntimeAnimatorController> AnimatorControllerCache = new();
        
        // Flag to prevent duplicate animation logging
        private static bool _animationsLogged = false;
        
        // Cache for external clip lengths
        private static readonly Dictionary<string, float> CustomClipLengthCache = new();
        private static bool _clipLengthCacheInitialized = false;
        
        // Default clip lengths for common animations (based on actual vanilla values from List_AnimatorClips.txt)
        public static readonly Dictionary<string, float> VanillaClipLengths = new Dictionary<string, float>
        {
            // Sword animations (from actual vanilla data)
            { "Sword-Attack-R4", 1.4f },  // Based on Sword-Attack-R4: 1.400s
            { "Attack1", 1.0f }, // mace & sword
            
            // Great Sword animations (from actual vanilla data)
            { "Greatsword Secondary Attack", 1.4f },  // Based on Greatsword Secondary Attack: 1.400s
            { "Greatsword BaseAttack (1)", 1.167f }, // Based on Greatsword BaseAttack (1): 1.167s
            
            // Axe animations (from actual vanilla data)
            { "Axe Secondary Attack", 1.4f },  // Based on Axe Secondary Attack: 1.400s
            { "axe_swing", 1.0f },
            
            // Battle Axe animations (from actual vanilla data)
            { "BattleAxeAltAttack", 0.857f }, // Based on BattleAxeAltAttack: 0.857s
            { "BattleAxe1", 1.0f },
            
            // Club/Mace animations
            { "MaceAltAttack", 1.4f },
            
            // Spear animations (from actual vanilla data)
            { "throw_spear", 1.133f },  // Based on throw_spear: 1.133s
            { "spear_poke", 0.667f }, // Based on 2Hand-Spear-Attack1: 0.667s
            
            // Polearm animations (from actual vanilla data)
            { "Atgeir360Attack", 2.167f }, // Based on Atgeir360Attack: 2.167s
            { "2Hand-Spear-Attack", 1.1f },
            
            // Knife animations (from actual vanilla data)
            { "Knife JumpAttack", 1.4f },  // Based on Knife JumpAttack: 1.400s
            { "Knife Attack Combo (1)", 0.4f },   // Based on Knife Attack Combo (1): 0.400s
            
            // Unarmed animations
            { "Kickstep", 0.9f },
            { "Punchstep 1", 0.7f },
            
            // Actual vanilla animation names and lengths from List_AnimatorClips.txt
            { "2Hand-Spear-Attack1", 0.667f },
            { "2Hand-Spear-Attack9", 0.7f },
            { "2Hand-Spear-Attack3", 0.733f },
            { "Knife Attack Leap", 1.5f },
            { "Knife Attack Combo (3)", 0.5f },
            { "Knife Attack Combo (2)", 0.4f },
            { "Greatsword BaseAttack (3)", 1.5f },
            { "Greatsword BaseAttack (2)", 1.0f },
            { "DualAxes Attack 1", 1.267f },
            { "DualAxes Attack 2 2", 1.433f },
            { "DualAxes Attack 3 2", 1.5f },
            { "DualAxes Attack Cleave", 1.9f },
            { "DualAxes Attack 4", 1.5f }
        };

        // Cache the field info for better performance
        private static FieldInfo? animatorFieldCache;

        public static void LoadAssets()
        {
            try
            {
                // Load AssetBundle
                asset = GetAssetBundle("extraattacksystem");
                if (asset != null)
                {
                    // Load animation clips
                    var animationClips = asset.LoadAllAssets<AnimationClip>();
                    if (ExtraAttackPlugin.IsDebugSystemMessagesEnabled)
                    {
                        ExtraAttackPlugin.LogInfo("System", $"Found {animationClips.Length} animation clips in AssetBundle");
                    }

                    foreach (var clip in animationClips)
                    {
                        string externalName = clip.name + "External";
                        CustomAnimationClips[externalName] = clip;
                        if (ExtraAttackPlugin.IsDebugSystemMessagesEnabled)
                        {
                            ExtraAttackPlugin.LogInfo("System", $"Loaded animation: {clip.name} -> {externalName}");
                        }
                    }

                    ExtraAttackPlugin.LogInfo("System", $"Successfully loaded {animationClips.Length} animations from AssetBundle");
                    ExtraAttackPlugin.LogInfo("System", $"CustomAnimationClips dictionary now has {CustomAnimationClips.Count} entries");
                    // Add AnimationEvents to external animations for attack detection
                    AnimationEventManager.AddEventsToCustomAnimations();
                }
                else
                {
                    ExtraAttackPlugin.LogWarning("System", "AssetBundle not found, using fallback animations");
                }

                // Initialize animation replacement maps
                InitializeAnimationMaps();
                
                // Pre-cache external clip lengths
                PreCacheExternalClipLengths();
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error loading assets: {ex.Message}");
                ExtraAttackPlugin.LogError("System", $"Stack trace: {ex.StackTrace}");
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
            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                var resourceNames = assembly.GetManifestResourceNames();

                // Try embedded resource (exclude manifest entries)
                string? resourceName = resourceNames.FirstOrDefault(n => n.EndsWith(filename, StringComparison.Ordinal) && !n.EndsWith(filename + ".manifest", StringComparison.Ordinal));
                if (resourceName == null)
                {
                    resourceName = resourceNames.FirstOrDefault(n => n.IndexOf(filename, StringComparison.OrdinalIgnoreCase) >= 0 && !n.EndsWith(".manifest", StringComparison.OrdinalIgnoreCase));
                }

                if (resourceName != null)
                {
                    using Stream? stream = assembly.GetManifestResourceStream(resourceName);
                    if (stream != null)
                    {
                        ExtraAttackPlugin.LogInfo("System", $"Loading AssetBundle from embedded resource: {resourceName}");
                        return AssetBundle.LoadFromStream(stream);
                    }
                    else
                    {
                        ExtraAttackPlugin.LogWarning("System", $"Embedded resource stream not found: {resourceName}");
                    }
                }

                // Try file system locations near the plugin DLL
                string? pluginPath = Path.GetDirectoryName(assembly.Location);
                if (pluginPath != null)
                {
                    string directPath = Path.Combine(pluginPath, filename);
                    string resourcesPath = Path.Combine(pluginPath, "Resources", filename);
                    if (File.Exists(directPath))
                    {
                        ExtraAttackPlugin.LogInfo("System", $"Loading AssetBundle from file: {directPath}");
                        return AssetBundle.LoadFromFile(directPath);
                    }
                    if (File.Exists(resourcesPath))
                    {
                        ExtraAttackPlugin.LogInfo("System", $"Loading AssetBundle from file: {resourcesPath}");
                        return AssetBundle.LoadFromFile(resourcesPath);
                    }

                    // Last resort: base directory of the process
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    string basePath = Path.Combine(baseDir, filename);
                    if (File.Exists(basePath))
                    {
                        ExtraAttackPlugin.LogInfo("System", $"Loading AssetBundle from base directory file: {basePath}");
                        return AssetBundle.LoadFromFile(basePath);
                    }
                }

                ExtraAttackPlugin.LogWarning("System", $"AssetBundle '{filename}' not found in embedded resources or file system");
                return null;
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error in GetAssetBundle: {ex.Message}");
                return null;
            }
        }

        public static void InitializeAnimationMaps()
        {
            // Skip initialization - AnimationReplacementConfig.ApplyToManager will handle all mappings
            ExtraAttackPlugin.LogInfo("System", "InitializeAnimationMaps: Skipping - AnimationReplacementConfig will handle mappings");

            if (CustomAnimationClips.Count > 0)
            {
                // Log available animations only once during initialization
                if (!_animationsLogged)
                {
                    ExtraAttackPlugin.LogInfo("System", "Available animations:");
                    foreach (var anim in CustomAnimationClips.Keys)
                    {
                        ExtraAttackPlugin.LogInfo("System", $"  - {anim}");
                    }
                    _animationsLogged = true;
                }
            }
        }

        // AnimatorOverrideController creation
        // Unified method to get weapon idle state
        public static string GetWeaponIdleState(Player player)
        {
            try
            {
                var weapon = player.GetCurrentWeapon();
                if (weapon?.m_shared == null)
                {
                    return "Idle"; // Default unarmed idle
                }
                
                var skillType = weapon.m_shared.m_skillType;
                var itemType = weapon.m_shared.m_itemType;
                
                // Determine weapon type using same logic as GetWeaponTypeFromSkillType
                bool is2H = itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon;
                
                string weaponType = skillType switch
                {
                    Skills.SkillType.Axes => is2H ? "Battleaxe" : "Axe",
                    Skills.SkillType.Swords => is2H ? "Greatsword" : "Sword",
                    Skills.SkillType.Clubs => "Club",
                    Skills.SkillType.Spears => "Spear",
                    Skills.SkillType.Polearms => "Polearm",
                    Skills.SkillType.Knives => "Knife",
                    Skills.SkillType.Unarmed => "Fist",
                    _ => "Sword" // Default to Sword if unknown
                };

                // Map weapon type to idle animation state name
                string idleState = weaponType switch
                {
                    "Sword" => "Idle_Sword",
                    "Axe" => "Idle_Axe", 
                    "Club" => "Idle_Club",
                    "Spear" => "Idle_Spear",
                    "Greatsword" => "Idle_Greatsword",
                    "Battleaxe" => "Idle_Battleaxe",
                    "Polearm" => "Idle_Atgeir",
                    "Knife" => "Idle_Knife",
                    "Fist" => "Idle",
                    _ => "Idle" // Default fallback
                };


                return idleState;
            }
            catch (System.Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error in GetWeaponIdleState: {ex.Message}");
                return "Idle"; // Default fallback
            }
        }

        public static UnityEngine.RuntimeAnimatorController MakeAOC(Dictionary<string, string> replacement, UnityEngine.RuntimeAnimatorController original, Player? player = null)
        {
            AnimatorOverrideController aoc = new(original);
            List<KeyValuePair<AnimationClip, AnimationClip>> anims = new();


                foreach (AnimationClip animation in aoc.animationClips)
                {
                    string name = animation.name;
                
                if (replacement.TryGetValue(name, out string value) && CustomAnimationClips.ContainsKey(value))
                {
                    AnimationClip newClip = UnityEngine.Object.Instantiate(CustomAnimationClips[value]);
                    newClip.name = name;
                    anims.Add(new KeyValuePair<AnimationClip, AnimationClip>(animation, newClip));
                }
                else
                {
                    anims.Add(new KeyValuePair<AnimationClip, AnimationClip>(animation, animation));
                }
            }

            aoc.ApplyOverrides(anims);
            return aoc;
        }

        // Direct Animator access - Using Reflection for runtime compatibility test
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
                        ExtraAttackPlugin.LogError("System", "Could not find m_animator field via reflection");
                        return null;
                    }
                }

                return animatorFieldCache.GetValue(player) as Animator;
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Failed to get animator via reflection: {ex.Message}");
                return null;
            }
        }

        // Fast UnityEngine.RuntimeAnimatorController replacement (safe approach)
        public static void FastReplaceRAC(Player player, UnityEngine.RuntimeAnimatorController replace)
        {
            try
            {
                var animator = GetPlayerAnimator(player);
                if (animator == null || replace == null)
                {
                    ExtraAttackPlugin.LogWarning("AOC", "Animator or replacement controller is null");
                    return;
                }

                var before = animator.runtimeAnimatorController;

                if (animator.runtimeAnimatorController == replace)
                {
                    return;
                }

                animator.runtimeAnimatorController = replace;

            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("AOC", $"Error in FastReplaceRAC: {ex.Message}");
            }
        }

        // Soft UnityEngine.RuntimeAnimatorController replacement (no Rebind/Update)
        public static void SoftReplaceRAC(Animator animator, UnityEngine.RuntimeAnimatorController replace, bool preserveSitCrouch = false)
        {
            try
            {
                if (animator == null || replace == null)
                {
                    ExtraAttackPlugin.LogWarning("AOC", "Animator or replacement controller is null (SoftReplaceRAC)");
                    return;
                }

                var before = animator.runtimeAnimatorController;

                if (animator.runtimeAnimatorController == replace)
                {
                    // 既に目的の RAC の場合は何もしない
                    return;
                }

                // オプション: 座り/しゃがみフラグの保持
                bool prevCrouch = false, prevSit = false, prevSitChair = false;
                int crouchHash = -1, sitHash = -1, sitChairHash = -1;
                if (preserveSitCrouch)
                {
                    try { crouchHash = ZSyncAnimation.GetHash("crouching"); } catch { }
                    try { sitHash = ZSyncAnimation.GetHash("emote_sit"); } catch { }
                    try { sitChairHash = ZSyncAnimation.GetHash("emote_sitchair"); } catch { }

                    try { if (crouchHash != -1) prevCrouch = animator.GetBool(crouchHash); } catch { }
                    try { if (sitHash != -1) prevSit = animator.GetBool(sitHash); } catch { }
                    try { if (sitChairHash != -1) prevSitChair = animator.GetBool(sitChairHash); } catch { }
                }

                // Replace controller (no Rebind/Update)
                animator.runtimeAnimatorController = replace;

                // Restore flags (if needed)
                if (preserveSitCrouch)
                {
                    try { if (crouchHash != -1) animator.SetBool(crouchHash, prevCrouch); } catch { }
                    try { if (sitHash != -1) animator.SetBool(sitHash, prevSit); } catch { }
                    try { if (sitChairHash != -1) animator.SetBool(sitChairHash, prevSitChair); } catch { }
                }

            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("AOC", $"Error in SoftReplaceRAC: {ex.Message}");
            }
        }

        // Fast UnityEngine.RuntimeAnimatorController replacement (safe approach)
        public static void FastReplaceRAC(Animator animator, UnityEngine.RuntimeAnimatorController replace)
        {
            try
            {
                if (animator == null || replace == null)
                {
                    ExtraAttackPlugin.LogWarning("AOC", "Animator or replacement controller is null");
                    return;
                }

                var before = animator.runtimeAnimatorController;

                if (animator.runtimeAnimatorController == replace)
                {
                    return;
                }

                animator.runtimeAnimatorController = replace;

            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("AOC", $"Error in FastReplaceRAC: {ex.Message}");
            }
        }

        // Pre-cache external clip lengths
        public static void PreCacheExternalClipLengths()
        {
            try
            {
                ExtraAttackPlugin.LogInfo("System", "Pre-caching external clip lengths...");
                
                int cachedCount = 0;
                
                foreach (var kvp in CustomAnimationClips)
                {
                    string clipName = kvp.Key;
                    AnimationClip clip = kvp.Value;
                    
                    if (clip != null)
                    {
                        float currentLength = clip.length;
                        CustomClipLengthCache[clipName] = currentLength;
                        cachedCount++;
                    }
                }
                
                ExtraAttackPlugin.LogInfo("System", $"Pre-cached {cachedCount} external clip lengths");
                _clipLengthCacheInitialized = true;
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error pre-caching external clip lengths: {ex.Message}");
            }
        }
        
        // Get external clip length with smart caching
        public static float GetExternalClipLengthSmart(string externalName)
        {
            try
            {
                if (string.IsNullOrEmpty(externalName)) return -1f;
                
                // Check if cache is initialized and has this clip
                if (_clipLengthCacheInitialized && CustomClipLengthCache.TryGetValue(externalName, out float cachedLength))
                {
                    return cachedLength;
                }
                
                // Fallback to direct lookup
                if (CustomAnimationClips.TryGetValue(externalName, out AnimationClip clip) && clip != null)
                {
                    float length = clip.length;
                    CustomClipLengthCache[externalName] = length;
                    return length;
                }
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("AOC", $"GetExternalClipLengthSmart error: {ex.Message}");
            }
            return -1f;
        }

        // Helper: get vanilla animation clip length from default values (simplified approach)
        public static float GetVanillaClipLength(string vanillaClipName)
        {
            try
            {
                if (string.IsNullOrEmpty(vanillaClipName)) return -1f;
                
                // Use default values directly
                if (VanillaClipLengths.TryGetValue(vanillaClipName, out float defaultLength))
                {
                    ExtraAttackPlugin.LogInfo("System", $"Using default clip length for {vanillaClipName}: {defaultLength:F3}s");
                    return defaultLength;
                }
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("AOC", $"GetVanillaClipLength error: {ex.Message}");
            }
            return -1f;
        }
        
        // Helper: recursively search for clip length in state machine
        private static float FindClipLengthInStateMachine(object stateMachine, string clipName)
        {
            try
            {
                // Use reflection to access state machine properties
                var statesProperty = stateMachine.GetType().GetProperty("states");
                if (statesProperty != null)
                {
                    var states = statesProperty.GetValue(stateMachine);
                    if (states is System.Collections.IEnumerable stateEnumerable)
                    {
                        foreach (var stateObj in stateEnumerable)
                        {
                            var stateProperty = stateObj.GetType().GetProperty("state");
                            if (stateProperty != null)
                            {
                                var state = stateProperty.GetValue(stateObj);
                                var motionProperty = state.GetType().GetProperty("motion");
                                if (motionProperty != null)
                                {
                                    var motion = motionProperty.GetValue(state);
                                    if (motion is AnimationClip clip && clip.name == clipName)
                                    {
                                        return clip.length;
                                    }
                                }
                            }
                        }
                    }
                }
                
                // Check sub-state machines
                var subStateMachinesProperty = stateMachine.GetType().GetProperty("stateMachines");
                if (subStateMachinesProperty != null)
                {
                    var subStateMachines = subStateMachinesProperty.GetValue(stateMachine);
                    if (subStateMachines is System.Collections.IEnumerable subEnumerable)
                    {
                        foreach (var subStateMachineObj in subEnumerable)
                        {
                            var stateMachineProperty = subStateMachineObj.GetType().GetProperty("stateMachine");
                            if (stateMachineProperty != null)
                            {
                                var subStateMachine = stateMachineProperty.GetValue(subStateMachineObj);
                                float length = FindClipLengthInStateMachine(subStateMachine, clipName);
                                if (length > 0) return length;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("AOC", $"FindClipLengthInStateMachine error: {ex.Message}");
            }
            return -1f;
        }

        // Helper: scan overrides to find secondary attack override length
        public static float TryGetSecondaryOverrideLength(AnimatorOverrideController aoc)
        {
            try
            {
                if (aoc == null) return -1f;
                var list = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                aoc.GetOverrides(list);
                foreach (var kv in list)
                {
                    var original = kv.Key;
                    var overrideClip = kv.Value;
                    string oname = original?.name ?? string.Empty;
                    if (oname.IndexOf("Secondary Attack", StringComparison.Ordinal) >= 0)
                    {
                        if (overrideClip != null && overrideClip != original)
                        {
                            return overrideClip.length;
                        }
                        if (original != null)
                        {
                            return original.length;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("AOC", $"TryGetSecondaryOverrideLength error: {ex.Message}");
            }
            return -1f;
        }

        // Clear cached AnimatorOverrideControllers and clip length cache
        // What: Remove all cached AOCs; optionally keep the "Original" entry
        // Why: Reflect YAML edits immediately after reload without restarting
        public static void ClearAOCCache(bool keepOriginal = true)
        {
            try
            {
                UnityEngine.RuntimeAnimatorController? original = null;
                if (keepOriginal)
                {
                    AnimatorControllerCache.TryGetValue("Original", out original);
                }

                // Remove all cached controllers
                AnimatorControllerCache.Clear();

                // Restore Original when requested
                if (keepOriginal && original != null)
                {
                    AnimatorControllerCache["Original"] = original;
                }

                // Clear external clip length cache to force re-evaluation after reload
                CustomClipLengthCache.Clear();
                _clipLengthCacheInitialized = false;

            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("AOC", $"Error in ClearAOCCache: {ex.Message}");
            }
        }

        public static string GetExtraAttackAnimationTrigger(ItemDrop.ItemData weapon)
        {
            // Return dedicated animation trigger per weapon and item type
            var skill = weapon.m_shared.m_skillType;
            var itemType = weapon.m_shared.m_itemType;
        
            // Exclude projectile types for now (Bow/Crossbow treated as Bow)
            if (itemType == ItemDrop.ItemData.ItemType.Bow || skill == Skills.SkillType.Bows)
            {
                return string.Empty; // do not override; handled later when ranged is implemented
            }
            
            // Resolve prefab and token names to detect specific vanilla dual items safely
            string prefabName = weapon.m_dropPrefab != null ? weapon.m_dropPrefab.name : string.Empty;
            string tokenName = weapon.m_shared != null ? weapon.m_shared.m_name : string.Empty;
            
            // Dual-wield (modded): prefer dual triggers when left-hand has a matching one-handed weapon (not shield/torch)
            var left = Player.m_localPlayer?.LeftItem;
            if (left != null && left.IsWeapon() && left.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Shield && left.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Torch)
            {
                // Only consider dual when current weapon is one-handed
                if (itemType == ItemDrop.ItemData.ItemType.OneHandedWeapon)
                {
                    if (skill == Skills.SkillType.Axes && left.m_shared.m_skillType == Skills.SkillType.Axes)
                    {
                        return "DualAxes Attack Cleave";
                    }
                    if (skill == Skills.SkillType.Knives && left.m_shared.m_skillType == Skills.SkillType.Knives)
                    {
                        return "dual_knives_secondary";
                    }
                }
            }
            
            // NEW: Polearms (Atgeir) has a dedicated secondary trigger in vanilla
            if (skill == Skills.SkillType.Polearms)
            {
                return "atgeir_secondary";
            }
            
            // Two-handed Swords
            if (skill == Skills.SkillType.Swords && (itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon || itemType == ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft))
            {
                return "Greatsword Secondary Attack";
            }
            
            // Two-handed Axes: differentiate vanilla Battleaxe vs Dual Axe variants
            if (skill == Skills.SkillType.Axes && (itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon || itemType == ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft))
            {
                // Vanilla dual axes (Berzerkr variants)
                bool isDualAxe =
                    string.Equals(prefabName, "AxeBerzerkr", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(prefabName, "AxeBerzerkrBlood", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(prefabName, "AxeBerzerkrLightning", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(prefabName, "AxeBerzerkrNature", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(tokenName, "$item_axe_berzerkr", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(tokenName, "$item_axe_berzerkr_blood", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(tokenName, "$item_axe_berzerkr_lightning", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(tokenName, "$item_axe_berzerkr_nature", StringComparison.OrdinalIgnoreCase);
                
                return isDualAxe ? "DualAxes Attack Cleave" : "BattleAxeAltAttack";
            }
            
            // Two-handed Knives: Dual Knife (Skoll and Hati)
            if (skill == Skills.SkillType.Knives && (itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon || itemType == ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft))
            {
                bool isDualKnife =
                    string.Equals(prefabName, "KnifeSkollAndHati", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(tokenName, "$item_knife_skollandhati", StringComparison.OrdinalIgnoreCase);
                if (isDualKnife)
                {
                    return "dual_knives_secondary";
                }
                // Fallback for any other potential two-handed knife
                return "Knife JumpAttack";
            }
            
            // One-handed defaults
            return skill switch
            {
                Skills.SkillType.Swords => "Sword-Attack-R4",
                Skills.SkillType.Axes => "Axe Secondary Attack",
                Skills.SkillType.Clubs => "MaceAltAttack",
                Skills.SkillType.Knives => "Knife JumpAttack",
                Skills.SkillType.Spears => "throw_spear",
                _ => "Sword-Attack-R4"
            };
        }






        // Get weapon animation name for combo generation
        private static string GetWeaponAnimationName(string weaponType)
        {
            return weaponType switch
            {
                "Sword" => "Sword-Attack-R4",
                "Axe" => "Axe Secondary Attack",
                "Club" => "MaceAltAttack",
                "Spear" => "Atgeir360Attack",
                "Greatsword" => "Greatsword Secondary Attack",
                "Battleaxe" => "BattleAxeAltAttack",
                "Polearm" => "Atgeir360Attack",
                "Knife" => "Knife JumpAttack",
                "Fist" => "Kickstep",
                _ => "Sword-Attack-R4"
            };
        }

        // Get weapon secondary animation based on weapon type
        private static string GetWeaponSecondaryAnimation(string weaponType)
        {
            return weaponType switch
            {
                "Sword" => "Sword-Attack-R4",
                "Greatsword" => "Greatsword Secondary Attack",
                "Axe" => "Axe Secondary Attack",
                "Club" => "MaceAltAttack",
                "Spear" => "throw_spear",
                "Battleaxe" => "BattleAxeAltAttack",
                "Polearm" => "Atgeir360Attack",
                "Knife" => "Knife JumpAttack",
                _ => "Sword-Attack-R4"
            };
        }

        // Create default weapon type mappings when config is empty
        public static void CreateDefaultWeaponTypeMappings()
        {
            try
            {
                ExtraAttackPlugin.LogInfo("System", "CreateDefaultWeaponTypeMappings: Creating default weapon type mappings");
                
                // Define weapon types and their default external animations
                var weaponTypes = new[]
                {
                    "Sword", "Axe", "Club", "Spear", "Greatsword", 
                    "Battleaxe", "Polearm", "Knife", "Fist"
                };
                
                var defaultAnimations = new[]
                {
                    "Sw-Ma-GS-Up_Attack_A_1External", "Sw-Ma-GS-Up_Attack_A_2External", "Sw-Ma-GS-Up_Attack_A_3External",
                    "OneHand_Up_Attack_B_1External", "OneHand_Up_Attack_B_2External", "OneHand_Up_Attack_B_3External",
                    "0MWA_DualWield_Attack02External", "MWA_RightHand_Attack03External", "Shield@ShieldAttack01External",
                    "Shield@ShieldAttack02External", "Attack04External", "0MGSA_Attack_Dash01External",
                    "2Hand-Sword-Attack8External", "2Hand_Skill01_WhirlWindExternal", "Eas_GreatSword_Combo1External",
                    "0MGSA_Attack_Dash02External", "0MGSA_Attack_Ground01External", "0MGSA_Attack_Ground02External",
                    "Pa_1handShiled_attack02External", "Attack_ShieldExternal", "0DS_Attack_07External",
                    "ChargeAttkExternal", "HardAttkExternal", "StrongAttk3External",
                    "Flying Knee Punch ComboExternal", "Eas_GreatSword_SlideAttackExternal", "Eas_GreatSwordSlash_01External"
                };
                
                int animationIndex = 0;
                foreach (var weaponType in weaponTypes)
                {
                    if (!AnimationReplacementMap.ContainsKey(weaponType))
                    {
                        AnimationReplacementMap[weaponType] = new Dictionary<string, string>();
                    }
                    
                    // Add Q, T, G mappings for each weapon type using unified key format
                    AnimationReplacementMap[weaponType]["secondary_Q"] = defaultAnimations[animationIndex++];
                    AnimationReplacementMap[weaponType]["secondary_T"] = defaultAnimations[animationIndex++];
                    AnimationReplacementMap[weaponType]["secondary_G"] = defaultAnimations[animationIndex++];
                    
                    ExtraAttackPlugin.LogInfo("System", $"CreateDefaultWeaponTypeMappings: Added {weaponType} with Q/T/G mappings");
                }
                
                ExtraAttackPlugin.LogInfo("System", "CreateDefaultWeaponTypeMappings: Default weapon type mappings created successfully");
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error in CreateDefaultWeaponTypeMappings: {ex.Message}");
            }
        }
    }
}