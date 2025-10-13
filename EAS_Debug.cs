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

        // Log all animation parameters (one-time output)
        public static void LogAllAnimationParameters()
        {
            ExtraAttackPlugin.LogInfo("System", "=== DEBUG: ANIMATION PARAMETERS ===");
            
            try
            {
                if (Player.m_localPlayer != null)
                {
                    // Use reflection to access private m_animator field
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
                        else
                        {
                            ExtraAttackPlugin.LogInfo("System", "Player animator is null");
                        }
                    }
                    else
                    {
                        ExtraAttackPlugin.LogInfo("System", "Could not find m_animator field");
                    }
                }
                else
                {
                    ExtraAttackPlugin.LogInfo("System", "Player not available");
                }
                
                ExtraAttackPlugin.LogInfo("System", "=== END ANIMATION PARAMETERS ===");
            }
            catch (System.Exception ex)
            {
                ExtraAttackPlugin.LogInfo("System", $"Error logging animation parameters: {ex.Message}");
            }
        }
        
        // Log animation parameters for specific player (used by ExtraAttackPatches_Core)
        public static void LogAllAnimationParameters(Player player)
        {
            ExtraAttackPlugin.LogInfo("System", "=== DEBUG: ANIMATION PARAMETERS ===");
            
            try
            {
                if (player != null)
                {
                    // Use reflection to access private m_animator field
                    var animatorField = typeof(Player).GetField("m_animator", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (animatorField != null)
                    {
                        var animator = animatorField.GetValue(player) as Animator;
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
                        else
                        {
                            ExtraAttackPlugin.LogInfo("System", "Player animator is null");
                        }
                    }
                    else
                    {
                        ExtraAttackPlugin.LogInfo("System", "Could not find m_animator field");
                    }
                }
                else
                {
                    ExtraAttackPlugin.LogInfo("System", "Player is null");
                }
                
                ExtraAttackPlugin.LogInfo("System", "=== END ANIMATION PARAMETERS ===");
            }
            catch (System.Exception ex)
            {
                ExtraAttackPlugin.LogInfo("System", $"Error logging animation parameters: {ex.Message}");
            }
        }
        
    }
}
