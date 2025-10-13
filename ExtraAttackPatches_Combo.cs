using HarmonyLib;
using System;
using UnityEngine;

namespace ExtraAttackSystem
{
    // Combo prevention patches - FINAL FIX
    public static class ExtraAttackPatches_Combo
    {
        /// <summary>
        /// Force m_previousAttack to null AFTER Humanoid.StartAttack completes
        /// This prevents combo chaining from T/G â†’ left-click
        /// </summary>
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]
        [HarmonyPriority(Priority.Last)]  // Execute after all other patches
        public static class Humanoid_StartAttack_ForceNullPrevious_Patch
        {
            public static void Postfix(Humanoid __instance, bool __result)
            {
                try
                {
                    if (!__result) return; // Attack didn't start, skip

                    if (__instance is Player player && player == Player.m_localPlayer)
                    {
                        var attackMode = ExtraAttackUtils.GetAttackMode(player);

                        // For extra attack modes, ALWAYS clear m_previousAttack
                        if (attackMode != ExtraAttackUtils.AttackMode.Normal)
                        {
                            var traverse = Traverse.Create(__instance);
                            Attack m_previousAttack = traverse.Field("m_previousAttack").GetValue<Attack>();
                            Attack m_currentAttack = traverse.Field("m_currentAttack").GetValue<Attack>();

                            string prevInfo = m_previousAttack != null ? $"EXISTS ({m_previousAttack.m_attackAnimation})" : "NULL";
                            string currInfo = m_currentAttack != null ? $"EXISTS ({m_currentAttack.m_attackAnimation})" : "NULL";

                            if (ExtraAttackPlugin.IsDebugComboEnabled)
                            {
                                ExtraAttackPlugin.LogInfo("COMBO",
                                    $"[POSTFIX] Humanoid.StartAttack: mode={attackMode}, prev={prevInfo}, curr={currInfo}");
                            }

                            if (m_previousAttack != null)
                            {
                                if (ExtraAttackPlugin.IsDebugComboEnabled)
                                {
                                    ExtraAttackPlugin.LogInfo("COMBO",
                                        $"!!! FORCE NULLIFYING m_previousAttack: {m_previousAttack.m_attackAnimation} !!!");
                                }

                                // CRITICAL: Set the actual field, not the parameter
                                traverse.Field("m_previousAttack").SetValue(null);

                                if (ExtraAttackPlugin.IsDebugComboEnabled)
                                {
                                    ExtraAttackPlugin.LogInfo("COMBO", "!!! m_previousAttack NOW NULL !!!");
                                }
                            }
                            else
                            {
                                if (ExtraAttackPlugin.IsDebugComboEnabled)
                                {
                                    ExtraAttackPlugin.LogInfo("COMBO", "m_previousAttack was already NULL");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.LogError("System",
                        $"Error in Humanoid_StartAttack_ForceNullPrevious_Patch: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// DEBUG: Log Humanoid.StartAttack calls (Prefix for visibility)
        /// </summary>
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]
        [HarmonyPriority(Priority.First)]
        public static class Humanoid_StartAttack_DebugLog_Patch
        {
            public static void Prefix(Humanoid __instance, bool secondaryAttack)
            {
                try
                {
                    if (__instance is Player player && player == Player.m_localPlayer)
                    {
                        var currentMode = ExtraAttackUtils.GetAttackMode(player);

                        var traverse = Traverse.Create(__instance);
                        Attack m_previousAttack = traverse.Field("m_previousAttack").GetValue<Attack>();
                        Attack m_currentAttack = traverse.Field("m_currentAttack").GetValue<Attack>();

                        string prevInfo = m_previousAttack != null ? $"EXISTS ({m_previousAttack.m_attackAnimation})" : "NULL";
                        string currInfo = m_currentAttack != null ? $"EXISTS ({m_currentAttack.m_attackAnimation})" : "NULL";

                        if (ExtraAttackPlugin.IsDebugComboEnabled)
                        {
                            ExtraAttackPlugin.LogInfo("COMBO",
                                $"[PREFIX] Humanoid.StartAttack: mode={currentMode}, sec={secondaryAttack}, " +
                                $"prev={prevInfo}, curr={currInfo}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.LogError("System",
                        $"Error in Humanoid_StartAttack_DebugLog_Patch: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// NEW: Pre-nullify m_previousAttack BEFORE Humanoid.StartAttack runs when exiting Extra mode
        /// Ensures Attack.Start receives previousAttack=null even if our Attack.Start patch does not match the signature
        /// </summary>
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]
        [HarmonyPriority(Priority.First)]
        public static class Humanoid_StartAttack_NullifyPrevious_Prefix
        {
            public static void Prefix(Humanoid __instance, bool secondaryAttack)
            {
                try
                {
                    if (__instance is Player player && player == Player.m_localPlayer)
                    {
                    }
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.LogError("System",
                        $"Error in Humanoid_StartAttack_NullifyPrevious_Prefix: {ex.Message}");
                }
            }
        }

        // NEW: Ensure combo chain is not carried over from previous attack when in Style modes (T/G)
        [HarmonyPatch(typeof(Attack), nameof(Attack.Start))]
        [HarmonyPriority(Priority.First)]
        public static class Attack_Start_NullifyPrevious_Patch
        {
            public static void Prefix(Humanoid character, ref Attack previousAttack, ref float timeSinceLastAttack)
            {
                try
                {
                    if (character is Player player && player == Player.m_localPlayer)
                    {
                        // Disabled: keep vanilla chain behavior; do not modify previousAttack or timeSinceLastAttack
                        if (ExtraAttackPlugin.IsDebugComboEnabled)
                        {
                            ExtraAttackPlugin.LogInfo("COMBO", "[DISABLED] Attack.Start Prefix: keep vanilla chain; no previousAttack/timeSinceLastAttack changes");
                        }

                        // Apply AOC just before vanilla SetTrigger executes inside Attack.Start
                    }
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.LogError("System", $"Error in Attack_Start_NullifyPrevious_Patch: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Skip Attack.OnAttackTrigger() when YAML has EnableHit=false
        /// This prevents BOTH sound and damage for the configured hit index
        /// </summary>
        [HarmonyPatch(typeof(Attack), nameof(Attack.OnAttackTrigger))]
        [HarmonyPriority(Priority.VeryHigh)]
        public static class Attack_OnAttackTrigger_SkipWhenDisabled_Patch
        {
            public static bool Prefix(Attack __instance)
            {
                try
                {
                    var traverse = Traverse.Create(__instance);
                    var character = traverse.Field("m_character").GetValue<Character>();

                    if (character is Player player && player == Player.m_localPlayer)
                    {
                        var attackMode = ExtraAttackUtils.GetAttackMode(player);
                        if (attackMode != ExtraAttackUtils.AttackMode.Normal)
                        {
                            if (ExtraAttackPatches_Core.TryGetPlayerAnimator(player, out Animator animator))
                            {
                                AnimatorClipInfo[] clipInfos = animator.GetCurrentAnimatorClipInfo(0);
                                if (clipInfos.Length > 0)
                                {
                                    string clipName = clipInfos[0].clip.name;
                                    AnimationClip clip = clipInfos[0].clip;

                                    string configKey = ExtraAttackPatches_Core.BuildConfigKey(player, clipName);
                                    var timing = AnimationTimingConfig.GetTiming(configKey);

                                    if (!timing.EnableHit)
                                    {
                                        if (ExtraAttackPlugin.IsDebugAOCOperationsEnabled)
                                        {
                                            ExtraAttackPlugin.LogInfo("SOUND",
                                                $"[SKIP] Attack.OnAttackTrigger: [{configKey}] EnableHit=false - Sound & Damage DISABLED");
                                        }
                                        return false; // Skip entire OnAttackTrigger
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.LogError("System", $"Error in Attack_OnAttackTrigger_SkipWhenDisabled_Patch: {ex.Message}");
                }

                return true;
            }
        }

        /// <summary>
        /// Prevent chain attacks during Style modes and immediately after mode exits
        /// This directly blocks Attack.CanStartChainAttack() to ensure no combo continuation
        /// </summary>
        [HarmonyPatch(typeof(Attack), nameof(Attack.CanStartChainAttack))]
        [HarmonyPriority(Priority.VeryHigh)]
        public static class Attack_CanStartChainAttack_BlockExtraMode_Patch
        {
            public static bool Prefix(Attack __instance, ref bool __result)
            {
                // Disabled: keep vanilla chain behavior; do not block chain
                return true;
            }
        }
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]
        [HarmonyPriority(Priority.VeryHigh)]
        public static class Humanoid_StartAttack_BlockPrimaryDuringExtra_Patch
        {
            public static bool Prefix(Humanoid __instance, ref bool __result, Character target, bool secondaryAttack)
            {
                try
                {
                    if (__instance is Player player && player == Player.m_localPlayer)
                    {
                        // Allow our own StartAttack call once
                        if (ExtraAttackUtils.ConsumeBypassNextStartAttack(player))
                        {
                            return true;
                        }

                        var mode = ExtraAttackUtils.GetAttackMode(player);
                        bool inExtra = mode != ExtraAttackUtils.AttackMode.Normal;

                        // Block during extra attack modes or while AC is initializing (transition)
                        bool duringTransition = ExtraAttackPatches_Animation.IsAOCInitializing;

                        // Consume per-input block flags first
                        if (!secondaryAttack && ExtraAttackUtils.ConsumeBlockNextPrimary(player))
                        {
                            __result = false; return false;
                        }
                        if (secondaryAttack && ExtraAttackUtils.ConsumeBlockNextSecondary(player))
                        {
                            __result = false; return false;
                        }

                        // Continuous chain window block for LMB
                        if (!secondaryAttack && ExtraAttackUtils.HasBlockPrimaryDuringChainWindow(player))
                        {
                            __result = false; return false;
                        }

                        // General block: when in extra mode or during AC transition, block vanilla LMB/MMB starts
                        if (inExtra || duringTransition)
                        {
                            __result = false;
                            return false;
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    ExtraAttackPlugin.LogError("System", $"Error in Humanoid_StartAttack_BlockPrimaryDuringExtra_Patch: {ex.Message}");
                }
                return true;
            }
        }
    }
}

namespace ExtraAttackSystem
{
    /// <summary>
    /// NEW: Clear the continuous chain window block when vanilla resets the chain flag
    /// </summary>
    [HarmonyPatch(typeof(CharacterAnimEvent), nameof(CharacterAnimEvent.ResetChain))]
    [HarmonyPriority(Priority.Last)]
    public static class CharacterAnimEvent_ResetChain_ClearBlock_Patch
    {
        public static void Postfix(CharacterAnimEvent __instance)
        {
            try
            {
                var player = __instance.GetComponent<Player>();
                if (player != null && player == Player.m_localPlayer)
                {
                    // Do NOT clear continuous block here; Attack.Start calls ResetChain at the beginning
                    // Keep block until Player exits the next Normal attack (handled in Player.Update)

                }
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error in CharacterAnimEvent_ResetChain_ClearBlock_Patch: {ex.Message}");
            }
        }
    }
}