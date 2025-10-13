using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BepInEx.Configuration;

namespace ExtraAttackSystem
{
    public static class EAS_Debug
    {
        // Note: All configuration and caching is handled in ExtraAttackPlugin.cs
        
        // Initialize debug configuration (called from ExtraAttackPlugin.cs)
        public static void Initialize()
        {
            // Configuration Manager entries are handled in ExtraAttackPlugin.cs
            ExtraAttackPlugin.LogInfo("System", "EAS_Debug system initialized");
        }
        
        // Public properties for checking debug states (using ExtraAttackPlugin values)
        public static bool IsDebugClipNamesEnabled => ExtraAttackPlugin.IsDebugClipNamesEnabled;
        public static bool IsDebugSystemMessagesEnabled => ExtraAttackPlugin.IsDebugSystemMessagesEnabled;
        
        
        // Log all animation events from all clips
        public static void LogAllAnimationEvents()
        {
            ExtraAttackPlugin.LogInfo("System", "=== DEBUG: ANIMATION EVENTS ===");
            
            try
            {
                var allClips = Resources.FindObjectsOfTypeAll<AnimationClip>();
                ExtraAttackPlugin.LogInfo("System", $"Found {allClips.Length} animation clips");
                
                foreach (var clip in allClips)
                {
                    if (clip.events != null && clip.events.Length > 0)
                    {
                        ExtraAttackPlugin.LogInfo("System", $"Clip: {clip.name} - {clip.events.Length} events");
                        foreach (var evt in clip.events)
                        {
                            ExtraAttackPlugin.LogInfo("System", $"  Event: {evt.functionName} at {evt.time:F3}s");
                        }
                    }
                }
                
                ExtraAttackPlugin.LogInfo("System", "=== END ANIMATION EVENTS ===");
            }
            catch (System.Exception ex)
            {
                ExtraAttackPlugin.LogInfo("System", $"Error logging animation events: {ex.Message}");
            }
        }
        
        // Log all animation clips
        public static void LogAllAnimationClips()
        {
            ExtraAttackPlugin.LogInfo("System", "LogAllAnimationClips called - executing list output");
            
            ExtraAttackPlugin.LogInfo("System", "=== DEBUG: ANIMATION CLIPS ===");
            
            try
            {
                var allClips = Resources.FindObjectsOfTypeAll<AnimationClip>();
                
                ExtraAttackPlugin.LogInfo("System", $"Found {allClips.Length} animation clips");
                
                foreach (var clip in allClips.OrderBy(c => c.name))
                {
                    ExtraAttackPlugin.LogInfo("System", 
                        $"  Clip: [{clip.name}] - Length: {clip.length:F3}s, FrameRate: {clip.frameRate}, Legacy: {clip.legacy}, Events: {clip.events?.Length ?? 0}");
                }
                
                ExtraAttackPlugin.LogInfo("System", "=== END ANIMATION CLIPS ===");
            }
            catch (System.Exception ex)
            {
                ExtraAttackPlugin.LogInfo("System", $"Error logging animation clips: {ex.Message}");
            }
        }
        
        
        // Log clip name during operations
        public static void LogClipName(string operation, string clipName)
        {
            if (!IsDebugClipNamesEnabled) return;
            
            ExtraAttackPlugin.LogInfo("System", $"{operation}: {clipName}");
        }
        
        // Log clip name with additional info
        public static void LogClipName(string operation, string clipName, string additionalInfo)
        {
            if (!IsDebugClipNamesEnabled) return;
            
            ExtraAttackPlugin.LogInfo("System", $"{operation}: {clipName} - {additionalInfo}");
        }

        // Log all animation parameters, events, and attack parameters (one-time output)
        public static void LogAllAnimationParameters()
        {
            ExtraAttackPlugin.LogInfo("System", "=== DEBUG: COMPLETE PARAMETERS LIST ===");
            
            try
            {
                if (Player.m_localPlayer != null)
                {
                    // 1. Animation Parameters
                    ExtraAttackPlugin.LogInfo("System", "--- ANIMATION PARAMETERS ---");
                    var animatorField = typeof(Player).GetField("m_animator", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (animatorField != null)
                    {
                        var animator = animatorField.GetValue(Player.m_localPlayer) as Animator;
                        if (animator != null)
                        {
                            var parameters = animator.parameters;
                            ExtraAttackPlugin.LogInfo("System", $"Found {parameters?.Length ?? 0} animator parameters");
                            
                            if (parameters != null)
                            {
                                foreach (var param in parameters)
                                {
                                    string valueStr = param.type switch
                                    {
                                        AnimatorControllerParameterType.Bool => animator.GetBool(param.nameHash).ToString(),
                                        AnimatorControllerParameterType.Int => animator.GetInteger(param.nameHash).ToString(),
                                        AnimatorControllerParameterType.Float => animator.GetFloat(param.nameHash).ToString("F3"),
                                        AnimatorControllerParameterType.Trigger => "Trigger",
                                        _ => "Unknown"
                                    };
                                    
                                    ExtraAttackPlugin.LogInfo("System", $"  Parameter: {param.name} ({param.type}) = {valueStr}");
                                }
                            }
                        }
                    }

                    // 2. Attack Parameters (if attacking)
                    ExtraAttackPlugin.LogInfo("System", "--- ATTACK PARAMETERS ---");
                    var weapon = Player.m_localPlayer.GetCurrentWeapon();
                    if (weapon != null)
                    {
                        ExtraAttackPlugin.LogInfo("System", $"Current Weapon: {weapon.m_shared.m_name}");
                        
                        // Get Attack component
                        var attackComponent = Player.m_localPlayer.GetComponent<Attack>();
                        if (attackComponent != null)
                        {
                            ExtraAttackPlugin.LogInfo("System", $"  attackRange: {attackComponent.m_attackRange}");
                            ExtraAttackPlugin.LogInfo("System", $"  attackHeight: {attackComponent.m_attackHeight}");
                            ExtraAttackPlugin.LogInfo("System", $"  attackOffset: {attackComponent.m_attackOffset}");
                            ExtraAttackPlugin.LogInfo("System", $"  attackAngle: {attackComponent.m_attackAngle}");
                            ExtraAttackPlugin.LogInfo("System", $"  attackRayWidth: {attackComponent.m_attackRayWidth}");
                            ExtraAttackPlugin.LogInfo("System", $"  attackRayWidthCharExtra: {attackComponent.m_attackRayWidthCharExtra}");
                            ExtraAttackPlugin.LogInfo("System", $"  attackHeightChar1: {attackComponent.m_attackHeightChar1}");
                            ExtraAttackPlugin.LogInfo("System", $"  attackHeightChar2: {attackComponent.m_attackHeightChar2}");
                            ExtraAttackPlugin.LogInfo("System", $"  maxYAngle: {attackComponent.m_maxYAngle}");
                            ExtraAttackPlugin.LogInfo("System", $"  attackStamina: {attackComponent.m_attackStamina}");
                            ExtraAttackPlugin.LogInfo("System", $"  attackEitr: {attackComponent.m_attackEitr}");
                        }
                        else
                        {
                            ExtraAttackPlugin.LogInfo("System", "  Attack component not found");
                        }
                    }
                    else
                    {
                        ExtraAttackPlugin.LogInfo("System", "No weapon equipped");
                    }

                    // 3. Animation Events (from current animation clips)
                    ExtraAttackPlugin.LogInfo("System", "--- ANIMATION EVENTS ---");
                    if (animatorField != null)
                    {
                        var animator = animatorField.GetValue(Player.m_localPlayer) as Animator;
                        if (animator != null)
                        {
                            var clips = animator.runtimeAnimatorController.animationClips;
                            ExtraAttackPlugin.LogInfo("System", $"Found {clips?.Length ?? 0} animation clips");
                            
                            if (clips != null)
                            {
                                foreach (var clip in clips.Take(10)) // Limit to first 10 clips
                                {
                                    ExtraAttackPlugin.LogInfo("System", $"  Clip: {clip.name} - Length: {clip.length:F3}s, Events: {clip.events?.Length ?? 0}");
                                    
                                    if (clip.events != null && clip.events.Length > 0)
                                    {
                                        foreach (var evt in clip.events)
                                        {
                                            ExtraAttackPlugin.LogInfo("System", $"    Event: {evt.functionName} at {evt.time:F3}s");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    ExtraAttackPlugin.LogInfo("System", "Player not available");
                }
                
                ExtraAttackPlugin.LogInfo("System", "=== END COMPLETE PARAMETERS LIST ===");
            }
            catch (System.Exception ex)
            {
                ExtraAttackPlugin.LogInfo("System", $"Error logging complete parameters: {ex.Message}");
            }
        }
        
        
    }
}
