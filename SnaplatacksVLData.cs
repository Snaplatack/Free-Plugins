// Requires: VehicleLicence

using System.Collections.Generic;
using System.Linq;
using System;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;
using System.ComponentModel;
using System.Net;
using System.IO;
using System.Collections.Specialized;

namespace Oxide.Plugins
{
    [Info("Snaplatack's Vehicle Licence Data", "Snaplatack", "1.0.0")]
    [Description("Data to support all my Vehicle Licence plugins")]
    class SnaplatacksVLData : RustPlugin
    {
        [PluginReference]
        private readonly Plugin VehicleLicenceUI, VehicleLicenceAddons, VehicleLicenceTransmitter, VehicleSpawnerUI;

        public SnaplatacksVLData Instance;
        private Timer shutdownTimer;
        private const string command = "snapvldata";
        private const string perm = "snaplatacksvldata.use";
        private const string errorMsg = "SYNTAX ERROR\n/snapvldata <reset>\n/snapvldata <set> <vehicleName> <displayname | skin>";

        #region Data Setup
        private void Init()
        {
            Instance = this;

            AddCovalenceCommand(command, nameof(UpdateDataVariable), perm);

            LoadCustomSettings();

            UpdateVehicleData();

            UpdateVLSettings();
        }

        private void Loaded()
        { 
            if (config.settings.autoReload) ReloadPlugins();

            if (!config.settings.autoUnload) return;
            
            shutdownTimer = timer.Once(10f,() =>
            {
                Interface.Oxide.UnloadPlugin(Name);
            });
        }

        private void Unload()
        {
            if (shutdownTimer != null) shutdownTimer.Destroy();
        }
        #endregion

        #region Commands
        private void UpdateDataVariable(IPlayer player, string cmd, string[] args)
        {
            // Command Format:
            // /snapvldata <reload>
            // /snapvldata <reset>
            // /snapvldata <set> <displayname | skin> <vehicleName> <new value>

            if (player == null && !player.IsServer) return;

            if (args.Length < 1)
            {
                Message(player, errorMsg);
                return;
            }

            string argType = args[0].ToLower();

            switch (argType)
            {
                case ("reload"):
                    ReloadPlugins();
                    return;
                case ("reset"):
                    UpdateVehicleData(true);
                    Message(player, "Successfully reset all data!");
                    return;
                default:
                    if (args.Length < 3)
                    {
                        Message(player, errorMsg);
                        return;
                    }
                    break;
            }

            string dataTypePassed = args[1].ToLower();

            if (dataTypePassed != "skin")
            {
                Message(player, errorMsg);
                return;
            }

            string vehicleName = args[2].ToLower();

            ulong.TryParse(args[3], out ulong newID);

            string msg;

            VehicleLicenceVehicles DataEntry = VehicleLicenceNames.vehicles.Find(x => x.VLName.ToLower() == vehicleName);

            if (DataEntry == null || VehicleLicenceNames.vehicles.Count == 0)
            {
                msg = "NO DATA FOUND! Resetting Data...";
                Message(player, msg);
                UpdateVehicleData(true);
                return;
            }

            SkinHistory skinData = CustomSettings.skinIsCustom[DataEntry.VLName];

            if (newID != 0)
            {
                skinData.newID = newID;
                DataEntry.SkinID = newID;
                msg = $"Updated the {dataTypePassed} for the {DataEntry.VLName} with {newID}!";
            }
            else
            {
                skinData.newID = 0;
                DataEntry.SkinID = skinData.originalID;
                msg = $"Reset the {dataTypePassed} for the {DataEntry.VLName}!";
            }

            Message(player, msg);

            SaveVLVehicles();
        }
        #endregion

        #region Methods

        #region Update Data
        private void UpdateVLSettings()
        {
            var VLData = VehicleLicence.Instance.configData.chat;

            VLChatSettings chatSettings = new();

            // Commands
            chatSettings.spawnCmd = VLData.spawnCommand;
            chatSettings.recallCmd = VLData.recallCommand;
            chatSettings.killCmd = VLData.killCommand;

            // Chat Settings
            chatSettings.prefix = VLData.prefix;
            chatSettings.steamIDIcon = VLData.steamIDIcon;

            VLChatParams.settings = chatSettings;

            SaveVLChatSettings();
        }

        private void UpdateVehicleData(bool resetData = false)
        {
            int i = 2;
            string[] carBS = new string[] {"SmallCar", "MediumCar", "LargeCar"};
            var karuzaVehicleConfig = Config.ReadObject<KaruzaConfiguration>($"{Interface.Oxide.ConfigDirectory}/KaruzaVehicleItemManager.json");
            var karuzaVehicleParams = karuzaVehicleConfig.VehicleItems.Count != 0 ? karuzaVehicleConfig.VehicleItems : new();

            if (karuzaVehicleParams.Count == 0) Puts($"Loaded '?' for all custom vehicle icons because KaruzaVehicleItemManager is missing!");

            if (resetData)
            {
                VehicleLicenceNames.vehicles = new();
                CustomSettings.skinIsCustom = new();
            }

            foreach (var vehicleType in VehicleLicence.Instance.allVehicleSettings)
            {
                VehicleLicenceVehicles VLParams = new();

                CustomSettings.skinIsCustom.TryGetValue(vehicleType.Key, out SkinHistory customSkinIDs);

                if (customSkinIDs == null) CustomSettings.skinIsCustom[vehicleType.Key] = customSkinIDs = new();

                if (vehicleType.Value.CustomVehicle)
                {
                    if ((karuzaVehicleParams.Count != 0) && !removedVehicles.Exists(x => x.vehicleName == vehicleType.Key))
                    {
                        if (!karuzaVehicleParams.Exists(x => CleanVehicleName(x.VLName) == CleanVehicleName(vehicleType.Key)))
                        {
                            Puts($"{vehicleType.Key} was not found in KaruzaVehicleItemManager. Skipping...");
                            continue;
                        }
                            
                        VLParams.PrefabName = CleanVehicleName(karuzaVehicleParams.Find(x => CleanVehicleName(x.VLName) == CleanVehicleName(vehicleType.Key)).VLName);

                        CustomSettings.skinIsCustom[vehicleType.Key].originalID = karuzaVehicleParams.Find(x => CleanVehicleName(x.VLName) == CleanVehicleName(vehicleType.Key)).SkinID;
                        
                        VLParams.SkinID = customSkinIDs.newID == 0
                            ? karuzaVehicleParams.Find(x => CleanVehicleName(x.VLName) == CleanVehicleName(vehicleType.Key)).SkinID
                            : customSkinIDs.newID;
                    }
                    else
                    {
                        VLParams.PrefabName = vehicleType.Key.ToLower();

                        CustomSettings.skinIsCustom[vehicleType.Key].originalID = removedVehicles.Find(x => x.vehicleName == vehicleType.Key).vehicleID;

                        VLParams.SkinID = customSkinIDs.newID == 0
                            ? removedVehicles.Find(x => x.vehicleName == vehicleType.Key).vehicleID
                            : customSkinIDs.newID;
                    }
                }
                else
                {
                    if (carBS.Contains(vehicleType.Key))
                    {
                        VLParams.FuckOffFacepunch = $"{i}modulecarspawned";
                        i++;
                    }

                    CustomSettings.skinIsCustom[vehicleType.Key].originalID = vehicleItems.Find(value => value.VLName == vehicleType.Key).SkinID;

                    VLParams.SkinID = customSkinIDs.newID == 0
                        ? vehicleItems.Find(value => value.VLName == vehicleType.Key).SkinID
                        : customSkinIDs.newID;

                    VLParams.PrefabName = vehicleItems.Find(value => value.VLName == vehicleType.Key).PrefabName;
                }

                VLParams.VLDisplayName = vehicleType.Value.DisplayName;
                VLParams.VLName = vehicleType.Key;

                VLParams.IsWaterVehicle = vehicleType.Value.IsWaterVehicle;
                VLParams.IsPurchasable = vehicleType.Value.Purchasable;
                VLParams.IsCustomVehicle = vehicleType.Value.CustomVehicle;

                VLParams.Permission = vehicleType.Value.Permission;
                VLParams.BypassCostPermission = vehicleType.Value.BypassCostPermission;

                VLParams.PurchasePrices = new();

                foreach (var price in vehicleType.Value.PurchasePrices)
                {
                    if (vehicleType.Value.PurchasePrices.Count == 0) break;

                    PriceInfo purchasePrices = new();

                    purchasePrices.currency = price.Key;
                    purchasePrices.amount = price.Value.amount;
                    purchasePrices.displayName = price.Value.displayName;

                    VLParams.PurchasePrices.Add(purchasePrices);
                }

                foreach (var price in vehicleType.Value.SpawnPrices)
                {
                    if (vehicleType.Value.SpawnPrices.Count == 0) break;

                    PriceInfo spawnPrices = new();

                    spawnPrices.currency = price.Key;
                    spawnPrices.amount = price.Value.amount;
                    spawnPrices.displayName = price.Value.displayName;

                    VLParams.SpawnPrices.Add(spawnPrices);
                }

                foreach (var price in vehicleType.Value.RecallPrices)
                {
                    if (vehicleType.Value.RecallPrices.Count == 0) break;

                    PriceInfo recallPrices = new();

                    recallPrices.currency = price.Key;
                    recallPrices.amount = price.Value.amount;
                    recallPrices.displayName = price.Value.displayName;

                    VLParams.RecallPrices.Add(recallPrices);
                }

                VehicleLicenceNames.vehicles.Add(VLParams);
            }

            SaveVLVehicles();
        }
        #endregion

        private void ReloadPlugins()
        {
            if (VehicleLicenceUI) Interface.Oxide.ReloadPlugin(VehicleLicenceUI.Name);
            if (VehicleLicenceAddons) Interface.Oxide.ReloadPlugin(VehicleLicenceAddons.Name);
            if (VehicleLicenceTransmitter) Interface.Oxide.ReloadPlugin(VehicleLicenceTransmitter.Name);
            if (VehicleSpawnerUI) Interface.Oxide.ReloadPlugin(VehicleSpawnerUI.Name);
        }
        #endregion

        #region Unload VL Commands
        private void UnloadVLCommands()
        {
            Puts($"Unloading commands in {VehicleLicence.Instance.Name}!");
            timer.Once(0.1f, () =>
            {
                foreach (var entry in VehicleLicence.Instance.allVehicleSettings)
                {
                    foreach (var command in entry.Value.Commands)
                    {
                        if (string.IsNullOrEmpty(command)) continue;

                        if (VehicleLicence.Instance.configData.chat.useUniversalCommand)
                        {
                            if (config.VLSettings.unloadUniversalVLCommands) cmd.RemoveChatCommand(command, VehicleLicence.Instance);
                        }

                        if (!string.IsNullOrEmpty(VehicleLicence.Instance.configData.chat.customKillCommandPrefix))
                        {
                            if (config.VLSettings.unloadKillVLCommands) cmd.RemoveChatCommand(VehicleLicence.Instance.configData.chat.customKillCommandPrefix + command, VehicleLicence.Instance);
                        }
                    }
                }
                
                if (config.VLSettings.unloadBuyVLCommands)
                    cmd.RemoveChatCommand(VehicleLicence.Instance.configData.chat.buyCommand, VehicleLicence.Instance);
                if (config.VLSettings.unloadSpawnVLCommands)
                    cmd.RemoveChatCommand(VehicleLicence.Instance.configData.chat.spawnCommand, VehicleLicence.Instance);
                if (config.VLSettings.unloadRecallVLCommands)
                    cmd.RemoveChatCommand(VehicleLicence.Instance.configData.chat.recallCommand, VehicleLicence.Instance);
                if (config.VLSettings.unloadKillVLCommands)
                    cmd.RemoveChatCommand(VehicleLicence.Instance.configData.chat.killCommand, VehicleLicence.Instance);
                if (config.VLSettings.unloadHelpVLCommands)
                    cmd.RemoveChatCommand(VehicleLicence.Instance.configData.chat.helpCommand, VehicleLicence.Instance);

                Puts($"Successfully unloaded commands in {VehicleLicence.Instance.Name}!");
            });
        }
        #endregion

        #region Method Helpers
        private string CleanVehicleName(string vName, bool lowerName = false)
        {
            vName = vName.Replace("assets/custom/", "").Replace(".prefab", "").Replace(".entity", "").Replace(".deployed", "").Replace("_", "").ToLower();
            return vName;
        }

        private void Message(IPlayer player, string msg)
        {
            bool isServer = player.IsServer;
            BasePlayer bPlayer = player.Object as BasePlayer;

            if (!isServer) Player.Message(bPlayer, msg);
            else Puts(msg);
        }
        #endregion

        #region Data File
        public VehicleLicenceData VehicleLicenceNames = new VehicleLicenceData();
        public VLChatData VLChatParams = new VLChatData();
        public CustomData CustomSettings = new CustomData();

        public class VehicleLicenceData
        {
            public List<VehicleLicenceVehicles> vehicles = new();
        }

        public class VLChatData
        {
            public VLChatSettings settings = new();
        }

        public class CustomData
        {
            public Dictionary<string, SkinHistory> skinIsCustom = new();
        }

        private void LoadCustomSettings()
        {
            try
            {
                CustomSettings = Interface.Oxide.DataFileSystem.ReadObject<CustomData>($"{Name}/CustomSettings");
            }
            catch (Exception)
            {
                CustomSettings = new CustomData();
            }
        }

        private void SaveVLVehicles()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/VehicleLicenceNames", VehicleLicenceNames);
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/CustomSettings", CustomSettings);
        }

        private void SaveVLChatSettings()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/VehicleLicenceChatSettings", VLChatParams);
        }
        #endregion

        #region Config
        public Configuration config;
        public class Configuration
        {
            [JsonProperty(PropertyName = "Vehicle Licence Settings")]
            public VLSettings VLSettings = new VLSettings
            {
                unloadVLCommands = false,
                unloadUniversalVLCommands = false,
                unloadBuyVLCommands = false,
                unloadSpawnVLCommands = false,
                unloadRecallVLCommands = false,
                unloadKillVLCommands = false,
                unloadHelpVLCommands = false
            };

            [JsonProperty(PropertyName = "General Settings")]
            public PluginSettings settings = new PluginSettings
            {
                autoUnload = false,
                autoReload = false
            };
 
            public Core.VersionNumber Version = new Core.VersionNumber(0, 0, 0);
        }

        protected override void LoadDefaultConfig() { config = new Configuration(); }
        protected override void LoadConfig()
        {
            try
            {
                base.LoadConfig();
                config = Config.ReadObject<Configuration>();

                if (config == null)
                {
                    LoadDefaultConfig();
                }

                if (config.Version != Version)
                {
                    UpdateConfig();
                }
            }
            catch
            {
                Puts($"\n////////////////////////\n\n\n// Configuration file {Name}.json is invalid!\n// Resetting the config now!\n\n\n////////////////////////");
                LoadDefaultConfig();
                SaveConfig();
            }
        }

        protected override void SaveConfig()
        {
            Puts($"Configuration changes saved to {Name}.json");
            Config.WriteObject(config, true);
        }

        private void UpdateConfig()
        {
            if (config.Version >= Version) return;

            if (config.Version < new VersionNumber(1, 0, 0))
            {
                LoadDefaultConfig();

                var configUpdateStr = "[CONFIG UPDATE] Updating to Version {0}";
                PrintWarning(string.Format(configUpdateStr, Version));
                config.Version = this.Version;

                SaveConfig();
            }
        }
        #endregion
        
        #region Hard Coded Data

        #region Vanilla Vehicles
        public readonly List<VanillaVehicleItems> vehicleItems = new List<VanillaVehicleItems>
        {
            // Medieval Vehicles
            new VanillaVehicleItems { VLName = "SiegeTower", PrefabName = "siegetower", SkinID = 3446373165 },
            new VanillaVehicleItems { VLName = "Catapult", PrefabName = "catapult", SkinID = 3446373078 },
            new VanillaVehicleItems { VLName = "Batteringram", PrefabName = "batteringram", SkinID = 3446372968 },
            new VanillaVehicleItems { VLName = "Ballista", PrefabName = "ballista", SkinID = 3446372639 },

            // Bikes
            new VanillaVehicleItems { VLName = "PedalTrike", PrefabName = "pedaltrike", SkinID = 3284205351 },
            new VanillaVehicleItems { VLName = "PedalBike", PrefabName = "pedalbike", SkinID = 3284205070 },
            new VanillaVehicleItems { VLName = "MotorBike_SideCar", PrefabName = "motorbikesidecar", SkinID = 3284204759 },
            new VanillaVehicleItems { VLName = "MotorBike", PrefabName = "motorbike", SkinID = 3284204457 },

            // Helicopters
            new VanillaVehicleItems { VLName = "AttackHelicopter", PrefabName = "attackhelicopter", SkinID = 3284204081 },
            new VanillaVehicleItems { VLName = "MiniCopter", PrefabName = "minicopter", SkinID = 2906148311 },
            new VanillaVehicleItems { VLName = "Chinook", PrefabName = "ch47", SkinID = 2783365479 },
            new VanillaVehicleItems { VLName = "TransportHelicopter", PrefabName = "scraptransporthelicopter", SkinID = 2783365006 },

            // Hot Air Ballons
            new VanillaVehicleItems { VLName = "HotAirBalloon", PrefabName = "hotairballoon", SkinID = 2783364912 },
            new VanillaVehicleItems { VLName = "ArmoredHotAirBalloon", PrefabName = "hotairballoonarmort1", SkinID = 3456457037 }, //Find new skinID //

            // SnowMobiles
            new VanillaVehicleItems { VLName = "TomahaSnowmobile", PrefabName = "tomahasnowmobile", SkinID = 3000416835 },
            new VanillaVehicleItems { VLName = "Snowmobile", PrefabName = "snowmobile", SkinID = 2783366199 },

            // Water Vehicles
            new VanillaVehicleItems { VLName = "SubmarineSolo", PrefabName = "submarinesolo", SkinID = 2783365665 },
            new VanillaVehicleItems { VLName = "SubmarineDuo", PrefabName = "submarineduo", SkinID = 2783365593 },
            new VanillaVehicleItems { VLName = "Tugboat", PrefabName = "tugboat", SkinID = 3000418301 },
            new VanillaVehicleItems { VLName = "RHIB", PrefabName = "rhib", SkinID = 2783365542 },
            new VanillaVehicleItems { VLName = "Rowboat", PrefabName = "rowboat", SkinID = 2783365250 },
            new VanillaVehicleItems { VLName = "Dpv", PrefabName = "dpv", SkinID = 3456456892 }, // Find new skinID //
            new VanillaVehicleItems { VLName = "Kayak", PrefabName = "kayak", SkinID = 3456457172 }, // Find new skinID //

            // Animals
            new VanillaVehicleItems { VLName = "RidableHorse", PrefabName = "ridablehorse2", SkinID = 2783365408 },

            // Cars
            new VanillaVehicleItems { VLName = "Sedan", PrefabName = "sedantest", SkinID = 2783365060 },
            new VanillaVehicleItems { VLName = "SmallCar", PrefabName = "carchassis2module", SkinID = 2783364084 },
            new VanillaVehicleItems { VLName = "MediumCar", PrefabName = "carchassis3module", SkinID = 2783364761 },
            new VanillaVehicleItems { VLName = "LargeCar", PrefabName = "carchassis4module", SkinID = 2783364660 },
            new VanillaVehicleItems { VLName = "MagnetCrane", PrefabName = "magnetcrane", SkinID = 3456457332 }, // Find new skinID //

            // Trains
            new VanillaVehicleItems { VLName = "SedanRail", PrefabName = "sedanrail", SkinID = 3456462924 }, // Find new skinID //
            new VanillaVehicleItems { VLName = "WorkCart", PrefabName = "workcart", SkinID = 3456460252 }, // Find new skinID //
            new VanillaVehicleItems { VLName = "WorkCartAboveGround", PrefabName = "workcartaboveground2", SkinID = 3456459616 }, // Find new skinID //
            new VanillaVehicleItems { VLName = "WorkCartCovered", PrefabName = "workcartcovered", SkinID = 3456460086 }, // Find new skinID //
            new VanillaVehicleItems { VLName = "CompleteTrain", PrefabName = "workcartaboveground", SkinID = 3456465174 }, // Find new skinID //
            new VanillaVehicleItems { VLName = "Locomotive", PrefabName = "locomotive", SkinID = 3456459351 }, // Find new skinID //
        };
        #endregion

        #region Removed Vehicles
        public readonly List<RemovedVehicles> removedVehicles = new List<RemovedVehicles>
        {
            new RemovedVehicles { vehicleName = "Jet", vehicleID = 3465683132 },
            new RemovedVehicles { vehicleName = "MarsFighterDetailed", vehicleID = 3465740616 },
            new RemovedVehicles { vehicleName = "OldFighter", vehicleID = 3465740246 },
            new RemovedVehicles { vehicleName = "RustWingDetailed", vehicleID = 3465741000 },
            new RemovedVehicles { vehicleName = "RustWingDetailedOld", vehicleID = 3465741411 },
            new RemovedVehicles { vehicleName = "TinFighterDetailed", vehicleID = 3465742128 },
            new RemovedVehicles { vehicleName = "TinFighterDetailedOld", vehicleID = 3465742429 },
        };
        #endregion

        #endregion

        public class VLSettings
        {
            [JsonProperty(PropertyName = "Enable Un-Registering of Vehicle Licence commands? [MUST BE ENABLED TO UN-REGISTER COMMANDS BELOW]")]
            public bool unloadVLCommands;

            [JsonProperty(PropertyName = "Un-Register Universal Vehicle Licence chat commands on plugin load?")]
            public bool unloadUniversalVLCommands;

            [JsonProperty(PropertyName = "Un-Register Buy Vehicle Licence chat commands on plugin load?")]
            public bool unloadBuyVLCommands;

            [JsonProperty(PropertyName = "Un-Register Spawn Vehicle Licence chat command on plugin load?")]
            public bool unloadSpawnVLCommands;

            [JsonProperty(PropertyName = "Un-Register Recall Vehicle Licence chat command on plugin load?")]
            public bool unloadRecallVLCommands;

            [JsonProperty(PropertyName = "Un-Register Kill Vehicle Licence chat command on plugin load?")]
            public bool unloadKillVLCommands;

            [JsonProperty(PropertyName = "Un-Register Help Vehicle Licence chat command on plugin load?")]
            public bool unloadHelpVLCommands;
        }

        public class PluginSettings
        {
            [JsonProperty(PropertyName = "Auto unload plugin after updating data?")]
            public bool autoUnload;

            [JsonProperty(PropertyName = "Auto reload associated plugins after updating data?")]
            public bool autoReload;
        }

        public class KaruzaVehicleItems
        {
            [JsonProperty(PropertyName = "PrefabPath")]
            public string VLName { get; set; } = string.Empty;
            public ulong SkinID { get; set; } = 0;
        }

        public class SkinHistory
        {
            public ulong originalID { get; set; }
            public ulong newID = 0;
        }

        public class VanillaVehicleItems
        {
            public string VLName { get; set; }
            public string PrefabName { get; set; }
            public ulong SkinID { get; set; }
        }

        public class KaruzaConfiguration
        {
            public bool SubscribeToOnEntityBuilt = true;
            public List<KaruzaVehicleItems> VehicleItems = new List<KaruzaVehicleItems>();
        }

        public class VLChatSettings
        {   
            public string spawnCmd { get; set; }
            public string recallCmd { get; set; }
            public string killCmd { get; set; }

            public string prefix { get; set; } = string.Empty;
            public ulong steamIDIcon { get; set; } = 0;
        }

        public class PriceInfo
        {
            public string currency;
            public int amount;
            public string displayName;
        }

        public class VehicleLicenceVehicles
        {
            public string VLName { get; set; }
            public string VLDisplayName { get; set; }
            public bool IsWaterVehicle { get; set; }
            public bool IsCustomVehicle { get; set; }
            public bool IsPurchasable { get; set; }
            public string PrefabName { get; set; }
            public string FuckOffFacepunch { get; set; } // Fuck Facepunch
            public ulong SkinID { get; set; }
            public string Permission { get; set; }
            public string BypassCostPermission { get; set; }
            public List<PriceInfo> PurchasePrices { get; set; } = new();
            public List<PriceInfo> SpawnPrices { get; set; } = new();
            public List<PriceInfo> RecallPrices { get; set; } = new();
        }

        public class RemovedVehicles
        {
            public string vehicleName { get; set; }
            public ulong vehicleID { get; set; }
        }
    }
}