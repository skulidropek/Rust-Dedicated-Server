using Oxide.Core.Plugins;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Unlock All Blueprints", "RustGPT", "1.2.0")]
    [Description("Automatically unlocks all blueprints for players when they join the server and provides a chat command to do the same")]

    public class UnlockAllBlueprints : RustPlugin
    {
        private const string PermissionUnlockAll = "unlockallblueprints.use";
        private const string PermissionLockAll = "unlockallblueprints.admin";

        private void Init()
        {
            // Register the permissions
            permission.RegisterPermission(PermissionUnlockAll, this);
            permission.RegisterPermission(PermissionLockAll, this);
        }

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
            if (!permission.UserHasPermission(player.UserIDString, PermissionUnlockAll))
            {
                player.ChatMessage("You do not have permission to use this command.");
                return;
            }

            UnlockBlueprintsForPlayer(player);
            player.ChatMessage("You have successfully unlocked all blueprints.");
        }

        [ChatCommand("lockall")]
        private void LockAllBlueprintsCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermissionLockAll))
            {
                player.ChatMessage("You do not have permission to use this command.");
                return;
            }

            LockBlueprintsForPlayer(player);
            player.ChatMessage("You have successfully locked all blueprints.");
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
