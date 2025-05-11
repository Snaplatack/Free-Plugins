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
using System.Collections.Specialized;

namespace Oxide.Plugins
{
    [Info("Vehicle Licence Transmitter", "Snaplatack", "1.0.2")]
    [Description("Spawn Vehicles through Vehicle Licence with a RF Transmitter")]
    public class VehicleLicenceTransmitter : RustPlugin
    {
        #region Variables
        [PluginReference]
        private readonly Plugin VehicleLicence, VehicleLicenceUI, Toastify, SnaplatacksVLData;
        public static VehicleLicenceTransmitter Instance;
        private VLChatSettings VLChat = new();
        private List<VehicleLicenceVehicles> VData = new();
        private static int layers = LayerMask.GetMask(
                    LayerMask.LayerToName((int)Rust.Layer.World),
                    LayerMask.LayerToName((int)Rust.Layer.Water),
                    LayerMask.LayerToName((int)Rust.Layer.Construction),
                    LayerMask.LayerToName((int)Rust.Layer.Default),
                    LayerMask.LayerToName((int)Rust.Layer.Vehicle_Detailed),
                    LayerMask.LayerToName((int)Rust.Layer.Terrain),
                    LayerMask.LayerToName((int)Rust.Layer.Physics_Projectile),
                    LayerMask.LayerToName((int)Rust.Layer.AI));
        #endregion

        #region Init & Unload
        private void OnServerInitialized()
        {
            Instance = this;

            foreach (var cmd in config.command.TransmitterCmd)
            {
                if (string.IsNullOrWhiteSpace(cmd)) continue;
                AddCovalenceCommand(cmd, nameof(GiveTransmitterCmd));
            }

            foreach (var cmd in config.command.AllItemsCmd)
            {
                if (string.IsNullOrWhiteSpace(cmd)) continue;
                AddCovalenceCommand(cmd, nameof(GiveAllVehicleItemsCmd));
            }

            if (config.Settings.givePlaceableItem)
            {
                Subscribe(nameof(OnEntityBuilt));
                Subscribe(nameof(CanBuild));
            }

            if (config.Settings.syncFrequencyWhenMount) Subscribe(nameof(OnEntityMounted));

            if (config.Settings.updateTranmitterOnSpawned) Subscribe(nameof(OnEntitySpawned));

            permission.RegisterPermission(config.Perms.UseTransmitterPerm, this);
            permission.RegisterPermission(config.Perms.Admin, this);

            var snapsVehicles = Config.ReadObject<SnaplatacksVLConfiguration>($"{Interface.Oxide.DataDirectory}/SnaplatacksVLData/VehicleLicenceNames.json");
            var snapsVehicleData = snapsVehicles.vehicles.Count != 0 ? snapsVehicles.vehicles : new();

            foreach (var vehicleType in snapsVehicles.vehicles)
            {
                VehicleLicenceVehicles VLParams = new();

                VLParams.VLName = vehicleType.VLName;
                VLParams.PrefabName = vehicleType.PrefabName;
                VLParams.SkinID = vehicleType.SkinID;
                VLParams.VLDisplayName = vehicleType.VLDisplayName;
                VLParams.IsPurchasable = vehicleType.IsPurchasable;
                VLParams.IsWaterVehicle = vehicleType.IsWaterVehicle;
                VLParams.FuckOffFacepunch = vehicleType.FuckOffFacepunch;

                VData.Add(VLParams);
            }

            var snapsSettings = Config.ReadObject<SnaplatacksVLSettings>($"{Interface.Oxide.DataDirectory}/SnaplatacksVLData/VehicleLicenceChatSettings.json");
            var snapsChatSettings = snapsSettings.settings;

            VLChatSettings chatSettings = new();

            chatSettings.spawnCmd = snapsChatSettings.spawnCmd;
            chatSettings.recallCmd = snapsChatSettings.recallCmd;
            chatSettings.killCmd = snapsChatSettings.killCmd;

            chatSettings.prefix = snapsChatSettings.prefix;
            chatSettings.steamIDIcon = snapsChatSettings.steamIDIcon;

            VLChat = chatSettings;

            foreach (var detonator in BaseNetworkable.serverEntities.OfType<Detonator>())
            {
                if (detonator == null) continue;

                BaseEntity transmitter = (BaseEntity)detonator;
                
                if (transmitter == null) continue;

                if (transmitter.skinID != config.Settings.transmitterSkinID && transmitter._name != config.Settings.transmitterName) continue;
                if (transmitter.transform.position == new Vector3(0, 0, 0)) continue;
                if (TransmitterTracker.Transmitters.ContainsKey(transmitter.net.ID.Value)) continue;

                if (transmitter is DroppedItem transmitterDropped)
                {
                    transmitterDropped.item.GetHeldEntity().gameObject.AddComponent<TransmitterTracker>();
                    continue;
                }

                transmitter.gameObject.AddComponent<TransmitterTracker>();
            }
        }
        	
        private void Init()
        {
            Unsubscribe(nameof(OnEntityMounted));
            Unsubscribe(nameof(OnEntityBuilt));
            Unsubscribe(nameof(CanBuild));
            Unsubscribe(nameof(OnEntitySpawned));
        }

        private void Loaded()
        {
            if (!ReferenceEquals(VehicleLicence, null)) return;

            timer.Once(0.1f, () =>
            {
                Interface.Oxide.UnloadPlugin(Name);
            });
        }

        private void Unload()
        {
            foreach (var transmitterTracker in TransmitterTracker.Transmitters.Values)
            {
                if (transmitterTracker == null) continue;
                GameObject.Destroy(transmitterTracker);
            }

            TransmitterTracker.Transmitters.Clear();
            TransmitterTracker.Transmitters = null;
        }
        #endregion

        #region Hooks
        private void OnLicensedVehiclePurchased(BasePlayer player, string vehicleName, bool response = true)
        {
            if (!config.Settings.giveOnPurchased) return;
            GiveTransmitter(player);
        }

        private void OnEntitySpawned(Detonator detonator)
        {
            if (detonator == null) return;
            
            BaseEntity transmitter = (BaseEntity)detonator;
            
            if (transmitter.skinID != config.Settings.transmitterSkinID && transmitter._name != config.Settings.transmitterName) return;
            if (transmitter.transform.position == new Vector3(0, 0, 0)) return;
            if (TransmitterTracker.Transmitters.ContainsKey(transmitter.net.ID.Value)) return;

            if (transmitter is DroppedItem transmitterDropped)
            {
                transmitterDropped.item.GetHeldEntity().gameObject.AddComponent<TransmitterTracker>();
                return;
            }

            transmitter.gameObject.AddComponent<TransmitterTracker>();
        }

        private void OnEntityMounted(BaseMountable mountable, BasePlayer player)
        {
            if (player == null) return;

            var entity = mountable.VehicleParent();
            if (entity == null) return;
            if (entity is BaseArcadeMachine) return;
            if (!entity.IsDriver(player)) return;

            string vName = ConvertVehicleName(entity.ShortPrefabName);

            Detonator transmitter = null;

            List<string> vehicleLicenses = (List<string>)VehicleLicence.Call("GetVehicleLicenses", player.userID.Get());

            if (!config.Settings.sortLicenses && VehicleLicenceUI)
                vehicleLicenses = vehicleLicenses.OrderBy(x => x).ToList();          

            List<Item> playerInv = GetPlayerInv(player);

            for (int i = 0; i < playerInv.Count; i++)
            {
                if (playerInv[i].name == null) continue;
                if (playerInv[i].name == config.Settings.transmitterName || playerInv[i].skin == config.Settings.transmitterSkinID)
                {
                    transmitter = (playerInv[i].GetHeldEntity() as Detonator);
                }
            }

            if (transmitter == null) return;

            for (int i = 0; i < vehicleLicenses.Count; i++)
            {
                if (vehicleLicenses[i] == ConvertVehicleName(vName, false, true)) // converting from prefab name to VLName UpperCased
                {
                    UpdateFrequency(transmitter, i + 2);
                    return;
                }
            }
        }

        private void OnEntityBuilt(Planner plan, GameObject go) // replaces karuzas function for VL commands
        {
            string vName = string.Empty;

            if (plan == null) return;

            var item = plan.GetItem();

            if (object.ReferenceEquals(item, null)) return;

            if (item.skin <= 0) return;

            if (item.name == null) return;

            if (ConvertVehicleName(item.name) != item.name) vName = ConvertVehicleName(item.name); // converting from lowered VLName name to VLName UpperCased

            if (!VData.Exists(x => x.VLName.ToLower() == item.name)) return;

            go.ToBaseEntity().Kill();
        }

        private object CanBuild(Planner planner, Construction prefab, Construction.Target target)
        {
            var item = planner.GetItem();

            if (item.skin <= 0) return null;

            if (item.name == null) return null;

            var vName = ConvertVehicleName(item.name, false, true); // converting from lowered VLName name to VLName UpperCased

            var player = item.GetOwnerPlayer();

            bool isSpawned = (bool)VehicleLicence.Call("SpawnLicensedVehicle", player, vName, VLChat.spawnCmd, true);

            if (!isSpawned) return false;

            item.Remove();

            return null;
        }
        #endregion

        #region Commands & Transmitter
        private void GiveTransmitterCmd(IPlayer iPlayer, string command, string[] args)
        {
            var IsServer = iPlayer.IsServer;
            BasePlayer player = iPlayer.Object as BasePlayer;

            if (iPlayer == null || player == null && !IsServer) return;

            if (args.Length > 0 || IsServer)
            {
                foreach (var bplayer in BasePlayer.allPlayerList)
                {
                    if (bplayer.displayName.ToLower().Contains(args[0].ToLower()) || args[0] == bplayer.UserIDString)
                    {
                        player = bplayer;
                        break;
                    }
                }
            }

            if (!permission.UserHasPermission(player.UserIDString, config.Perms.UseTransmitterPerm) && !IsServer) return;

            GiveTransmitter(player);
        }

        private void GiveAllVehicleItemsCmd(IPlayer iPlayer, string command, string[] args)
        {
            var IsServer = iPlayer.IsServer;
            if ((iPlayer == null && !IsServer) || !iPlayer.HasPermission(config.Perms.Admin)) return;
			
            if (args.Length > 0 || IsServer)
            {
                foreach (var iPlayerFound in covalence.Players.All)
                {
                    if (iPlayerFound.Name.ToLower().Contains(args[0].ToLower()) || args[0] == iPlayerFound.Id)
                    {
                        iPlayer = iPlayerFound;
                        break;
                    }
                }
            }

            var bPlayer = iPlayer.Object as BasePlayer;

            if (bPlayer == null) return;

            var pPos1 = bPlayer.transform.position + new Vector3(3, 0, 0);
            var pPos2 = bPlayer.transform.position + new Vector3(3, 1, 0);
            var pPos3 = bPlayer.transform.position + new Vector3(3, 2, 0);
            var pPos4 = bPlayer.transform.position + new Vector3(3, 3, 0);
            var Box1 = GameManager.server.CreateEntity("assets/prefabs/deployable/large wood storage/box.wooden.large.prefab", pPos1, new Quaternion()) as StorageContainer;
            var Box2 = GameManager.server.CreateEntity("assets/prefabs/deployable/large wood storage/box.wooden.large.prefab", pPos2, new Quaternion()) as StorageContainer;
            var Box3 = GameManager.server.CreateEntity("assets/prefabs/deployable/large wood storage/box.wooden.large.prefab", pPos3, new Quaternion()) as StorageContainer;
            var Box4 = GameManager.server.CreateEntity("assets/prefabs/deployable/large wood storage/box.wooden.large.prefab", pPos4, new Quaternion()) as StorageContainer;
            Box1.Spawn();
            Box2.Spawn();
            Box3.Spawn();
            Box4.Spawn();

            Box1.skinID = 2891003619;
            Box2.skinID = 2891003619;
            Box3.skinID = 2891003619;
            Box4.skinID = 2891003619;

            foreach (var entry in VData)
            {
                var item = GiveVehicleItem(iPlayer.Object as BasePlayer, entry.VLName);

                if (!Box1.inventory.IsFull())
                {
                    item.MoveToContainer(Box1.inventory);
                    continue;
                }
  
                if (!Box2.inventory.IsFull())
                {
                    item.MoveToContainer(Box2.inventory);
                    continue;
                }

                if (!Box3.inventory.IsFull())
                {
                    item.MoveToContainer(Box3.inventory);
                    continue;
                }

                if (!Box4.inventory.IsFull())
                    item.MoveToContainer(Box4.inventory);
            }
        }

        private void GiveTransmitter(BasePlayer player, BaseEntity entity = null)
        {
            Item transmitter = ItemManager.CreateByName("rf.detonator", 1, config.Settings.transmitterSkinID);

            if (config.Settings.transmitterName != string.Empty) transmitter.name = config.Settings.transmitterName;

            List<Item> playerInv = GetPlayerInv(player);

            for (int i = 0; i < playerInv.Count; i++)
            {
                if (playerInv[i].name == null) continue;
                if (playerInv[i].name == transmitter.name || playerInv[i].skin == transmitter.skin) return;
            }

            player.inventory.GiveItem(transmitter);
            transmitter.GetHeldEntity().gameObject.AddComponent<TransmitterTracker>();
        }
        #endregion

        #region Method Helpers

        private string CleanVehicleName(string vName, bool lowerName = false)
        {
            vName = vName.Replace("assets/custom/", "").Replace(".prefab", "").Replace(".entity", "").Replace(".deployed", "").Replace("_", "");
            if (lowerName) vName = vName.ToLower();
            return vName;
        }

        private string ConvertVehicleName(string vName, bool lowerName = false, bool VLName = false, bool VLDisplayName = false)
        {
            var allVehicleData = VData;
            
            var vNameEdited = vName.Replace("assets/custom/", "").Replace(".prefab", "").Replace(".entity", "").Replace(".deployed", "").Replace("_", "").ToLower();

            var vData = allVehicleData?.Find(x => x.PrefabName.ToLower() == vNameEdited || x.VLName.ToLower() == vNameEdited);

            if (vData == null) // FUCK YOU FP FOR MAKING ME SUFFER
            {
                vData = allVehicleData.Find(x => x.FuckOffFacepunch == vNameEdited);
            }

            vName = VLDisplayName == false ? (VLName ? vData.VLName : vData.PrefabName) : vData.VLDisplayName;
            
            if (lowerName) vName = vName.ToLower();
            
            return vName;
        }

        private void SendMessage(IPlayer iPlayer, string langmsg, string vehicle, bool isPositive)
        {
            var IsServer = iPlayer.IsServer;
            var player = iPlayer.Object as BasePlayer;
            if (player == null && !IsServer) return;

            var msg = lang.GetMessage(langmsg, this, player.UserIDString);

            if (config.toastifySettings.ToastPosMsgID != string.Empty || config.toastifySettings.ToastNegMsgID != string.Empty && Toastify != null)
            {
                string toastId = config.toastifySettings.ToastPosMsgID;
                if (!isPositive) toastId = config.toastifySettings.ToastNegMsgID;

                Toastify?.CallHook("SendToast", player, toastId, null, string.Format(msg, vehicle), config.toastifySettings.ToastDur);
            }

            msg = String.Format(msg, vehicle);

            if (VLChat.prefix == string.Empty)
            {
                Player.Message(player, String.Format(msg, vehicle), VLChat.steamIDIcon);
                return;
            }

            Player.Message(player, $"{VLChat.prefix} " + String.Format(msg, vehicle), VLChat.steamIDIcon);
        }

        private Item GiveVehicleItem(BasePlayer player, string vName)
        {
            Item item = null;
            string ITEM_NAME;
            
            var vehicleParams = VData;

            vName = vehicleParams.Find(x => x.PrefabName == CleanVehicleName(vName, true) || CleanVehicleName(x.VLName, true) == CleanVehicleName(vName, true)).VLName;

            ITEM_NAME = (vehicleParams.Find(x => x.PrefabName == vName || x.VLName == vName).IsWaterVehicle) ? "innertube" : "drone";

            item = vehicleParams != null
                ? ItemManager.CreateByPartialName(ITEM_NAME, 1, vehicleParams.Find(x => x.PrefabName == vName || x.VLName == vName).SkinID)
                : ItemManager.CreateByPartialName(ITEM_NAME, 1, 3456950821);

            item.name = CleanVehicleName(vName, true);
            item.text = vName;

            if (!player.inventory.containerMain.IsFull())
            {
                player.GiveItem(item);
            }
            
            return item;
        }

        private void RemoveVehicleItem(BasePlayer player, string vName)
        {
            List<Item> playerInv = GetPlayerInv(player);
            Item item = null;

            for (int i = 0; i < playerInv.Count; i++)
            {
                item = playerInv[i];
                if (item.name == null) continue;
                if (item.name == ConvertVehicleName(vName, true))
                {
                    ItemManager.RemoveItem(item, 0f);
                    return;
                }
            }
        }
        
        private List<Item> GetPlayerInv(BasePlayer player)
        {
            List<Item> playerInv = new();
            List<Item> playerBP = new();
            player.inventory.GetAllItems(playerInv);
            player.inventory.AddBackpackContentsToList(playerBP);
            playerInv.AddRange(playerBP);
            return playerInv;
        }

        private void UpdateFrequency(Detonator transmitter, int frequency)
        {
            Item transmitterItem = transmitter.GetItem();

            if (transmitter != null && transmitterItem.skin != null)
            {
                RFManager.RemoveBroadcaster(transmitter.GetFrequency(), transmitter);

                transmitter.frequency = frequency;
                
                RFManager.AddBroadcaster(frequency, transmitter);

                transmitter.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);

                var item = transmitterItem;
                if (item != null)
                {
                    if (item.instanceData == null) item.instanceData = new ProtoBuf.Item.InstanceData() { ShouldPool = false };

                    item.instanceData.dataInt = frequency;
                    item.MarkDirty();
                }  
            }
        }
        #endregion

        #region MonoBehaviour
        public class TransmitterTracker : MonoBehaviour // Huge thanks to Karuza for helping here <3
        {
            public Detonator transmitter { get; private set; }
            public static Dictionary<ulong, TransmitterTracker> Transmitters = new Dictionary<ulong, TransmitterTracker>();

            private void Awake()
            {
                transmitter = GetComponent<Detonator>();
                Transmitters.Add(transmitter.net.ID.Value, this);
            }

            bool isPressed;
            bool isKill;
            float timeToKill;
            int frequency;
            string vName;
            BaseVehicle vehicle;
            Oxide.Plugins.VehicleLicenceTransmitter VLT = VehicleLicenceTransmitter.Instance;

            private void LateUpdate()
            {
                if (!transmitter.IsOn())
                {
                    isPressed = false;
                    isKill = false;

                    return;
                }

                if (isKill)
                {
                    return;
                }

                if (VLT.config.Settings.repairTransmitter) transmitter.GetItem().condition = 100f;

                BasePlayer player = transmitter.GetOwnerPlayer();
                if (!isPressed)
                {
                    isPressed = true;
                    timeToKill = Time.time + VLT.config.Settings.amountOfTime;
                    frequency = transmitter.frequency;

                    List<string> vehicleLicenses = (List<string>)VLT.VehicleLicence?.Call("GetVehicleLicenses", player.userID.Get());

                    if (!VLT.permission.UserHasPermission(player.UserIDString, VLT.config.Perms.UseTransmitterPerm))
                        return;

                    if (VLT.config.Settings.sortLicenses || ReferenceEquals(VLT.VehicleLicenceUI, null))
                        vehicleLicenses = vehicleLicenses.OrderBy(x => x).ToList();

                    if (!ReferenceEquals(VLT.VehicleLicenceUI, null))
                        vehicleLicenses = (List<string>)VLT.VehicleLicenceUI.Call("UpdateVehicleFilter", player, vehicleLicenses);

                    if (vehicleLicenses.Count == 0 && ReferenceEquals(VLT.VehicleLicenceUI, null))
                    {
                        VLT.SendMessage(player.IPlayer, "NeedVehicleToSpawn", string.Empty, false);
                        return;
                    }   

                    if (frequency <= 1) 
                    {
                        if (VLT.VehicleLicenceUI) // UI plugin
                        {
                            player.SendConsoleCommand("vehiclelicenceui.open");
                            return;
                        }

                        string[] licenses = new string[vehicleLicenses.Count];

                        for (int i = 0; i < vehicleLicenses.Count; i++)
                        {
                            string numberText = $"{i + 2}";
                            string licenseText = vehicleLicenses[i];

                            if (VLT.VData.Exists(x => x.VLName == licenseText))
                                licenseText = VLT.VData.Find(x => x.VLName == licenseText).VLDisplayName;

                            string numberFormatted = !string.IsNullOrWhiteSpace(VLT.config.Settings.numberListColor)
                                ? $"<color={VLT.config.Settings.numberListColor}>{numberText}</color>"
                                : numberText;

                            string licenseFormatted = !string.IsNullOrWhiteSpace(VLT.config.Settings.vehicleListColor)
                                ? $"<color={VLT.config.Settings.vehicleListColor}>{licenseText}</color>"
                                : licenseText;

                            licenses[i] = $"{numberFormatted}. {licenseFormatted}";
                        }

                        var licenseList = String.Join("\n", licenses);

                        VLT.SendMessage(player.IPlayer, "CurrentVehiclesList", licenseList, true);
                        return;
                    }

                    #region Raycast
                    if (Physics.Raycast(player.eyes.HeadRay(), out RaycastHit hit, VLT.config.Settings.distanceToDetect, layers))
                    {
                        if (hit.rigidbody != null)
                        {
                            vehicle = hit.GetEntity()?.GetParentEntity() as BaseVehicle;
                            
                            if (vehicle == null) vehicle = hit.GetEntity() as BaseVehicle;

                            if (vehicle == null) return;

                            int position = 0;

                            for (int i = 0; i < vehicleLicenses.Count; i++)
                            {
                                if (VLT.ConvertVehicleName(vehicle.ShortPrefabName, true, true) == vehicleLicenses[i].ToLower())
                                {
                                    position = i + 2;
                                    vName = vehicleLicenses[i];
                                    break;
                                }

                                if (i == vehicleLicenses.Count - 1) vName = String.Empty;
                            }

                            if (!string.IsNullOrEmpty(vName))
                            {
                                VLT.SendMessage(player.IPlayer, "CurrentRecallVehicle", VLT.ConvertVehicleName(vName, false, false, true), true);
                                VLT.UpdateFrequency(transmitter, frequency);
                                return;
                            }
                            
                            vName = VLT.ConvertVehicleName(vehicle.ShortPrefabName, false, false, true); // Trying to get PrefabName
                            VLT.UpdateFrequency(transmitter, 0);
                            VLT.SendMessage(player.IPlayer, "UnownedVehicle", vName, true);
                        }
                        else
                        {
                            for (int i = 0; i < vehicleLicenses.Count; i++)
                            {
                                if (i + 2 == frequency)
                                {
                                    vName = vehicleLicenses[i];
                                    break;
                                }
                            }

                            vehicle = (BaseVehicle)VLT.VehicleLicence?.Call("GetLicensedVehicle", player.userID.Get(), vName);

                            if (vehicle == null)
                            {
                                VLT.VehicleLicence.Call("SpawnLicensedVehicle", player, vName, VLT.VLChat.spawnCmd);

                                if (VLT.config.Settings.givePlaceableItem) VLT.RemoveVehicleItem(player, vName);
                            }
                            else
                            {
                                VLT.VehicleLicence.Call("RecallLicensedVehicle", player, vName, VLT.VLChat.recallCmd);
                            }
                        }
                        
                    }
                    
                    if (string.IsNullOrEmpty(vName)) VLT.UpdateFrequency(transmitter, 0);
                    #endregion
                }

                if (timeToKill < Time.time)
                {
                    if (!VLT.permission.UserHasPermission(player.UserIDString, VLT.config.Perms.UseTransmitterPerm))
                    {
                        isKill = true;
                        return;
                    }

                    if (vehicle != null)
                    {
                        vehicle = (BaseVehicle)VLT.VehicleLicence?.Call("GetLicensedVehicle", player.userID.Get(), vName);

                        VLT.VehicleLicence.Call("KillLicensedVehicle", player, vName);

                        if (vehicle == null) return;

                        if (VLT.config.Settings.givePlaceableItem)
                        {
                            vName = vehicle.ShortPrefabName;
                            VLT.GiveVehicleItem(player, VLT.ConvertVehicleName(vName, true)); // Trying to get PrefabName lowered
                        }
                    }

                    isKill = true;
                }
            }
        }
        #endregion

        #region Configuration
        public Configuration config;
        public class Configuration
        {
            [JsonProperty(PropertyName = "General Settings")]
            public PluginSettings Settings = new PluginSettings
            {
                givePlaceableItem = false,
                repairTransmitter = true,
                giveOnPurchased = false,
                transmitterName = "Vehicle Manager",
                transmitterSkinID = 3392706239,
                numberListColor = "orange",
                vehicleListColor = "#009eff",
                syncFrequencyWhenMount = false,
                updateTranmitterOnSpawned = false,
                sortLicenses = false,
                distanceToDetect = 10.0f,
                amountOfTime = 3.0f
            };

            [JsonProperty(PropertyName = "Commands")]
            public ServerCommands command = new ServerCommands
            {
                TransmitterCmd = new () { "givetransmitter" },
                AllItemsCmd = new () { "giveallvitems" }
            };

            [JsonProperty(PropertyName = "Permissions")]
            public ServerPermissions Perms = new ServerPermissions
            {
                UseTransmitterPerm = "vehiclelicencetransmitter.givetransmitter",
                Admin = "vehiclelicencetransmitter.admin"
            };

            [JsonProperty(PropertyName = "Toastify Settings")]
            public VLTToastifySettings toastifySettings = new VLTToastifySettings()
            {
                ToastPosMsgID = "success",
                ToastNegMsgID = "error",
                ToastDur = 5
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
            // Current Version = 1.0.2
            if (config.Version >= Version) return;

            if (config.Version < new VersionNumber(1, 0, 2))
            {
                config.Settings.sortLicenses = true;
                config.command.TransmitterCmd = new () { "givetransmitter", "vlt" };
                config.command.AllItemsCmd = new () { "giveallvitems", "vgive" };
            }

            var configUpdateStr = "[CONFIG UPDATE] Updating to Version {0}";
            PrintWarning(string.Format(configUpdateStr, Version));
            config.Version = this.Version;

            SaveConfig();
        }
        #endregion

        #region LangFile
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                ["VariableFormatting"] = "{0} = Vehicle(s)",
                ["NeedVehicleToSpawn"] = "You must own at least 1 vehicle so you can spawn one!",
                ["CurrentVehiclesList"] = "YOUR CURRENT VEHICLES\n<i># = frequency to spawn!</i>\n{0}",
                ["CurrentRecallVehicle"] = "Recalled vehicle is now set to <color=#009eff>{0}</color>.",
                ["UnownedVehicle"] = "You do not currently own a <color=#009eff>{0}</color>."
            }, this);
        }
        #endregion
    }

    #region Classes
    public class PluginSettings
    {
        [JsonProperty(PropertyName = "Give Vehicle Item when vehicle is stored?")]
        public bool givePlaceableItem;

        [JsonProperty(PropertyName = "Keep transmitter on full health? If disabled, health will decrease at normal vanilla rate when used.")]
        public bool repairTransmitter;

        [JsonProperty(PropertyName = "Give player a transmitter when a vehicle is purchased?")]
        public bool giveOnPurchased;

        [JsonProperty(PropertyName = "Name for the Recall Transmitter?")]
        public string transmitterName;

        [JsonProperty(PropertyName = "Skin ID for the Recall Transmitter?")]
        public ulong transmitterSkinID;

        [JsonProperty(PropertyName = "Numbers colors when checking available licences? [HEX: #b0511e]")]
        public string numberListColor;

        [JsonProperty(PropertyName = "Vehicles colors when checking available licences? [HEX: #b0511e]")]
        public string vehicleListColor;

        [JsonProperty(PropertyName = "Sort vehicles alphabetically? [Is overidden to true if VehicleLicenceUI is loaded to sync natively]")]
        public bool sortLicenses;

        [JsonProperty(PropertyName = "Auto sync frequency when mounting vehicle? [Default is false]")]
        public bool syncFrequencyWhenMount;

        [JsonProperty(PropertyName = "Re-attach object to the Transmitter when recieving one? [Good for plugins that restore items on death]")]
        public bool updateTranmitterOnSpawned;

        [JsonProperty(PropertyName = "Distance from vehicle to detect it? [Default: 10]")]
        public float distanceToDetect;

        [JsonProperty(PropertyName = "Amount of seconds to kill a vehicle? [Default: 3]")]
        public float amountOfTime;
    }

    public class ServerCommands
    {
        [JsonProperty(PropertyName = "Commands to give a transmitter", ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<string> TransmitterCmd;

        [JsonProperty(PropertyName = "Commands to give all vehicle items",ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<string> AllItemsCmd;
    }

    public class ServerPermissions
    {
        [JsonProperty(PropertyName = "Vehicle Transmitter Use")]
        public string UseTransmitterPerm;

        [JsonProperty(PropertyName = "Vehicle Transmitter Admin")]
        public string Admin;
    }

    public class VLTToastifySettings
    {
        [JsonProperty(PropertyName = "Toastify Positive Msg ID")]
        public string ToastPosMsgID;

        [JsonProperty(PropertyName = "Toastify Negative Msg ID")]
        public string ToastNegMsgID;

        [JsonProperty(PropertyName = "Toastify Msg Duration")]
        public float ToastDur;
    }

    public class VLChatSettings
    {   
        public string spawnCmd { get; set; }
        public string recallCmd { get; set; }
        public string killCmd { get; set; }

        public string prefix { get; set; }
        public ulong steamIDIcon { get; set; }
    }

    public class SnaplatacksVLConfiguration
    {
        public List<VehicleLicenceVehicles> vehicles = new List<VehicleLicenceVehicles>();
    }

    public class SnaplatacksVLSettings
    {
        public VLChatSettings settings = new VLChatSettings();
    }

    public class VanillaVehicleItems
    {
        public string VLName { get; set; }
        public string PrefabName { get; set; }
        public ulong SkinID { get; set; }
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
    #endregion
}