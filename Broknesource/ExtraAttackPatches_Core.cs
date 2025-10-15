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
                var attackMode = EAS_CommonUtils.GetAttackMode(player);

                // Build unified key format: WeaponType_secondary_Mode
                string styleSuffix = attackMode switch
                {
                    EAS_CommonUtils.AttackMode.secondary_Q => "_secondary_Q",
                    EAS_CommonUtils.AttackMode.secondary_T => "_secondary_T",
                    EAS_CommonUtils.AttackMode.secondary_G => "_secondary_G",
                    _ => string.Empty
                };

                // Build key with weapon type and mode
                ItemDrop.ItemData weapon = player.GetCurrentWeapon();
                if (weapon != null && !string.IsNullOrEmpty(styleSuffix))
                {
                    string weaponType = EAS_CommonUtils.GetWeaponTypeFromSkill(weapon.m_shared!.m_skillType, weapon);
                    string unifiedKey = $"{weaponType}{styleSuffix}";
                    if (AnimationTimingConfig.HasConfig(unifiedKey)) return unifiedKey;
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

        [HarmonyPatch(typeof(CharacterAnimEvent), "CustomFixedUpdate")]
        public static class CharacterAnimEvent_CacheAnimator_Patch
        {
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
                    EAS_CommonUtils.CleanupPlayer(__instance);
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