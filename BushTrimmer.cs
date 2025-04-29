using Oxide.Core;
using Facepunch;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Bush Trimmer", "Snaplatack", "1.0.0")]
    [Description("Players can remove bushes with a command")]
    class BushTrimmer : CovalencePlugin
    {
        private static int layers = (int)(Rust.Layers.Mask.Terrain | Rust.Layers.Mask.Bush);

        #region Initialize
        private void Init()
        {
            AddCovalenceCommand(config.commandPerm.TrimBushCommand, nameof(ClearBushCmd), config.commandPerm.AllowTrim);
        }
        #endregion

        #region Commands
        private void ClearBushCmd(IPlayer iPlayer, string command, string[] args)
        {
            BasePlayer player = iPlayer.Object as BasePlayer;

            if (!Physics.Raycast(player.eyes.HeadRay(), out RaycastHit hit, config.settings.distanceToDetect, layers))
            {
                return;
            }

            PooledList<BushEntity> nearbyEntities = Pool.Get<PooledList<BushEntity>>();

            Vis.Entities<BushEntity>(hit.point, config.settings.radiusToDetect, nearbyEntities, layers);

            TrimEntity(iPlayer, nearbyEntities);
        }
        #endregion

        #region Methods
        private void TrimEntity(IPlayer iPlayer, PooledList<BushEntity> nearbyEntities)
        {
            foreach (var bush in nearbyEntities)
            {
                if (bush == null || bush.IsDestroyed) continue;
                if (!bush.ShortPrefabName.Contains("bush")) continue;

                if (bush != null && config.settings.reply) iPlayer.Reply("Trimmed bush!");
                bush.Kill();
                break;
            }

            Pool.Free(ref nearbyEntities);
        }


        #endregion

        #region Configuration
        public Configuration config;
        public class Configuration
        {
            [JsonProperty(PropertyName = "General Settings")]
            public BTPluginSettings settings = new BTPluginSettings
            {
                reply = true,
                distanceToDetect = 5.0f,
                radiusToDetect = 1.0f
            };

            [JsonProperty(PropertyName = "Permissions & Commands")]
            public BTPermsCommands commandPerm = new BTPermsCommands
            {
                AllowTrim = "bushtrimmer.allow",
                TrimBushCommand = "trimbush"
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
            }

            var configUpdateStr = "[CONFIG UPDATE] Updating to Version {0}";
            PrintWarning(string.Format(configUpdateStr, Version));
            config.Version = this.Version;

            SaveConfig();
        }
        #endregion

    }

    #region Definitions
    public class BTPluginSettings
    {
        [JsonProperty(PropertyName = "Reply with a message when trimming a bush")]
        public bool reply;

        [JsonProperty(PropertyName = "Radius to detect bushes from line of sight [I recommend 1.0 to trim single bushes]")]
        public float radiusToDetect;

        [JsonProperty(PropertyName = "Distant to detect bushes from line of sight [5.0 is default]")]
        public float distanceToDetect;
    }

    public class BTPermsCommands
    {
        [JsonProperty(PropertyName = "Permission to allow trimming bushes")]
        public string AllowTrim;

        [JsonProperty(PropertyName = "Command to trim bushes")]
        public string TrimBushCommand;
    }
    #endregion
}
