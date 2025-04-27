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
        private readonly Plugin VehicleLicenceUI, /*VehicleLicenceAddons,*/ VehicleLicenceTransmitter, VehicleSpawnerUI;

        public SnaplatacksVLData Instance;
        private Timer shutdownTimer;
        private const string cmd = "snapvldata";
        private const string errorMsg = "SYNTAX ERROR\n/snapvldata < reset >\n/snapvldata < set > < vehicleName > < displayname | skin >";

        #region Data Setup
        private void Init()
        {
            Instance = this;

            AddCovalenceCommand(cmd, nameof(UpdateDataVariable));

            LoadData();

            UpdateData();
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
            if (player == null && !player.IsServer || !player.IsAdmin) return;

            if (args.Length < 1)
            {
                Message(player, errorMsg);
                return;
            }

            string argType = args[0].ToLower();

            if (args.Length == 1 && argType == "reset")
            {
                if (UpdateData()) Message(player, "Successfully reset all data!");
                return;
            }

            if (args.Length < 3 || argType != "set")
            {
                Message(player, errorMsg);
                return;
            }

            string dataTypePassed = args[1].ToLower();

            string vehicleName = args[2].ToLower();

            string newName = args[3];

            ulong.TryParse(newName, out var newID);

            VehicleLicenceVehicles DataEntry = VehicleLicenceNames.vehicles.Find(x => x.VLName.ToLower() == vehicleName);

            switch (dataTypePassed)
            {
                case ("displayname"):
                {  
                    DataEntry.VLDisplayName = newName;
                    dataTypePassed = dataTypePassed.Insert(7, " ");
                    break;
                }
                case ("skin"):
                {
                    DataEntry.SkinID = newID;
                    newName = newID.ToString();
                    break;
                }
                default:
                    Message(player, errorMsg);
                    return;
            }

            Message(player, $"Updated the {dataTypePassed} for the {DataEntry.VLName} with {newName}!");

            SaveVLVehicles();
        }
        #endregion

        #region Methods
        private bool UpdateData()
        {
            try
            {
                int i = 2;
                string[] carBS = new string[] {"SmallCar", "MediumCar", "LargeCar"};
                var karuzaVehicleConfig = Config.ReadObject<KaruzaConfiguration>($"{Interface.Oxide.ConfigDirectory}/KaruzaVehicleItemManager.json");
                var karuzaVehicleParams = karuzaVehicleConfig.VehicleItems.Count != 0 ? karuzaVehicleConfig.VehicleItems : new();

                if (karuzaVehicleParams.Count == 0) Puts($"Loaded '?' for all custom vehicle icons because KaruzaVehicleItemManager is missing!");

                foreach (var vehicleType in VehicleLicence.Instance.allVehicleSettings)
                {
                    VehicleLicenceVehicles VLParams = new();

                    if (VehicleLicenceNames.vehicles.Exists(x => x.VLName == vehicleType.Key) && !config.settings.resetData) continue;

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

                            VLParams.SkinID = karuzaVehicleParams.Find(x => CleanVehicleName(x.VLName) == CleanVehicleName(vehicleType.Key)).SkinID;
                        }
                        else
                        {
                            VLParams.PrefabName = vehicleType.Key.ToLower();
                            VLParams.SkinID = removedVehicles.Find(x => x.vehicleName == vehicleType.Key).vehicleID; // this ID is a '?' png
                        }
                    }
                    else
                    {
                        if (carBS.Contains(vehicleType.Key))
                        {
                            VLParams.FuckOffFacepunch = $"{i}modulecarspawned";
                            i++;
                        }

                        VLParams.SkinID = vehicleItems.Find(value => value.VLName == vehicleType.Key).SkinID;
                        VLParams.PrefabName = vehicleItems.Find(value => value.VLName == vehicleType.Key).PrefabName;
                    }

                    VLParams.VLDisplayName = vehicleType.Value.DisplayName;
                    VLParams.VLName = vehicleType.Key;

                    VLParams.IsWaterVehicle = vehicleType.Value.IsWaterVehicle;
                    VLParams.IsPurchasable = vehicleType.Value.Purchasable;
                    VLParams.IsCustomVehicle = vehicleType.Value.CustomVehicle;

                    VehicleLicenceNames.vehicles.Add(VLParams);
                }
            
                SaveVLVehicles();
                return true;
            }
            catch (Exception e)
            {
                Puts(e.Message);
                return false;
            }
        }

        private void ReloadPlugins()
        {
            if (VehicleLicenceUI) Interface.Oxide.ReloadPlugin(VehicleLicenceUI.Name);
            //if (VehicleLicenceAddons) Interface.Oxide.ReloadPlugin(VehicleLicenceAddons.Name);
            if (VehicleLicenceTransmitter) Interface.Oxide.ReloadPlugin(VehicleLicenceTransmitter.Name);
            if (VehicleSpawnerUI) Interface.Oxide.ReloadPlugin(VehicleSpawnerUI.Name);
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

        public class VehicleLicenceData
        {
            public List<VehicleLicenceVehicles> vehicles = new();
        }

        private void LoadData()
        {
            try
            {
                VehicleLicenceNames = Interface.Oxide.DataFileSystem.ReadObject<VehicleLicenceData>($"{Name}/VehicleLicenceNames");
            }

            catch (Exception)
            {
                VehicleLicenceNames = new VehicleLicenceData();
            }
        }

        private void SaveVLVehicles()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/VehicleLicenceNames", VehicleLicenceNames);
        }
        #endregion

        #region Config
        public Configuration config;
        public class Configuration
        {
            [JsonProperty(PropertyName = "General Settings")]
            public PluginSettings settings = new PluginSettings
            {
                autoUnload = true,
                autoReload = false,
                resetData = false
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

        public class PluginSettings
        {
            [JsonProperty(PropertyName = "Auto unload plugin after updating data?")]
            public bool autoUnload;

            [JsonProperty(PropertyName = "Auto reload associated plugins after updating data?")]
            public bool autoReload;

            [JsonProperty(PropertyName = "Reset data file on every reload? [true = reset on load]")]
            public bool resetData;
        }

        public class CustomVehicleItems
        {
            [JsonProperty(PropertyName = "PrefabPath")]
            public string VLName { get; set; }
            public ulong SkinID { get; set; }
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
            public List<CustomVehicleItems> VehicleItems = new List<CustomVehicleItems>();
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
        }

        public class RemovedVehicles
        {
            public string vehicleName { get; set; }
            public ulong vehicleID { get; set; }
        }
    }
}