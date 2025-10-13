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
        public static readonly Dictionary<string, UnityEngine.RuntimeAnimatorController> CustomRuntimeControllers = new();
        public static readonly Dictionary<string, float> ExternalClipLengthCache = new();
        
        // Cache for vanilla clip lengths to avoid repeated searches
        private static readonly Dictionary<string, float> VanillaClipLengthCache = new();
        
        // Cache for external clip lengths with change detection
        private static readonly Dictionary<string, float> CachedExternalClipLengths = new();
        private static readonly Dictionary<string, float> LastKnownClipLengths = new();
        private static bool _clipLengthCacheInitialized = false;
        
        // Default clip lengths for common animations (based on actual vanilla values from List_AnimatorClips.txt)
        private static readonly Dictionary<string, float> DefaultClipLengths = new Dictionary<string, float>
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
                        ExternalAnimations[externalName] = clip;
                        if (ExtraAttackPlugin.IsDebugSystemMessagesEnabled)
                        {
                            ExtraAttackPlugin.LogInfo("System", $"Loaded animation: {clip.name} -> {externalName}");
                        }
                    }

                    ExtraAttackPlugin.LogInfo("System", $"Successfully loaded {animationClips.Length} animations from AssetBundle");
                    ExtraAttackPlugin.LogInfo("System", $"ExternalAnimations dictionary now has {ExternalAnimations.Count} entries");
                    // Add AnimationEvents to external animations for attack detection
                    AnimationEventManager.AddEventsToExternalAnimations();
                }
                else
                {
                    ExtraAttackPlugin.LogWarning("System", "AssetBundle not found, using fallback animations");
                }

                // Initialize animation replacement maps
                InitializeAnimationMaps();
                
                // Pre-cache vanilla clip lengths for better performance
                PreCacheVanillaClipLengths();
                
                // Pre-cache external clip lengths with change detection
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
            // Initialize weapon type specific maps for Q/T/G
            var weaponTypes = new[] { "Swords", "Axes", "Clubs", "Spears", "GreatSwords", "BattleAxes", "Polearms", "Knives", "Fists", "Unarmed" };
            
            // Create basic weapon type mappings for secondary_Q, secondary_T, secondary_G
            foreach (var weaponType in weaponTypes)
            {
                if (!ReplacementMap.ContainsKey(weaponType))
                {
                    ReplacementMap[weaponType] = new Dictionary<string, string>();
                }
                
                // Create Q/T/G mappings for each weapon type
                string qExternalClip = GetExternalClipForWeaponType(weaponType, "Q");
                string tExternalClip = GetExternalClipForWeaponType(weaponType, "T");
                string gExternalClip = GetExternalClipForWeaponType(weaponType, "G");
                
                if (!string.IsNullOrEmpty(qExternalClip))
                {
                    ReplacementMap[weaponType]["secondary_Q"] = qExternalClip;
                }
                if (!string.IsNullOrEmpty(tExternalClip))
                {
                    ReplacementMap[weaponType]["secondary_T"] = tExternalClip;
                }
                if (!string.IsNullOrEmpty(gExternalClip))
                {
                    ReplacementMap[weaponType]["secondary_G"] = gExternalClip;
                }
            }

            if (ExternalAnimations.Count > 0)
            {
                ExtraAttackPlugin.LogInfo("System", "Available animations:");
                foreach (var anim in ExternalAnimations.Keys)
                {
                    ExtraAttackPlugin.LogInfo("System", $"  - {anim}");
                }

                // ========================================================================
                // WEAPON TYPE MAPPINGS: Load from YAML files (MOD default settings)
                // ========================================================================

                // Load weapon type mappings from YAML files
                ExtraAttackPlugin.LogInfo("System", "Loading weapon type mappings from YAML files...");

                // Generate combo keys for all pairs of one-handed weapon skills (right x left) - DualWield compatibility
                var oneHandSkills = new Skills.SkillType[]
                {
                    Skills.SkillType.Swords,
                    Skills.SkillType.Axes,
                    Skills.SkillType.Clubs,
                    Skills.SkillType.Knives,
                    Skills.SkillType.Spears
                };

                int combosCreated = 0;
                foreach (var right in oneHandSkills)
                {
                    string weaponType = right.ToString();
                    
                    // Check if weapon type has Q/T/G mappings
                    bool hasQ = ReplacementMap.ContainsKey(weaponType) && ReplacementMap[weaponType].ContainsKey("secondary_Q");
                    bool hasT = ReplacementMap.ContainsKey(weaponType) && ReplacementMap[weaponType].ContainsKey("secondary_T");
                    bool hasG = ReplacementMap.ContainsKey(weaponType) && ReplacementMap[weaponType].ContainsKey("secondary_G");

                    foreach (var left in oneHandSkills)
                    {
                        // Q-mode combos: copy from weapon type if available
                        string qComboKey = $"secondary_Q_{right}_Left{left}";
                        if (!ReplacementMap.ContainsKey(qComboKey) && hasQ)
                        {
                            string vanillaClip = GetWeaponAnimationName(weaponType);
                            string externalClip = ReplacementMap[weaponType]["secondary_Q"];
                            ReplacementMap[qComboKey] = new Dictionary<string, string> { { vanillaClip, externalClip } };
                            combosCreated += 1;
                        }

                        // T-mode combos: copy from weapon type if available
                        string tComboKey = $"secondary_T_{right}_Left{left}";
                        if (!ReplacementMap.ContainsKey(tComboKey) && hasT)
                        {
                            string vanillaClip = GetWeaponAnimationName(weaponType);
                            string externalClip = ReplacementMap[weaponType]["secondary_T"];
                            ReplacementMap[tComboKey] = new Dictionary<string, string> { { vanillaClip, externalClip } };
                            combosCreated += 1;
                        }

                        // G-mode combos: copy from weapon type if available
                        string gComboKey = $"secondary_G_{right}_Left{left}";
                        if (!ReplacementMap.ContainsKey(gComboKey) && hasG)
                        {
                            string vanillaClip = GetWeaponAnimationName(weaponType);
                            string externalClip = ReplacementMap[weaponType]["secondary_G"];
                            ReplacementMap[gComboKey] = new Dictionary<string, string> { { vanillaClip, externalClip } };
                            combosCreated += 1;
                        }
                    }
                }

                // Stats
                int totalKeys = ReplacementMap.Count;
                int totalMappings = ReplacementMap.Values.Sum(m => m.Count);

                ExtraAttackPlugin.LogInfo("System", "Animation replacement maps initialized:");
                
                // Log weapon type specific mappings
                foreach (var weaponType in weaponTypes)
                {
                    if (ReplacementMap.ContainsKey(weaponType))
                    {
                        var qCount = ReplacementMap[weaponType].ContainsKey("secondary_Q") ? 1 : 0;
                        var tCount = ReplacementMap[weaponType].ContainsKey("secondary_T") ? 1 : 0;
                        var gCount = ReplacementMap[weaponType].ContainsKey("secondary_G") ? 1 : 0;
                        
                        ExtraAttackPlugin.LogInfo("System", $"  {weaponType}: Q={qCount}, T={tCount}, G={gCount}");
                    }
                }
                ExtraAttackPlugin.LogInfo("System", $"  Combo keys generated: {combosCreated}");
                ExtraAttackPlugin.LogInfo("System", $"  Total keys: {totalKeys}, total mappings across all keys: {totalMappings}");
            }
            else
            {
                ExtraAttackPlugin.LogInfo("System", "No external animations found - using fallback mode");
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
                    Skills.SkillType.Axes => is2H ? "BattleAxes" : "Axes",
                    Skills.SkillType.Swords => is2H ? "GreatSwords" : "Swords",
                    Skills.SkillType.Clubs => "Clubs",
                    Skills.SkillType.Spears => "Spears",
                    Skills.SkillType.Polearms => "Polearms",
                    Skills.SkillType.Knives => "Knives",
                    Skills.SkillType.Unarmed => "Fists",
                    _ => "Swords" // Default to Swords if unknown
                };

                // Map weapon type to idle animation state name
                string idleState = weaponType switch
                {
                    "Swords" => "Idle_Sword",
                    "Axes" => "Idle_Axe", 
                    "Clubs" => "Idle_Club",
                    "Spears" => "Idle_Spear",
                    "GreatSwords" => "Idle_Greatsword",
                    "BattleAxes" => "Idle_Battleaxe",
                    "Polearms" => "Idle_Atgeir",
                    "Knives" => "Idle_Knife",
                    "Fists" => "Idle",
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
                
                if (replacement.TryGetValue(name, out string value) && ExternalAnimations.ContainsKey(value))
                {
                    AnimationClip newClip = UnityEngine.Object.Instantiate(ExternalAnimations[value]);
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

        // Helper: cache and return external clip length
        public static float GetExternalClipLength(string externalName)
        {
            try
            {
                if (string.IsNullOrEmpty(externalName)) return -1f;
                if (ExternalClipLengthCache.TryGetValue(externalName, out float cached))
                {
                    return cached;
                }
                if (ExternalAnimations.TryGetValue(externalName, out AnimationClip clip) && clip != null)
                {
                    float len = clip.length;
                    ExternalClipLengthCache[externalName] = len;
                    return len;
                }
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("AOC", $"GetExternalClipLength error: {ex.Message}");
            }
            return -1f;
        }

        // Pre-cache vanilla clip lengths during initialization (simplified approach)
        public static void PreCacheVanillaClipLengths()
        {
            try
            {
                ExtraAttackPlugin.LogInfo("System", "Pre-caching vanilla clip lengths from default values...");
                
                // Pre-cache all default clip lengths
                int cachedCount = 0;
                foreach (var kvp in DefaultClipLengths)
                {
                    VanillaClipLengthCache[kvp.Key] = kvp.Value;
                    cachedCount++;
                }
                
                ExtraAttackPlugin.LogInfo("System", $"Pre-cached {cachedCount} vanilla clip lengths from default values");
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error pre-caching vanilla clip lengths: {ex.Message}");
            }
        }
        

        // Pre-cache external clip lengths with change detection
        public static void PreCacheExternalClipLengths()
        {
            try
            {
                ExtraAttackPlugin.LogInfo("System", "Pre-caching external clip lengths with change detection...");
                
                bool hasChanges = false;
                int cachedCount = 0;
                int changedCount = 0;
                
                foreach (var kvp in ExternalAnimations)
                {
                    string clipName = kvp.Key;
                    AnimationClip clip = kvp.Value;
                    
                    if (clip != null)
                    {
                        float currentLength = clip.length;
                        
                        // Check if this clip has changed
                        if (LastKnownClipLengths.TryGetValue(clipName, out float lastLength))
                        {
                            if (Math.Abs(currentLength - lastLength) > 0.001f) // Allow small floating point differences
                            {
                                hasChanges = true;
                                changedCount++;
                                ExtraAttackPlugin.LogInfo("System", $"Clip length changed for {clipName}: {lastLength:F3}s -> {currentLength:F3}s");
                            }
                        }
                        else
                        {
                            // New clip
                            hasChanges = true;
                            changedCount++;
                        }
                        
                        // Update cache
                        CachedExternalClipLengths[clipName] = currentLength;
                        LastKnownClipLengths[clipName] = currentLength;
                        cachedCount++;
                    }
                }
                
                if (hasChanges)
                {
                    ExtraAttackPlugin.LogInfo("System", $"Pre-cached {cachedCount} external clip lengths ({changedCount} changed)");
                }
                else
                {
                    ExtraAttackPlugin.LogInfo("System", $"Pre-cached {cachedCount} external clip lengths (no changes detected)");
                }
                
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
                if (_clipLengthCacheInitialized && CachedExternalClipLengths.TryGetValue(externalName, out float cachedLength))
                {
                    return cachedLength;
                }
                
                // Fallback to direct lookup
                if (ExternalAnimations.TryGetValue(externalName, out AnimationClip clip) && clip != null)
                {
                    float length = clip.length;
                    CachedExternalClipLengths[externalName] = length;
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
                
                // First check cache
                if (VanillaClipLengthCache.TryGetValue(vanillaClipName, out float cachedLength))
                {
                    return cachedLength;
                }
                
                // Use default values directly (simplified approach)
                if (DefaultClipLengths.TryGetValue(vanillaClipName, out float defaultLength))
                {
                    VanillaClipLengthCache[vanillaClipName] = defaultLength;
                    ExtraAttackPlugin.LogInfo("Config", $"Using default clip length for {vanillaClipName}: {defaultLength:F3}s");
                    return defaultLength;
                }
                
                // Cache negative result to avoid repeated searches
                VanillaClipLengthCache[vanillaClipName] = -1f;
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
                    CustomRuntimeControllers.TryGetValue("Original", out original);
                }

                // Remove all cached controllers
                CustomRuntimeControllers.Clear();

                // Restore Original when requested
                if (keepOriginal && original != null)
                {
                    CustomRuntimeControllers["Original"] = original;
                }

                // Clear external clip length cache to force re-evaluation after reload
                ExternalClipLengthCache.Clear();

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

        // Apply weapon type specific settings to ReplacementMap
        public static void ApplyWeaponTypeSettings()
        {
            ExtraAttackPlugin.LogInfo("System", "ApplyWeaponTypeSettings: START");
            try
            {
                
                // Get weapon type config
                var weaponTypeConfig = AnimationTimingConfig.GetWeaponTypeConfig();
                
                ExtraAttackPlugin.LogInfo("System", $"ApplyWeaponTypeSettings: weaponTypeConfig is null: {weaponTypeConfig == null}");
                ExtraAttackPlugin.LogInfo("System", $"ApplyWeaponTypeSettings: WeaponTypes is null: {weaponTypeConfig?.WeaponTypes == null}");
                ExtraAttackPlugin.LogInfo("System", $"ApplyWeaponTypeSettings: WeaponTypes.Count: {weaponTypeConfig?.WeaponTypes?.Count ?? -1}");
                
                // If weaponTypeConfig is null or empty, create default mappings
                if (weaponTypeConfig == null || weaponTypeConfig.WeaponTypes == null || weaponTypeConfig.WeaponTypes.Count == 0)
                {
                    ExtraAttackPlugin.LogInfo("System", "weaponTypeConfig is null or empty - creating default mappings");
                    CreateDefaultWeaponTypeMappings();
                    return;
                }
                
                ExtraAttackPlugin.LogInfo("System", $"ApplyWeaponTypeSettings: weaponTypeConfig has {weaponTypeConfig.WeaponTypes?.Count ?? 0} weapon types, {weaponTypeConfig.IndividualWeapons?.Count ?? 0} individual weapons");

                // Apply weapon type specific animation mappings
                if (weaponTypeConfig.WeaponTypes != null)
                {
                    foreach (var weaponType in weaponTypeConfig.WeaponTypes.Keys)
                {
                    var weaponSettings = weaponTypeConfig.WeaponTypes[weaponType];
                    
                    ExtraAttackPlugin.LogInfo("System", $"ApplyWeaponTypeSettings: {weaponType} has {weaponSettings.Count} settings, keys: {string.Join(", ", weaponSettings.Keys)}");
                    
                    foreach (var setting in weaponSettings.Keys)
                    {
                        var timing = weaponSettings[setting];
                        
                        ExtraAttackPlugin.LogInfo("System", $"ApplyWeaponTypeSettings: Processing {weaponType}, setting: {setting}");
                        
                        // Create animation clip mappings based on weapon type and mode
                        var animationMappings = CreateWeaponTypeAnimationMappings(weaponType, setting, timing);
                        
                        ExtraAttackPlugin.LogInfo("System", $"ApplyWeaponTypeSettings: animationMappings.Count = {animationMappings.Count}");
                        
                        // Apply to ReplacementMap - create key if it doesn't exist
                        if (!ReplacementMap.ContainsKey(weaponType))
                        {
                            ReplacementMap[weaponType] = new Dictionary<string, string>();
                            ExtraAttackPlugin.LogInfo("System", $"ApplyWeaponTypeSettings: Created ReplacementMap[{weaponType}]");
                        }
                        
                        foreach (var mapping in animationMappings)
                        {
                            ReplacementMap[weaponType][mapping.Key] = mapping.Value;
                            ExtraAttackPlugin.LogInfo("System", $"ApplyWeaponTypeSettings: Added {weaponType}[{mapping.Key}] = {mapping.Value}");
                        }
                    }
                }
                }

                // Apply individual weapon animation mappings (only if YAML config exists)
                if (weaponTypeConfig.IndividualWeapons != null && weaponTypeConfig.IndividualWeapons.Count > 0)
                {
                    foreach (var individualWeapon in weaponTypeConfig.IndividualWeapons.Keys)
                    {
                        var timing = weaponTypeConfig.IndividualWeapons[individualWeapon];
                        
                        // Create individual weapon key if it doesn't exist
                        if (!ReplacementMap.ContainsKey(individualWeapon))
                        {
                            ReplacementMap[individualWeapon] = new Dictionary<string, string>();
                        }
                        
                        // Create animation mappings for individual weapon
                        var animationMappings = CreateIndividualWeaponAnimationMappings(individualWeapon, timing);
                        foreach (var mapping in animationMappings)
                        {
                            ReplacementMap[individualWeapon][mapping.Key] = mapping.Value;
                        }
                    }
                }
                else
                {
                    // Create sample individual weapon entries for demonstration
                    CreateSampleIndividualWeaponEntries();
                }

                ExtraAttackPlugin.LogInfo("System", "Applied weapon type specific animation mappings to ReplacementMap");
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error applying weapon type settings: {ex.Message}");
            }
        }

        // Create sample individual weapon entries for demonstration
        private static void CreateSampleIndividualWeaponEntries()
        {
            try
            {
                ExtraAttackPlugin.LogInfo("System", "CreateSampleIndividualWeaponEntries: Starting to create sample entries for THSwordWood");
                // Create sample entries for Wooden Greatsword (TwoHandedWeapon type)
                var sampleWeapons = new[]
                {
                    "secondary_Q_THSwordWood",
                    "secondary_T_THSwordWood", 
                    "secondary_G_THSwordWood"
                };

                foreach (var weaponKey in sampleWeapons)
                {
                    if (!ReplacementMap.ContainsKey(weaponKey))
                    {
                        ReplacementMap[weaponKey] = new Dictionary<string, string>();
                    }

                    // Add sample animation mappings
                    if (ExternalAnimations.Count > 0)
                    {
                        // Q mode: Great sword secondary attack (own weapon type)
                        if (weaponKey.Contains("_Q_"))
                        {
                            ReplacementMap[weaponKey]["Greatsword Secondary Attack"] = "2Hand-Sword-Attack8External";
                        }
                        // T mode: Battle axe secondary attack (different weapon type)
                        else if (weaponKey.Contains("_T_"))
                        {
                            ReplacementMap[weaponKey]["BattleAxeAltAttack"] = "BattleAxeAltAttackExternal";
                        }
                        // G mode: Polearm 360 attack (different weapon type)
                        else if (weaponKey.Contains("_G_"))
                        {
                            ReplacementMap[weaponKey]["Atgeir360Attack"] = "2Hand_Skill01_WhirlWindExternal";
                        }
                    }
                }

                ExtraAttackPlugin.LogInfo("System", "CreateSampleIndividualWeaponEntries: Created sample individual weapon entries for THSwordWood");
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error creating sample individual weapon entries: {ex.Message}");
            }
        }

        // Create animation mappings for weapon type specific settings
        private static Dictionary<string, string> CreateWeaponTypeAnimationMappings(string weaponType, string setting, AnimationTimingConfig.AnimationTiming timing)
        {
            var mapping = new Dictionary<string, string>();
            
            // Map based on weapon type and attack mode
            if (setting.Contains("_Q"))
            {
                // Q mode: use weapon's own secondary attack
                // 自分の武器種のバニラクリップ → 自分の武器種の外部クリップ
                string vanillaClip = GetWeaponSecondaryAnimation(weaponType);
                string externalClip = GetExternalClipForWeaponType(weaponType, "Q");
                if (!string.IsNullOrEmpty(vanillaClip) && !string.IsNullOrEmpty(externalClip))
                {
                    mapping[vanillaClip] = externalClip;
                }
            }
            else if (setting.Contains("_T"))
            {
                // T mode: use different weapon type secondary attack
                // 自分の武器種のバニラクリップ → 異なる武器種の外部クリップ
                string vanillaClip = GetWeaponSecondaryAnimation(weaponType);
                string externalClip = GetExternalClipForWeaponType(weaponType, "T");
                if (!string.IsNullOrEmpty(vanillaClip) && !string.IsNullOrEmpty(externalClip))
                {
                    mapping[vanillaClip] = externalClip;
                }
            }
            else if (setting.Contains("_G"))
            {
                // G mode: use different weapon type secondary attack
                // 自分の武器種のバニラクリップ → さらに異なる武器種の外部クリップ
                string vanillaClip = GetWeaponSecondaryAnimation(weaponType);
                string externalClip = GetExternalClipForWeaponType(weaponType, "G");
                if (!string.IsNullOrEmpty(vanillaClip) && !string.IsNullOrEmpty(externalClip))
                {
                    mapping[vanillaClip] = externalClip;
                }
            }
            
            return mapping;
        }


        // Get external clip for weapon type and mode
        private static string GetExternalClipForWeaponType(string weaponType, string mode)
        {
            // Q/T/G modes use completely different animation clips
            switch (weaponType)
            {
                case "Swords":
                    return mode == "Q" ? "2Hand-Sword-Attack8External" :
                           mode == "T" ? "2Hand_Skill01_WhirlWindExternal" :
                           "Eas_GreatSword_JumpAttackExternal";
                case "Axes":
                    return mode == "Q" ? "OneHand_Up_Attack_B_1External" :
                           mode == "T" ? "2Hand-Sword-Attack8External" :
                           "Eas_GreatSword_JumpAttackExternal";
                case "Clubs":
                    return mode == "Q" ? "Eas_GreatSword_CastingExternal" :
                           mode == "T" ? "2Hand_Skill01_WhirlWindExternal" :
                           "2Hand-Sword-Attack8External";
                case "Spears":
                    return mode == "Q" ? "Eas_GreatSword_JumpAttackExternal" :
                           mode == "T" ? "2Hand_Skill01_WhirlWindExternal" :
                           "2Hand-Sword-Attack8External";
                case "GreatSwords":
                    return mode == "Q" ? "2Hand-Sword-Attack8External" :
                           mode == "T" ? "2Hand_Skill01_WhirlWindExternal" :
                           "Eas_GreatSword_JumpAttackExternal";
                case "BattleAxes":
                    return mode == "Q" ? "2Hand_Skill01_WhirlWindExternal" :
                           mode == "T" ? "2Hand-Sword-Attack8External" :
                           "Eas_GreatSword_JumpAttackExternal";
                case "Polearms":
                    return mode == "Q" ? "Eas_GreatSword_JumpAttackExternal" :
                           mode == "T" ? "2Hand-Sword-Attack8External" :
                           "2Hand_Skill01_WhirlWindExternal";
                case "Knives":
                    return mode == "Q" ? "ChargeAttkExternal" :
                           mode == "T" ? "Eas_GreatSword_JumpAttackExternal" :
                           "2Hand-Sword-Attack8External";
                case "Fists":
                    return mode == "Q" ? "Flying Knee Punch ComboExternal" :
                           mode == "T" ? "2Hand_Skill01_WhirlWindExternal" :
                           "Eas_GreatSword_JumpAttackExternal";
                default:
                    return mode == "Q" ? "2Hand-Sword-Attack8External" :
                           mode == "T" ? "2Hand-Sword-Attack8External" :
                           "BattleAxeAltAttackExternal";
            }
        }

        // Create animation mappings for individual weapon settings
        private static Dictionary<string, string> CreateIndividualWeaponAnimationMappings(string weaponName, AnimationTimingConfig.AnimationTiming timing)
        {
            var mapping = new Dictionary<string, string>();
            
            // Map individual weapon to appropriate animation
            if (!string.IsNullOrEmpty(weaponName) && weaponName.Contains("Sword"))
            {
                mapping["Sword-Attack-R4"] = "Sword-Attack-R4";
            }
            else if (!string.IsNullOrEmpty(weaponName) && weaponName.Contains("GreatSword"))
            {
                mapping["Sword-Attack-R4"] = "Greatsword Secondary Attack";
            }
            else if (!string.IsNullOrEmpty(weaponName) && weaponName.Contains("Axe"))
            {
                mapping["Sword-Attack-R4"] = "Axe Secondary Attack";
            }
            else if (!string.IsNullOrEmpty(weaponName) && weaponName.Contains("Club"))
            {
                mapping["Sword-Attack-R4"] = "MaceAltAttack";
            }
            else if (!string.IsNullOrEmpty(weaponName) && weaponName.Contains("Spear"))
            {
                mapping["Sword-Attack-R4"] = "throw_spear";
            }
            
            return mapping;
        }


        // Get weapon animation name for combo generation
        private static string GetWeaponAnimationName(string weaponType)
        {
            return weaponType switch
            {
                "Swords" => "Sword-Attack-R4",
                "Axes" => "Axe Secondary Attack",
                "Clubs" => "MaceAltAttack",
                "Spears" => "Atgeir360Attack",
                "GreatSwords" => "Greatsword Secondary Attack",
                "BattleAxes" => "BattleAxeAltAttack",
                "Polearms" => "Atgeir360Attack",
                "Knives" => "Knife JumpAttack",
                "Fists" => "Kickstep",
                _ => "Sword-Attack-R4"
            };
        }

        // Get weapon secondary animation based on weapon type
        private static string GetWeaponSecondaryAnimation(string weaponType)
        {
            return weaponType switch
            {
                "Swords" => "Sword-Attack-R4",
                "GreatSwords" => "Greatsword Secondary Attack",
                "Axes" => "Axe Secondary Attack",
                "Clubs" => "MaceAltAttack",
                "Spears" => "throw_spear",
                "BattleAxes" => "BattleAxeAltAttack",
                "Polearms" => "Atgeir360Attack",
                "Knives" => "Knife JumpAttack",
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
                    "Swords", "Axes", "Clubs", "Spears", "GreatSwords", 
                    "BattleAxes", "Polearms", "Knives", "Fists"
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
                    if (!ReplacementMap.ContainsKey(weaponType))
                    {
                        ReplacementMap[weaponType] = new Dictionary<string, string>();
                    }
                    
                    // Add Q, T, G mappings for each weapon type using unified key format
                    ReplacementMap[weaponType]["secondary_Q"] = defaultAnimations[animationIndex++];
                    ReplacementMap[weaponType]["secondary_T"] = defaultAnimations[animationIndex++];
                    ReplacementMap[weaponType]["secondary_G"] = defaultAnimations[animationIndex++];
                    
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