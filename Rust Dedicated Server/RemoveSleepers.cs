using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("RemoveSleepers", "RustGPT", "1.0.8")]
    [Description("Removes sleepers from the visible world, but keeps their position and detailed inventory data for respawn.")]

    public class RemoveSleepers : CovalencePlugin
    {
        private Dictionary<string, PlayerData> playerData = new Dictionary<string, PlayerData>();

        private class PlayerData
        {
            public Vector3 Position { get; set; }
            public List<SerializedItem> Inventory { get; set; } = new List<SerializedItem>();
        }

        private class SerializedItem
        {
            public int ItemID { get; set; }
            public int Amount { get; set; }
            public string Name { get; set; }
            public int Container { get; set; } // 0 = Main, 1 = Belt, 2 = Wear
            public ulong SkinID { get; set; } // ID скина предмета
            public string CustomName { get; set; } // Кастомное название предмета
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            var data = new PlayerData
            {
                Position = player.transform.position
            };

            // Сохранение предметов из основного контейнера
            SaveItemsFromContainer(player.inventory.containerMain.itemList, data, 0);

            // Сохранение предметов из пояса
            SaveItemsFromContainer(player.inventory.containerBelt.itemList, data, 1);

            // Сохранение предметов из контейнера снаряжения
            SaveItemsFromContainer(player.inventory.containerWear.itemList, data, 2);

            playerData[player.UserIDString] = data;

            // Перемещаем слипера под карту
            player.transform.position = new Vector3(0, -1000, 0);

            Puts($"Removed sleeper for player {player.displayName} ({player.UserIDString}) and saved their data.");
        }

        private void SaveItemsFromContainer(List<Item> itemList, PlayerData data, int containerType)
        {
            foreach (var item in itemList)
            {
                data.Inventory.Add(new SerializedItem
                {
                    ItemID = item.info.itemid,
                    Amount = item.amount,
                    Name = item.info.displayName.english,
                    Container = containerType,
                    SkinID = item.skin, // Сохранение ID скина
                    CustomName = item.name // Сохранение кастомного названия предмета
                });
            }
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            if (player.IsSleeping())
            {
                // Проверяем, есть ли сохраненные данные для игрока
                if (playerData.TryGetValue(player.UserIDString, out PlayerData data))
                {
                    // Восстанавливаем позицию игрока
                    player.Teleport(data.Position);

                    // Восстанавливаем инвентарь игрока
                    player.inventory.Strip();
                    RestoreItemsToContainer(player.inventory.containerMain, data.Inventory, 0);
                    RestoreItemsToContainer(player.inventory.containerBelt, data.Inventory, 1);
                    RestoreItemsToContainer(player.inventory.containerWear, data.Inventory, 2);

                    // Завершаем сон игрока
                    player.EndSleeping();

                    Puts($"Player {player.displayName} ({player.UserIDString}) respawned and their data was restored.");
                }
                else
                {
                    Puts($"No saved data found for player {player.displayName} ({player.UserIDString}).");
                }
            }
        }

        private void RestoreItemsToContainer(ItemContainer container, List<SerializedItem> items, int containerType)
        {
            foreach (var item in items)
            {
                if (item.Container == containerType)
                {
                    var newItem = ItemManager.CreateByItemID(item.ItemID, item.Amount, item.SkinID);
                    if (!string.IsNullOrEmpty(item.CustomName))
                    {
                        newItem.name = item.CustomName; // Восстановление кастомного названия предмета
                    }
                    container.GiveItem(newItem);
                }
            }
        }

        private void OnServerSave()
        {
            // Сохраняем данные игроков в файл
            Interface.Oxide.DataFileSystem.WriteObject("RemoveSleepersData", playerData);
        }

        private void OnServerInitialized()
        {
            // Загружаем данные игроков из файла
            playerData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, PlayerData>>("RemoveSleepersData");
        }
    }
}
