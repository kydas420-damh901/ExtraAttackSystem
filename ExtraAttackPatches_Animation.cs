using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace ExtraAttackSystem
{
    // Animation-related utilities and helpers
    public static class ExtraAttackPatches_Animation
    {
        // Initialize Animator Override Controllers (AOC) cache
        // Cache flag: ensure we only prewarm once per session
        private static bool s_AOCPrewarmed = false;
        // NEW: Reentrancy guard to prevent infinite recursion when Prewarm calls BuildOrGetAOCFor
        private static bool s_AOCInitializing = false;
        public static bool IsAOCInitializing => s_AOCInitializing;
        public static void InitializeAOC(Player player, Animator animator)
        {
            try
            {
                // Prevent re-entrant initialization
                if (s_AOCInitializing)
                {
                    return;
                }
                s_AOCInitializing = true;
        
                if (animator == null)
                {
                    s_AOCInitializing = false;
                    return;
                }
        
        
                // Cache original runtime controller once
                if (!AnimationManager.CustomRuntimeControllers.ContainsKey("Original"))
                {
                    var original = animator.runtimeAnimatorController;
                    if (original != null)
                    {
                        AnimationManager.CustomRuntimeControllers["Original"] = original;
                    }
                }
        
                // Prewarm AOCs for style maps to avoid first-press lag
                if (!s_AOCPrewarmed && player != null)
                {
                    try
                    {
                        PrewarmAOCForStyles(player, animator);
                        s_AOCPrewarmed = true;
                    }
                    catch (Exception prewarmEx)
                    {
                        ExtraAttackPlugin.LogError("System", $"Error in PrewarmAOCForStyles: {prewarmEx.Message}");
                    }
                }
        
                // Clear reentrancy guard on normal completion
                s_AOCInitializing = false;
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error in InitializeAOC: {ex.Message}");
                // Clear reentrancy guard on exception
                s_AOCInitializing = false;
            }
        }

        // Prewarm AOCs for each style to reduce first-press lag and populate caches
        private static void PrewarmAOCForStyles(Player player, Animator animator)
        {
            // Pre-generate AOCs only for weapons that have YAML configuration
            try
            {
                // Pre-generate base weapon type AOCs (these are always needed)
                BuildOrGetAOCFor(player, animator, ExtraAttackUtils.AttackMode.secondary_Q);
                BuildOrGetAOCFor(player, animator, ExtraAttackUtils.AttackMode.secondary_T);
                BuildOrGetAOCFor(player, animator, ExtraAttackUtils.AttackMode.secondary_G);
                
                
                // Individual weapon AOCs will be generated on-demand when equipment changes
                // This avoids the complexity of creating mock players for pre-generation
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error in PrewarmAOCForStyles: {ex.Message}");
            }
        }

        // Build YAML config key using resolved vanilla animation name and secondary Q/T/G suffix
        public static string BuildConfigKeyByClip(Player player, Animator animator, AnimationClip clip, int hitIndex)
        {
            string clipName = clip != null ? clip.name : string.Empty;
            try
            {
                // Determine current attack mode once for suffix candidates
                var mode = ExtraAttackSystem.ExtraAttackUtils.GetAttackMode(player);

                // Map attack mode to unified secondary suffix
                string secondarySuffix = mode switch
                {
                    ExtraAttackUtils.AttackMode.secondary_Q => "_secondary_Q",
                    ExtraAttackUtils.AttackMode.secondary_T => "_secondary_T",
                    ExtraAttackUtils.AttackMode.secondary_G => "_secondary_G",
                    _ => string.Empty
                };
        
                if (ResolveMappingFromClip(clipName, out string vanillaName, out string _))
                {
                    // Only probe unified secondary suffix and base
                    var candidateSuffixes = new List<string>();
                    if (!string.IsNullOrEmpty(secondarySuffix))
                    {
                        candidateSuffixes.Add(secondarySuffix);
                    }
                    candidateSuffixes.Add(string.Empty);

                    var seen = new HashSet<string>();
                    foreach (var suf in candidateSuffixes)
                    {
                        if (!seen.Add(suf)) continue;
                        string keyHit = string.IsNullOrEmpty(suf) ? $"{vanillaName}_hit{hitIndex}" : $"{vanillaName}{suf}_hit{hitIndex}";
                        string keyBase = string.IsNullOrEmpty(suf) ? vanillaName : $"{vanillaName}{suf}";
                        if (AnimationTimingConfig.HasConfig(keyHit)) return keyHit;
                        if (AnimationTimingConfig.HasConfig(keyBase)) return keyBase;
                    }
                }

                // Fallback: probe using clipName with unified secondary suffix
                {
                    var candidateSuffixes2 = new List<string>();
                    if (!string.IsNullOrEmpty(secondarySuffix))
                    {
                        candidateSuffixes2.Add(secondarySuffix);
                    }
                    candidateSuffixes2.Add(string.Empty);

                    var seen2 = new HashSet<string>();
                    foreach (var suf in candidateSuffixes2)
                    {
                        if (!seen2.Add(suf)) continue;
                        string keyHit2 = string.IsNullOrEmpty(suf) ? $"{clipName}_hit{hitIndex}" : $"{clipName}{suf}_hit{hitIndex}";
                        string keyBase2 = string.IsNullOrEmpty(suf) ? clipName : $"{clipName}{suf}";
                        if (AnimationTimingConfig.HasConfig(keyHit2)) return keyHit2;
                        if (AnimationTimingConfig.HasConfig(keyBase2)) return keyBase2;
                    }
                }

                // Final fallback
                return ExtraAttackPatches_Core.BuildConfigKey(player, clipName, hitIndex);
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error in BuildConfigKeyByClip: {ex.Message}");
                return ExtraAttackPatches_Core.BuildConfigKey(player, clipName, hitIndex);
            }
        }

        // Resolve mapping: external clip name -> vanilla name and style suffix
        public static bool ResolveMappingFromClip(string clipName, out string vanillaName, out string styleSuffix)
        {
            vanillaName = clipName;
            styleSuffix = string.Empty;
            try
            {
                // Priority 1: expected style based on current attack mode
                ExtraAttackUtils.AttackMode mode = ExtraAttackUtils.AttackMode.Normal;
                if (Player.m_localPlayer != null)
                {
                    mode = ExtraAttackUtils.GetAttackMode(Player.m_localPlayer);
                }
                string expectedPrefix = mode switch
                {
                    ExtraAttackUtils.AttackMode.secondary_Q => "secondary_Q",
                    ExtraAttackUtils.AttackMode.secondary_T => "secondary_T",
                    ExtraAttackUtils.AttackMode.secondary_G => "secondary_G",
                    _ => string.Empty
                };

                if (!string.IsNullOrEmpty(expectedPrefix))
                {
                    foreach (var entry in AnimationManager.ReplacementMap)
                    {
                        if (!entry.Key.StartsWith(expectedPrefix, StringComparison.Ordinal))
                        {
                            continue;
                        }
                        foreach (var kv in entry.Value)
                        {
                            if (string.Equals(kv.Value, clipName, StringComparison.Ordinal))
                            {
                                vanillaName = kv.Key;
                                styleSuffix = "_" + expectedPrefix;
                                return true;
                            }
                        }
                    }
                }

                // Priority 2: scan all maps and infer style suffix
                foreach (var entry in AnimationManager.ReplacementMap)
                {
                    string key = entry.Key;
                    string suffix = key.StartsWith("secondary_Q", StringComparison.Ordinal) ? "_secondary_Q" :
                                    key.StartsWith("secondary_T", StringComparison.Ordinal) ? "_secondary_T" :
                                    key.StartsWith("secondary_G", StringComparison.Ordinal) ? "_secondary_G" :
                                    string.Empty;

                    foreach (var kv in entry.Value)
                    {
                        if (string.Equals(kv.Value, clipName, StringComparison.Ordinal))
                        {
                            vanillaName = kv.Key;
                            styleSuffix = suffix;
                            return true;
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error in ResolveMappingFromClip: {ex.Message}");
                return false;
            }
        }

        // NEW: Build or get AnimatorOverrideController for a given style and weapon pairing
        public static RuntimeAnimatorController? BuildOrGetAOCFor(Player player, Animator animator, ExtraAttackUtils.AttackMode mode)
        {
            try
            {
                var traverse = Traverse.Create(player);
                var right = traverse.Field("m_rightItem").GetValue<ItemDrop.ItemData>();
                var left = traverse.Field("m_leftItem").GetValue<ItemDrop.ItemData>();
                string rightIdent = right?.m_shared?.m_name ?? string.Empty;
                var rightSkill = right?.m_shared?.m_skillType ?? Skills.SkillType.Swords;
                var leftSkill = left?.m_shared?.m_skillType ?? Skills.SkillType.None;
                bool leftIsShield = left?.m_shared?.m_itemType == ItemDrop.ItemData.ItemType.Shield;
                bool leftIsTorch = left?.m_shared?.m_itemType == ItemDrop.ItemData.ItemType.Torch; // Torch special-case

                // Check if individual weapon has YAML configuration before generating AOC
                if (!string.IsNullOrEmpty(rightIdent))
                {
                    string modePrefix = mode == ExtraAttackUtils.AttackMode.secondary_Q ? "secondary_Q" : mode == ExtraAttackUtils.AttackMode.secondary_T ? "secondary_T" : "secondary_G";
                    string individualWeaponKey = $"{modePrefix}_{rightIdent}";
                    
                    // Only generate individual weapon AOC if it exists in YAML
                    if (!AnimationManager.ReplacementMap.ContainsKey(individualWeaponKey) || AnimationManager.ReplacementMap[individualWeaponKey].Count == 0)
                    {
                        if (ExtraAttackPlugin.DebugAOCOperations.Value)
                        {
                        }
                        // Fall back to weapon type AOC instead of individual weapon AOC
                        rightIdent = string.Empty;
                    }
                }
        
                // Create item/type specific maps as needed based on current equipment
                EnsureItemStyleMaps(rightSkill, leftSkill, leftIsShield, leftIsTorch, rightIdent);
                // NEW: Ensure secondary maps exist (item/type/left variants) for current equipment
                string secondaryPrefix = mode == ExtraAttackUtils.AttackMode.secondary_Q ? "secondary_Q" : mode == ExtraAttackUtils.AttackMode.secondary_T ? "secondary_T" : "secondary_G";
                EnsureItemSecondaryMaps(rightSkill, leftSkill, leftIsShield, leftIsTorch, rightIdent, secondaryPrefix);

                // NEW: Prefer secondary_Q/T/G maps first, fallback to existing style maps
                string baseKey = string.Empty;
                string resolvedKey = string.Empty;

                // Attempt secondary selection using weapon type structure
                {
                    string currentWeaponType = GetWeaponTypeFromSkillType(rightSkill, right);
                    string modeKey = secondaryPrefix; // secondary_Q, secondary_T, secondary_G
                    
                    // Check if weapon type has the mode mapping
                    // âœ… ä¿®æ­£: ç›´æŽ¥ãƒ¢ãƒ¼ãƒ‰ã‚­ãƒ¼ï¼ˆsecondary_Q/T/Gï¼‰ã§æ¤œç´¢
                    bool hasWeaponTypeMapping = AnimationManager.ReplacementMap.ContainsKey(currentWeaponType) && 
                                              AnimationManager.ReplacementMap[currentWeaponType].ContainsKey(secondaryPrefix);
                    
                    string baseKeySec = hasWeaponTypeMapping ? currentWeaponType : string.Empty;
                    
                    // Check for individual weapon mapping
                    if (!string.IsNullOrEmpty(rightIdent))
                    {
                        string individualKey = $"{secondaryPrefix}_{rightIdent}";
                        if (AnimationManager.ReplacementMap.ContainsKey(individualKey) && AnimationManager.ReplacementMap[individualKey].Count > 0)
                        {
                            baseKeySec = individualKey;
                        }
                    }

                    // Consider left-hand variants for secondary
                    string resolvedKeySec = baseKeySec;
                    if (leftIsShield)
                    {
                        string candidate = $"{baseKeySec}_LeftShield";
                        if (AnimationManager.ReplacementMap.ContainsKey(candidate) && AnimationManager.ReplacementMap[candidate].Count > 0)
                        {
                            resolvedKeySec = candidate;
                        }
                    }
                    else if (leftIsTorch)
                    {
                        string candidate = $"{baseKeySec}_LeftTorch";
                        if (AnimationManager.ReplacementMap.ContainsKey(candidate) && AnimationManager.ReplacementMap[candidate].Count > 0)
                        {
                            resolvedKeySec = candidate;
                        }
                    }
                    else if (leftSkill != Skills.SkillType.None)
                    {
                        string candidate = $"{baseKeySec}_Left{leftSkill}";
                        if (AnimationManager.ReplacementMap.ContainsKey(candidate) && AnimationManager.ReplacementMap[candidate].Count > 0)
                        {
                            resolvedKeySec = candidate;
                        }
                    }

                    // If secondary map exists, use it
                    if (!string.IsNullOrEmpty(baseKeySec))
                    {
                        baseKey = baseKeySec;
                        resolvedKey = resolvedKeySec;
                    }
                }

                // Fallback to style maps when secondary not available
                if (string.IsNullOrEmpty(resolvedKey))
                {
                    switch (mode)
                    {
                        case ExtraAttackUtils.AttackMode.secondary_Q:
                            // Prefer item-specific map; fallback to weapon-type map if available
                            string typeKey1 = AnimationManager.ReplacementMap.ContainsKey($"secondary_Q_{rightSkill}") ? $"secondary_Q_{rightSkill}" : "secondary_Q";
                            baseKey = string.IsNullOrEmpty(rightIdent) ? typeKey1 : $"secondary_Q_{rightIdent}";
                            if (!AnimationManager.ReplacementMap.ContainsKey(baseKey) || AnimationManager.ReplacementMap[baseKey].Count == 0)
                            {
                                baseKey = typeKey1;
                            }
                            break;
                        case ExtraAttackUtils.AttackMode.secondary_T:
                            {
                                string typeKey2 = AnimationManager.ReplacementMap.ContainsKey($"secondary_T_{rightSkill}") ? $"secondary_T_{rightSkill}" : "secondary_T_Swords";
                                baseKey = string.IsNullOrEmpty(rightIdent) ? typeKey2 : $"secondary_T_{rightIdent}";
                            if (!AnimationManager.ReplacementMap.ContainsKey(baseKey) || AnimationManager.ReplacementMap[baseKey].Count == 0)
                            {
                                baseKey = typeKey2;
                            }
                        }
                        break;
                        case ExtraAttackUtils.AttackMode.secondary_G:
                        {
                                string typeKey3 = AnimationManager.ReplacementMap.ContainsKey($"secondary_G_{rightSkill}") ? $"secondary_G_{rightSkill}" : "secondary_G_Swords";
                                baseKey = string.IsNullOrEmpty(rightIdent) ? typeKey3 : $"secondary_G_{rightIdent}";
                            if (!AnimationManager.ReplacementMap.ContainsKey(baseKey) || AnimationManager.ReplacementMap[baseKey].Count == 0)
                            {
                                baseKey = typeKey3;
                            }
                        }
                        break;
                    default:
                            baseKey = "secondary_Q";
                        break;
                }
        
                    // Consider left-hand variants for style
                    resolvedKey = baseKey;
                if (leftIsShield)
                {
                    string candidate = $"{baseKey}_LeftShield";
                    if (AnimationManager.ReplacementMap.ContainsKey(candidate) && AnimationManager.ReplacementMap[candidate].Count > 0)
                    {
                        resolvedKey = candidate;
                    }
                }
                else if (leftIsTorch)
                {
                    string candidate = $"{baseKey}_LeftTorch";
                    if (AnimationManager.ReplacementMap.ContainsKey(candidate) && AnimationManager.ReplacementMap[candidate].Count > 0)
                    {
                        resolvedKey = candidate;
                    }
                }
                else if (leftSkill != Skills.SkillType.None)
                {
                    string candidate = $"{baseKey}_Left{leftSkill}";
                    if (AnimationManager.ReplacementMap.ContainsKey(candidate) && AnimationManager.ReplacementMap[candidate].Count > 0)
                    {
                        resolvedKey = candidate;
                        }
                    }
                }
        
                // Select replacement map
                Dictionary<string, string>? map = null;
                
                // For weapon type structure, get the mode mapping from the weapon type
                if (!string.IsNullOrEmpty(baseKey) && AnimationManager.ReplacementMap.ContainsKey(baseKey))
                {
                    var weaponTypeMap = AnimationManager.ReplacementMap[baseKey];
                    
                    // NEW: ç›´æŽ¥ãƒ¢ãƒ¼ãƒ‰ã‚­ãƒ¼ï¼ˆsecondary_Q/T/Gï¼‰ã§æ¤œç´¢
                    if (weaponTypeMap.ContainsKey(secondaryPrefix))
                    {
                        string externalClip = weaponTypeMap[secondaryPrefix];
                        // ãƒãƒ‹ãƒ©ã‚¯ãƒªãƒƒãƒ—åã‚’å–å¾—ã—ã¦ãƒžãƒƒãƒ”ãƒ³ã‚°ã‚’ä½œæˆ
                        string modeKey = secondaryPrefix.Replace("secondary_", "");
                        string vanillaClip = GetVanillaClipName(baseKey, modeKey);
                        map = new Dictionary<string, string> { { vanillaClip, externalClip } };
                    }
                    else
                    {
                    }
                }
                
                // Fallback to direct key lookup
                if (map == null)
                {
                if (AnimationManager.ReplacementMap.TryGetValue(resolvedKey, out var m) && m.Count > 0)
                {
                    map = m;
                }
                else if (AnimationManager.ReplacementMap.TryGetValue(baseKey, out var m2) && m2.Count > 0)
                {
                    map = m2;
                    }
                }

                var original = animator.runtimeAnimatorController;
                // Diagnostic: compute how many overrides have corresponding external clips loaded
                int externalResolved = map != null ? System.Linq.Enumerable.Count(map.Values, v => AnimationManager.ExternalAnimations.ContainsKey(v)) : 0;
                int externalMissing = map != null ? System.Linq.Enumerable.Count(map.Values, v => !AnimationManager.ExternalAnimations.ContainsKey(v)) : 0;
                int externalTotal = AnimationManager.ExternalAnimations.Count;
                string originalName = (original is AnimatorOverrideController) ? "AnimatorOverrideController" : (original?.name ?? "null");
                string mapKey = map == null ? "<none>" : resolvedKey;
        
                // No mappings -> wrap Original in empty AOC to maintain AOC->AOC swaps
                if (map == null || map.Count == 0)
                {
                    try
                    {
                        var originalRac = animator?.runtimeAnimatorController;
                        if (originalRac == null)
                        {
                            return null;
                        }

                        var empty = new Dictionary<string, string>();
                        var wrappedController = AnimationManager.MakeAOC(empty, originalRac);
                        return wrappedController;
                    }
                    catch (Exception ex)
                    {
                        ExtraAttackPlugin.LogError("System", $"BuildOrGetAOCFor: failed to wrap Original in AOC: {ex.Message}");
                        return animator?.runtimeAnimatorController; // Fallback: raw RAC
                    }
                }
        
                // Cache per mode + weapon type + resolved key (equipment-specific)
                string styleKey = mode == ExtraAttackUtils.AttackMode.secondary_Q ? "secondary_Q" : mode == ExtraAttackUtils.AttackMode.secondary_T ? "secondary_T" : "secondary_G";
                // Include base controller identity and weapon type to avoid cache collisions across different weapon controllers
                string baseId = original != null ? (original.name ?? "null") : "null";
                string cacheWeaponType = GetWeaponTypeFromSkillType(rightSkill, right);
                // Map "Unarmed" to "Fists" for consistency
                if (cacheWeaponType == "Unarmed") cacheWeaponType = "Fists";
                string cacheKey = $"{cacheWeaponType}:{styleKey}:{resolvedKey}:{baseId}";
        
                if (!AnimationManager.CustomRuntimeControllers.TryGetValue(cacheKey, out var controller) || controller == null)
                {
                    var originalNonNull = original ?? (AnimationManager.CustomRuntimeControllers.ContainsKey("Original") ? AnimationManager.CustomRuntimeControllers["Original"] : null);
                    if (originalNonNull == null)
                    {
                        return original;
                    }
                    controller = AnimationManager.MakeAOC(map, originalNonNull, player);
                    AnimationManager.CustomRuntimeControllers[cacheKey] = controller;
                    if (ExtraAttackPlugin.DebugAOCOperations.Value)
                    {
                    }
                }
                else if (ExtraAttackPlugin.DebugAOCOperations.Value)
                {
                }
        
                return controller;
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error in BuildOrGetAOCFor: {ex.Message}");
                return animator.runtimeAnimatorController;
            }
        }

        // Get weapon type from skill type and weapon data
        // ã‚¹ã‚­ãƒ«ã‚¿ã‚¤ãƒ—ã¨æ­¦å™¨ãƒ‡ãƒ¼ã‚¿ã‹ã‚‰æ­¦å™¨ã‚¿ã‚¤ãƒ—ã‚’åˆ¤å®šã™ã‚‹
        private static string GetWeaponTypeFromSkillType(Skills.SkillType skillType, ItemDrop.ItemData? weaponData = null)
        {
            if (ExtraAttackPlugin.DebugAOCOperations.Value)
            {
            }
            
            // Check if it's a 2H weapon using ItemDrop.ItemData.SharedData.m_itemType
            bool is2H = false;
            if (weaponData?.m_shared != null)
            {
                // Check if the weapon type indicates 2H
                var itemType = weaponData.m_shared.m_itemType;
                is2H = itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon || 
                       itemType == ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft;
            }
            
            if (ExtraAttackPlugin.DebugAOCOperations.Value)
            {
                var itemTypeStr = weaponData?.m_shared?.m_itemType.ToString() ?? "null";
            }
            
            // Battle Axe is Axes skill type + 2H, Great Sword is Swords skill type + 2H
            if (skillType == Skills.SkillType.Axes)
            {
                if (is2H)
                {
                    if (ExtraAttackPlugin.DebugAOCOperations.Value)
                    {
                    }
                    return "BattleAxes";
                }
                else
                    return "Axes";
            }
            else if (skillType == Skills.SkillType.Swords)
            {
                if (is2H)
                {
                    if (ExtraAttackPlugin.DebugAOCOperations.Value)
                    {
                    }
                    return "GreatSwords";
                }
                else
                    return "Swords";
            }
            
            return skillType.ToString();
        }

        // Get vanilla clip name for weapon type and mode
        // è£…å‚™ã—ã¦ã„ã‚‹æ­¦å™¨ã®å®Ÿéš›ã®ã‚»ã‚«ãƒ³ãƒ€ãƒªã‚¢ãƒ‹ãƒ¡ãƒ¼ã‚·ãƒ§ãƒ³ã‚¯ãƒªãƒƒãƒ—åã‚’è¿”ã™
        private static string GetVanillaClipName(string weaponType, string mode)
        {
            // Always return the equipped weapon's secondary trigger name
            return weaponType switch
            {
                "Swords" => "Sword-Attack-R4", // sword_secondary trigger
                "GreatSwords" => "Greatsword Secondary Attack", // greatsword_secondary trigger
                "Axes" => "Axe Secondary Attack", // axe_secondary trigger
                "Clubs" => "MaceAltAttack", // club_secondary trigger
                "Spears" => "throw_spear", // spear_throw trigger
                "BattleAxes" => "BattleAxeAltAttack", // battleaxe_secondary trigger
                "Polearms" => "Atgeir360Attack", // polearm_secondary trigger
                "Knives" => "Knife JumpAttack", // knife_secondary trigger
                "Fists" => "Kickstep", // fist_secondary trigger
                _ => "Sword-Attack-R4"
            };
        }

        // Create item-specific style maps if missing by copying from base per-style/per-type maps
        private static bool EnsureItemStyleMaps(Skills.SkillType rightSkill, Skills.SkillType leftSkill, bool leftIsShield, bool leftIsTorch, string rightIdent)
        {
            try
            {
                bool created = false;
                if (string.IsNullOrEmpty(rightIdent))
                {
                    return false;
                }
        
                // Helper to copy from first available source; fallback to base type if specific left variant missing
                bool Ensure(string targetKey, params string[] fallbackSourceKeys)
                {
                    if (!AnimationManager.ReplacementMap.ContainsKey(targetKey))
                    {
                        Dictionary<string, string>? source = null;
                        foreach (var fk in fallbackSourceKeys)
                        {
                            if (AnimationManager.ReplacementMap.ContainsKey(fk) && AnimationManager.ReplacementMap[fk].Count > 0)
                            {
                                source = new Dictionary<string, string>(AnimationManager.ReplacementMap[fk]);
                                break;
                            }
                        }
                        AnimationManager.ReplacementMap[targetKey] = source ?? new Dictionary<string, string>();
                        created = true;
                        if (ExtraAttackPlugin.DebugAOCOperations.Value)
                        {
                            string fromKey = source != null ? "copied" : "empty";
                        }
                    }
                    return AnimationManager.ReplacementMap[targetKey].Count > 0;
                }
        
                // Q mode base and combos
                Ensure($"secondary_Q_{rightIdent}", "secondary_Q");
                if (leftIsShield)
                {
                    Ensure($"secondary_Q_{rightIdent}_LeftShield", $"secondary_Q_{rightSkill}_LeftShield", "secondary_Q");
                }
                else if (leftIsTorch)
                {
                    Ensure($"secondary_Q_{rightIdent}_LeftTorch", $"secondary_Q_{rightSkill}_LeftTorch", "secondary_Q");
                }
                else
                {
                    Ensure($"secondary_Q_{rightIdent}_Left{leftSkill}", $"secondary_Q_{rightSkill}_Left{leftSkill}", "secondary_Q");
                }
        
                // T mode base and combos (fallback to per-type or swords)
                string style2Type = rightSkill == Skills.SkillType.Swords || AnimationManager.ReplacementMap.ContainsKey($"secondary_T_{rightSkill}") ? $"secondary_T_{rightSkill}" : "secondary_T_Swords";
                Ensure($"secondary_T_{rightIdent}", style2Type);
                if (leftIsShield)
                {
                    Ensure($"secondary_T_{rightIdent}_LeftShield", $"secondary_T_{rightSkill}_LeftShield", style2Type);
                }
                else if (leftIsTorch)
                {
                    Ensure($"secondary_T_{rightIdent}_LeftTorch", $"secondary_T_{rightSkill}_LeftTorch", style2Type);
                }
                else
                {
                    Ensure($"secondary_T_{rightIdent}_Left{leftSkill}", $"secondary_T_{rightSkill}_Left{leftSkill}", style2Type);
                }
        
                // G mode base and combos (fallback to per-type or swords)
                string style3Type = rightSkill == Skills.SkillType.Swords || AnimationManager.ReplacementMap.ContainsKey($"secondary_G_{rightSkill}") ? $"secondary_G_{rightSkill}" : "secondary_G_Swords";
                Ensure($"secondary_G_{rightIdent}", style3Type);
                if (leftIsShield)
                {
                    Ensure($"secondary_G_{rightIdent}_LeftShield", $"secondary_G_{rightSkill}_LeftShield", style3Type);
                }
                else if (leftIsTorch)
                {
                    Ensure($"secondary_G_{rightIdent}_LeftTorch", $"secondary_G_{rightSkill}_LeftTorch", style3Type);
                }
                else
                {
                    Ensure($"secondary_G_{rightIdent}_Left{leftSkill}", $"secondary_G_{rightSkill}_Left{leftSkill}", style3Type);
                }
        
                return created;
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error in EnsureItemStyleMaps: {ex.Message}");
                return false;
            }
        }

        // NEW: Create secondary item/type/left variant maps by copying from available secondary/type base
        private static bool EnsureItemSecondaryMaps(Skills.SkillType rightSkill, Skills.SkillType leftSkill, bool leftIsShield, bool leftIsTorch, string rightIdent, string secondaryPrefix)
        {
            try
            {
                bool created = false;
                if (string.IsNullOrEmpty(rightIdent))
                {
                    return false;
                }

                // Helper to copy from first available source; fallback to base secondary type if specific left variant missing
                bool Ensure(string targetKey, params string[] fallbackSourceKeys)
                {
                    if (!AnimationManager.ReplacementMap.ContainsKey(targetKey))
                    {
                        Dictionary<string, string>? source = null;
                        foreach (var fk in fallbackSourceKeys)
                        {
                            if (AnimationManager.ReplacementMap.ContainsKey(fk) && AnimationManager.ReplacementMap[fk].Count > 0)
                            {
                                source = new Dictionary<string, string>(AnimationManager.ReplacementMap[fk]);
                                break;
                            }
                        }
                        AnimationManager.ReplacementMap[targetKey] = source ?? new Dictionary<string, string>();
                        created = true;
                if (ExtraAttackPlugin.DebugAOCOperations.Value)
                {
                            string fromKey = source != null ? "copied" : "empty";
                        }
                    }
                    return AnimationManager.ReplacementMap[targetKey].Count > 0;
                }

                // Secondary base and combos
                string typeKeySec = AnimationManager.ReplacementMap.ContainsKey($"{secondaryPrefix}_{rightSkill}") ? $"{secondaryPrefix}_{rightSkill}" : secondaryPrefix;
                Ensure($"{secondaryPrefix}_{rightIdent}", typeKeySec);
                if (leftIsShield)
                {
                    Ensure($"{secondaryPrefix}_{rightIdent}_LeftShield", $"{secondaryPrefix}_{rightSkill}_LeftShield", typeKeySec);
                }
                else if (leftIsTorch)
                {
                    Ensure($"{secondaryPrefix}_{rightIdent}_LeftTorch", $"{secondaryPrefix}_{rightSkill}_LeftTorch", typeKeySec);
                }
                else
                {
                    Ensure($"{secondaryPrefix}_{rightIdent}_Left{leftSkill}", $"{secondaryPrefix}_{rightSkill}_Left{leftSkill}", typeKeySec);
                }

                return created;
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error in EnsureItemSecondaryMaps: {ex.Message}");
                return false;
            }
        }

        // NEW: Apply style-specific AnimatorOverrideController just before attack
        public static void ApplyStyleAOC(Player player, Animator animator, ExtraAttackUtils.AttackMode mode)
        {
            try
            {
                // Guard: animator is required
                if (animator == null)
                {
                    return;
                }

                var desired = BuildOrGetAOCFor(player, animator, mode);
                if (desired == null)
                {
                    return;
                }

                // Apply the AOC
                AnimationManager.SoftReplaceRAC(animator, desired, preserveSitCrouch: true);
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error in ApplyStyleAOC: {ex.Message}");
            }
        }
        

        // NEW: Diagnostic helper. Currently no-op to avoid API guessing; keeps compile-safe.
        public static void ScrubMissingRootMotionOverrides(RuntimeAnimatorController desired, Animator animator, ExtraAttackUtils.AttackMode mode)
        {
            try
            {
                if (desired == null || animator == null)
                {
                    return;
                }
                if (ExtraAttackPlugin.DebugAOCOperations.Value)
                {
                    string dname = (desired is AnimatorOverrideController) ? "AnimatorOverrideController" : (desired?.name ?? "null");
                }
                // NOTE: Intentionally left as no-op until vanilla root motion curve checks are defined.
            }
            catch (Exception)
            {
                // Surface error to caller's try/catch for logging consistency
                throw;
            }
        }

        // NEW: Suppress vanilla 'emote_stop' only during configured guard windows
        // Why: Avoid global suppression; allow vanilla stand-up outside guard windows while protecting post-attack/insufficient-stamina windows.
    [HarmonyPatch(typeof(ZSyncAnimation), nameof(ZSyncAnimation.SetTrigger), new Type[] { typeof(string) })]
    [HarmonyPriority(Priority.First)]
    internal static class ExtraAttackPatches_EmoteStopSuppressor
    {
        [HarmonyPrefix]
        private static bool ZSyncAnimation_SetTrigger_SuppressEmoteStop(ZSyncAnimation __instance, string name)
        {
            try
            {
                // Only care about 'emote_stop'
                if (!string.Equals(name, "emote_stop", StringComparison.Ordinal))
                {
                    return true; // run original
                }

                    // Optional global disable for troubleshooting
                    if (ExtraAttackPlugin.DebugDisableSetTriggerOverride.Value)
                {
                    return true; // run original
                }

                // Resolve player
                Player? player = null;
                try { player = __instance.GetComponentInParent<Player>(); } catch { }
                if (player == null)
                {
                        return true; // run original; context unknown
                    }

                    // Allow emote_stop when actually in emote (vanilla flow)
                if (player.InEmote())
                {
                    return true; // run original
                }


                    // Outside guard window, allow vanilla behavior
                    return true;
            }
            catch (Exception ex)
            {
                ExtraAttackSystem.ExtraAttackPlugin.LogError("System", $"Error in EmoteStop suppressor: {ex.Message}");
                return true; // fail-safe: do not block original
                }
            }
        }
    }
}