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
        public static string BuildConfigKey(Player player, string clipName)
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
                    ExtraAttackUtils.AttackMode.secondary_Q => "_secondary_Q",
                    ExtraAttackUtils.AttackMode.secondary_T => "_secondary_T",
                    ExtraAttackUtils.AttackMode.secondary_G => "_secondary_G",
                    _ => string.Empty
                };

                string legacySuffix = attackMode switch
                {
                    ExtraAttackUtils.AttackMode.secondary_Q => "_Q",
                    ExtraAttackUtils.AttackMode.secondary_T => "_T",
                    ExtraAttackUtils.AttackMode.secondary_G => "_G",
                    _ => string.Empty
                };

                var suffixCandidates = new List<string>();
                if (!string.IsNullOrEmpty(styleSuffix)) suffixCandidates.Add(styleSuffix);
                if (!string.IsNullOrEmpty(legacySuffix)) suffixCandidates.Add(legacySuffix);
                suffixCandidates.Add(string.Empty);

                // Try keys in order: clip + suffix, clip
                foreach (var suffix in suffixCandidates)
                {
                    string keyBase = string.IsNullOrEmpty(suffix)
                        ? clipName
                        : $"{clipName}{suffix}";

                    if (AnimationTimingConfig.HasConfig(keyBase)) return keyBase;
                }

                // Final fallback
                if (AnimationTimingConfig.HasConfig(clipName)) return clipName;

                return clipName;
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error in BuildConfigKey: {ex.Message}");
                return clipName;
            }
        }


        // Common utility for getting current animation clip info
        public static bool TryGetCurrentClipInfo(Player player, out string clipName, out AnimationClip clip)
        {
            clipName = string.Empty;
            clip = null!;

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
                return true;
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error in TryGetCurrentClipInfo: {ex.Message}");
                return false;
            }
        }

        // Common utility for debug animator parameters (using EAS_Debug.cs)
        public static void LogAnimatorParameters(Player player, string context)
        {
            // Animation Parameters is now a list-type debug - use LogAllAnimationParameters instead
            try
            {
                if (TryGetPlayerAnimator(player, out Animator? animator) && animator != null)
                {
                    EAS_Debug.LogAllAnimationParameters();
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

                            if (!parametersLogged)
                            {
                                parametersLogged = true;
                                LogAnimatorParameters(player, "Animator cached");
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