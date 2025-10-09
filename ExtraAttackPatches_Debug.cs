using HarmonyLib;
using UnityEngine;
using ExtraAttackSystem;

namespace ExtraAttackSystem
{
    [HarmonyPatch(typeof(Attack), nameof(Attack.Start))]
    public static class Attack_Start_DebugPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Attack __instance, Humanoid character, ItemDrop.ItemData weapon, bool __result)
        {
            if (__result && character == Player.m_localPlayer)
            {
                ExtraAttackUtils.AttackMode currentMode = ExtraAttackUtils.GetAttackMode(character as Player);
                if (currentMode != ExtraAttackUtils.AttackMode.Normal)
                {
                    ExtraAttackPlugin.LogInfo("Debug", $"Attack.Start: mode={currentMode}, animation={__instance.m_attackAnimation}");
                    ExtraAttackPlugin.LogInfo("Debug", $"Attack.Start: Using animation '{__instance.m_attackAnimation}' for mode {currentMode}");
                    
                    // アニメーターの現在の状態も確認
                    var animator = character.GetComponent<Animator>();
                    if (animator != null)
                    {
                        var currentState = animator.GetCurrentAnimatorStateInfo(0);
                        ExtraAttackPlugin.LogInfo("Debug", $"Attack.Start: Current animator state: {currentState.shortNameHash} (normalizedTime: {currentState.normalizedTime})");
                    }
                }
            }
        }
    }
}