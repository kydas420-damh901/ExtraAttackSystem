using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ExtraAttackSystem
{
    // Player input and update patches
    public static class ExtraAttackPatches_Player
    {
        [HarmonyPatch(typeof(Player), "Update")]
        public static class Player_Update_Patch
        {
            private static bool extraAttackTriggered = false;
            private static bool testButton1Pressed = false;
            private static bool testButton2Pressed = false;
            private static bool reloadKeyPressed = false;
            private static readonly Dictionary<Player, bool> wasInAttack = new();

            public static void Postfix(Player __instance)
            {
                if (__instance == null || !__instance.IsOwner())
                    return;

                try
                {
                    // F6 key: Reload YAML
                    if (Input.GetKeyDown(KeyCode.F6))
                    {
                        if (!reloadKeyPressed)
                        {
                            AnimationTimingConfig.Reload();
                            __instance.Message(MessageHud.MessageType.Center, "AnimationTiming.yaml reloaded!");
                            reloadKeyPressed = true;
                        }
                    }
                    else
                    {
                        reloadKeyPressed = false;
                    }

                    // Track attack state and reset mode when attack ends
                    bool currentlyInAttack = __instance.InAttack();
                    if (wasInAttack.TryGetValue(__instance, out bool previouslyInAttack))
                    {
                        if (previouslyInAttack && !currentlyInAttack)
                        {
                            var currentMode = ExtraAttackUtils.GetAttackMode(__instance);
                            if (currentMode != ExtraAttackUtils.AttackMode.Normal)
                            {
                                ExtraAttackUtils.SetAttackMode(__instance, ExtraAttackUtils.AttackMode.Normal);

                                var animEvent = __instance.GetComponent<CharacterAnimEvent>();
                                if (animEvent != null)
                                {
                                    animEvent.ResetChain();
                                }

                                ExtraAttackPlugin.LogInfo("AOC", $"Attack finished, reset mode from {currentMode} to Normal");
                            }
                        }
                    }
                    wasInAttack[__instance] = currentlyInAttack;

                    // Q key: Extra Attack
                    if (ExtraAttackPlugin.IsExtraAttackKeyPressed())
                    {
                        if (!extraAttackTriggered && CanPerformExtraAttack(__instance))
                        {
                            TriggerExtraAttack(__instance);
                            extraAttackTriggered = true;
                        }
                    }
                    else
                    {
                        extraAttackTriggered = false;
                    }

                    // G key: Custom Attack 2
                    if (ExtraAttackPlugin.IsTestButton1Pressed() && !testButton1Pressed)
                    {
                        if (CanPerformNormalAttack(__instance, "G"))
                        {
                            TriggerNormalAttack(__instance, "G");
                            testButton1Pressed = true;
                        }
                    }
                    else if (!ExtraAttackPlugin.IsTestButton1Pressed())
                    {
                        testButton1Pressed = false;
                    }

                    // T key: Custom Attack 1
                    if (ExtraAttackPlugin.IsTestButton2Pressed() && !testButton2Pressed)
                    {
                        if (CanPerformNormalAttack(__instance, "T"))
                        {
                            TriggerNormalAttack(__instance, "T");
                            testButton2Pressed = true;
                        }
                    }
                    else if (!ExtraAttackPlugin.IsTestButton2Pressed())
                    {
                        testButton2Pressed = false;
                    }
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.LogError("System", $"Error in Player_Update_Patch: {ex.Message}");
                }
            }

            private static bool CanPerformExtraAttack(Player player)
            {
                if (player.InAttack())
                {
                    return false;
                }

                if (ExtraAttackUtils.IsPlayerOnCooldown(player, ExtraAttackUtils.AttackMode.ExtraQ))
                {
                    float remaining = ExtraAttackUtils.GetPlayerCooldownRemaining(player, ExtraAttackUtils.AttackMode.ExtraQ);
                    ExtraAttackUtils.ShowMessage(player, "extra_attack_cooldown", remaining.ToString("F1"));
                    return false;
                }

                ItemDrop.ItemData weapon = player.GetCurrentWeapon();
                if (weapon == null)
                {
                    return false;
                }

                float staminaCost = ExtraAttackPlugin.GetStaminaCost(weapon.m_shared.m_skillType);
                if (player.GetStamina() < staminaCost)
                {
                    return false;
                }

                return true;
            }

            private static bool CanPerformNormalAttack(Player player, string buttonName)
            {
                if (player.InAttack())
                {
                    return false;
                }

                var mode = buttonName == "T" ? ExtraAttackUtils.AttackMode.ExtraT : ExtraAttackUtils.AttackMode.ExtraG;
                if (ExtraAttackUtils.IsPlayerOnCooldown(player, mode))
                {
                    float remaining = ExtraAttackUtils.GetPlayerCooldownRemaining(player, mode);
                    ExtraAttackUtils.ShowMessage(player, "extra_attack_cooldown", remaining.ToString("F1"));
                    return false;
                }

                ItemDrop.ItemData weapon = player.GetCurrentWeapon();
                if (weapon == null)
                {
                    return false;
                }

                return true;
            }

            private static void TriggerExtraAttack(Player player)
            {
                try
                {
                    ItemDrop.ItemData weapon = player.GetCurrentWeapon();
                    if (weapon == null) return;

                    if (!ExtraAttackPatches_Core.TryGetPlayerAnimator(player, out Animator? animator) || animator == null)
                    {
                        return;
                    }

                    if (!AnimationManager.CustomRuntimeControllers.ContainsKey("ExtraAttack_Q"))
                    {
                        ExtraAttackPatches_Animation.InitializeAOC(player, animator);
                    }

                    ExtraAttackUtils.SetAttackMode(player, ExtraAttackUtils.AttackMode.ExtraQ);
                    ExtraAttackUtils.SetPlayerCooldown(player, ExtraAttackUtils.AttackMode.ExtraQ);

                    player.StartAttack(null, true);
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.LogError("System", $"Error in TriggerExtraAttack: {ex.Message}");
                    ExtraAttackUtils.SetAttackMode(player, ExtraAttackUtils.AttackMode.Normal);
                }
            }

            private static void TriggerNormalAttack(Player player, string buttonName)
            {
                try
                {
                    ItemDrop.ItemData weapon = player.GetCurrentWeapon();
                    if (weapon == null)
                    {
                        return;
                    }

                    if (!ExtraAttackPatches_Core.TryGetPlayerAnimator(player, out Animator? animator) || animator == null)
                    {
                        return;
                    }

                    if (!AnimationManager.CustomRuntimeControllers.ContainsKey("ExtraAttack_T_Swords"))
                    {
                        ExtraAttackPatches_Animation.InitializeAOC(player, animator);
                    }

                    var mode = buttonName == "T" ? ExtraAttackUtils.AttackMode.ExtraT : ExtraAttackUtils.AttackMode.ExtraG;
                    ExtraAttackUtils.SetAttackMode(player, mode);
                    ExtraAttackUtils.SetPlayerCooldown(player, mode);

                    var animEvent = player.GetComponent<CharacterAnimEvent>();
                    if (animEvent != null)
                    {
                        animEvent.ResetChain();
                    }

                    player.StartAttack(null, false);
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.LogError("System", $"Error in {buttonName} button: {ex.Message}");
                    ExtraAttackUtils.SetAttackMode(player, ExtraAttackUtils.AttackMode.Normal);
                }
            }
        }
    }
}