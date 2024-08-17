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

        /// <summary>
        /// Called when the plugin is initialized.
        /// Registers permissions required for using commands.
        /// </summary>
        private void Init()
        {
            // Register the permissions
            permission.RegisterPermission(PermissionUnlockAll, this);
            permission.RegisterPermission(PermissionLockAll, this);
        }

        /// <summary>
        /// Called when the server is initialized.
        /// Unlocks all blueprints for currently active players.
        /// </summary>
        private void OnServerInitialized()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                UnlockBlueprintsForPlayer(player);
            }
        }

        /// <summary>
        /// Called when a player connects to the server.
        /// Automatically unlocks all blueprints for the connected player.
        /// </summary>
        /// <param name="player">The player who has connected.</param>
        private void OnPlayerConnected(BasePlayer player)
        {
            UnlockBlueprintsForPlayer(player);
        }

        /// <summary>
        /// Chat command for unlocking all blueprints for the player who uses the command.
        /// Requires the player to have the appropriate permission.
        /// </summary>
        /// <param name="player">The player who used the command.</param>
        /// <param name="command">The command name.</param>
        /// <param name="args">Any arguments passed with the command.</param>
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

        /// <summary>
        /// Chat command for locking all blueprints for the player who uses the command.
        /// Requires the player to have admin permission.
        /// </summary>
        /// <param name="player">The player who used the command.</param>
        /// <param name="command">The command name.</param>
        /// <param name="args">Any arguments passed with the command.</param>
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

        /// <summary>
        /// Unlocks all blueprints for the specified player.
        /// </summary>
        /// <param name="player">The player whose blueprints will be unlocked.</param>
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

        /// <summary>
        /// Locks all blueprints for the specified player.
        /// </summary>
        /// <param name="player">The player whose blueprints will be locked.</param>
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
