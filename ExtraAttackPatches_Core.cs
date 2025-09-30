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
                string modeSuffix = attackMode switch
                {
                    ExtraAttackUtils.AttackMode.ExtraQ => "_Q",
                    ExtraAttackUtils.AttackMode.ExtraT => "_T",
                    ExtraAttackUtils.AttackMode.ExtraG => "_G",
                    _ => ""
                };

                string key1 = $"{clipName}{modeSuffix}_hit{hitIndex}";
                string key2 = $"{clipName}{modeSuffix}";
                string key3 = $"{clipName}_hit{hitIndex}";
                string key4 = clipName;

                if (AnimationTimingConfig.HasConfig(key1)) return key1;
                if (AnimationTimingConfig.HasConfig(key2)) return key2;
                if (AnimationTimingConfig.HasConfig(key3)) return key3;
                if (AnimationTimingConfig.HasConfig(key4)) return key4;

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
                                ExtraAttackPlugin.LogInfo("AnimationParameters", "=== DEBUG: ANIMATOR PARAMETERS ===");

                                AnimatorControllerParameter[] parameters = ___m_animator.parameters;
                                ExtraAttackPlugin.LogInfo("AnimationParameters", $"Total parameters: {parameters.Length}");

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

                                    ExtraAttackPlugin.LogInfo("AnimationParameters", $"  {typeStr.PadRight(10)} | {param.name}");
                                }

                                ExtraAttackPlugin.LogInfo("AnimationParameters", "=== END ANIMATOR PARAMETERS ===");
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