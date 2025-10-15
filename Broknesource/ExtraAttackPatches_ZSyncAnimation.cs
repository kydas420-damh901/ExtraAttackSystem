using HarmonyLib;
using System;
using UnityEngine;

namespace ExtraAttackSystem
{
    /// <summary>
    /// ZSyncAnimation patches for dynamic AOC switching and sitting state prevention
    /// </summary>
    public static class ExtraAttackPatches_ZSyncAnimation
    {
        // Patch ZSyncAnimation.RPC_SetTrigger to switch AOC right before animation trigger
        [HarmonyPatch(typeof(ZSyncAnimation), "RPC_SetTrigger")]
        public static class ZSyncAnimation_RPC_SetTrigger_Patch
        {
            [HarmonyPrefix]
            public static void Prefix(ZSyncAnimation __instance, long sender, string name)
            {
                try
                {
                    var player = __instance.GetComponent<Player>();
                    if (player == null || player != Player.m_localPlayer)
                    {
                        return;
                    }

                    // Check if this is an extra attack trigger
                    var mode = EAS_CommonUtils.GetAttackMode(player);
                    
                    if (mode != EAS_CommonUtils.AttackMode.Normal)
                    {
                        // Apply custom AOC for extra attacks
                        var animator = AnimationManager.GetPlayerAnimator(player);
                        if (animator != null)
                        {
                            // Prevent sitting state by forcing weapon idle state before AOC application
                            if (animator.GetBool("emote_sit") || animator.GetBool("emote_sitchair"))
                            {
                                string weaponIdleState = AnimationManager.GetWeaponIdleState(player);
                                if (!string.IsNullOrEmpty(weaponIdleState))
                                {
                                    animator.Play(weaponIdleState, -1, 0f);
                                    // Clear sitting state completely
                                    animator.SetBool("emote_sit", false);
                                    animator.SetBool("emote_sitchair", false);
                                    
                                    // Clear ZDO emote state to prevent network sync issues
                                    try
                                    {
                                        var nview = HarmonyLib.Traverse.Create(player).Field("m_nview").GetValue<ZNetView>();
                                        if (nview != null && nview.IsValid())
                                        {
                                            nview.GetZDO().Set("emote", "");
                                        }
                                    }
                                    catch (System.Exception ex)
                                    {
                                        ExtraAttackPlugin.LogWarning("AOC", $"Failed to clear ZDO emote state: {ex.Message}");
                                    }
                                    
                                    if (ExtraAttackPlugin.DebugAOCOperations.Value)
                                    {
                                        ExtraAttackPlugin.LogInfo("AOC", $"ZSyncAnimation.RPC_SetTrigger: Forced weapon idle state '{weaponIdleState}' and cleared all sitting state");
                                    }
                                }
                            }
                            
                            ExtraAttackPatches_Animation.ApplyStyleAOC(player, animator, mode);
                            
                            // Post-AOC sit state clearing to handle delayed sit state from AOC
                            if (animator.GetBool("emote_sit") || animator.GetBool("emote_sitchair"))
                            {
                                animator.SetBool("emote_sit", false);
                                animator.SetBool("emote_sitchair", false);
                                
                                // Clear ZDO emote state again after AOC application
                                try
                                {
                                    var nview = HarmonyLib.Traverse.Create(player).Field("m_nview").GetValue<ZNetView>();
                                    if (nview != null && nview.IsValid())
                                    {
                                        nview.GetZDO().Set("emote", "");
                                    }
                                }
                                catch (System.Exception ex)
                                {
                                    ExtraAttackPlugin.LogWarning("AOC", $"Failed to clear ZDO emote state after AOC: {ex.Message}");
                                }
                                
                                if (ExtraAttackPlugin.DebugAOCOperations.Value)
                                {
                                    ExtraAttackPlugin.LogInfo("AOC", $"ZSyncAnimation.RPC_SetTrigger: Cleared delayed sit state after AOC application for {mode}");
                                }
                            }
                            
                            if (ExtraAttackPlugin.DebugAOCOperations.Value)
                            {
                                ExtraAttackPlugin.LogInfo("AOC", $"ZSyncAnimation.RPC_SetTrigger: Applied AOC for {mode} before trigger '{name}'");
                            }
                        }
                    }
                    else
                    {
                        // For normal attacks, revert to original AOC
                        var animator = AnimationManager.GetPlayerAnimator(player);
                        if (animator != null && AnimationManager.AnimatorControllerCache.TryGetValue("Original", out var originalController))
                        {
                            AnimationManager.FastReplaceRAC(player, originalController);
                            
                            if (ExtraAttackPlugin.DebugAOCOperations.Value)
                            {
                                ExtraAttackPlugin.LogInfo("AOC", $"ZSyncAnimation.RPC_SetTrigger: Reverted to Original AOC for normal trigger '{name}'");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.LogError("System", $"Error in ZSyncAnimation.RPC_SetTrigger patch: {ex.Message}");
                }
            }
        }

    }
}