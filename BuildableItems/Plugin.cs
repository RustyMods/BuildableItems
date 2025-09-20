using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using JetBrains.Annotations;
using PieceManager;
using ServerSync;
using UnityEngine;

namespace BuildableItems
{
    public static class Extensions
    {
        public static bool HasIcons(this ItemDrop.ItemData item) => item.m_shared.m_icons.Length > 0;
        
        public static bool IsMeshReadable(this GameObject prefab)
        {
            foreach (var renderer in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!renderer.mesh.isReadable) return false;
            }
            return true;
        }
    }
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class BuildableItemsPlugin : BaseUnityPlugin
    {
        internal const string ModName = "BuildableItems";
        internal const string ModVersion = "1.0.0";
        internal const string Author = "RustyMods";
        private const string ModGUID = Author + "." + ModName;
        private static readonly string ConfigFileName = ModGUID + ".cfg";

        private static readonly string ConfigFileFullPath =
            Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;

        internal static string ConnectionError = "";
        private readonly Harmony _harmony = new(ModGUID);
        public static readonly ManualLogSource BuildableItemsLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);

        private static readonly ConfigSync ConfigSync = new(ModGUID)
            { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };

        public enum Toggle
        {
            On = 1,
            Off = 0
        }

        // private static ConfigEntry<Toggle> _serverConfigLocked = null!;

        public void Awake()
        {
            // _serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On,
            //     "If on, the configuration is locked and can be changed by server admins only.");
            // _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);
            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            SetupWatcher();
        }

        [HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.Awake))]
        private static class FejdStartup_Awake
        {
            [UsedImplicitly]
            private static void Postfix(FejdStartup __instance)
            {
                ZNetScene? scene = __instance.m_objectDBPrefab.GetComponent<ZNetScene>();
                ItemDrop? hammer = scene.m_prefabs.Find(x => x.name == "Hammer").GetComponent<ItemDrop>();
                var workbench = scene.m_prefabs.Find(x => x.name == "piece_workbench").GetComponent<Piece>();
                foreach (GameObject? prefab in scene.m_prefabs)
                {
                    if (prefab == null || !prefab.TryGetComponent(out ItemDrop component) ||
                        !component.m_itemData.HasIcons() || prefab.GetComponent<Piece>()
                        || prefab.GetComponent<Fish>() || prefab.name.ToLower().EndsWith("_material") || !prefab.IsMeshReadable()) continue;
                    Piece piece = prefab.AddComponent<Piece>();
                    piece.m_name = component.m_itemData.m_shared.m_name;
                    piece.m_icon = component.m_itemData.GetIcon();
                    piece.m_description = component.m_itemData.m_shared.m_description;
                    piece.m_placeEffect = workbench.m_placeEffect;
                    piece.m_category = PiecePrefabManager.GetCategory(component.m_itemData.m_shared.m_itemType.ToString());
                    piece.m_resources = new List<Piece.Requirement>()
                    {
                        new ()
                        {
                            m_resItem = component,
                            m_amount = 1,
                            m_recover = true
                        }
                    }.ToArray();
                    
                    hammer.m_itemData.m_shared.m_buildPieces.m_pieces.Add(prefab);
                }
            }
        }

        [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.MakePiece))]
        private static class ItemDrop_MakePiece_Patch
        {
            [UsedImplicitly]
            private static void Postfix(ItemDrop __instance)
            {
                if (__instance.TryGetComponent(out ParticleSystem system))
                {
                    system.Pause();
                }
            }
        }

        private void OnDestroy()
        {
            Config.Save();
        }

        private void SetupWatcher()
        {
            FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName);
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = true;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                BuildableItemsLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                BuildableItemsLogger.LogError($"There was an issue loading your {ConfigFileName}");
                BuildableItemsLogger.LogError("Please check your config entries for spelling and format!");
            }
        }

        private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description,
            bool synchronizedSetting = true)
        {
            ConfigDescription extendedDescription =
                new(
                    description.Description +
                    (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"),
                    description.AcceptableValues, description.Tags);
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, extendedDescription);
            //var configEntry = Config.Bind(group, name, value, description);

            SyncedConfigEntry<T> syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        private ConfigEntry<T> config<T>(string group, string name, T value, string description,
            bool synchronizedSetting = true)
        {
            return config(group, name, value, new ConfigDescription(description), synchronizedSetting);
        }

        private class ConfigurationManagerAttributes
        {
            [UsedImplicitly] public int? Order = null!;
            [UsedImplicitly] public bool? Browsable = null!;
            [UsedImplicitly] public string? Category = null!;
            [UsedImplicitly] public Action<ConfigEntryBase>? CustomDrawer = null!;
        }
    }
}