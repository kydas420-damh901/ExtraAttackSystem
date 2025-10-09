using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ExtraAttackSystem
{
    // Core patches: Animator caching and utility functions
    public static class ExtraAttackPatches_Core
    {
        private static readonly Dictionary<Player, Animator> playerAnimators = new();

        public static bool TryGetPlayerAnimator(Player player, out Animator animator)
        {
            return playerAnimators.TryGetValue(player, out animator);
        }

        // Utility functions for other patches
        public static string BuildConfigKey(Player player, string clipName, int hitIndex)
        {
            try
            {
                var attackMode = ExtraAttackUtils.GetAttackMode(player);

                // Greatsword YAML key unification: when equipped and Style mode active, force clipName
                ItemDrop.ItemData weapon = player.GetCurrentWeapon();
                bool isGreatsword = weapon != null &&
                                    weapon.m_shared.m_skillType == Skills.SkillType.Swords &&
                                    weapon.m_shared.m_itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon;
                if (isGreatsword && attackMode != ExtraAttackUtils.AttackMode.Normal)
                {
                    clipName = "greatsword_secondary";
                }

                // Prefer style-based suffix, then fallback to legacy suffixes (_Q/_T/_G), then no suffix
                string styleSuffix = attackMode switch
                {
                    ExtraAttackUtils.AttackMode.ea_secondary_Q => "_ea_secondary_Q",
                    ExtraAttackUtils.AttackMode.ea_secondary_T => "_ea_secondary_T",
                    ExtraAttackUtils.AttackMode.ea_secondary_G => "_ea_secondary_G",
                    _ => string.Empty
                };

                string legacySuffix = attackMode switch
                {
                    ExtraAttackUtils.AttackMode.ea_secondary_Q => "_Q",
                    ExtraAttackUtils.AttackMode.ea_secondary_T => "_T",
                    ExtraAttackUtils.AttackMode.ea_secondary_G => "_G",
                    _ => string.Empty
                };

                var suffixCandidates = new List<string>();
                if (!string.IsNullOrEmpty(styleSuffix)) suffixCandidates.Add(styleSuffix);
                if (!string.IsNullOrEmpty(legacySuffix)) suffixCandidates.Add(legacySuffix);
                suffixCandidates.Add(string.Empty);

                // Try keys in order: clip + suffix + _hitN, clip + suffix, clip + _hitN, clip
                foreach (var suffix in suffixCandidates)
                {
                    string keyHit = string.IsNullOrEmpty(suffix)
                        ? $"{clipName}_hit{hitIndex}"
                        : $"{clipName}{suffix}_hit{hitIndex}";
                    string keyBase = string.IsNullOrEmpty(suffix)
                        ? clipName
                        : $"{clipName}{suffix}";

                    if (AnimationTimingConfig.HasConfig(keyHit)) return keyHit;
                    if (AnimationTimingConfig.HasConfig(keyBase)) return keyBase;
                }

                // Final fallback
                if (AnimationTimingConfig.HasConfig($"{clipName}_hit{hitIndex}")) return $"{clipName}_hit{hitIndex}";
                if (AnimationTimingConfig.HasConfig(clipName)) return clipName;

                return clipName;
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error in BuildConfigKey: {ex.Message}");
                return clipName;
            }
        }

        public static int GetCurrentHitIndex(Animator animator, AnimationClip clip)
        {
            try
            {
                var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                float normalizedTime = stateInfo.normalizedTime % 1f;
                float currentTime = normalizedTime * clip.length;

                float closestTimeDiff = float.MaxValue;
                int hitIndex = 0;

                foreach (var evt in clip.events)
                {
                    if (evt.functionName == "OnAttackTrigger")
                    {
                        float timeDiff = Mathf.Abs(evt.time - currentTime);

                        if (timeDiff < closestTimeDiff && timeDiff < 0.1f)
                        {
                            closestTimeDiff = timeDiff;
                            hitIndex = evt.intParameter;
                        }
                    }
                }

                return hitIndex;
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error in GetCurrentHitIndex: {ex.Message}");
                return 0;
            }
        }

        // Common utility for getting current animation clip info
        public static bool TryGetCurrentClipInfo(Player player, out string clipName, out AnimationClip clip, out int hitIndex)
        {
            clipName = string.Empty;
            clip = null!;
            hitIndex = 0;

            try
            {
                if (!TryGetPlayerAnimator(player, out Animator animator) || animator == null)
                {
                    return false;
                }

                var clipInfos = animator.GetCurrentAnimatorClipInfo(0);
                if (clipInfos.Length == 0)
                {
                    return false;
                }

                clip = clipInfos[0].clip;
                clipName = clip.name;
                hitIndex = GetCurrentHitIndex(animator, clip);
                return true;
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error in TryGetCurrentClipInfo: {ex.Message}");
                return false;
            }
        }

        // Common utility for debug animator parameters
        public static void LogAnimatorParameters(Player player, string context)
        {
            if (!ExtraAttackPlugin.DebugAnimationParameters.Value)
                return;

            try
            {
                if (TryGetPlayerAnimator(player, out Animator? animator) && animator != null)
                {
                    int crouchingHash = ZSyncAnimation.GetHash("crouching");
                    int sitHash = ZSyncAnimation.GetHash("emote_sit");
                    int chairHash = ZSyncAnimation.GetHash("emote_sitchair");
                    bool crouchB = false, sitB = false, chairB = false;
                    
                    try { crouchB = animator.GetBool(crouchingHash); } catch { }
                    try { sitB = animator.GetBool(sitHash); } catch { }
                    try { chairB = animator.GetBool(chairHash); } catch { }
                    
                    ExtraAttackPlugin.LogInfo("AnimationParameters", $"{context}: crouch={crouchB} sit={sitB} sitchair={chairB}");
                }
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error in LogAnimatorParameters: {ex.Message}");
            }
        }

        [HarmonyPatch(typeof(CharacterAnimEvent), "CustomFixedUpdate")]
        public static class CharacterAnimEvent_CacheAnimator_Patch
        {
            private static bool parametersLogged = false;

            public static void Postfix(ref Animator ___m_animator, Character ___m_character)
            {
                try
                {
                    if (___m_character is Player player && player == Player.m_localPlayer)
                    {
                        if (___m_animator != null && !playerAnimators.ContainsKey(player))
                        {
                            playerAnimators[player] = ___m_animator;
                            ExtraAttackPlugin.LogInfo("System", "Animator cached successfully");

                            if (ExtraAttackPlugin.DebugAnimationParameters.Value && !parametersLogged)
                            {
                                parametersLogged = true;
                                ExtraAttackPlugin.LogInfo("System", "=== DEBUG: ANIMATOR PARAMETERS ===");
                                // Removed stray character causing compile error
                                AnimatorControllerParameter[] parameters = ___m_animator.parameters;
                                ExtraAttackPlugin.LogInfo("System", $"Total parameters: {parameters.Length}");

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

                                    ExtraAttackPlugin.LogInfo("System", $"  {typeStr.PadRight(10)} | {param.name}");
                                }

                                ExtraAttackPlugin.LogInfo("System", "=== END ANIMATOR PARAMETERS ===");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.LogError("System", $"Error caching animator: {ex.Message}");
                }
            }
        }

        [HarmonyPatch(typeof(Player), "OnDestroy")]
        public static class Player_OnDestroy_Patch
        {
            public static void Postfix(Player __instance)
            {
                try
                {
                    playerAnimators.Remove(__instance);
                    ExtraAttackUtils.CleanupPlayer(__instance);
                    ExtraAttackPlugin.LogInfo("System", "Cleaned up player data");
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.LogError("System", $"Error in OnDestroy: {ex.Message}");
                }
            }
        }
    }
}