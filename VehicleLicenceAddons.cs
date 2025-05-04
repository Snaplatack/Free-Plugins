using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Vehicle Licence Addons", "Snaplatack", "1.0.4")]
    [Description("Additional options for the Vehicle Licence plugin")]
    public class VehicleLicenceAddons : CovalencePlugin
    {
        #region Global Vars
        [PluginReference]
        private readonly Plugin VehicleLicence, ServerRewards, Economics;
        public static VehicleLicenceAddons Instance;
        private VLChatSettings VLChat = new();
        #endregion

        #region Init
        private void Init()
        {
            Instance = this;

            AddCovalenceCommand(config.cmdPerms.vOwnedCmd, nameof(CurrentOwnedLicensesCmd));
            AddCovalenceCommand(config.cmdPerms.vSpawnedCmd, nameof(CurrentlySpawnedVehiclesCmd));
            AddCovalenceCommand(config.cmdPerms.vTransferCmd, nameof(LicenseTransferCmd));
            AddCovalenceCommand(config.cmdPerms.vTradeCmd, nameof(LicenseTradeCmd));
            AddCovalenceCommand(config.cmdPerms.vAcceptTradeCmd, nameof(AcceptTradeCmd));
            AddCovalenceCommand(config.cmdPerms.vDenyTradeCmd, nameof(DenyTradeCmd));
            AddCovalenceCommand(config.cmdPerms.vCancelTradeCmd, nameof(CancelTradeCmd));
            AddCovalenceCommand("vhelp", nameof(VehicleAddonHelp));

            permission.RegisterPermission(config.cmdPerms.vTransferPerm, this);
            permission.RegisterPermission(config.cmdPerms.vTradePerm, this);
            permission.RegisterPermission(config.cmdPerms.vAdminPerm, this);

            LoadData();

            var snapsSettings = Config.ReadObject<SnaplatacksVLSettings>($"{Interface.Oxide.DataDirectory}/SnaplatacksVLData/VehicleLicenceChatSettings.json");
            var snapsChatSettings = snapsSettings.settings;

            VLChatSettings chatSettings = new();

            chatSettings.spawnCmd = snapsChatSettings.spawnCmd;
            chatSettings.recallCmd = snapsChatSettings.recallCmd;
            chatSettings.killCmd = snapsChatSettings.killCmd;

            VLChat = chatSettings;
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
            SaveData();
        }
        #endregion

        #region Commands

        #region Help
        private void VehicleAddonHelp(IPlayer iPlayer, string command, string[] args)
        {
            iPlayer.Reply($"Commands:\n/{config.cmdPerms.vOwnedCmd}\n/{config.cmdPerms.vSpawnedCmd}\n/{config.cmdPerms.vTradeCmd} <vehicle to give> <vehicle to recieve> <player name>\n/{config.cmdPerms.vTransferCmd} <vehicle to give> <player name>\n/vHelp");
        }
        #endregion

        #region Owned Licenses & Vehicles
        private void CurrentOwnedLicensesCmd(IPlayer iPlayer, string command, string[] args)
        {
            // cmdName <playerName>
            ulong playerID = ulong.Parse(iPlayer.Id);
            IPlayer iPlayerFound = iPlayer;
            ulong iPlayerFoundID = 0;
            List<string> playerLicenses;

            if (iPlayer == null && !iPlayer.IsServer) return;

            if (config.settings.othersViewAllLicenses || iPlayer.HasPermission(config.cmdPerms.vAdminPerm) || iPlayer.IsServer)
            {
                if (args.Length <= 0 || args.Length > 1)
                {
                    iPlayerFoundID = playerID;
                }
                else
                {
                    iPlayerFound = FindIPlayer(args[0]);
                    iPlayerFoundID = ulong.Parse(iPlayerFound.Id);
                }

                if (iPlayerFound == null)
                {
                    SendMessage(iPlayer, null, "couldntFindPlayer", string.Empty, string.Empty);
                    return;
                }

                playerLicenses = (List<string>)VehicleLicence.Call("GetVehicleLicenses", iPlayerFoundID);
            }
            else playerLicenses = (List<string>)VehicleLicence.Call("GetVehicleLicenses", playerID);

            if (playerLicenses == null)
            {
                SendMessage(iPlayer, iPlayerFound, "noLicensesFound", string.Empty, string.Empty);
                return;
            }

            playerLicenses = playerLicenses.OrderBy(x => x).ToList();

            string playerLicensesFormat = String.Join("\n", playerLicenses);

            if (args.Length != 0)
            {
                SendMessage(iPlayer, iPlayerFound, "currentLicensesAdmin", playerLicensesFormat, string.Empty, false);
                return;
            }

            SendMessage(iPlayer, null, "currentLicenses", playerLicensesFormat, string.Empty, false);
        }

        private void CurrentlySpawnedVehiclesCmd(IPlayer iPlayer, string command, string[] args)
        {
            ulong playerID = ulong.Parse(iPlayer.Id);
            IPlayer iPlayerFound = iPlayer;
            ulong iPlayerFoundID = 0;
            HashSet<string> playerVehiclesFound = new();
            List<string> playerLicenses;

            if (iPlayer == null && !iPlayer.IsServer) return;

            if (config.settings.othersViewAllVehicles || iPlayer.HasPermission(config.cmdPerms.vAdminPerm) || iPlayer.IsServer)
            {
                if (args.Length <= 0 || args.Length > 1)
                {
                    iPlayerFoundID = playerID;
                }
                else
                {
                    iPlayerFound = FindIPlayer(args[0]);
                    iPlayerFoundID = ulong.Parse(iPlayerFound.Id);
                }

                if (iPlayerFound == null)
                {
                    SendMessage(iPlayer, null, "couldntFindPlayer", string.Empty, string.Empty);
                    return;
                }

                playerLicenses = (List<string>)VehicleLicence.Call("GetVehicleLicenses", iPlayerFoundID);
            }
            else playerLicenses = (List<string>)VehicleLicence.Call("GetVehicleLicenses", playerID);

            if (playerLicenses == null)
            {
                SendMessage(iPlayer, iPlayerFound, "noVehiclesFound", string.Empty, string.Empty);
                return;
            }

            playerLicenses = playerLicenses.OrderBy(x => x).ToList();

            BaseEntity vehicle;

            for (int i = 0; i < playerLicenses.Count; i++) // will need changed to support 'config.settings.othersViewAllVehicles' perm
            {
                vehicle = !iPlayer.IsServer
                    ? (BaseVehicle)VehicleLicence.Call("GetLicensedVehicle", playerID, playerLicenses[i])
                    : (BaseVehicle)VehicleLicence.Call("GetLicensedVehicle", iPlayerFoundID, playerLicenses[i]);

                if (vehicle == null) continue;

                playerVehiclesFound.Add(playerLicenses[i]);
            }

            string playerVehiclesFormat = String.Join("\n", playerVehiclesFound);

            if (args.Length != 0)
            {
                SendMessage(iPlayer, iPlayerFound, "currentVehiclesAdmin", playerVehiclesFormat, string.Empty, false);
                return;
            }

            SendMessage(iPlayer, null, "currentVehicles", playerVehiclesFormat, string.Empty, false);
        }
        #endregion

        #region License Transfer & Trading
        private void LicenseTransferCmd(IPlayer Sender, string command, string[] args)
        {
            // Command Format: /cmdName <vehicle to give> <player>
            ulong SenderID = ulong.Parse(Sender.Id);

            if (!Sender.HasPermission(config.cmdPerms.vTransferPerm)) return;

            if (Sender == null && !Sender.IsServer) return;

            if (args.Length <= 1)
            {
                SendMessage(Sender, null, "incorrectCmdFormat", "/" + config.cmdPerms.vTransferCmd + " <vehicleNameToSend> <playerName>", string.Empty);
                return;
            }

            string vehicleName = args[0].ToLower();
            IPlayer Reciever = FindIPlayer(args[1]);
            ulong RecieverID = ulong.Parse(Reciever.Id);

            if (Reciever == null)
            {
                return;
            }

            List<string> playerLicensesSender = (List<string>)VehicleLicence.Call("GetVehicleLicenses", SenderID);

            vehicleName = playerLicensesSender.Find(value => value.ToLower() == vehicleName);

            var recieverHasLicense = (bool)VehicleLicence.Call("HasVehicleLicense", RecieverID, vehicleName);

            if (recieverHasLicense)
            {
                SendMessage(Sender, Reciever, "playerAlreadyOwns", vehicleName, string.Empty);
                return;
            }

            bool isAdded = (bool)VehicleLicence.Call("AddVehicleLicense", RecieverID, vehicleName);

            BaseVehicle vehicle = (BaseVehicle)VehicleLicence.Call("GetLicensedVehicle", SenderID, vehicleName);

            bool isRemoved = (bool)VehicleLicence.Call("RemoveVehicleLicense", SenderID, vehicleName);

            SendMessage(Sender, Reciever, "sentLicense", vehicleName, string.Empty);

            SendMessage(Reciever, Sender, "recievedLicense", vehicleName, string.Empty);

            if (!config.settings.vKillGive) return;

            if (vehicle != null) vehicle.Kill();
        }

        private void LicenseTradeCmd(IPlayer Sender, string command, string[] args)
        {
            // Command Format: /cmdName <vehicle to give> <player> <vehicle to recieve | amount to recieve>
            ulong SenderID = ulong.Parse(Sender.Id);

            if (Sender == null && !Sender.IsServer) return;

            if (!Sender.HasPermission(config.cmdPerms.vTradePerm)) return;

            if (args.Length <= 2)
            {
                SendMessage(Sender, null, "incorrectCmdFormat", "/" + config.cmdPerms.vTradeCmd + " <vehicleNameToSend> <vehicleNameToRecieve> <playerName>", string.Empty);
                return;
            }

            string sendersVehicleName = args[0].ToLower();
            string recieversVehicleName = args[2].ToLower();

            IPlayer Reciever = FindIPlayer(args[1]);
            ulong RecieverID = ulong.Parse(Reciever.Id);

            if (Reciever == null)
            {
                SendMessage(Sender, null, "couldntFindPlayer", string.Empty, string.Empty);
                return;
            }

            var pendingRequests = Data.trades.tradeRequests.Find(x => x.playerSenderId == SenderID && x.playerRecieverId == RecieverID);

            if (pendingRequests != null)
            {
                SendMessage(Sender, Reciever, "pendingRequestPresent", sendersVehicleName, recieversVehicleName);
                return;
            }

            List<string> SendersLicenses = (List<string>)VehicleLicence.Call("GetVehicleLicenses", SenderID);

            var sendersVehicleNameFound = SendersLicenses.Find(value => value.ToLower() == sendersVehicleName);

            if (String.IsNullOrEmpty(sendersVehicleNameFound))
            {
                SendMessage(Sender, Reciever, "playerAlreadyOwns", sendersVehicleName, recieversVehicleName);
                return;
            }

            sendersVehicleName = sendersVehicleNameFound;

            var amount = int.TryParse(recieversVehicleName, out var amountFound);

            if (!amount)
            {
                List<string> playerLicensesReciever = (List<string>)VehicleLicence.Call("GetVehicleLicenses", RecieverID);

                var recieversVehicleNameFound = playerLicensesReciever.Find(value => value.ToLower() == recieversVehicleName);

                if (String.IsNullOrEmpty(recieversVehicleNameFound))
                {
                    SendMessage(Sender, Reciever, "playerAlreadyOwns", recieversVehicleName, sendersVehicleName);
                    return;
                }

                recieversVehicleName = recieversVehicleNameFound;
            }

            // Add Trade to Data
            TradeRequestSettings tradeRequest = new();

            tradeRequest.playerSenderId = SenderID;
            tradeRequest.playerRecieverId = RecieverID;
            tradeRequest.vehicleNameSend = sendersVehicleName;

            if (!amount) tradeRequest.vehicleNameReceive = recieversVehicleName;

            if (config.settings.useServerRewards && !config.settings.useEconomics)
            {
                tradeRequest.sellAmountSR = amountFound;
            }

            if (config.settings.useEconomics && !config.settings.useServerRewards)
            {
                tradeRequest.sellAmountEco = amountFound;
            }

            Data.trades.tradeRequests.Add(tradeRequest);
            SaveData(); //Used to Debug

            if (amountFound != 0)
            {
                recieversVehicleName = $"{amountFound}";
                SendMessage(Sender, Reciever, "sentTradeOffer", sendersVehicleName, config.settings.currencySymbol + recieversVehicleName);
                SendMessage(Reciever, Sender, "recievedTradeOffer", sendersVehicleName, config.settings.currencySymbol + recieversVehicleName);
                return;
            }

            if (amountFound != 0)
            {
                recieversVehicleName = $"{amountFound}";
                SendMessage(Sender, Reciever, "sentTradeOffer", sendersVehicleName, config.settings.currencySymbol + recieversVehicleName);
                SendMessage(Reciever, Sender, "recievedTradeOffer", sendersVehicleName, config.settings.currencySymbol + recieversVehicleName);
                return;
            }

            SendMessage(Sender, Reciever, "sentTradeOffer", sendersVehicleName, recieversVehicleName);
            SendMessage(Reciever, Sender, "recievedTradeOffer", sendersVehicleName, recieversVehicleName);
        }
        #endregion

        #region Accept & Deny Trade
        private void AcceptTradeCmd(IPlayer iPlayer, string command, string[] args)
        {
            // Command Format: /cmdName <player>
            bool isServer = iPlayer.IsServer;
            BasePlayer playerReciever = iPlayer.Object as BasePlayer;
            TradeRequestSettings currentRequest = new TradeRequestSettings();

            if (playerReciever == null && !isServer) return;

            if (args.Length <= 0)
            {
                SendMessage(iPlayer, null, "incorrectCmdFormat", "/" + config.cmdPerms.vAcceptTradeCmd + " <playerName>", string.Empty);
                return;
            }

            var playerSender = (FindIPlayer(args[0])).Object as BasePlayer;

            if (playerSender == null)
            {
                SendMessage(iPlayer, null, "couldntFindPlayer", string.Empty, string.Empty);
                return;
            }

            for (int i = 0; i < Data.trades.tradeRequests.Count; i++)
            {
                if (Data.trades.tradeRequests[i].playerSenderId == playerSender.userID.Get() &&
                    Data.trades.tradeRequests[i].playerRecieverId == playerReciever.userID.Get())
                {
                    currentRequest = Data.trades.tradeRequests[i];
                    break;
                }
                continue;
            }

            if (currentRequest == null)
            {
                SendMessage(iPlayer, playerSender.IPlayer, "noTradeRequests", string.Empty, string.Empty);
                return;
            }

            var vehicleNameSender = currentRequest.vehicleNameSend;
            var vehicleNameReciever = currentRequest.vehicleNameReceive;
            var sellAmountSR = currentRequest.sellAmountSR;
            var sellAmountEco = currentRequest.sellAmountEco;

            BaseVehicle vehicleSender = (BaseVehicle)VehicleLicence.Call("GetLicensedVehicle", playerSender.userID.Get(), vehicleNameSender);

            var isRemovedSender = (bool)VehicleLicence.Call("RemoveVehicleLicense", playerSender.userID.Get(), vehicleNameSender);

            BaseVehicle vehicleReciever = vehicleSender;

            if (sellAmountSR == 0 && sellAmountEco == 0)
            {
                var isAddedSender = (bool)VehicleLicence.Call("AddVehicleLicense", playerSender.userID.Get(), vehicleNameReciever);

                vehicleReciever = (BaseVehicle)VehicleLicence.Call("GetLicensedVehicle", playerReciever.userID.Get(), vehicleNameReciever);

                var isRemovedReciever = (bool)VehicleLicence.Call("RemoveVehicleLicense", playerReciever.userID.Get(), vehicleNameReciever);
            }

            var isAddedReciever = (bool)VehicleLicence.Call("AddVehicleLicense", playerReciever.userID.Get(), vehicleNameSender);

            if (config.settings.vKillTrade)
            {
                if (vehicleSender != null && isRemovedSender) vehicleSender.Kill();
                if (vehicleReciever != null && isRemovedSender) vehicleReciever.Kill();
            }

            if (config.settings.useServerRewards && !config.settings.useEconomics)
            {
                ServerRewards?.Call("TakePoints", playerReciever.userID.Get(), currentRequest.sellAmountSR);
            }

            if (config.settings.useEconomics && !config.settings.useServerRewards)
            {
                Economics?.Call("Withdraw", playerReciever.userID.Get(), currentRequest.sellAmountEco);
            }

            Data.trades.tradeRequests.Remove(currentRequest);

            if (currentRequest.playerRecieverId == playerReciever.userID.Get())
            {
                Data.trades.tradeRequests.Remove(currentRequest);
            }

            SaveData(); //Used to Debug

            if (sellAmountSR != 0)
            {
                vehicleNameReciever = $"{sellAmountSR}";
                SendMessage(iPlayer, playerSender.IPlayer, "acceptedTradeOfferReciever", vehicleNameSender, config.settings.currencySymbol + vehicleNameReciever);
                SendMessage(playerSender.IPlayer, iPlayer, "acceptedTradeOfferSender", vehicleNameSender, config.settings.currencySymbol + vehicleNameReciever);
                return;
            }

            if (sellAmountEco != 0)
            {
                vehicleNameReciever = $"{sellAmountEco}";
                SendMessage(iPlayer, playerSender.IPlayer, "acceptedTradeOfferReciever", vehicleNameSender, config.settings.currencySymbol + vehicleNameReciever);
                SendMessage(playerSender.IPlayer, iPlayer, "acceptedTradeOfferSender", vehicleNameSender, config.settings.currencySymbol + vehicleNameReciever);
                return;
            }

            SendMessage(iPlayer, playerSender.IPlayer, "acceptedTradeOfferReciever", vehicleNameSender, vehicleNameReciever);
            SendMessage(playerSender.IPlayer, iPlayer, "acceptedTradeOfferSender", vehicleNameSender, vehicleNameReciever);
        }

        private void DenyTradeCmd(IPlayer iPlayer, string command, string[] args)
        {
            // Command Format: /cmdName <player>
            bool isServer = iPlayer.IsServer;
            BasePlayer playerReciever = iPlayer.Object as BasePlayer;
            TradeRequestSettings currentRequest = new TradeRequestSettings();

            if (playerReciever == null && !isServer) return;

            if (args.Length <= 0)
            {
                SendMessage(iPlayer, null, "incorrectCmdFormat", "/" + config.cmdPerms.vDenyTradeCmd + " <playerName>", string.Empty);
                return;
            }

            BasePlayer playerSender = (FindIPlayer(args[0])).Object as BasePlayer;

            if (playerSender == null)
            {
                SendMessage(iPlayer, null, "couldntFindPlayer", string.Empty, string.Empty);
                return;
            }

            for (int i = 0; i < Data.trades.tradeRequests.Count; i++)
            {
                if (Data.trades.tradeRequests[i].playerSenderId == playerSender.userID.Get() &&
                    Data.trades.tradeRequests[i].playerRecieverId == playerReciever.userID.Get())
                {
                    currentRequest = Data.trades.tradeRequests[i];
                    break;
                }
            }

            if (currentRequest == null)
            {
                SendMessage(iPlayer, playerSender.IPlayer, "noTradeRequests", string.Empty, string.Empty);
                return;
            }

            var vehicleNameSender = currentRequest.vehicleNameSend;
            var vehicleNameReciever = currentRequest.vehicleNameReceive;
            var sellAmountSR = currentRequest.sellAmountSR;
            var sellAmountEco = currentRequest.sellAmountEco;

            Data.trades.tradeRequests.Remove(currentRequest);

            if (currentRequest.playerRecieverId == playerReciever.userID.Get())
            {
                Data.trades.tradeRequests.Remove(currentRequest);
            }

            SaveData(); //Used to Debug

            if (sellAmountSR != 0)
            {
                vehicleNameReciever = sellAmountSR.ToString();
                SendMessage(iPlayer, playerSender.IPlayer, "deniedTradeOfferReciever", vehicleNameSender, config.settings.currencySymbol + vehicleNameReciever);
                SendMessage(playerSender.IPlayer, iPlayer, "deniedTradeOfferSender", vehicleNameSender, config.settings.currencySymbol + vehicleNameReciever);
                return;
            }

            if (sellAmountEco != 0)
            {
                vehicleNameReciever = sellAmountEco.ToString();
                SendMessage(iPlayer, playerSender.IPlayer, "deniedTradeOfferReciever", vehicleNameSender, config.settings.currencySymbol + vehicleNameReciever);
                SendMessage(playerSender.IPlayer, iPlayer, "deniedTradeOfferSender", vehicleNameSender, config.settings.currencySymbol + vehicleNameReciever);
                return;
            }

            SendMessage(iPlayer, playerSender.IPlayer, "deniedTradeOfferReciever", vehicleNameSender, vehicleNameReciever);
            SendMessage(playerSender.IPlayer, iPlayer, "deniedTradeOfferSender", vehicleNameSender, vehicleNameReciever);
        }
        #endregion

        #region Cancel Trade
        private void CancelTradeCmd(IPlayer Sender, string command, string[] args)
        {
            // Command Format: /cmdName <player>
            ulong SenderID = ulong.Parse(Sender.Id);

            TradeRequestSettings currentRequest = new TradeRequestSettings();

            if (Sender == null) return;

            if (args.Length <= 0)
            {
                SendMessage(Sender, null, "incorrectCmdFormat", "/" + config.cmdPerms.vCancelTradeCmd + " <playerName>", string.Empty);
                return;
            }

            var Reciever = FindIPlayer(args[0]);
            var RecieverID = ulong.Parse(Reciever.Id);

            if (Reciever == null)
            {
                SendMessage(Sender, null, "couldntFindPlayer", string.Empty, string.Empty);
                return;
            }

            for (int i = 0; i < Data.trades.tradeRequests.Count; i++)
            {
                if (Data.trades.tradeRequests[i].playerSenderId == SenderID &&
                    Data.trades.tradeRequests[i].playerRecieverId == RecieverID)
                {
                    currentRequest = Data.trades.tradeRequests[i];
                    break;
                }
            }

            if (currentRequest == null)
            {
                SendMessage(Sender, Reciever, "noTradeRequests", string.Empty, string.Empty);
                return;
            }

            var vehicleNameSender = currentRequest.vehicleNameSend;
            var vehicleNameReciever = currentRequest.vehicleNameReceive;
            var sellAmountSR = currentRequest.sellAmountSR;
            var sellAmountEco = currentRequest.sellAmountEco;

            if (currentRequest.playerRecieverId == RecieverID)
            {
                Data.trades.tradeRequests.Remove(currentRequest);
            }

            SaveData(); //Used to Debug

            if (sellAmountSR != 0)
            {
                vehicleNameReciever = sellAmountSR.ToString();
                SendMessage(Sender, Reciever, "canceledTradeOfferReciever", vehicleNameSender, config.settings.currencySymbol + vehicleNameReciever);
                return;
            }

            if (sellAmountEco != 0)
            {
                vehicleNameReciever = sellAmountEco.ToString();
                SendMessage(Sender, Reciever, "canceledTradeOfferReciever", vehicleNameSender, config.settings.currencySymbol + vehicleNameReciever);
                return;
            }

            SendMessage(Sender, Reciever, "canceledTradeOfferReciever", vehicleNameSender, vehicleNameReciever);
        }
        #endregion

        #endregion

        #region Method Helpers
        private IPlayer FindIPlayer(string playerName)
        {
            string argument = playerName.ToLower();
            foreach (var iPlayer in covalence.Players.All)
            {
                if (iPlayer.Name.ToLower().Contains(argument) || iPlayer.Id == argument)
                    return iPlayer;
            }
            return null;
        }

        private void SendMessage(IPlayer iPlayer, IPlayer iPlayer2, string message, string vehicleName1, string vehicleName2, bool usePrefix = true)
        {
            // Message Format: {0} = player?.Name | {1} = player2?.Name | {2} = vehicleName1 | {3} = vehicleName2
            var isServer = iPlayer.IsServer;
            BasePlayer player = iPlayer.Object as BasePlayer;

            if (player == null && !isServer) return;

            message = lang.GetMessage(message, this);

            if (isServer)
            {
                Puts(string.Format(message, iPlayer?.Name, iPlayer2?.Name, vehicleName1, vehicleName2));
                return;
            }

            string prefix = usePrefix ? $"{VLChat.prefix} " : string.Empty;

            message = prefix + string.Format(message, iPlayer?.Name, iPlayer2?.Name, vehicleName1, vehicleName2);

            Player.Message(player, message, VLChat.steamIDIcon);
        }
        #endregion

        #region Configuration
        private Configuration config;
        public class Configuration
        {
            [JsonProperty(PropertyName = "Settings")]
            public VLAPluginSettings settings = new VLAPluginSettings()
            {
                clearDataOnWipe = false,
                othersViewAllLicenses = false,
                othersViewAllVehicles = false,
                vKillGive = false,
                vKillTrade = false,
                useServerRewards = false,
                useEconomics = false,
                currencySymbol = "$"
            };

            [JsonProperty(PropertyName = "Commands & Permissions")]
            public VLAPluginCmdPerm cmdPerms = new VLAPluginCmdPerm()
            {
                vTransferCmd = "givelicense",
                vTradeCmd = "tradelicense",
                vAcceptTradeCmd = "acceptlicense",
                vDenyTradeCmd = "denylicense",
                vCancelTradeCmd = "cancellicense",
                vOwnedCmd = "mylicenses",
                vSpawnedCmd = "myvehicles",
                vAdminPerm = "vehiclelicenceaddons.admin",
                vTransferPerm = "vehiclelicenceaddons.allowgivelicense",
                vTradePerm = "vehiclelicenceaddons.allowtradelicense"
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
            //current version = 1.0.4
            if (config.Version >= Version) return;
            if (config.Version < new VersionNumber(1, 0, 3))
            {
                LoadDefaultConfig();

                var configUpdateStr = "[CONFIG UPDATE] Updating to Version {0}";
                PrintWarning(string.Format(configUpdateStr, Version));
                config.Version = this.Version;
                
                SaveConfig();
            }

        }
        #endregion

        #region DataFile
        public PluginData Data = new PluginData();
        public class PluginData
        {
            [JsonProperty(PropertyName = "Pending Trades")]
            public Trading trades = new Trading()
            {
                tradeRequests = new List<TradeRequestSettings>()
            };
        }

        private void LoadData()
        {
            try
            {
                Data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>($"{Name}");
            }
            catch (Exception)
            {
                Data = new PluginData();
            }
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}", Data);
        }

        void OnNewSave()
        {
            if (config.settings.clearDataOnWipe)
            {
                Data = new PluginData();
            }
        }
        #endregion

        #region LangFile
        // Message Format: {0} = iPlayer?.Name | {1} = iPlayer2?.Name | {2} = vehicleName1 | {3} = vehicleName2
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                ["noLicensesFound"] = "No licenses found for <color=orange>{1}</color>!",
                ["noVehiclesFound"] = "No vehicles found for <color=orange>{1}</color>!",
                ["currentLicenses"] = "<size=20><color=#009eff>LICENSES</color></size>\n{2}",
                ["currentLicensesAdmin"] = "<size=20><color=#009eff>{1}</color>'s Licenses</size>\n{2}",

                ["currentVehicles"] = "<size=20><color=#009eff>VEHICLES</color></size>\n{2}",
                ["currentVehiclesAdmin"] = "<size=20><color=#009eff>{1}</color>'s Vehicles</size>\n{2}",

                ["sentLicense"] = "You sent your <color=#009eff>{2}</color> to <color=orange>{1}</color>!",
                ["recievedLicense"] = "<color=orange>{1}</color> gave you their <color=#009eff>{2}</color>!",

                ["pendingRequestPresent"] = "You already have a pending request with <color=orange>{1}</color>!",
                ["canceledTradeOfferReciever"] = "Canceled your trade request with <color=orange>{1}</color>!",
                ["sentTradeOffer"] = "Sent a trade offer for your <color=#009eff>{2}</color> in return of <color=orange>{1}</color>'s <color=#009eff>{3}</color>!",
                ["recievedTradeOffer"] = "<color=orange>{1}</color> wants to trade your <color=#009eff>{3}</color> for their <color=#009eff>{2}</color>!",

                ["noTradeRequests"] = "You have no pending trade requests from <color=orange>{0}</color>",
                ["acceptedTradeOfferSender"] = "<color=orange>{1}</color> <color=green>accepted</color> your trade for your <color=#009eff>{2}</color> in return of their <color=#009eff>{3}</color>!",
                ["acceptedTradeOfferReciever"] = "You accepted <color=orange>{1}</color>'s trade for their <color=#009eff>{3}</color> in return of your <color=#009eff>{2}</color>!",

                ["deniedTradeOfferSender"] = "<color=orange>{1}</color> <color=red>denied</color> your trade for your <color=#009eff>{2}</color> for their <color=#009eff>{3}</color>!",
                ["deniedTradeOfferReciever"] = "You denied <color=orange>{1}</color>'s trade for their <color=#009eff>{3}</color> in return of your <color=#009eff>{2}</color>!",

                ["playerAlreadyOwns"] = "<color=orange>{1}</color> already owns a <color=#009eff>{2}</color>!",
                ["couldntFindPlayer"] = "Error finding that player! Please put their whole name if possible!",
                ["incorrectCmdFormat"] = "Wrong format! <color=orange>{2}</color>"
            }, this);
        }
        #endregion
    }

    #region Definitions
    public class TradeRequestSettings
    {
        public ulong playerSenderId;
        public ulong playerRecieverId;
        public string vehicleNameSend;
        public string vehicleNameReceive;
        public int sellAmountSR;
        public double sellAmountEco;
    }

    public class VLAPluginCmdPerm
    {
        [JsonProperty(PropertyName = "Transfer Vehicle License Command")]
        public string vTransferCmd;

        [JsonProperty(PropertyName = "Trade Vehicle License Command")]
        public string vTradeCmd;

        [JsonProperty(PropertyName = "Accept Trade Vehicle License Command")]
        public string vAcceptTradeCmd;

        [JsonProperty(PropertyName = "Deny Trade Vehicle License Command")]
        public string vDenyTradeCmd;

        [JsonProperty(PropertyName = "Cancel Trade Vehicle License Command")]
        public string vCancelTradeCmd;

        [JsonProperty(PropertyName = "Check Current Licenses Command")]
        public string vOwnedCmd;

        [JsonProperty(PropertyName = "Check Spawned Licensed Vehicles Command")]
        public string vSpawnedCmd;

        [JsonProperty(PropertyName = "Admin Perm")]
        public string vAdminPerm;

        [JsonProperty(PropertyName = "License Transferring Perm")]
        public string vTransferPerm;

        [JsonProperty(PropertyName = "License Trading Perm")]
        public string vTradePerm;
    }

    public class VLAPluginSettings
    {
        [JsonProperty(PropertyName = "Players Can View Anyone Elses Licenses")]
        public bool othersViewAllLicenses;

        [JsonProperty(PropertyName = "Players Can View Anyone Elses Vehicles")]
        public bool othersViewAllVehicles;

        [JsonProperty(PropertyName = "Clear Pending Trades On New Wipe")]
        public bool clearDataOnWipe;

        [JsonProperty(PropertyName = "Kill Original Vehicle When Given")]
        public bool vKillGive;

        [JsonProperty(PropertyName = "Kill Both Vehicles When Traded")]
        public bool vKillTrade;

        [JsonProperty(PropertyName = "Use Server Rewards When Trading")]
        public bool useServerRewards;

        [JsonProperty(PropertyName = "Use Economics When Trading")]
        public bool useEconomics;

        [JsonProperty(PropertyName = "Currency Symbol [Gets added before the number]")]
        public string currencySymbol;
    }

    public class Trading
    {
        public List<TradeRequestSettings> tradeRequests;
    }

    public class SnaplatacksVLSettings
    {
        public VLChatSettings settings = new VLChatSettings();
    }

    public class VLChatSettings
    {   
        public string spawnCmd { get; set; }
        public string recallCmd { get; set; }
        public string killCmd { get; set; }

        public string prefix { get; set; } = string.Empty;
        public ulong steamIDIcon { get; set; } = 0;
    }
    #endregion
}
