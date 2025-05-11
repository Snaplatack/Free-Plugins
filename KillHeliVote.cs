using System.Collections.Generic;
using System;
using Oxide.Core.Libraries.Covalence;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using System.ComponentModel;

namespace Oxide.Plugins
{
    [Info("Kill Heli Vote", "Snaplatack", "1.2.2")]
    [Description("Players can vote to kill all Patrol Helicopters")]
    class KillHeliVote : RustPlugin
    {
        #region Variables
        [PluginReference]
        private readonly Plugin Toastify, Notify, HeliSignals;
        private Timer reminderMessage;

        VotingProperties votingProperties = new VotingProperties();
        BoolChecks boolChecks = new BoolChecks();
        #endregion

        #region LoadUnload
        private void Init()
        {
            Unsubscribe(nameof(OnExplosiveThrown));
            Unsubscribe(nameof(OnExplosiveDropped));
        }
        
        private void OnServerInitialized()
        {
            LoadData();

            if (config.Settings.destroyHeliSignal)
            {
                Subscribe(nameof(OnExplosiveThrown));
                Subscribe(nameof(OnExplosiveDropped));
            }

            // Setup Helis
            foreach (var entity in BaseNetworkable.serverEntities.OfType<PatrolHelicopter>())
            {
                if (entity is PatrolHelicopter)
                {
                    SetupHeli(entity);
                }
            }

            AddCovalenceCommand(config.cmdPerms.VoteCommand, nameof(HeliVoteCmd), $"{Name}.{config.cmdPerms.UsePerm}");
            AddCovalenceCommand(config.cmdPerms.KillCommand, nameof(HeliKillCmd), $"{Name}.{config.cmdPerms.IsAdmin}");

            // Setup Players
            var players = BasePlayer.activePlayerList;
            var playerCount = players.Count;

            votingProperties.activePlayersCount = playerCount;

            if (bannedPlayers.playerList.players.Count == 0 && !(bannedPlayers.playerList.players.Contains("SteamID 1") || bannedPlayers.playerList.players.Contains("SteamID 2"))) return;

            foreach (var bPlayer in players)
            {
                if (bannedPlayers.playerList.players.Contains(bPlayer.UserIDString))
                        playerCount--;
            }
        }

        private void Unload()
        {
            SaveData();
        }
        #endregion

        #region Hooks
        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null) return;

            if (bannedPlayers.playerList.players.Contains(player.UserIDString)) return;

            if (boolChecks.HeliIsOut && !Data.info.PlayerVotes.Contains(player.UserIDString))
            {
                NextFrame(() =>
                {
                    NotifyPlayer(player.IPlayer, config.NotifSetting.NotifyPosMsgID, config.NotifSetting.ToastPosMsgID, lang.GetMessage("heli_announcement", this, player.UserIDString) + lang.GetMessage("pending_votes", this), config.NotifSetting.ToastDur);
                });
            }

            votingProperties.activePlayersCount++;
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (player == null) return;

            if (Data.info.PlayerVotes.Contains(player.UserIDString))
            {
                votingProperties.votes--;
                Data.info.PlayerVotes.Remove(player.UserIDString);
            }

            if (bannedPlayers.playerList.players.Contains(player.UserIDString)) return;

            votingProperties.activePlayersCount--;

            if (votingProperties.activePlayersCount < 0) votingProperties.activePlayersCount = 0;
        }

        private void OnEntitySpawned(PatrolHelicopter heli)
        {
            if (heli == null) return;

            boolChecks.FirstHeliSpawned = 0;
            
            SetupHeli(heli);
        }

        private void OnEntityKill(PatrolHelicopter heli)
        {
            if (heli == null) return;

            if (boolChecks.TimerStarted)
            {
                reminderMessage.Destroy();
                boolChecks.TimerStarted = false;
            }

            Data.info.ActiveHelis.Remove(heli.net.ID.Value);

            if (Data.info.ActiveHelis.Count == 0)
            {
                boolChecks.HeliIsOut = false;
                Data.info.PlayerVotes.Clear();
            }
        }

        // Heli Signals Plugin
        private void OnExplosiveDropped(BasePlayer player, SupplySignal entity, ThrownWeapon weapon) => OnExplosiveThrown(player, entity, weapon);
        private void OnExplosiveThrown(BasePlayer player, SupplySignal entity, ThrownWeapon weapon)
        {
            if (player == null || entity == null || weapon == null) return;

            if (entity.skinID <= 0) return;

            var isHeliSignal = (bool)HeliSignals?.CallHook("IsHeliSignalObject", entity.skinID);

            if (isHeliSignal)
            {
                boolChecks.IsHeliSignalHeli = true;
                return;
            }

            boolChecks.IsHeliSignalHeli = false;
        }
        #endregion

        #region Commands
        private void HeliKillCmd(IPlayer iPlayer, string command, string[] args)
        {
            var IsServer = iPlayer.IsServer;
            var player = iPlayer.Object as BasePlayer;

            if (player == null && !IsServer) return;

            if (Data.info.ActiveHelis.Count == 0)
            {
                if (!IsServer)
                {
                    NotifyPlayer(player.IPlayer, config.NotifSetting.NotifyNegMsgID, config.NotifSetting.ToastNegMsgID, lang.GetMessage("heli_not_out", this, player.UserIDString), config.NotifSetting.ToastDur);
                    return;
                }

                Puts(lang.GetMessage("heli_not_out", this));
                return;
            }

            Puts(lang.GetMessage("heli_killed", this));
            KillHelis();
        }

        private void HeliVoteCmd(IPlayer iPlayer, string command, string[] args)
        {
            if (iPlayer == null || command == null) return;

            if (Data.info.ActiveHelis.Count == 0)
            {
                NotifyPlayer(iPlayer, config.NotifSetting.NotifyNegMsgID, config.NotifSetting.ToastNegMsgID, lang.GetMessage("heli_not_out", this, iPlayer.Id), config.NotifSetting.ToastDur);
                return;
            }

            if (bannedPlayers.playerList.players.Contains(iPlayer.Id))
            {
                NotifyPlayer(iPlayer, config.NotifSetting.NotifyNegMsgID, config.NotifSetting.ToastNegMsgID, lang.GetMessage("player_banned", this, iPlayer.Id), config.NotifSetting.ToastDur);
                return;
            }

            if (Data.info.PlayerVotes.Contains(iPlayer.Id))
            {
                NotifyPlayer(iPlayer, config.NotifSetting.NotifyNegMsgID, config.NotifSetting.ToastNegMsgID, lang.GetMessage("player_voted", this, iPlayer.Id), config.NotifSetting.ToastDur);
            }
            else
            {
                Data.info.PlayerVotes.Add(iPlayer.Id);
                votingProperties.votes++;
                NotifyPlayer(iPlayer, config.NotifSetting.NotifyPosMsgID, config.NotifSetting.ToastPosMsgID, lang.GetMessage("player_vote", this, iPlayer.Id), config.NotifSetting.ToastDur);
            }

            if (config.Settings.PercentVotesRequired <= (votingProperties.votes / votingProperties.activePlayersCount) * 100)
            {
                KillHelis();
            }
        }
        #endregion

        #region Functions
        private void SetupHeli(PatrolHelicopter heli, ulong signalID = 0, int FirstHeliSpawned = 0)
        {
            if (!Data.info.ActiveHelis.ContainsKey(heli.net.ID.Value)) Data.info.ActiveHelis.Add(heli.net.ID.Value, boolChecks.IsHeliSignalHeli);

            boolChecks.HeliIsOut = true;

            if (Data.info.ActiveHelis[heli.net.ID.Value] && config.Settings.destroyHeliSignal) return;
            
            if (boolChecks.FirstHeliSpawned == 0) StartTimer();

            boolChecks.FirstHeliSpawned++;
        }

        private void StartTimer()
        {
            if (boolChecks.TimerStarted) reminderMessage.Destroy();

            boolChecks.TimerStarted = true;

            NotifyServer(config.NotifSetting.NotifyPosMsgID, config.NotifSetting.ToastPosMsgID, lang.GetMessage("heli_announcement", this), config.NotifSetting.ToastDur);

            reminderMessage = timer.Every(config.Settings.AnnounceTime, () =>
            {
                NotifyServer(config.NotifSetting.NotifyPosMsgID, config.NotifSetting.ToastPosMsgID, lang.GetMessage("heli_announcement", this) + lang.GetMessage("pending_votes", this), config.NotifSetting.ToastDur);
            });
        }

        private void KillHelis()
        {
            for (int i = Data.info.ActiveHelis.Count - 1; i >= 0; i--)
            {
                var heliEntity = (BaseEntity)BaseNetworkable.serverEntities.Find(new NetworkableId(Data.info.ActiveHelis.Keys.ToList()[i]));

                if (heliEntity != null && !Data.info.ActiveHelis.Values.ToList()[i])
                    heliEntity.Kill();
            }

            Data.info.ActiveHelis.Clear();

            NotifyServer(config.NotifSetting.NotifyPosMsgID, config.NotifSetting.ToastPosMsgID, lang.GetMessage("heli_killed", this), config.NotifSetting.ToastDur);

            votingProperties.votes = 0;

            boolChecks.HeliIsOut = false;

            if (boolChecks.TimerStarted)
            {
                reminderMessage.Destroy();
                boolChecks.TimerStarted = false;
            }

            Data.info.PlayerVotes.Clear();
        }
        #endregion

        #region Messages
        private void NotifyPlayer(IPlayer iPlayer, int? msgNum, string toastId, string message, float? duration)
        {
            var isServer = iPlayer.IsServer;
            var player = iPlayer.Object as BasePlayer;

            var percentage = config.Settings.PercentVotesRequired / 100f;
            var totalVoters = (int)(votingProperties.activePlayersCount * percentage);

            if (totalVoters == 0) totalVoters = 1;

            if (config.UseMessage.UseToastify && Toastify != null)
                Toastify?.CallHook("SendToast", player, toastId, null, string.Format(message, votingProperties.votes, totalVoters, config.cmdPerms.VoteCommand), duration);

            if (config.UseMessage.UseNotify && Notify != null)
                Notify?.CallHook("SendNotify", player, msgNum, string.Format(message, votingProperties.votes, totalVoters, config.cmdPerms.VoteCommand));

            if (config.UseMessage.UseChat || (config.UseMessage.UseToastify && Toastify == null) || (config.UseMessage.UseNotify && Notify == null))
                Player.Message(player, string.Format(message, votingProperties.votes, totalVoters, config.cmdPerms.VoteCommand));
        }

        private void NotifyServer(int? msgNum, string toastId, string message, float? duration)
        {
            var ust = config.UseMessage.UseServerToastify && Toastify != null;
            var usn = config.UseMessage.UseServerNotify && Notify != null;

            var percentage = config.Settings.PercentVotesRequired / 100f;
            var totalVoters = (int)(votingProperties.activePlayersCount * percentage);

            if (totalVoters == 0) totalVoters = 1;

            if (config.UseMessage.UseServerChat || (config.UseMessage.UseServerToastify && Toastify == null) || (config.UseMessage.UseServerNotify && Notify == null))
                Server.Broadcast(string.Format(message, votingProperties.votes, totalVoters, config.cmdPerms.VoteCommand));

            if (ust || usn)
            {
                foreach (var playerInList in BasePlayer.activePlayerList)
                {
                    if (ust)
                        Toastify?.CallHook("SendToast", playerInList, toastId, null, string.Format(message, votingProperties.votes, totalVoters, config.cmdPerms.VoteCommand), duration);

                    if (usn)
                        Notify?.CallHook("SendNotify", playerInList, msgNum, string.Format(message, votingProperties.votes, totalVoters, config.cmdPerms.VoteCommand));
                }
            }
        }
        #endregion

        #region Configuration
        private Configuration config;
        public class Configuration
        {
            public GeneralSettings Settings = new GeneralSettings()
            {
                AnnounceTime = 300,
                PercentVotesRequired = 80,
                destroyHeliSignal = true
            };

            [JsonProperty(PropertyName = "Commands & Permissions")]
            public CmdPerms cmdPerms = new CmdPerms()
            {
                UsePerm = "use",
                IsAdmin = "admin",
                VoteCommand = "voteheli",
                KillCommand = "killhelis"
            };

            [JsonProperty(PropertyName = "Messages")]
            public UseMessages UseMessage = new UseMessages()
            {
                UseChat = true,
                UseServerChat = true,
                UseToastify = false,
                UseServerToastify = false,
                UseNotify = false,
                UseServerNotify = false
            };

            [JsonProperty(PropertyName = "Notifications")]
            public NotificationSettings NotifSetting = new NotificationSettings()
            {
                ToastPosMsgID = "success",
                ToastNegMsgID = "error",
                ToastDur = 10,
                NotifyPosMsgID = 0,
                NotifyNegMsgID = 1
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
                Puts($"Configuration file {Name}.json is invalid!\nResetting the config now!");
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
            //current version = 1.2.2
            if (config.Version >= Version) return;
            if (config.Version < new VersionNumber(1, 2, 2))
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
        public BannedPlayers bannedPlayers = new BannedPlayers();

        public class PluginData
        {
            public DataVariables info = new DataVariables()
            {
                ActiveHelis = new Dictionary<ulong, bool>(),
                PlayerVotes = new HashSet<string>()
            };
        }

        public class BannedPlayers
        {
            public BannedPlayerList playerList = new BannedPlayerList()
            {
                players = new HashSet<string> { "SteamID 1", "SteamID 2" }
            };
        }

        private void LoadData()
        {
            try
            {
                Data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>($"{Name}/GeneralData");
                bannedPlayers = Interface.Oxide.DataFileSystem.ReadObject<BannedPlayers>($"{Name}/BannedPlayers");
            }
            catch (Exception)
            {
                Data = new PluginData();
                bannedPlayers = new BannedPlayers();
            }
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/GeneralData", Data);
        }

        void OnNewSave()
        {
            Data = new PluginData();
        }
        #endregion

        #region LangFile
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                ["Variables"] = "{0} = Players Voted | {1} = Players Active | {2} = Vote Command",
                ["player_banned"] = "You are not allowed to vote!",
                ["heli_announcement"] = "A Patrol Helicopter has appeared!\nCast your vote with <color=#87a3ff>/{2}</color> to kill it!",
                ["pending_votes"] = "\nWe still need {0}/{1} votes!",
                ["heli_not_out"] = "Patrol Heli is not out!",
                ["invalid_syntax_toggle"] = "<color=red>Invalid Syntax!</color>\n<color=orange>Cmd</color>: /khv <enable | disable>",
                ["heli_killed"] = "All Patrol Helis have been <color=red>KILLED</color>!!",
                ["player_voted"] = "You have already voted!\nWe are still waiting for {0}/{1} players to vote!",
                ["player_vote"] = "You have voted!\nWe are still waiting for {0}/{1} players to vote!"
            }, this);
        }
        #endregion
    }

    #region Classes
    public class GeneralSettings
    {
        [JsonProperty(PropertyName = "Time to announce heli vote's [in seconds]")]
        public int AnnounceTime;

        [JsonProperty(PropertyName = "Percentage of vote's required [0 - 100]")]
        public float PercentVotesRequired;

        [JsonProperty(PropertyName = "Destroy HeliSignal Patrol Helicopters?")]
        public bool destroyHeliSignal;
    }

    public class CmdPerms
    {
        [JsonProperty(PropertyName = "Heli vote Command")]
        public string VoteCommand;

        [JsonProperty(PropertyName = "Heli kill command [Requires admin perm]")]
        public string KillCommand;

        [JsonProperty(PropertyName = "Voting Permission")]
        public string UsePerm;

        [JsonProperty(PropertyName = "Admin Permission")]
        public string IsAdmin;
    }

    public class UseMessages
    {
        [JsonProperty(PropertyName = "Use Chat Messages?")]
        public bool UseChat;

        [JsonProperty(PropertyName = "Use chat for Server Messages?")]
        public bool UseServerChat;

        [JsonProperty(PropertyName = "Use Toastify Messages?")]
        public bool UseToastify;

        [JsonProperty(PropertyName = "Use Toastify for Server Messages?")]
        public bool UseServerToastify;

        [JsonProperty(PropertyName = "Use Notify Messages?")]
        public bool UseNotify;

        [JsonProperty(PropertyName = "Use Notify for Server Messages?")]
        public bool UseServerNotify;
    }

    public class NotificationSettings
    {
        [JsonProperty(PropertyName = "Toastify Positive Msg ID")]
        public string ToastPosMsgID;

        [JsonProperty(PropertyName = "Toastify Negative Msg ID")]
        public string ToastNegMsgID;

        [JsonProperty(PropertyName = "Toastify Msg Duration")]
        public float ToastDur;

        [JsonProperty(PropertyName = "Notify Positive Msg ID")]
        public int NotifyPosMsgID;

        [JsonProperty(PropertyName = "Notify Negative Msg ID")]
        public int NotifyNegMsgID;
    }

    public class BannedPlayerList
    {
        public HashSet<string> players;
    }

    public class DataVariables
    {
        public Dictionary<ulong, bool> ActiveHelis;
        public HashSet<string> PlayerVotes;
    }

    public class VotingProperties
    {
        public double votes = 0;
        public float activePlayersCount = 0;
    }

    public class BoolChecks
    {
        public bool HeliIsOut = false;
        public bool TimerStarted = false;
        public bool IsHeliSignalHeli = false;
        public int FirstHeliSpawned = 0;
    }
    #endregion
}