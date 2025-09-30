using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ExtraAttackSystem
{
    public static class ExtraAttackPatches
    {
        private static readonly Dictionary<Player, Animator> playerAnimators = new();
<<<<<<< HEAD
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
=======
>>>>>>> e233f14d20c2c5b8b9cabdc94021f07d78cf3d58

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

<<<<<<< HEAD
=======
                            // DEBUG: Animation Parameters (once per session)
>>>>>>> e233f14d20c2c5b8b9cabdc94021f07d78cf3d58
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

<<<<<<< HEAD
        // Block vanilla secondary attack input using Reflection
        [HarmonyPatch(typeof(Player), "PlayerAttackInput")]
        public static class Player_PlayerAttackInput_Patch
        {
            private static System.Reflection.FieldInfo? queuedSecondAttackTimerField;

=======
        // Initialize AnimatorOverrideControllers and keep Original as default
        [HarmonyPatch(typeof(Player), "Start")]
        public static class Player_Start_Patch
        {
>>>>>>> e233f14d20c2c5b8b9cabdc94021f07d78cf3d58
            public static void Postfix(Player __instance)
            {
                try
                {
<<<<<<< HEAD
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
=======
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
>>>>>>> e233f14d20c2c5b8b9cabdc94021f07d78cf3d58
                        }
                    }
                }
                catch (Exception ex)
                {
<<<<<<< HEAD
                    ExtraAttackPlugin.ExtraAttackLogger.LogError($"Error in PlayerAttackInput_Patch: {ex.Message}");
=======
                    ExtraAttackPlugin.ExtraAttackLogger.LogError($"Error in Player_Start_Patch: {ex.Message}");
>>>>>>> e233f14d20c2c5b8b9cabdc94021f07d78cf3d58
                }
            }
        }

<<<<<<< HEAD
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
=======
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
>>>>>>> e233f14d20c2c5b8b9cabdc94021f07d78cf3d58
            }
        }

        // Simple button input handling
        [HarmonyPatch(typeof(Player), "Update")]
        public static class Player_Update_Patch
        {
            private static bool extraAttackTriggered = false;
            private static bool testButton1Pressed = false;
            private static bool testButton2Pressed = false;
<<<<<<< HEAD
=======
            private static bool controllerSwitched = false; // NEW: Track if we already switched
>>>>>>> e233f14d20c2c5b8b9cabdc94021f07d78cf3d58

            public static void Postfix(Player __instance)
            {
                if (__instance == null || !__instance.IsOwner())
                    return;

                try
                {
<<<<<<< HEAD
=======
                    // Original Extra Attack functionality
>>>>>>> e233f14d20c2c5b8b9cabdc94021f07d78cf3d58
                    if (ExtraAttackPlugin.IsExtraAttackKeyPressed())
                    {
                        if (!extraAttackTriggered && CanPerformExtraAttack(__instance))
                        {
                            TriggerExtraAttack(__instance);
                            extraAttackTriggered = true;
<<<<<<< HEAD
=======
                            controllerSwitched = true; // Mark that we switched
>>>>>>> e233f14d20c2c5b8b9cabdc94021f07d78cf3d58
                        }
                    }
                    else
                    {
                        extraAttackTriggered = false;
<<<<<<< HEAD
                    }

=======
                        controllerSwitched = false; // Reset when key released
                    }

                    // Test buttons (keep existing functionality)
>>>>>>> e233f14d20c2c5b8b9cabdc94021f07d78cf3d58
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
<<<<<<< HEAD
                if (player.InAttack())
                {
                    ExtraAttackPlugin.ExtraAttackLogger.LogInfo("Cannot use Extra Attack: Already attacking");
                    return false;
                }

=======
                // Check cooldown
>>>>>>> e233f14d20c2c5b8b9cabdc94021f07d78cf3d58
                if (ExtraAttackUtils.IsPlayerOnCooldown(player))
                {
                    float remaining = ExtraAttackUtils.GetPlayerCooldownRemaining(player);
                    ExtraAttackUtils.ShowMessage(player, "extra_attack_cooldown", remaining.ToString("F1"));
                    return false;
                }

<<<<<<< HEAD
=======
                // Check if weapon equipped
>>>>>>> e233f14d20c2c5b8b9cabdc94021f07d78cf3d58
                ItemDrop.ItemData weapon = player.GetCurrentWeapon();
                if (weapon == null)
                {
                    ExtraAttackUtils.ShowMessage(player, "extra_attack_no_weapon");
                    return false;
                }

<<<<<<< HEAD
=======
                // Check stamina
>>>>>>> e233f14d20c2c5b8b9cabdc94021f07d78cf3d58
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
<<<<<<< HEAD
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
=======
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
>>>>>>> e233f14d20c2c5b8b9cabdc94021f07d78cf3d58

                    // Show message
                    ExtraAttackUtils.ShowMessage(player, "extra_attack_triggered");
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.ExtraAttackLogger.LogError($"Error in TriggerExtraAttack: {ex.Message}");
<<<<<<< HEAD
                    ExtraAttackUtils.SetExtraAttackState(player, false);
=======
>>>>>>> e233f14d20c2c5b8b9cabdc94021f07d78cf3d58
                }
            }

            private static void PlayTestAnimation(Player player, int buttonNumber)
            {
                try
                {
<<<<<<< HEAD
=======
                    // Get animation clip from AssetBundle
>>>>>>> e233f14d20c2c5b8b9cabdc94021f07d78cf3d58
                    string clipName = buttonNumber == 1 ? "Great Sword Slash_40External" : "Great Sword Jump AttackExternal";

                    if (!AnimationManager.ExternalAnimations.TryGetValue(clipName, out AnimationClip clip))
                    {
                        ExtraAttackPlugin.ExtraAttackLogger.LogWarning($"Animation clip not found: {clipName}");
                        player.Message(MessageHud.MessageType.Center, $"Test animation {buttonNumber} not loaded");
                        return;
                    }

<<<<<<< HEAD
=======
                    // Temporarily disable Animator to allow Animation component to work
>>>>>>> e233f14d20c2c5b8b9cabdc94021f07d78cf3d58
                    if (playerAnimators.TryGetValue(player, out Animator animator) && animator != null)
                    {
                        animator.enabled = false;
                        ExtraAttackPlugin.ExtraAttackLogger.LogInfo("Temporarily disabled Animator");
                    }

<<<<<<< HEAD
=======
                    // Clone the clip and force legacy mode
>>>>>>> e233f14d20c2c5b8b9cabdc94021f07d78cf3d58
                    AnimationClip legacyClip = UnityEngine.Object.Instantiate(clip);
                    legacyClip.legacy = true;
                    legacyClip.wrapMode = WrapMode.Once;

                    ExtraAttackPlugin.ExtraAttackLogger.LogInfo($"Created legacy clip: {legacyClip.name}, legacy={legacyClip.legacy}, length={legacyClip.length}");

<<<<<<< HEAD
=======
                    // Get or add Animation component for legacy playback
>>>>>>> e233f14d20c2c5b8b9cabdc94021f07d78cf3d58
                    Animation animComponent = player.GetComponent<Animation>();
                    if (animComponent == null)
                    {
                        animComponent = player.gameObject.AddComponent<Animation>();
                        animComponent.playAutomatically = false;
                        ExtraAttackPlugin.ExtraAttackLogger.LogInfo("Added Animation component for debug playback");
                    }

<<<<<<< HEAD
=======
                    // Remove old clip if exists
>>>>>>> e233f14d20c2c5b8b9cabdc94021f07d78cf3d58
                    string animName = $"TestAnim{buttonNumber}";
                    if (animComponent.GetClip(animName) != null)
                    {
                        animComponent.RemoveClip(animName);
                    }

<<<<<<< HEAD
                    animComponent.AddClip(legacyClip, animName);
                    animComponent.Play(animName);

=======
                    // Add clip to Animation component
                    animComponent.AddClip(legacyClip, animName);

                    // Play the animation
                    animComponent.Play(animName);

                    // Re-enable Animator after a delay (animation length)
>>>>>>> e233f14d20c2c5b8b9cabdc94021f07d78cf3d58
                    player.StartCoroutine(ReEnableAnimatorAfterDelay(player, legacyClip.length));

                    player.Message(MessageHud.MessageType.Center, $"Test Animation {buttonNumber}!");
                    ExtraAttackPlugin.ExtraAttackLogger.LogInfo($"Test button {buttonNumber}: Playing {clipName} via Animation component, isPlaying={animComponent.isPlaying}");
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.ExtraAttackLogger.LogError($"Error playing test animation: {ex.Message}");
                    ExtraAttackPlugin.ExtraAttackLogger.LogError($"Stack trace: {ex.StackTrace}");

<<<<<<< HEAD
=======
                    // Make sure to re-enable Animator on error
>>>>>>> e233f14d20c2c5b8b9cabdc94021f07d78cf3d58
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

<<<<<<< HEAD
        // Damage multiplier patch
=======
        // Damage multiplier patch for Extra Attack
>>>>>>> e233f14d20c2c5b8b9cabdc94021f07d78cf3d58
        [HarmonyPatch(typeof(Attack), "DoMeleeAttack")]
        public static class Attack_DoMeleeAttack_Patch
        {
            public static void Prefix(Attack __instance, ref Character ___m_character)
            {
                try
                {
                    if (___m_character is Player player && ExtraAttackUtils.IsPlayerInExtraAttack(player))
                    {
<<<<<<< HEAD
                        ExtraAttackPlugin.ExtraAttackLogger.LogInfo("============= DAMAGE CALCULATION START =============");
=======
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
>>>>>>> e233f14d20c2c5b8b9cabdc94021f07d78cf3d58

                        ItemDrop.ItemData weapon = player.GetCurrentWeapon();
                        if (weapon != null)
                        {
                            float multiplier = ExtraAttackPlugin.GetDamageMultiplier(weapon.m_shared.m_skillType);

<<<<<<< HEAD
=======
                            // Multiply all damage types
>>>>>>> e233f14d20c2c5b8b9cabdc94021f07d78cf3d58
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
<<<<<<< HEAD
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
=======
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
>>>>>>> e233f14d20c2c5b8b9cabdc94021f07d78cf3d58
                        }
                    }
                }
                catch (Exception ex)
                {
<<<<<<< HEAD
                    ExtraAttackPlugin.ExtraAttackLogger.LogError($"Error in DoMeleeAttack Postfix: {ex.Message}");
=======
                    ExtraAttackPlugin.ExtraAttackLogger.LogError($"Error restoring damage values: {ex.Message}");
>>>>>>> e233f14d20c2c5b8b9cabdc94021f07d78cf3d58
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