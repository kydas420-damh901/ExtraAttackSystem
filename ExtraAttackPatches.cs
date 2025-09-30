using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ExtraAttackSystem
{
    public static class ExtraAttackPatches
    {
        private static readonly Dictionary<Player, Animator> playerAnimators = new();
        private static readonly Dictionary<string, AnimationClip> originalClips = new();

        // Helper method to get secondary attack clip name
        private static string GetSecondaryAttackClipName(ItemDrop.ItemData weapon)
        {
            return weapon.m_shared.m_skillType switch
            {
                Skills.SkillType.Swords => "Sword-Attack-R4",
                Skills.SkillType.Axes => "Axe Secondary Attack",
                Skills.SkillType.Clubs => "MaceAltAttack",
                Skills.SkillType.Knives => "Knife JumpAttack",
                Skills.SkillType.Spears => "atgeir_secondary",
                _ => "Greatsword Secondary Attack"
            };
        }

        // Initialize AOC lazily when needed
        private static void EnsureAOCInitialized(Player player, Animator animator)
        {
            try
            {
                if (animator.runtimeAnimatorController is AnimatorOverrideController)
                {
                    return;
                }

                // Save current animator state
                AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
                float normalizedTime = currentState.normalizedTime;
                int currentStateHash = currentState.fullPathHash;

                // Create ONE AnimatorOverrideController
                AnimatorOverrideController aoc = new(animator.runtimeAnimatorController);

                // Apply it
                animator.runtimeAnimatorController = aoc;

                // Immediately restore state to prevent transition glitch
                animator.Play(currentStateHash, 0, normalizedTime);
                animator.Update(0f);

                // Store reference
                AnimationManager.CustomRuntimeControllers["Main"] = aoc;

                ExtraAttackPlugin.ExtraAttackLogger.LogInfo("Main AnimatorOverrideController initialized (lazy init)");
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.ExtraAttackLogger.LogError($"Error initializing AOC: {ex.Message}");
            }
        }

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

        // Block vanilla secondary attack input using Reflection
        [HarmonyPatch(typeof(Player), "PlayerAttackInput")]
        public static class Player_PlayerAttackInput_Patch
        {
            private static System.Reflection.FieldInfo? queuedSecondAttackTimerField;

            public static void Postfix(Player __instance)
            {
                try
                {
                    if (ExtraAttackPlugin.IsExtraAttackKeyPressed())
                    {
                        // Cache field info for performance
                        if (queuedSecondAttackTimerField == null)
                        {
                            queuedSecondAttackTimerField = typeof(Player).GetField("m_queuedSecondAttackTimer",
                                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        }

                        if (queuedSecondAttackTimerField != null)
                        {
                            float currentValue = (float)queuedSecondAttackTimerField.GetValue(__instance);
                            if (currentValue > 0f)
                            {
                                queuedSecondAttackTimerField.SetValue(__instance, 0f);
                                ExtraAttackPlugin.ExtraAttackLogger.LogInfo("BLOCKED vanilla secondary attack (PlayerAttackInput)");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.ExtraAttackLogger.LogError($"Error in PlayerAttackInput_Patch: {ex.Message}");
                }
            }
        }

        // Apply AOC for Extra Attack animations
        [HarmonyPatch(typeof(ZSyncAnimation), "RPC_SetTrigger")]
        public static class ZSyncAnimation_RPC_SetTrigger_Patch
        {
            public static void Prefix(ZSyncAnimation __instance, long sender, string name)
            {
                try
                {
                    Player player = __instance.GetComponent<Player>();
                    if (player == null || player != Player.m_localPlayer)
                        return;

                    if (!playerAnimators.TryGetValue(player, out Animator animator) || animator == null)
                        return;

                    // Apply ExtraAttack AOC during Extra Attack state
                    if (ExtraAttackUtils.IsPlayerInExtraAttack(player))
                    {
                        if (name.Contains("secondary") || name.Contains("Secondary"))
                        {
                            // Ensure AOC is initialized
                            if (!AnimationManager.CustomRuntimeControllers.ContainsKey("ExtraAttack"))
                            {
                                InitializeAOC(player, animator);
                            }

                            // Apply ExtraAttack AOC
                            if (AnimationManager.CustomRuntimeControllers.TryGetValue("ExtraAttack", out var aoc) && aoc != null)
                            {
                                AnimationManager.FastReplaceRAC(player, aoc);
                                ExtraAttackPlugin.ExtraAttackLogger.LogInfo($"Applied ExtraAttack AOC for trigger: {name}");
                            }
                        }
                    }
                    else
                    {
                        // Restore Original AOC when not in ExtraAttack state
                        if (AnimationManager.CustomRuntimeControllers.TryGetValue("Original", out var originalAoc) && originalAoc != null)
                        {
                            if (animator.runtimeAnimatorController != originalAoc)
                            {
                                AnimationManager.FastReplaceRAC(player, originalAoc);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.ExtraAttackLogger.LogError($"Error in RPC_SetTrigger_Patch: {ex.Message}");
                }
            }
        }

        // Initialize AOC when first needed
        private static void InitializeAOC(Player player, Animator animator)
        {
            try
            {
                if (animator.runtimeAnimatorController == null)
                {
                    ExtraAttackPlugin.ExtraAttackLogger.LogWarning("RuntimeAnimatorController is null");
                    return;
                }

                // Create Original AOC (no replacements)
                if (!AnimationManager.CustomRuntimeControllers.ContainsKey("Original"))
                {
                    AnimationManager.CustomRuntimeControllers["Original"] = AnimationManager.MakeAOC(
                        new Dictionary<string, string>(),
                        animator.runtimeAnimatorController);
                    ExtraAttackPlugin.ExtraAttackLogger.LogInfo("Original AOC initialized");
                }

                // Create ExtraAttack AOC (with custom animations)
                if (!AnimationManager.CustomRuntimeControllers.ContainsKey("ExtraAttack"))
                {
                    if (AnimationManager.ReplacementMap.TryGetValue("ExtraAttack", out var replacementMap) && replacementMap.Count > 0)
                    {
                        AnimationManager.CustomRuntimeControllers["ExtraAttack"] = AnimationManager.MakeAOC(
                            replacementMap,
                            animator.runtimeAnimatorController);
                        ExtraAttackPlugin.ExtraAttackLogger.LogInfo($"ExtraAttack AOC initialized with {replacementMap.Count} replacements");
                    }
                    else
                    {
                        ExtraAttackPlugin.ExtraAttackLogger.LogWarning("No replacement map found for ExtraAttack");
                    }
                }
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.ExtraAttackLogger.LogError($"Error initializing AOC: {ex.Message}");
            }
        }

        // Simple button input handling
        [HarmonyPatch(typeof(Player), "Update")]
        public static class Player_Update_Patch
        {
            private static bool extraAttackTriggered = false;
            private static bool testButton1Pressed = false;
            private static bool testButton2Pressed = false;

            public static void Postfix(Player __instance)
            {
                if (__instance == null || !__instance.IsOwner())
                    return;

                try
                {
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
                if (player.InAttack())
                {
                    ExtraAttackPlugin.ExtraAttackLogger.LogInfo("Cannot use Extra Attack: Already attacking");
                    return false;
                }

                if (ExtraAttackUtils.IsPlayerOnCooldown(player))
                {
                    float remaining = ExtraAttackUtils.GetPlayerCooldownRemaining(player);
                    ExtraAttackUtils.ShowMessage(player, "extra_attack_cooldown", remaining.ToString("F1"));
                    return false;
                }

                ItemDrop.ItemData weapon = player.GetCurrentWeapon();
                if (weapon == null)
                {
                    ExtraAttackUtils.ShowMessage(player, "extra_attack_no_weapon");
                    return false;
                }

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
                    ExtraAttackPlugin.ExtraAttackLogger.LogInfo("============= EXTRA ATTACK TRIGGERED =============");

                    ItemDrop.ItemData weapon = player.GetCurrentWeapon();
                    if (weapon == null) return;

                    if (!playerAnimators.TryGetValue(player, out Animator? animator) || animator == null)
                    {
                        ExtraAttackPlugin.ExtraAttackLogger.LogError("Animator not found in cache");
                        return;
                    }

                    // Ensure AOC is initialized
                    if (!AnimationManager.CustomRuntimeControllers.ContainsKey("ExtraAttack"))
                    {
                        InitializeAOC(player, animator);
                    }

                    // Set extra attack state BEFORE calling StartAttack
                    ExtraAttackUtils.SetExtraAttackState(player, true);

                    // Set cooldown
                    ExtraAttackUtils.SetPlayerCooldown(player);

                    // Call StartAttack - AOC will be applied by RPC_SetTrigger patch
                    // StartAttack will trigger vanilla secondary, but AOC will replace the animation
                    player.StartAttack(null, true);

                    ExtraAttackPlugin.ExtraAttackLogger.LogInfo("StartAttack called - AOC will handle animation replacement");
                    ExtraAttackPlugin.ExtraAttackLogger.LogInfo("==================================================");

                    // Show message
                    ExtraAttackUtils.ShowMessage(player, "extra_attack_triggered");
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.ExtraAttackLogger.LogError($"Error in TriggerExtraAttack: {ex.Message}");
                    ExtraAttackUtils.SetExtraAttackState(player, false);
                }
            }

            private static void PlayTestAnimation(Player player, int buttonNumber)
            {
                try
                {
                    string clipName = buttonNumber == 1 ? "Great Sword Slash_40External" : "Great Sword Jump AttackExternal";

                    if (!AnimationManager.ExternalAnimations.TryGetValue(clipName, out AnimationClip clip))
                    {
                        ExtraAttackPlugin.ExtraAttackLogger.LogWarning($"Animation clip not found: {clipName}");
                        player.Message(MessageHud.MessageType.Center, $"Test animation {buttonNumber} not loaded");
                        return;
                    }

                    if (playerAnimators.TryGetValue(player, out Animator animator) && animator != null)
                    {
                        animator.enabled = false;
                        ExtraAttackPlugin.ExtraAttackLogger.LogInfo("Temporarily disabled Animator");
                    }

                    AnimationClip legacyClip = UnityEngine.Object.Instantiate(clip);
                    legacyClip.legacy = true;
                    legacyClip.wrapMode = WrapMode.Once;

                    ExtraAttackPlugin.ExtraAttackLogger.LogInfo($"Created legacy clip: {legacyClip.name}, legacy={legacyClip.legacy}, length={legacyClip.length}");

                    Animation animComponent = player.GetComponent<Animation>();
                    if (animComponent == null)
                    {
                        animComponent = player.gameObject.AddComponent<Animation>();
                        animComponent.playAutomatically = false;
                        ExtraAttackPlugin.ExtraAttackLogger.LogInfo("Added Animation component for debug playback");
                    }

                    string animName = $"TestAnim{buttonNumber}";
                    if (animComponent.GetClip(animName) != null)
                    {
                        animComponent.RemoveClip(animName);
                    }

                    animComponent.AddClip(legacyClip, animName);
                    animComponent.Play(animName);

                    player.StartCoroutine(ReEnableAnimatorAfterDelay(player, legacyClip.length));

                    player.Message(MessageHud.MessageType.Center, $"Test Animation {buttonNumber}!");
                    ExtraAttackPlugin.ExtraAttackLogger.LogInfo($"Test button {buttonNumber}: Playing {clipName} via Animation component, isPlaying={animComponent.isPlaying}");
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.ExtraAttackLogger.LogError($"Error playing test animation: {ex.Message}");
                    ExtraAttackPlugin.ExtraAttackLogger.LogError($"Stack trace: {ex.StackTrace}");

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

        // Damage multiplier patch
        [HarmonyPatch(typeof(Attack), "DoMeleeAttack")]
        public static class Attack_DoMeleeAttack_Patch
        {
            public static void Prefix(Attack __instance, ref Character ___m_character)
            {
                try
                {
                    if (___m_character is Player player && ExtraAttackUtils.IsPlayerInExtraAttack(player))
                    {
                        ExtraAttackPlugin.ExtraAttackLogger.LogInfo("============= DAMAGE CALCULATION START =============");

                        ItemDrop.ItemData weapon = player.GetCurrentWeapon();
                        if (weapon != null)
                        {
                            float multiplier = ExtraAttackPlugin.GetDamageMultiplier(weapon.m_shared.m_skillType);

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
                    if (___m_character is Player player)
                    {
                        if (ExtraAttackUtils.IsPlayerInExtraAttack(player))
                        {
                            ItemDrop.ItemData weapon = player.GetCurrentWeapon();
                            if (weapon != null)
                            {
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

                            ExtraAttackUtils.SetExtraAttackState(player, false);

                            ExtraAttackPlugin.ExtraAttackLogger.LogInfo("============= EXTRA ATTACK COMPLETED =============");
                        }
                    }
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.ExtraAttackLogger.LogError($"Error in DoMeleeAttack Postfix: {ex.Message}");
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