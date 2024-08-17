using Oxide.Core.Plugins;
using Oxide.Core;
using System.Collections.Generic;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("Unlock All Blueprints", "RustGPT", "1.0.0")]
    [Description("Unlocks all blueprints for the player")]

    public class UnlockAllBlueprints : RustPlugin
    {
        [ChatCommand("unlockall")]
        private void UnlockAllBlueprintsCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                player.ChatMessage("You don't have permission to use this command.");
                return;
            }

            var itemList = ItemManager.itemList;
            int unlockedCount = 0;

            foreach (var itemDefinition in itemList)
            {
                if (itemDefinition != null && itemDefinition.Blueprint != null && !player.blueprints.HasUnlocked(itemDefinition))
                {
                    player.blueprints.Unlock(itemDefinition);
                    unlockedCount++;
                }
            }

            player.ChatMessage($"You have successfully unlocked {unlockedCount} blueprints.");
        }
    }
}
