using Oxide.Core.Plugins;
using Oxide.Core;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("Unlock All Blueprints", "YourName", "1.0.3")]
    [Description("Allows players to learn all blueprints quickly with one command")]

    public class UnlockAllBlueprints : RustPlugin
    {
        [ChatCommand("unlockall")]
        private void UnlockAllBlueprintsCommand(BasePlayer player, string command, string[] args)
        {
            var blueprints = player.GetComponent<PlayerBlueprints>();
            if (blueprints != null)
            {
                blueprints.UnlockAll();
                player.ChatMessage("You have successfully learned all blueprints.");
            }
            else
            {
                player.ChatMessage("Failed to access your blueprints.");
            }
        }
    }
}
