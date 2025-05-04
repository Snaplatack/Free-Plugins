using Oxide.Core;
using Facepunch;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Bush Trimmer", "Snaplatack", "1.0.1")]
    [Description("Admins can remove bushes with a command")]
    class BushTrimmer : RustPlugin
    {
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

            float radius;

            if (args.Length > 0)
                float.TryParse(args[0], out radius);
            else
                radius = config.settings.radiusToDetect;

            player.SendConsoleCommand($"debug.clear_bushes {radius}");
            Player.Message(player, $"Removing bushes within {radius}m of your position...");
        }
        #endregion

        #region Configuration
        public Configuration config;
        public class Configuration
        {
            [JsonProperty(PropertyName = "General Settings")]
            public BTPluginSettings settings = new BTPluginSettings
            {
                radiusToDetect = 1.2f
            };

            [JsonProperty(PropertyName = "Permissions & Commands")]
            public BTPermsCommands commandPerm = new BTPermsCommands
            {
                AllowTrim = "bushtrimmer.allow",
                TrimBushCommand = "trimbush"
            };

            public Core.VersionNumber Version = new Core.VersionNumber(0, 0, 0);
        }

        protected override void LoadDefaultConfig() => config = new Configuration();
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

            if (config.Version < new VersionNumber(1, 0, 1))
            {
                LoadDefaultConfig();
                var configUpdateStr = "[CONFIG UPDATE] Updating to Version {0}";
                PrintWarning(string.Format(configUpdateStr, Version));
                config.Version = this.Version;

                SaveConfig();
            }
        }
        #endregion
    }

    #region Definitions
    public class BTPluginSettings
    {
        [JsonProperty(PropertyName = "Radius to detect bushes from players position [I recommend 1.2 to trim single bushes]")]
        public float radiusToDetect;
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
