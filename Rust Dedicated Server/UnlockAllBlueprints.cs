/// <summary>
/// Unlock All Blueprints Plugin
/// 
/// This plugin provides functionality to automatically unlock all blueprints for players when they join the server.
/// Additionally, it offers chat commands for players (or admins) to unlock or lock all blueprints manually.
///
/// Permissions:
/// - `unlockallblueprints.use`: Allows a player to use the /unlockall command to unlock all blueprints.
/// - `unlockallblueprints.admin`: Allows a player to use the /lockall command to lock all blueprints.
///
/// Commands:
/// - `/unlockall`: Unlocks all blueprints for the player who uses the command. Requires the `unlockallblueprints.use` permission.
/// - `/lockall`: Locks all blueprints for the player who uses the command. Requires the `unlockallblueprints.admin` permission.
///
/// Example Usage:
/// 1. Grant permission to a player to unlock blueprints:
///    `oxide.grant user <username or steamid> unlockallblueprints.use`
///
/// 2. Grant admin permission to lock blueprints:
///    `oxide.grant user <username or steamid> unlockallblueprints.admin`
///
/// 3. Player uses `/unlockall` in chat to unlock all blueprints.
/// 4. Admin uses `/lockall` in chat to lock all blueprints.
///
/// </summary>
/// 
namespace Oxide.Plugins
{
    [Info("Unlock All Blueprints", "RustGPT", "1.5.0")]
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
                ManageBlueprints(player, unlock: true);
            }
        }

        /// <summary>
        /// Called when a player connects to the server.
        /// Automatically unlocks all blueprints for the connected player.
        /// </summary>
        /// <param name="player">The player who has connected.</param>
        private void OnPlayerConnected(BasePlayer player)
        {
            ManageBlueprints(player, unlock: true);
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
            ManageBlueprints(player, unlock: true);
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
            ManageBlueprints(player, unlock: false);
        }

        /// <summary>
        /// Manages the unlocking or locking of blueprints for the specified player.
        /// Checks the player's permissions before executing the action.
        /// </summary>
        /// <param name="player">The player whose blueprints will be managed.</param>
        /// <param name="unlock">True to unlock blueprints, false to lock them.</param>
        private void ManageBlueprints(BasePlayer player, bool unlock)
        {
            string permission = unlock ? PermissionUnlockAll : PermissionLockAll;

            if (!this.permission.UserHasPermission(player.UserIDString, permission))
            {
                player.ChatMessage($"You do not have permission to {(unlock ? "unlock" : "lock")} blueprints.");
                return;
            }

            var blueprints = player.GetComponent<PlayerBlueprints>();
            if (blueprints == null)
            {
                player.ChatMessage("Failed to access your blueprints.");
                return;
            }

            if (unlock)
            {
                blueprints.UnlockAll();
                player.ChatMessage("All blueprints have been unlocked.");
            }
            else
            {
                blueprints.Reset();
                player.ChatMessage("All blueprints have been locked.");
            }
        }
    }
}
