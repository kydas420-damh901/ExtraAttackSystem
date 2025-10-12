using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ExtraAttackSystem
{

    internal static class ExtraAttackPatches_Player
    {
        // Track equipment changes for AOC switching
        private static readonly Dictionary<Player, string> lastRightItem = new();
        private static readonly Dictionary<Player, string> lastLeftItem = new();

        [HarmonyPatch(typeof(Player), "Awake")]
        public static class Player_Awake_Patch
        {
            public static void Postfix(Player __instance)
            {
                if (__instance == null || !__instance.IsOwner())
                    return;

                try
                {
                    // Initialize AOC on character load
                    if (ExtraAttackPatches_Core.TryGetPlayerAnimator(__instance, out Animator? animator) && animator != null)
                    {
                        ExtraAttackPatches_Animation.InitializeAOC(__instance, animator);
                        
                        if (ExtraAttackPlugin.DebugAOCOperations.Value)
                        {
                            ExtraAttackPlugin.LogInfo("AOC", "Initialized AOC on character load");
                        }
                    }
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.LogError("System", $"Error in Player_Awake_Patch: {ex.Message}");
                }
            }
        }

        [HarmonyPatch(typeof(Player), "Update")]
        public static class Player_Update_Patch
        {
            private static bool extraAttackTriggered = false;
            private static bool testButton1Pressed = false;
            private static bool testButton2Pressed = false;
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
                        ExtraAttackPlugin.LogInfo("System", "F6 pressed: Starting YAML reload process");
                        
                        // What: Reload YAML configs from BepInEx\config\ExtraAttackSystem
                        // Why: Apply user edits without restarting the game
                        ExtraAttackPlugin.LogInfo("System", "F6: Reloading AnimationReplacement configs");
                        AnimationReplacementConfig.Reload();
                        
                        // Also reload timing & exclusion configs to reflect user edits consistently
                        ExtraAttackPlugin.LogInfo("System", "F6: Reloading AnimationTiming configs");
                        AnimationTimingConfig.Reload();
                        
                        ExtraAttackExclusionConfig.Reload();
                        
                        // Clear AOC cache to force rebuild on next ApplyStyleAOC
                        ExtraAttackPlugin.LogInfo("System", "F6: Clearing AOC cache");
                        AnimationManager.ClearAOCCache(true);
                        
                        ExtraAttackPlugin.LogInfo("System", "F6: YAML reload process completed");
                        
                        // Show notification in game
                        if (MessageHud.instance != null)
                        {
                            MessageHud.instance.ShowMessage(MessageHud.MessageType.TopLeft, "YAML configs reloaded successfully", 0, null);
                        }
                    }

                    // Check for equipment changes and update AOC
                    CheckEquipmentChanges(__instance);

                    // Track attack state and reset mode when attack ends
                    bool currentlyInAttack = __instance.InAttack();
                    if (wasInAttack.TryGetValue(__instance, out bool previouslyInAttack))
                    {
                        if (previouslyInAttack && !currentlyInAttack)
                        {
                            var currentMode = ExtraAttackUtils.GetAttackMode(__instance);
                            if (currentMode != ExtraAttackUtils.AttackMode.Normal)
                            {
                                if (ExtraAttackPatches_Core.TryGetPlayerAnimator(__instance, out Animator? animator) && animator != null)
                                {
                                    if (AnimationManager.CustomRuntimeControllers.ContainsKey("Original"))
                                    {
                                        var originalController = AnimationManager.CustomRuntimeControllers["Original"];
                                        // Restore only emote flags across controller swap, and only if currently in emote
                                        int emoteSitHash = ZSyncAnimation.GetHash("emote_sit");
                                        int emoteSitChairHash = ZSyncAnimation.GetHash("emote_sitchair");
                                        bool wasInEmote = __instance.InEmote();
                                        bool emoteSitBefore = false;
                                        bool emoteSitChairBefore = false;
                                        if (wasInEmote)
                                        {
                                            try { emoteSitBefore = animator.GetBool(emoteSitHash); } catch { }
                                            try { emoteSitChairBefore = animator.GetBool(emoteSitChairHash); } catch { }
                                        }

                                        // NEW: Capture crouching parameter before controller swap to restore later
                                        int crouchHashRevert = ZSyncAnimation.GetHash("crouching");
                                        bool crouchParamBeforeSwap = false;
                                        try { crouchParamBeforeSwap = animator.GetBool(crouchHashRevert); } catch { }

                                        // Wakeup pre-guard removed per user request (no wakeup force-false; no ZDO override)
                                        
                                        // Use centralized revert helper to avoid duplication and maintain guards
                                        
                                        // Detailed diagnostics on revert
                                        int crouchHash = ZSyncAnimation.GetHash("crouching");
                                        bool crouchBeforeReassert = false;
                                        try { crouchBeforeReassert = animator != null && animator.GetBool(crouchHash); } catch { }

                                        // Extra diagnostics: current crouch toggle state before reassert
                                        var fiCT = HarmonyLib.AccessTools.Field(typeof(Player), "m_crouchToggled");
                                        bool crouchToggleState = false;
                                        try { if (fiCT != null) crouchToggleState = (bool)fiCT.GetValue(__instance); } catch { }
                                        // Reassert crouch after revert if player was crouching before Extra attack OR current toggle indicates crouch
                                        if (crouchToggleState)
                                        {
                                            // Ensure animator crouching bool is asserted immediately to avoid one-frame stand-up during controller revert
                                            int crouchHash2 = ZSyncAnimation.GetHash("crouching");
                                            try { animator?.SetBool(crouchHash2, true); } catch { }
                                            try { __instance.SetCrouch(true); } catch { }
                                            ExtraAttackPlugin.LogInfo("AOC", $"Reasserted crouch after revert; crouchBefore={crouchBeforeReassert} (toggle={crouchToggleState})");
                                        }
                                        else
                                        {
                                            ExtraAttackPlugin.LogInfo("AOC", $"No crouch reassert needed; crouchBefore={crouchBeforeReassert} (toggle={crouchToggleState})");
                                        }

                                        if (ExtraAttackPlugin.DebugAOCOperations.Value)
                                        {
                                            ExtraAttackPlugin.LogInfo("AOC", $"Reverted to Original controller after {currentMode} attack (emote restore: {wasInEmote})");
                                        }
                                    }
                                }

                                // Reset mode to Normal when Extra attack finishes
                                ExtraAttackUtils.SetAttackMode(__instance, ExtraAttackUtils.AttackMode.Normal);
                            }
                            else
                            {
                                // Keep vanilla chain: do not clear any custom block window here
                                if (ExtraAttackUtils.HasBlockPrimaryDuringChainWindow(__instance))
                                {
                                    ExtraAttackUtils.ClearBlockPrimaryDuringChainWindow(__instance);
                                    ExtraAttackPlugin.LogInfo("COMBO", "Cleared LMB chain block window after Normal attack end");
                                }
                            }
                        }
                    }
                    wasInAttack[__instance] = currentlyInAttack;

                    // Q key: Extra Attack (secondary_Q)
                    if (ExtraAttackPlugin.IsExtraAttackKey_QPressed())
                    {
                        if (!extraAttackTriggered && CanPerformExtraAttack(__instance))
                        {
                            TriggerExtraAttack_Q(__instance);
                            extraAttackTriggered = true;
                        }
                    }
                    else
                    {
                        extraAttackTriggered = false;
                    }

                    // T key: Custom Attack 1 (secondary_T)
                    if (ExtraAttackPlugin.IsExtraAttackKey_TPressed() && !testButton2Pressed)
                    {
                        if (CanPerformExtraAttack(__instance))
                        {
                            TriggerExtraAttack_T(__instance);
                            testButton2Pressed = true;
                        }
                    }
                    else if (!ExtraAttackPlugin.IsExtraAttackKey_TPressed())
                    {
                        testButton2Pressed = false;
                    }

                    // G key: Custom Attack 2 (secondary_G)
                    if (ExtraAttackPlugin.IsExtraAttackKey_GPressed() && !testButton1Pressed)
                    {
                        if (CanPerformExtraAttack(__instance))
                        {
                            TriggerExtraAttack_G(__instance);
                            testButton1Pressed = true;
                        }
                    }
                    else if (!ExtraAttackPlugin.IsExtraAttackKey_GPressed())
                    {
                        testButton1Pressed = false;
                    }
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.LogError("System", $"Error in Player_Update_Patch: {ex.Message}");
                }
            }

            // Check if UI or camera is blocking input
            private static bool IsUIOrCameraBlocking()
            {
                return (Chat.instance != null && Chat.instance.HasFocus()) ||
                       Console.IsVisible() ||
                       TextInput.IsVisible() ||
                       StoreGui.IsVisible() ||
                       InventoryGui.IsVisible() ||
                       Menu.IsVisible() ||
                       (TextViewer.instance != null && TextViewer.instance.IsVisible()) ||
                       Minimap.IsOpen() ||
                       GameCamera.InFreeFly() ||
                       PlayerCustomizaton.IsBarberGuiVisible() ||
                       Hud.InRadial();
            }

            // Check if player state blocks input
            private static bool IsPlayerStateBlocking(Player player)
            {
                return player.IsDead() || player.InCutscene() || player.IsTeleporting();
            }

            // Check if player is in blocking interaction state
            private static bool IsInteractionBlocking(Player player)
            {
                return player.InAttack() || player.InDodge() || player.IsBlocking() || player.InMinorAction();
            }

            // Check if equipped items block extra attacks
            private static bool AreItemsBlocking(Player player)
            {
                var traverse = Traverse.Create(player);
                ItemDrop.ItemData rightItem = traverse.Field("m_rightItem").GetValue<ItemDrop.ItemData>();
                ItemDrop.ItemData leftItem = traverse.Field("m_leftItem").GetValue<ItemDrop.ItemData>();

                if ((rightItem != null && ExtraAttackExclusionConfig.ShouldBlockExtraForItem(rightItem)) ||
                    (leftItem != null && ExtraAttackExclusionConfig.ShouldBlockExtraForItem(leftItem)))
                {
                    ExtraAttackUtils.ShowMessage(player, "extra_attack_tool_bomb_blocked");
                    return true;
                }

                return false;
            }

            // Centralized input blocking check
            private static bool IsExtraInputBlocked(Player player)
            {
                return IsUIOrCameraBlocking() ||
                       IsPlayerStateBlocking(player) ||
                       IsInteractionBlocking(player) ||
                       AreItemsBlocking(player);
            }

            private static bool CanPerformExtraAttack(Player player)
            {
                // New: centralized guard for UI/camera/player/interact/tools/exclusions
                if (IsExtraInputBlocked(player))
                {
                    return false;
                }

                if (player.InAttack())
                {
                    return false;
                }

                if (ExtraAttackUtils.IsPlayerOnCooldown(player, ExtraAttackUtils.AttackMode.secondary_Q))
                {
                    float remaining = ExtraAttackUtils.GetPlayerCooldownRemaining(player, ExtraAttackUtils.AttackMode.secondary_Q);
                    ExtraAttackUtils.ShowMessage(player, "extra_attack_cooldown", remaining.ToString("F1"));
                    return false;
                }

                ItemDrop.ItemData weapon = player.GetCurrentWeapon();
                if (weapon == null)
                {
                    ExtraAttackUtils.ShowMessage(player, "extra_attack_no_weapon");
                    return false;
                }

                // Secondary attack must be defined for Q/T/G route
                if (!weapon.HaveSecondaryAttack())
                {
                    return false;
                }

                // NEW: per-style stamina check for Q using effective cost
                float staminaCost1 = ExtraAttackUtils.GetEffectiveStaminaCost(player, weapon, ExtraAttackUtils.AttackMode.secondary_Q);
                if (player.GetStamina() < staminaCost1)
                {
                    ExtraAttackUtils.ShowMessage(player, "extra_attack_no_stamina");
                    return false;
                }

                return true;
            }

            private static bool CanPerformNormalAttack(Player player, string buttonName)
            {
                // New: centralized guard for UI/camera/player/interact/tools/exclusions
                if (IsExtraInputBlocked(player))
                {
                    return false;
                }

                if (player.InAttack())
                {
                    return false;
                }

                var mode = buttonName == "T" ? ExtraAttackUtils.AttackMode.secondary_T : ExtraAttackUtils.AttackMode.secondary_G;
                if (ExtraAttackUtils.IsPlayerOnCooldown(player, mode))
                {
                    float remaining = ExtraAttackUtils.GetPlayerCooldownRemaining(player, mode);
                    ExtraAttackUtils.ShowMessage(player, "extra_attack_cooldown", remaining.ToString("F1"));
                    return false;
                }

                ItemDrop.ItemData weapon = player.GetCurrentWeapon();
                if (weapon == null)
                {
                    ExtraAttackUtils.ShowMessage(player, "extra_attack_no_weapon");
                    return false;
                }

                // Secondary attack must be defined for T/G route as we trigger StartAttack with secondary flag
                if (!weapon.HaveSecondaryAttack())
                {
                    ExtraAttackUtils.ShowMessage(player, "extra_attack_no_secondary");
                    return false;
                }

                // NEW: per-style stamina check for T/G using effective cost
                float staminaCost2 = ExtraAttackUtils.GetEffectiveStaminaCost(player, weapon, mode);
                if (player.GetStamina() < staminaCost2)
                {
                    ExtraAttackUtils.ShowMessage(player, "extra_attack_no_stamina");
                    return false;
                }

                return true;
            }

            private static void TriggerNormalAttack(Player player, string buttonName)
            {
                try
                {
                    // If player is sitting, stand up first (vanilla behavior)
                    if (player.IsSitting())
                    {
                        if (ExtraAttackPlugin.DebugAOCOperations.Value)
                        {
                        }
                        // Use reflection to call StopEmote since it's protected
                        try
                        {
                            var stopEmoteMethod = typeof(Player).GetMethod("StopEmote", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            stopEmoteMethod?.Invoke(player, null);
                        }
                        catch (System.Exception ex)
                        {
                            ExtraAttackPlugin.LogError("System", $"Error calling StopEmote: {ex.Message}");
                        }
                        // Wait for stand up animation to complete before proceeding
                        return;
                    }

                    ItemDrop.ItemData weapon = player.GetCurrentWeapon();
                    if (weapon == null)
                    {
                        return;
                    }

                    // Diagnostic: capture weapon/state when normal (Style2/3) path is triggered

                    if (!ExtraAttackPatches_Core.TryGetPlayerAnimator(player, out Animator? animator) || animator == null)
                    {
                        return;
                    }

                    // Ensure AOCs are initialized at least once
                    if (!AnimationManager.CustomRuntimeControllers.ContainsKey("Original"))
                    {
                        ExtraAttackPatches_Animation.InitializeAOC(player, animator);
                    }

                    var mode = buttonName == "T" ? ExtraAttackUtils.AttackMode.secondary_T : ExtraAttackUtils.AttackMode.secondary_G;
                    ExtraAttackUtils.SetAttackMode(player, mode);

                    // NOTE: Removed: Do not call Player.StopEmote() before attack to avoid emoteID change -> UpdateEmote emote_stop

                    // Capture crouch toggle state (vanilla Player.m_crouchToggled) before applying style AOC to restore after attack
                    bool crouchTogglePre = false;
                    var fiCrouchToggle = HarmonyLib.AccessTools.Field(typeof(Player), "m_crouchToggled");
                    if (fiCrouchToggle != null)
                    {
                        try { crouchTogglePre = (bool)fiCrouchToggle.GetValue(player); } catch { }
                    }
                    else
                    {
                        try { crouchTogglePre = player.IsCrouching(); } catch { }
                    }
                    if (ExtraAttackPlugin.DebugAOCOperations.Value)
                    {
                        ExtraAttackPlugin.LogInfo("AOC", $"[{buttonName}] Pre-ApplyAOC: crouchToggle={crouchTogglePre} stamina={player.GetStamina():F1}");
                    }
                    
                    // AOC is already set at equipment change time - no need to switch during attack


                    ExtraAttackUtils.SetPlayerCooldown(player, mode);

                    // Diagnostic: runtime animator controller state just before StartAttack
                    var rac = animator.runtimeAnimatorController;


                    // Keep vanilla chain: do not clear queued timer or primary hold

                    // Keep vanilla chain: do not manually call ResetChain here; Attack.Start handles it in vanilla
                    
                    // Mark bypass for our own StartAttack call
                    ExtraAttackUtils.MarkBypassNextStartAttack(player);
                    player.StartAttack(null, true);

                    // Diagnostics: capture animator parameters immediately after StartAttack
                    ExtraAttackPatches_Core.LogAnimatorParameters(player, $"[{buttonName}] After StartAttack");
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.LogError("System", $"Error in {buttonName} button: {ex}");
                    ExtraAttackUtils.SetAttackMode(player, ExtraAttackUtils.AttackMode.Normal);
                }
            }

            private static void TriggerExtraAttack(Player player)
            {
                try
                {
                    // If player is sitting, stand up first (vanilla behavior)
                    if (player.IsSitting())
                    {
                        if (ExtraAttackPlugin.DebugAOCOperations.Value)
                        {
                        }
                        // Use reflection to call StopEmote since it's protected
                        try
                        {
                            var stopEmoteMethod = typeof(Player).GetMethod("StopEmote", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            stopEmoteMethod?.Invoke(player, null);
                        }
                        catch (System.Exception ex)
                        {
                            ExtraAttackPlugin.LogError("System", $"Error calling StopEmote: {ex.Message}");
                        }
                        // Wait for stand up animation to complete before proceeding
                        return;
                    }

                    // Get attack mode for AOC application (after sitting check)
                    var mode = ExtraAttackUtils.GetAttackMode(player);
                    
                    TriggerExtraAttackWithMode(player, mode);
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.LogError("System", $"Error in Extra Attack (Q): {ex.Message}");
                    ExtraAttackUtils.SetAttackMode(player, ExtraAttackUtils.AttackMode.Normal);
                }
            }

            // NEW: Separate methods for Q/T/G
            private static void TriggerExtraAttack_Q(Player player)
            {
                TriggerExtraAttackWithMode(player, ExtraAttackUtils.AttackMode.secondary_Q);
            }
            private static void TriggerExtraAttack_T(Player player)
            {
                TriggerExtraAttackWithMode(player, ExtraAttackUtils.AttackMode.secondary_T);
            }
            private static void TriggerExtraAttack_G(Player player)
            {
                TriggerExtraAttackWithMode(player, ExtraAttackUtils.AttackMode.secondary_G);
            }

            private static void TriggerExtraAttackWithMode(Player player, ExtraAttackUtils.AttackMode mode)
            {
                try
                {
                    // If player is sitting, stand up first (vanilla behavior)
                    if (player.IsSitting())
                    {
                        if (ExtraAttackPlugin.DebugAOCOperations.Value)
                        {
                        }
                        // Use reflection to call StopEmote since it's protected
                        try
                        {
                            var stopEmoteMethod = typeof(Player).GetMethod("StopEmote", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            stopEmoteMethod?.Invoke(player, null);
                        }
                        catch (System.Exception ex)
                        {
                            ExtraAttackPlugin.LogError("System", $"Error calling StopEmote: {ex.Message}");
                        }
                        // Wait for stand up animation to complete before proceeding
                        return;
                    }

                    ItemDrop.ItemData weapon = player.GetCurrentWeapon();
                    if (weapon == null)
                    {
                        return;
                    }

                    if (!ExtraAttackPatches_Core.TryGetPlayerAnimator(player, out Animator? animator) || animator == null)
                    {
                        return;
                    }

                    // Ensure AOCs are initialized at least once
                    if (!AnimationManager.CustomRuntimeControllers.ContainsKey("Original"))
                    {
                        ExtraAttackPatches_Animation.InitializeAOC(player, animator);
                    }

                    // Set the specified mode instead of always using secondary_Q
                    ExtraAttackUtils.SetAttackMode(player, mode);
                    
                    // The vanilla SetControls will handle emote state; we avoid explicit intervention here.

                    // Capture crouch toggle state (vanilla Player.m_crouchToggled) before applying style AOC to restore after attack
                    bool crouchTogglePre = false;
                    var fiCrouchToggle = HarmonyLib.AccessTools.Field(typeof(Player), "m_crouchToggled");
                    if (fiCrouchToggle != null)
                    {
                        try { crouchTogglePre = (bool)fiCrouchToggle.GetValue(player); } catch { }
                    }
                    else
                    {
                        try { crouchTogglePre = player.IsCrouching(); } catch { }
                    }
                    if (ExtraAttackPlugin.DebugAOCOperations.Value)
                    {
                        ExtraAttackPlugin.LogInfo("AOC", $"[{mode}] Pre-ApplyAOC: crouchToggle={crouchTogglePre} stamina={player.GetStamina():F1}");
                    }
                    

                    
                    ExtraAttackUtils.SetPlayerCooldown(player, mode);

                    // Mark bypass for our own StartAttack call
                    ExtraAttackUtils.MarkBypassNextStartAttack(player);
                    player.StartAttack(null, true);

                    // Diagnostics: capture animator parameters immediately after StartAttack
                    ExtraAttackPatches_Core.LogAnimatorParameters(player, $"[{mode}] After StartAttack");
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.LogError("System", $"Error in Extra Attack ({mode}): {ex.Message}");
                    ExtraAttackUtils.SetAttackMode(player, ExtraAttackUtils.AttackMode.Normal);
                }
            }
        }

        // Keep crouch visual during Extra attacks (Prefix to vanilla UpdateCrouch)
        [HarmonyPatch(typeof(Player), "UpdateCrouch")]
        public static class Player_UpdateCrouch_KeepSneakDuringExtra_Prefix
        {
            public static bool Prefix(Player __instance, ref ZSyncAnimation ___m_zanim)
            {
                try
                {
                    // Only adjust local player; prevent vanilla from forcing crouching=false during attack when extra mode is active
                    return true;
                }
                catch (System.Exception ex)
                {
                    ExtraAttackPlugin.LogError("System", $"Error in Player_UpdateCrouch_KeepSneakDuringExtra_Prefix: {ex.Message}");
                }
                return true; // Run original logic otherwise
            }
        }

        // Keep crouch visual during Extra attacks (Postfix to vanilla UpdateCrouch)
        [HarmonyPatch(typeof(Player), "UpdateCrouch")]
        public static class Player_UpdateCrouch_KeepSneakDuringExtra_Patch
        {
            public static void Postfix(Player __instance, ref ZSyncAnimation ___m_zanim)
            {
                try
                {
                    // Only adjust local player; respect crouch toggle and apply during Extra attack modes
                    if (__instance == Player.m_localPlayer && __instance != null)
                    {
                        // Use crouch toggle or pre-recorded crouch state instead of IsCrouching() (anim tag) to decide
                        bool crouchToggle = false;
                        var fiCrouch = HarmonyLib.AccessTools.Field(typeof(Player), "m_crouchToggled");
                        if (fiCrouch != null)
                        {
                            try { crouchToggle = (bool)fiCrouch.GetValue(__instance); } catch { }
                        }
                        if (__instance.InAttack() && (crouchToggle))
                         {
                             // Force crouching animator bool to remain true during Extra attack to avoid stand-up motion
                             // Avoid accessing private Player.s_crouching; use ZSyncAnimation.GetHash("crouching")
                             int crouchingHash = ZSyncAnimation.GetHash("crouching");
                             ___m_zanim?.SetBool(crouchingHash, true);
                         }
                    }
                }
                catch (System.Exception ex)
                {
                    ExtraAttackPlugin.LogError("System", $"Error in Player_UpdateCrouch_KeepSneakDuringExtra_Patch: {ex.Message}");
                }
            }
        }

        // Restore crouch toggle during Extra attacks if vanilla SetControls toggled it off
        [HarmonyPatch(typeof(Player), "SetControls")]
        public static class Player_SetControls_RestoreCrouch_Postfix
        {
            // Postfix: executed after vanilla SetControls processed input; if extra attack is active and crouch was recorded before extra, restore toggle
            public static void Postfix(Player __instance)
            {
                try
                {
                    if (__instance == null || __instance != Player.m_localPlayer)
                        return;
                    return;
                }
                catch (System.Exception ex)
                {
                    ExtraAttackPlugin.LogError("System", $"Error in Player_SetControls_RestoreCrouch_Postfix: {ex.Message}");
                }
            }
        }

        // Helper removed as per cleanup request

        // Check for equipment changes and update AOC accordingly
        private static void CheckEquipmentChanges(Player player)
        {
            try
            {
                if (player == null) return;

                var traverse = Traverse.Create(player);
                var rightItem = traverse.Field("m_rightItem").GetValue<ItemDrop.ItemData>();
                var leftItem = traverse.Field("m_leftItem").GetValue<ItemDrop.ItemData>();

                string currentRightItem = rightItem?.m_shared?.m_name ?? string.Empty;
                string currentLeftItem = leftItem?.m_shared?.m_name ?? string.Empty;

                // Check if equipment changed
                bool rightChanged = !lastRightItem.TryGetValue(player, out string lastRight) || lastRight != currentRightItem;
                bool leftChanged = !lastLeftItem.TryGetValue(player, out string lastLeft) || lastLeft != currentLeftItem;

                if (rightChanged || leftChanged)
                {
                    // Update tracking
                    lastRightItem[player] = currentRightItem;
                    lastLeftItem[player] = currentLeftItem;

                    // Check if current equipment should be excluded
                    bool shouldExclude = false;
                    if (rightItem != null && ExtraAttackExclusionConfig.ShouldBlockExtraForItem(rightItem))
                    {
                        shouldExclude = true;
                    }
                    if (leftItem != null && ExtraAttackExclusionConfig.ShouldBlockExtraForItem(leftItem))
                    {
                        shouldExclude = true;
                    }

                    if (!shouldExclude)
                    {
                        // Update AOC for current equipment
                        UpdateAOCForEquipment(player, rightItem, leftItem);
                    }
                    else
                    {
                        // Revert to original AOC for excluded items
                        RevertToOriginalAOC(player);
                    }
                }
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error in CheckEquipmentChanges: {ex.Message}");
            }
        }

        // Update AOC for current equipment
        private static void UpdateAOCForEquipment(Player player, ItemDrop.ItemData? rightItem, ItemDrop.ItemData? leftItem)
        {
            try
            {
                if (ExtraAttackPatches_Core.TryGetPlayerAnimator(player, out Animator? animator) && animator != null)
                {
                    // Initialize AOC for current equipment
                    ExtraAttackPatches_Animation.InitializeAOC(player, animator);
                    
                    // Pre-generate Q/T/G AOCs for current equipment (generation only, no application)
                    ExtraAttackPatches_Animation.BuildOrGetAOCFor(player, animator, ExtraAttackUtils.AttackMode.secondary_Q);
                    ExtraAttackPatches_Animation.BuildOrGetAOCFor(player, animator, ExtraAttackUtils.AttackMode.secondary_T);
                    ExtraAttackPatches_Animation.BuildOrGetAOCFor(player, animator, ExtraAttackUtils.AttackMode.secondary_G);
                    
                    if (ExtraAttackPlugin.DebugAOCOperations.Value)
                    {
                        ExtraAttackPlugin.LogInfo("AOC", $"Updated AOC for equipment: Right={rightItem?.m_shared?.m_name ?? "None"}, Left={leftItem?.m_shared?.m_name ?? "None"}");
                    }
                }
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error in UpdateAOCForEquipment: {ex.Message}");
            }
        }

        // Revert to original AOC for excluded items
        private static void RevertToOriginalAOC(Player player)
        {
            try
            {
                if (ExtraAttackPatches_Core.TryGetPlayerAnimator(player, out Animator? animator) && animator != null)
                {
                    
                    if (ExtraAttackPlugin.DebugAOCOperations.Value)
                    {
                        ExtraAttackPlugin.LogInfo("AOC", "Reverted to original AOC for excluded equipment");
                    }
                }
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error in RevertToOriginalAOC: {ex.Message}");
            }
        }

        // Reload YAML configs (for F6 key or button)
        public static void ReloadYamlConfigs()
        {
            try
            {
                ExtraAttackPlugin.LogInfo("System", "Starting YAML reload process");
                
                // What: Reload YAML configs from BepInEx\config\ExtraAttackSystem
                // Why: Apply user edits without restarting the game
                ExtraAttackPlugin.LogInfo("System", "Reloading AnimationReplacement configs");
                AnimationReplacementConfig.Reload();
                
                // Also reload timing & exclusion configs to reflect user edits consistently
                ExtraAttackPlugin.LogInfo("System", "Reloading AnimationTiming configs");
                AnimationTimingConfig.Reload();
                
                ExtraAttackExclusionConfig.Reload();
                
                // Clear AOC cache to force rebuild on next ApplyStyleAOC
                ExtraAttackPlugin.LogInfo("System", "Clearing AOC cache");
                AnimationManager.ClearAOCCache(true);
                
                ExtraAttackPlugin.LogInfo("System", "YAML reload process completed");
                
                // Show notification in game
                if (MessageHud.instance != null)
                {
                    MessageHud.instance.ShowMessage(MessageHud.MessageType.TopLeft, "YAML configs reloaded successfully", 0, null);
                }
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error during YAML reload: {ex.Message}");
                
                // Show error notification in game
                if (MessageHud.instance != null)
                {
                    MessageHud.instance.ShowMessage(MessageHud.MessageType.TopLeft, "YAML reload error", 0, null);
                }
            }
        }
    }
}