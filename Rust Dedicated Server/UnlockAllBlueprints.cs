using Oxide.Core.Plugins;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Unlock All Blueprints", "RustGPT", "1.1.0")]
    [Description("Automatically unlocks all blueprints for players when they join the server and provides a chat command to do the same")]

    public class UnlockAllBlueprints : RustPlugin
    {
        private void OnServerInitialized()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                UnlockBlueprintsForPlayer(player);
            }
        }

        // Срабатывает при подключении игрока
        private void OnPlayerConnected(BasePlayer player)
        {
            UnlockBlueprintsForPlayer(player);
        }

        [ChatCommand("unlockall")]
        private void UnlockAllBlueprintsCommand(BasePlayer player, string command, string[] args)
        {
            UnlockBlueprintsForPlayer(player);
            player.ChatMessage("You have successfully unlocked all blueprints.");
        }

        [ChatCommand("lockall")]
        private void LockAllBlueprintsCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
                return;

            LockBlueprintsForPlayer(player);
        }

        // Метод для разблокировки чертежей
        private void UnlockBlueprintsForPlayer(BasePlayer player)
        {
            var blueprints = player.GetComponent<PlayerBlueprints>();
            if (blueprints != null)
            {
                blueprints.UnlockAll();
                player.ChatMessage("All blueprints have been unlocked automatically.");
            }
            else
            {
                player.ChatMessage("Failed to access your blueprints.");
            }
        }

        // Метод для блокировки чертежей
        private void LockBlueprintsForPlayer(BasePlayer player)
        {
            var blueprints = player.GetComponent<PlayerBlueprints>();
            if (blueprints != null)
            {
                blueprints.Reset();
                player.ChatMessage("All blueprints have been locked automatically.");
            }
            else
            {
                player.ChatMessage("Failed to access your blueprints.");
            }
        }
    }
}
