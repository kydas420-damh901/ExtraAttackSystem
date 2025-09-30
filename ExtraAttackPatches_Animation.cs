using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ExtraAttackSystem
{
    // Animation patches: ZSyncAnimation and AOC management
    public static class ExtraAttackPatches_Animation
    {
        private static readonly HashSet<string> SecondaryTriggers = new()
        {
            "sword_secondary",
            "axe_secondary",
            "battleaxe_secondary",
            "mace_secondary",
            "knife_secondary",
            "dual_knives_secondary",
            "dualaxes_secondary",
            "atgeir_secondary",
            "greatsword_secondary",
            "unarmed_kick"
        };

        private static readonly HashSet<string> PrimaryAttackTriggers = new()
        {
            "swing_longsword0", "swing_longsword1", "swing_longsword2",
            "swing_axe", "swing_axe0", "swing_axe1", "swing_axe2", "axe_swing",
            "knife_stab", "knife_stab0", "knife_stab1", "knife_stab2",
            "knife_slash0", "knife_slash1", "knife_slash2",
            "battleaxe_attack0", "battleaxe_attack1", "battleaxe_attack2",
            "atgeir_attack0", "atgeir_attack1", "atgeir_attack2",
            "unarmed_attack0", "unarmed_attack1",
            "club_attack0", "club_attack1", "club_attack2",
            "greatsword_attack0", "greatsword_attack1", "greatsword_attack2",
            "BattleAxe1", "BattleAxe2", "BattleAxe_Combo3",
            "2Hand-Spear-Attack1", "2Hand-Spear-Attack9", "2Hand-Spear-Attack3",
            "Punchstep 1", "Punchstep 2",
            "Knife Attack Combo (1)", "Knife Attack Combo (2)", "Knife Attack Combo (3)"
        };

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
                                ExtraAttackPlugin.LogInfo("System", "BLOCKED vanilla secondary attack (PlayerAttackInput)");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.LogError("System", $"Error in PlayerAttackInput_Patch: {ex.Message}");
                }
            }
        }

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

                    if (!ExtraAttackPatches_Core.TryGetPlayerAnimator(player, out Animator animator) || animator == null)
                        return;

                    bool isSecondaryTrigger = SecondaryTriggers.Contains(name);
                    bool isPrimaryTrigger = PrimaryAttackTriggers.Contains(name);

                    if (isSecondaryTrigger)
                    {
                        ExtraAttackPlugin.LogInfo("AttackTriggers", $"Detected secondary trigger: {name}");
                    }

                    if (isPrimaryTrigger)
                    {
                        ExtraAttackPlugin.LogInfo("AttackTriggers", $"Detected primary attack trigger: {name}");
                    }

                    var attackMode = ExtraAttackUtils.GetAttackMode(player);

                    if (attackMode == ExtraAttackUtils.AttackMode.Normal)
                    {
                        if (AnimationManager.CustomRuntimeControllers.TryGetValue("Original", out var originalAoc) && originalAoc != null)
                        {
                            if (animator.runtimeAnimatorController != originalAoc)
                            {
                                AnimationManager.FastReplaceRAC(player, originalAoc);
                                ExtraAttackPlugin.LogInfo("AOC", "Applied Original AOC (Normal attack)");
                            }
                        }
                    }
                    else
                    {
                        string aocKey = GetAOCKey(player, attackMode);

                        if (!AnimationManager.CustomRuntimeControllers.ContainsKey(aocKey))
                        {
                            InitializeAOC(player, animator);
                        }

                        if (AnimationManager.CustomRuntimeControllers.TryGetValue(aocKey, out var customAoc) && customAoc != null)
                        {
                            AnimationManager.FastReplaceRAC(player, customAoc);
                            ExtraAttackPlugin.LogInfo("AOC", $"Applied {aocKey} AOC for trigger: {name}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.LogError("AOC", $"Error in RPC_SetTrigger_Patch: {ex.Message}");
                }
            }

            private static string GetAOCKey(Player player, ExtraAttackUtils.AttackMode mode)
            {
                if (mode == ExtraAttackUtils.AttackMode.ExtraQ)
                {
                    return "ExtraAttack_Q";
                }

                ItemDrop.ItemData? weapon = player.GetCurrentWeapon();
                if (weapon == null)
                {
                    return mode == ExtraAttackUtils.AttackMode.ExtraT ? "ExtraAttack_T_Swords" : "ExtraAttack_G_Swords";
                }

                bool isClub = weapon.m_shared.m_skillType == Skills.SkillType.Clubs;

                if (mode == ExtraAttackUtils.AttackMode.ExtraT)
                {
                    return isClub ? "ExtraAttack_T_Clubs" : "ExtraAttack_T_Swords";
                }
                else
                {
                    return isClub ? "ExtraAttack_G_Clubs" : "ExtraAttack_G_Swords";
                }
            }

            public static void Postfix(ZSyncAnimation __instance, string name)
            {
                try
                {
                    if (ExtraAttackPlugin.DebugClipNames?.Value != true)
                        return;

                    Player player = __instance.GetComponent<Player>();
                    if (player == null || player != Player.m_localPlayer)
                        return;

                    var attackMode = ExtraAttackUtils.GetAttackMode(player);
                    if (attackMode == ExtraAttackUtils.AttackMode.Normal)
                        return;

                    if (!ExtraAttackPatches_Core.TryGetPlayerAnimator(player, out Animator animator) || animator == null)
                        return;

                    __instance.StartCoroutine(LogCurrentClip(animator, name, attackMode));
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.LogError("ClipNames", $"Error in RPC_SetTrigger Postfix: {ex.Message}");
                }
            }

            private static System.Collections.IEnumerator LogCurrentClip(Animator animator, string triggerName, ExtraAttackUtils.AttackMode mode)
            {
                yield return new UnityEngine.WaitForSeconds(0.1f);

                AnimatorClipInfo[] clipInfos = animator.GetCurrentAnimatorClipInfo(0);
                if (clipInfos.Length > 0)
                {
                    string clipName = clipInfos[0].clip.name;
                    if (!clipName.Contains("Idle") && !clipName.Contains("Block"))
                    {
                        ExtraAttackPlugin.LogInfo("ClipNames", $"DEBUG [{mode}]: Trigger '{triggerName}' → Playing Clip: '{clipName}'");
                    }
                    else
                    {
                        AnimatorClipInfo[] nextClipInfos = animator.GetNextAnimatorClipInfo(0);
                        if (nextClipInfos.Length > 0)
                        {
                            ExtraAttackPlugin.LogInfo("ClipNames", $"DEBUG [{mode}]: Trigger '{triggerName}' → Next Clip: '{nextClipInfos[0].clip.name}'");
                        }
                        else
                        {
                            ExtraAttackPlugin.LogWarning("ClipNames", $"DEBUG [{mode}]: Trigger '{triggerName}' → Still on idle: '{clipName}'");
                        }
                    }
                }
                else
                {
                    ExtraAttackPlugin.LogWarning("ClipNames", $"DEBUG [{mode}]: Trigger '{triggerName}' → No clip info available");
                }
            }
        }

        internal static void InitializeAOC(Player player, Animator animator)
        {
            try
            {
                if (animator.runtimeAnimatorController == null)
                {
                    ExtraAttackPlugin.LogWarning("AOC", "RuntimeAnimatorController is null");
                    return;
                }

                bool needsInitialization = !AnimationManager.CustomRuntimeControllers.ContainsKey("Original") ||
                                           !AnimationManager.CustomRuntimeControllers.ContainsKey("ExtraAttack_Q") ||
                                           !AnimationManager.CustomRuntimeControllers.ContainsKey("ExtraAttack_T_Swords") ||
                                           !AnimationManager.CustomRuntimeControllers.ContainsKey("ExtraAttack_T_Clubs") ||
                                           !AnimationManager.CustomRuntimeControllers.ContainsKey("ExtraAttack_G_Swords") ||
                                           !AnimationManager.CustomRuntimeControllers.ContainsKey("ExtraAttack_G_Clubs");

                if (!needsInitialization)
                {
                    return;
                }

                ExtraAttackPlugin.LogInfo("AOC", "Initializing AOCs (one-time setup)...");

                if (!AnimationManager.CustomRuntimeControllers.ContainsKey("Original"))
                {
                    AnimationManager.CustomRuntimeControllers["Original"] = AnimationManager.MakeAOC(
                        new Dictionary<string, string>(),
                        animator.runtimeAnimatorController);
                    ExtraAttackPlugin.LogInfo("AOC", "Original AOC initialized");
                }

                if (!AnimationManager.CustomRuntimeControllers.ContainsKey("ExtraAttack_Q"))
                {
                    if (AnimationManager.ReplacementMap.TryGetValue("ExtraAttack_Q", out var qMap) && qMap.Count > 0)
                    {
                        AnimationManager.CustomRuntimeControllers["ExtraAttack_Q"] = AnimationManager.MakeAOC(
                            qMap, animator.runtimeAnimatorController);
                        ExtraAttackPlugin.LogInfo("AOC", $"ExtraAttack_Q AOC initialized with {qMap.Count} replacements");
                    }
                }

                if (!AnimationManager.CustomRuntimeControllers.ContainsKey("ExtraAttack_T_Swords"))
                {
                    if (AnimationManager.ReplacementMap.TryGetValue("ExtraAttack_T_Swords", out var tSwordsMap) && tSwordsMap.Count > 0)
                    {
                        AnimationManager.CustomRuntimeControllers["ExtraAttack_T_Swords"] = AnimationManager.MakeAOC(
                            tSwordsMap, animator.runtimeAnimatorController);
                        ExtraAttackPlugin.LogInfo("AOC", $"ExtraAttack_T_Swords AOC initialized with {tSwordsMap.Count} replacements");
                    }
                }

                if (!AnimationManager.CustomRuntimeControllers.ContainsKey("ExtraAttack_T_Clubs"))
                {
                    if (AnimationManager.ReplacementMap.TryGetValue("ExtraAttack_T_Clubs", out var tClubsMap) && tClubsMap.Count > 0)
                    {
                        AnimationManager.CustomRuntimeControllers["ExtraAttack_T_Clubs"] = AnimationManager.MakeAOC(
                            tClubsMap, animator.runtimeAnimatorController);
                        ExtraAttackPlugin.LogInfo("AOC", $"ExtraAttack_T_Clubs AOC initialized with {tClubsMap.Count} replacements");
                    }
                }

                if (!AnimationManager.CustomRuntimeControllers.ContainsKey("ExtraAttack_G_Swords"))
                {
                    if (AnimationManager.ReplacementMap.TryGetValue("ExtraAttack_G_Swords", out var gSwordsMap) && gSwordsMap.Count > 0)
                    {
                        AnimationManager.CustomRuntimeControllers["ExtraAttack_G_Swords"] = AnimationManager.MakeAOC(
                            gSwordsMap, animator.runtimeAnimatorController);
                        ExtraAttackPlugin.LogInfo("AOC", $"ExtraAttack_G_Swords AOC initialized with {gSwordsMap.Count} replacements");
                    }
                }

                if (!AnimationManager.CustomRuntimeControllers.ContainsKey("ExtraAttack_G_Clubs"))
                {
                    if (AnimationManager.ReplacementMap.TryGetValue("ExtraAttack_G_Clubs", out var gClubsMap) && gClubsMap.Count > 0)
                    {
                        AnimationManager.CustomRuntimeControllers["ExtraAttack_G_Clubs"] = AnimationManager.MakeAOC(
                            gClubsMap, animator.runtimeAnimatorController);
                        ExtraAttackPlugin.LogInfo("AOC", $"ExtraAttack_G_Clubs AOC initialized with {gClubsMap.Count} replacements");
                    }
                }

                ExtraAttackPlugin.LogInfo("AOC", "AOC initialization complete");
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("AOC", $"Error initializing AOC: {ex.Message}");
            }
        }
    }
}