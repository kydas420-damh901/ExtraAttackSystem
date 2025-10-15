using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ExtraAttackSystem
{
    public static class EAS_ExclusionConfig
    {
        private static string ConfigFolderPath => Path.Combine(BepInEx.Paths.ConfigPath, "ExtraAttackSystem");
        private static string ConfigFilePath => Path.Combine(ConfigFolderPath, "ExtraAttackExclusion.yaml");

        public class ExclusionConfig
        {
            public List<string> ExcludedItemNames { get; set; } = new List<string>();
            public List<string> ExcludedItemTypes { get; set; } = new List<string>();
            public List<string> ExcludedPrefabNames { get; set; } = new List<string>();
        }

        private static ExclusionConfig current = new ExclusionConfig();

        public static void Initialize()
        {
            try
            {
                if (!Directory.Exists(ConfigFolderPath))
                {
                    Directory.CreateDirectory(ConfigFolderPath);
                }

                if (ShouldCreateOrRegenerateExclusionConfig())
                {
                    CreateDefault();
                    ExtraAttackSystemPlugin.LogInfo("System", "Created/regenerated ExtraAttackExclusion.yaml");
                }
                else
                {
                    Load();
                    ExtraAttackSystemPlugin.LogInfo("System", $"Loaded ExtraAttackExclusion.yaml: names={current.ExcludedItemNames.Count}, types={current.ExcludedItemTypes.Count}, prefabs={current.ExcludedPrefabNames.Count}");
                }
            }
            catch (Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error initializing EAS_ExclusionConfig: {ex.Message}");
            }
        }

        private static bool ShouldCreateOrRegenerateExclusionConfig()
        {
            if (!File.Exists(ConfigFilePath))
            {
                ExtraAttackSystemPlugin.LogInfo("System", "ExtraAttackExclusion.yaml not found, will create");
                return true;
            }

            try
            {
                string content = File.ReadAllText(ConfigFilePath, Encoding.UTF8).Trim();
                if (string.IsNullOrEmpty(content))
                {
                    ExtraAttackSystemPlugin.LogInfo("System", "ExtraAttackExclusion.yaml is empty, will regenerate");
                    return true;
                }

                if (!content.Contains("ExcludedItemNames:") && !content.Contains("ExcludedItemTypes:") && !content.Contains("ExcludedPrefabNames:"))
                {
                    ExtraAttackSystemPlugin.LogInfo("System", "ExtraAttackExclusion.yaml has no exclusion data, will regenerate");
                    return true;
                }

                ExtraAttackSystemPlugin.LogInfo("System", "ExtraAttackExclusion.yaml exists and has content, skipping generation");
                return false;
            }
            catch (Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error checking ExtraAttackExclusion.yaml: {ex.Message}");
                return true;
            }
        }

        private static void CreateDefault()
        {
            current = new ExclusionConfig();

            // Default: block all Tool types (Hoe/Hammer/Pickaxe etc.)
            current.ExcludedItemTypes.Add(nameof(ItemDrop.ItemData.ItemType.Tool));

            // Default: explicit prefabs for common tools
            current.ExcludedPrefabNames.AddRange(new[]
            {
                "Hammer",
                "Hoe",
                "PickaxeAntler",
                "PickaxeIron",
                "PickaxeBlackMetal",
                "Cultivator",
                "Tankard",
                "TankardOdin"
            });

            Save();
        }

        private static void Load()
        {
            try
            {
                if (!File.Exists(ConfigFilePath))
                {
                    ExtraAttackSystemPlugin.LogInfo("System", "ExtraAttackExclusion.yaml not found");
                    return;
                }

                string content = File.ReadAllText(ConfigFilePath, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(content))
                {
                    ExtraAttackSystemPlugin.LogInfo("System", "ExtraAttackExclusion.yaml is empty");
                    return;
                }

                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .IgnoreUnmatchedProperties()
                    .Build();

                current = deserializer.Deserialize<ExclusionConfig>(content) ?? new ExclusionConfig();
                ExtraAttackSystemPlugin.LogInfo("System", "ExtraAttackExclusion.yaml loaded successfully");
            }
            catch (Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error loading ExtraAttackExclusion.yaml: {ex.Message}");
                current = new ExclusionConfig();
            }
        }

        private static void Save()
        {
            try
            {
                var serializer = new SerializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();

                string yaml = serializer.Serialize(current);
                File.WriteAllText(ConfigFilePath, yaml, Encoding.UTF8);
                ExtraAttackSystemPlugin.LogInfo("System", $"Saved ExtraAttackExclusion.yaml to {ConfigFilePath}");
            }
            catch (Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error saving ExtraAttackExclusion.yaml: {ex.Message}");
            }
        }

        // Check if item is excluded
        public static bool IsItemExcluded(ItemDrop.ItemData item)
        {
            if (item == null) return true;

            try
            {
                // Check item name
                if (current.ExcludedItemNames.Contains(item.m_shared.m_name))
                {
                    return true;
                }

                // Check item type
                if (current.ExcludedItemTypes.Contains(item.m_shared.m_itemType.ToString()))
                {
                    return true;
                }

                // Check prefab name
                if (item.m_dropPrefab != null)
                {
                    string prefabName = item.m_dropPrefab.name;
                    if (current.ExcludedPrefabNames.Contains(prefabName))
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error checking item exclusion: {ex.Message}");
                return false;
            }
        }

        // Reload exclusion config
        public static void Reload()
        {
            try
            {
                Load();
                ExtraAttackSystemPlugin.LogInfo("System", "ExtraAttackExclusion.yaml reloaded successfully");
            }
            catch (Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error reloading ExtraAttackExclusion.yaml: {ex.Message}");
            }
        }
    }
}
