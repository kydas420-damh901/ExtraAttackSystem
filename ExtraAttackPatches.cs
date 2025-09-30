using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ExtraAttackSystem
{
    public static class ExtraAttackPatches
    {
        private static readonly Dictionary<Player, Animator> playerAnimators = new();

        // Cache animator via Harmony injection
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
                            ExtraAttackPlugin.ExtraAttackLogger.LogInfo("Animator cached successfully");

                            // DEBUG: Animation Parameters (once per session)
                            if (ExtraAttackPlugin.DebugAnimationParameters.Value && !parametersLogged)
                            {
                                parametersLogged = true;
                                ExtraAttackPlugin.ExtraAttackLogger.LogInfo("=== DEBUG: ANIMATOR PARAMETERS ===");

                                AnimatorControllerParameter[] parameters = ___m_animator.parameters;
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
                        }
                    }
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.ExtraAttackLogger.LogError($"Error caching animator: {ex.Message}");
                }
            }
        }

        // Initialize AnimatorOverrideControllers and keep Original as default
        [HarmonyPatch(typeof(Player), "Start")]
        public static class Player_Start_Patch
        {
            public static void Postfix(Player __instance)
            {
                try
                {
                    if (AnimationManager.CustomRuntimeControllers.Count == 0 && Player.m_localPlayer is not null)
                    {
                        if (playerAnimators.TryGetValue(__instance, out Animator animator) && animator != null)
                        {
                            // Create Original controller (no replacements)
                            AnimationManager.CustomRuntimeControllers["Original"] = AnimationManager.MakeAOC(
                                new Dictionary<string, string>(), animator.runtimeAnimatorController);
                            ExtraAttackPlugin.ExtraAttackLogger.LogInfo("Original AnimatorOverrideController initialized");

                            // Create ExtraAttack controller (with replacements)
                            if (AnimationManager.ReplacementMap.ContainsKey("ExtraAttack") &&
                                AnimationManager.ReplacementMap["ExtraAttack"].Count > 0)
                            {
                                AnimationManager.CustomRuntimeControllers["ExtraAttack"] = AnimationManager.MakeAOC(
                                    AnimationManager.ReplacementMap["ExtraAttack"], animator.runtimeAnimatorController);
                                ExtraAttackPlugin.ExtraAttackLogger.LogInfo("ExtraAttack AnimatorOverrideController initialized");
                            }

                            // Set Original as default controller
                            animator.runtimeAnimatorController = AnimationManager.CustomRuntimeControllers["Original"];
                            animator.Update(Time.deltaTime);
                            ExtraAttackPlugin.ExtraAttackLogger.LogInfo("Set Original controller as default");
                        }
                    }
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.ExtraAttackLogger.LogError($"Error in Player_Start_Patch: {ex.Message}");
                }
            }
        }

        // Block vanilla secondary attack when Extra Attack key is pressed
        [HarmonyPatch(typeof(Player), "SetControls")]
        public static class Player_SetControls_Patch
        {
            public static void Prefix(ref bool secondaryAttack)
            {
                if (ExtraAttackPlugin.IsExtraAttackKeyPressed())
                {
                    secondaryAttack = false;
                }
            }
        }

        // Simple button input handling
        [HarmonyPatch(typeof(Player), "Update")]
        public static class Player_Update_Patch
        {
            private static bool extraAttackTriggered = false;
            private static bool testButton1Pressed = false;
            private static bool testButton2Pressed = false;
            private static bool controllerSwitched = false; // NEW: Track if we already switched

            public static void Postfix(Player __instance)
            {
                if (__instance == null || !__instance.IsOwner())
                    return;

                try
                {
                    // Original Extra Attack functionality
                    if (ExtraAttackPlugin.IsExtraAttackKeyPressed())
                    {
                        if (!extraAttackTriggered && CanPerformExtraAttack(__instance))
                        {
                            TriggerExtraAttack(__instance);
                            extraAttackTriggered = true;
                            controllerSwitched = true; // Mark that we switched
                        }
                    }
                    else
                    {
                        extraAttackTriggered = false;
                        controllerSwitched = false; // Reset when key released
                    }

                    // Test buttons (keep existing functionality)
                    if (ExtraAttackPlugin.IsTestButton1Pressed() && !testButton1Pressed)
                    {
                        PlayTestAnimation(__instance, 1);
                        testButton1Pressed = true;
                    }
                    else if (!ExtraAttackPlugin.IsTestButton1Pressed())
                    {
                        testButton1Pressed = false;
                    }

                    if (ExtraAttackPlugin.IsTestButton2Pressed() && !testButton2Pressed)
                    {
                        PlayTestAnimation(__instance, 2);
                        testButton2Pressed = true;
                    }
                    else if (!ExtraAttackPlugin.IsTestButton2Pressed())
                    {
                        testButton2Pressed = false;
                    }
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.ExtraAttackLogger.LogError($"Error in Player_Update_Patch: {ex.Message}");
                }
            }

            private static bool CanPerformExtraAttack(Player player)
            {
                // Check cooldown
                if (ExtraAttackUtils.IsPlayerOnCooldown(player))
                {
                    float remaining = ExtraAttackUtils.GetPlayerCooldownRemaining(player);
                    ExtraAttackUtils.ShowMessage(player, "extra_attack_cooldown", remaining.ToString("F1"));
                    return false;
                }

                // Check if weapon equipped
                ItemDrop.ItemData weapon = player.GetCurrentWeapon();
                if (weapon == null)
                {
                    ExtraAttackUtils.ShowMessage(player, "extra_attack_no_weapon");
                    return false;
                }

                // Check stamina
                float staminaCost = ExtraAttackPlugin.GetStaminaCost(weapon.m_shared.m_skillType);
                if (player.GetStamina() < staminaCost)
                {
                    ExtraAttackUtils.ShowMessage(player, "extra_attack_no_stamina");
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

                    // Only switch if not already switched
                    if (!controllerSwitched && playerAnimators.TryGetValue(player, out Animator animator) && animator != null)
                    {
                        if (AnimationManager.CustomRuntimeControllers.TryGetValue("ExtraAttack", out var controller))
                        {
                            animator.runtimeAnimatorController = controller;
                            animator.Update(Time.deltaTime);
                            ExtraAttackPlugin.ExtraAttackLogger.LogInfo("Switched to ExtraAttack controller");
                        }
                    }

                    // Set extra attack state (for damage multiplier)
                    ExtraAttackUtils.SetExtraAttackState(player, true);

                    // Consume stamina
                    float staminaCost = ExtraAttackPlugin.GetStaminaCost(weapon.m_shared.m_skillType);
                    player.UseStamina(staminaCost);

                    // Set cooldown
                    ExtraAttackUtils.SetPlayerCooldown(player);

                    // Use vanilla StartAttack - it handles all the parameter passing internally
                    player.StartAttack(null, true); // null = no target, true = secondary attack

                    ExtraAttackPlugin.ExtraAttackLogger.LogInfo("Started secondary attack with custom animations");

                    // Show message
                    ExtraAttackUtils.ShowMessage(player, "extra_attack_triggered");
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.ExtraAttackLogger.LogError($"Error in TriggerExtraAttack: {ex.Message}");
                }
            }

            private static void PlayTestAnimation(Player player, int buttonNumber)
            {
                try
                {
                    // Get animation clip from AssetBundle
                    string clipName = buttonNumber == 1 ? "Great Sword Slash_40External" : "Great Sword Jump AttackExternal";

                    if (!AnimationManager.ExternalAnimations.TryGetValue(clipName, out AnimationClip clip))
                    {
                        ExtraAttackPlugin.ExtraAttackLogger.LogWarning($"Animation clip not found: {clipName}");
                        player.Message(MessageHud.MessageType.Center, $"Test animation {buttonNumber} not loaded");
                        return;
                    }

                    // Temporarily disable Animator to allow Animation component to work
                    if (playerAnimators.TryGetValue(player, out Animator animator) && animator != null)
                    {
                        animator.enabled = false;
                        ExtraAttackPlugin.ExtraAttackLogger.LogInfo("Temporarily disabled Animator");
                    }

                    // Clone the clip and force legacy mode
                    AnimationClip legacyClip = UnityEngine.Object.Instantiate(clip);
                    legacyClip.legacy = true;
                    legacyClip.wrapMode = WrapMode.Once;

                    ExtraAttackPlugin.ExtraAttackLogger.LogInfo($"Created legacy clip: {legacyClip.name}, legacy={legacyClip.legacy}, length={legacyClip.length}");

                    // Get or add Animation component for legacy playback
                    Animation animComponent = player.GetComponent<Animation>();
                    if (animComponent == null)
                    {
                        animComponent = player.gameObject.AddComponent<Animation>();
                        animComponent.playAutomatically = false;
                        ExtraAttackPlugin.ExtraAttackLogger.LogInfo("Added Animation component for debug playback");
                    }

                    // Remove old clip if exists
                    string animName = $"TestAnim{buttonNumber}";
                    if (animComponent.GetClip(animName) != null)
                    {
                        animComponent.RemoveClip(animName);
                    }

                    // Add clip to Animation component
                    animComponent.AddClip(legacyClip, animName);

                    // Play the animation
                    animComponent.Play(animName);

                    // Re-enable Animator after a delay (animation length)
                    player.StartCoroutine(ReEnableAnimatorAfterDelay(player, legacyClip.length));

                    player.Message(MessageHud.MessageType.Center, $"Test Animation {buttonNumber}!");
                    ExtraAttackPlugin.ExtraAttackLogger.LogInfo($"Test button {buttonNumber}: Playing {clipName} via Animation component, isPlaying={animComponent.isPlaying}");
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.ExtraAttackLogger.LogError($"Error playing test animation: {ex.Message}");
                    ExtraAttackPlugin.ExtraAttackLogger.LogError($"Stack trace: {ex.StackTrace}");

                    // Make sure to re-enable Animator on error
                    if (playerAnimators.TryGetValue(player, out Animator animator) && animator != null)
                    {
                        animator.enabled = true;
                    }
                }
            }

            private static System.Collections.IEnumerator ReEnableAnimatorAfterDelay(Player player, float delay)
            {
                yield return new UnityEngine.WaitForSeconds(delay + 0.1f);

                if (playerAnimators.TryGetValue(player, out Animator animator) && animator != null)
                {
                    animator.enabled = true;
                    ExtraAttackPlugin.ExtraAttackLogger.LogInfo("Re-enabled Animator");
                }
            }
        }

        // Damage multiplier patch for Extra Attack
        [HarmonyPatch(typeof(Attack), "DoMeleeAttack")]
        public static class Attack_DoMeleeAttack_Patch
        {
            public static void Prefix(Attack __instance, ref Character ___m_character)
            {
                try
                {
                    if (___m_character is Player player && ExtraAttackUtils.IsPlayerInExtraAttack(player))
                    {
                        // Double-check: Only apply multiplier if using ExtraAttack controller
                        if (playerAnimators.TryGetValue(player, out Animator animator) && animator != null)
                        {
                            var currentController = animator.runtimeAnimatorController;
                            bool isExtraAttackController = AnimationManager.CustomRuntimeControllers.TryGetValue("ExtraAttack", out var extraController)
                                && currentController == extraController;

                            if (!isExtraAttackController)
                            {
                                // Not using ExtraAttack controller, so don't apply multiplier
                                ExtraAttackPlugin.ExtraAttackLogger.LogInfo("Skipping multiplier - not using ExtraAttack controller");
                                return;
                            }
                        }

                        ItemDrop.ItemData weapon = player.GetCurrentWeapon();
                        if (weapon != null)
                        {
                            float multiplier = ExtraAttackPlugin.GetDamageMultiplier(weapon.m_shared.m_skillType);

                            // Multiply all damage types
                            weapon.m_shared.m_damages.m_damage *= multiplier;
                            weapon.m_shared.m_damages.m_blunt *= multiplier;
                            weapon.m_shared.m_damages.m_slash *= multiplier;
                            weapon.m_shared.m_damages.m_pierce *= multiplier;
                            weapon.m_shared.m_damages.m_chop *= multiplier;
                            weapon.m_shared.m_damages.m_pickaxe *= multiplier;
                            weapon.m_shared.m_damages.m_fire *= multiplier;
                            weapon.m_shared.m_damages.m_frost *= multiplier;
                            weapon.m_shared.m_damages.m_lightning *= multiplier;
                            weapon.m_shared.m_damages.m_poison *= multiplier;
                            weapon.m_shared.m_damages.m_spirit *= multiplier;

                            ExtraAttackPlugin.ExtraAttackLogger.LogInfo($"Extra Attack damage multiplier applied: x{multiplier}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.ExtraAttackLogger.LogError($"Error in Attack_DoMeleeAttack_Patch: {ex.Message}");
                }
            }

            public static void Postfix(Attack __instance, ref Character ___m_character)
            {
                try
                {
                    if (___m_character is Player player && ExtraAttackUtils.IsPlayerInExtraAttack(player))
                    {
                        ItemDrop.ItemData weapon = player.GetCurrentWeapon();
                        if (weapon != null)
                        {
                            // Restore original damage values
                            float multiplier = ExtraAttackPlugin.GetDamageMultiplier(weapon.m_shared.m_skillType);
                            float divisor = 1f / multiplier;

                            weapon.m_shared.m_damages.m_damage *= divisor;
                            weapon.m_shared.m_damages.m_blunt *= divisor;
                            weapon.m_shared.m_damages.m_slash *= divisor;
                            weapon.m_shared.m_damages.m_pierce *= divisor;
                            weapon.m_shared.m_damages.m_chop *= divisor;
                            weapon.m_shared.m_damages.m_pickaxe *= divisor;
                            weapon.m_shared.m_damages.m_fire *= divisor;
                            weapon.m_shared.m_damages.m_frost *= divisor;
                            weapon.m_shared.m_damages.m_lightning *= divisor;
                            weapon.m_shared.m_damages.m_poison *= divisor;
                            weapon.m_shared.m_damages.m_spirit *= divisor;

                            ExtraAttackPlugin.ExtraAttackLogger.LogInfo("Restored original damage values");
                        }

                        // Reset extra attack state
                        ExtraAttackUtils.SetExtraAttackState(player, false);

                        // Switch back to Original controller
                        if (playerAnimators.TryGetValue(player, out Animator animator) && animator != null)
                        {
                            if (AnimationManager.CustomRuntimeControllers.TryGetValue("Original", out var controller))
                            {
                                animator.runtimeAnimatorController = controller;
                                animator.Update(Time.deltaTime);
                                ExtraAttackPlugin.ExtraAttackLogger.LogInfo("Switched back to Original controller");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.ExtraAttackLogger.LogError($"Error restoring damage values: {ex.Message}");
                }
            }
        }

        // Clean up
        [HarmonyPatch(typeof(Player), "OnDestroy")]
        public static class Player_OnDestroy_Patch
        {
            public static void Postfix(Player __instance)
            {
                try
                {
                    playerAnimators.Remove(__instance);
                    ExtraAttackUtils.CleanupPlayer(__instance);
                    ExtraAttackPlugin.ExtraAttackLogger.LogInfo("Cleaned up player data");
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.ExtraAttackLogger.LogError($"Error in OnDestroy: {ex.Message}");
                }
            }
        }
    }
}