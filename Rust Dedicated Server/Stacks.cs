using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Stacks", "RustGPT", "2.0.4")]
    public class Stacks : RustPlugin
    {
        [PluginReference] private Plugin FurnaceSplitter;
        private static Dictionary<string, int> itemStackSizes = new Dictionary<string, int>();

        private static Dictionary<string, int> specialItemCategories = new Dictionary<string, int>()
            {{"Attire", 2}, {"Tool", 1}, {"Weapon", 1}};

        private void Init()
        {
            LoadConfiguration();
            bool configChanged = false;

            if (config.Settings == null)
            {
                config.Settings = new List<string>();
                configChanged = true;
            }

            if (config.BlacklistedItems == null)
            {
                config.BlacklistedItems = new List<string>() { "Blue Keycard", "Green Keycard", "Red Keycard" };
                configChanged = true;
            }

            if (config.ExcludedSkins == null)
            {
                config.ExcludedSkins = new List<ulong>();
                configChanged = true;
            }

            if (config.ExcludedItems == null)
            {
                config.ExcludedItems = new List<string>();
                configChanged = true;
            }

            if (configChanged)
            {
                SaveConfig();
            }
        }

        private void OnServerInitialized()
        {
            var items = ItemManager.itemList?.ToList();
            if (items == null)
            {
                Puts("Error: ItemManager.itemList is null");
                return;
            }

            if (config == null)
            {
                Puts("Error: Config is null");
                return;
            }

            if (config.ItemStackSizes == null)
            {
                Puts("Error: Config.ItemStackSizes is null");
                return;
            }

            List<string> newItems = new List<string>();
            List<string> removedItems = new List<string>();

            foreach (var item in items.OrderBy(r => r.category))
            {
                if (item == null)
                    continue;

                var category = item.category.ToString();
                var itemName = item.displayName?.english;

                if (category == null || itemName == null)
                    continue;

                if (!config.ItemStackSizes.ContainsKey(category))
                {
                    config.ItemStackSizes.Add(category, new Dictionary<string, int> { { itemName, item.stackable } });
                    newItems.Add($"'{itemName}' в категории '{category}'");
                }
                else if (!config.ItemStackSizes[category].ContainsKey(itemName))
                {
                    config.ItemStackSizes[category].Add(itemName, item.stackable);
                    newItems.Add($"'{itemName}' в категории '{category}'");
                }

                if (!itemStackSizes.ContainsKey(category + "|" + itemName))
                    itemStackSizes.Add(category + "|" + itemName, item.stackable);

                item.stackable = config.ItemStackSizes[category][itemName];
            }

            foreach (var category in config.ItemStackSizes.Keys.ToList())
            {
                foreach (var item in config.ItemStackSizes[category].ToDictionary(x => x.Key, x => x.Value))
                {
                    if (!items.Exists(x => x.displayName.english == item.Key && x.category.ToString() == category))
                    {
                        config.ItemStackSizes[category].Remove(item.Key);
                        removedItems.Add($"'{item.Key}' из категории '{category}'");
                    }
                }

                if (config.ItemStackSizes[category].Count == 0)
                    config.ItemStackSizes.Remove(category);
            }

            if (newItems.Count > 0 || removedItems.Count > 0)
            {
                SaveConfig();

                if (newItems.Count > 0)
                {
                    Puts("В конфигурационный файл были добавлены новые предметы:");
                    foreach (var item in newItems) Puts(item);
                }

                if (removedItems.Count > 0)
                {
                    Puts("Конфигурационный файл был очищен от устаревших предметов:");
                    foreach (var item in removedItems) Puts(item);
                }
            }
        }

        private void Unload()
        {
            foreach (var item in ItemManager.itemList)
                item.stackable = itemStackSizes[item.category.ToString() + "|" + item.displayName.english];
        }

        private ItemContainer.CanAcceptResult? CanAcceptItem(ItemContainer container, Item item, int targetPos)
        {
            if (container == null || item == null || container.playerOwner == null) return null;
            if (config.Settings.Contains(item.info.displayName.english) || config.Settings.Contains(item.info.shortname))
                return null;
            if (container.playerOwner.inventory.containerBelt == container)
            {
                if (IsItemWithCondition(item.info) &&
                    ((config.BlacklistedItems.Contains(item.info.displayName.english) ||
                      config.BlacklistedItems.Contains(item.info.shortname)) ||
                     specialItemCategories.ContainsKey(item.info.category.ToString()) &&
                     specialItemCategories[item.info.category.ToString()] == 1))
                {
                    if (item.amount > 1 || CanStackItemInContainer(container, item, targetPos))
                        return ItemContainer.CanAcceptResult.CannotAccept;
                }
            }

            if (container.playerOwner.inventory.containerWear == container)
            {
                if ((config.BlacklistedItems.Contains(item.info.displayName.english) ||
                     config.BlacklistedItems.Contains(item.info.shortname)) ||
                    specialItemCategories.ContainsKey(item.info.category.ToString()) &&
                    specialItemCategories[item.info.category.ToString()] == 2)
                {
                    if (item.amount > 1 || CanStackItemInContainer(container, item, targetPos))
                        return ItemContainer.CanAcceptResult.CannotAccept;
                }
            }

            return null;
        }

        private bool? CanStackItem(Item item1, Item item2)
        {
            if (config.ExcludedSkins.Contains(item1.skin) || config.ExcludedSkins.Contains(item2.skin)) return null;
            if (config.ExcludedItems.Contains(item1.info.displayName.english) || config.ExcludedItems.Contains(item2.info.displayName.english))
                return null;
            if (config.ExcludedItems.Contains(item1.info.shortname) || config.ExcludedItems.Contains(item2.info.shortname))
                return null;
            if (item1 == item2) return false;
            if (item1.info.stackable <= 1 || item2.info.stackable <= 1) return false;
            if (item1.info.itemid != item2.info.itemid) return false;
            if ((item1.hasCondition || item2.hasCondition) && item1.condition != item2.condition) return false;
            if (item1.skin != item2.skin) return false;
            if (item1.name != item2.name) return false;
            if (!item1.IsValid()) return false;
            if (item1.IsBlueprint() && item1.blueprintTarget != item2.blueprintTarget) return false;
            return true;
        }

        private bool? CanCombineDroppedItem(DroppedItem droppedItem, DroppedItem anotherDroppedItem)
        {
            var item = droppedItem.item;
            var anotherItem = anotherDroppedItem.item;
            return CanStackItem(item, anotherItem) == false ? false : (bool?)null;
        }

        private Item OnItemSplit(Item item, int amount)
        {
            if (config.ExcludedSkins.Contains(item.skin))
                return null;
            if (config.ExcludedItems.Contains(item.info.displayName.english)) return null;
            if (config.ExcludedItems.Contains(item.info.shortname)) return null;
            item.amount -= amount;
            Item newItem = ItemManager.CreateByItemID(item.info.itemid, 1, item.skin);
            newItem.amount = amount;
            newItem.condition = item.condition;
            newItem.name = item.name;
            if (item.IsBlueprint())
                newItem.blueprintTarget = item.blueprintTarget;
            item.MarkDirty();
            return newItem;
        }

        private object CanMoveItem(Item item, PlayerInventory inventory, ItemContainerId containerId, int slot, int maxMove)
        {
            if (item == null || inventory == null || item.amount < ushort.MaxValue || !config.AllowStackOver64K)
                return null;
            ItemContainer container = inventory.FindContainer(containerId);
            if (container == null) return null;
            ItemContainer mainContainer = inventory.GetContainer(PlayerInventory.Type.Main);
            BasePlayer player = mainContainer?.GetOwnerPlayer();
            if (player != null && FurnaceSplitter != null)
            {
                bool valid = true;
                bool enabled = false;
                bool hasPermission = true;
                try
                {
                    enabled = (bool)FurnaceSplitter?.CallHook("GetEnabled", player);
                    hasPermission = (bool)FurnaceSplitter?.CallHook("HasPermission", player);
                }
                catch
                {
                    valid = false;
                }

                if (valid && enabled && hasPermission)
                {
                    BaseEntity entity = container.entityOwner;
                    if (entity is BaseOven && (entity as BaseOven).inventory.capacity > 1) return null;
                }
            }

            bool splitItemFlag = false;
            int maxStackSize = config.ItemStackSizes[item.info.category.ToString()][item.info.displayName.english];
            if (item.amount > maxStackSize) splitItemFlag = true;
            if (maxMove + item.amount / ushort.MaxValue == item.amount % ushort.MaxValue)
            {
                if (splitItemFlag)
                {
                    Item splitItemA = item.SplitItem(maxStackSize);
                    if (!splitItemA.MoveToContainer(container, slot, true))
                    {
                        item.amount += splitItemA.amount;
                        splitItemA.Remove(0f);
                    }

                    ItemManager.DoRemoves();
                    inventory.ServerUpdate(0f);
                    return true;
                }

                item.MoveToContainer(container, slot, true);
                return true;
            }
            else if (maxMove + (item.amount / 2) / ushort.MaxValue == (item.amount / 2) % ushort.MaxValue + item.amount % 2)
            {
                if (splitItemFlag)
                {
                    Item splitItemB;
                    if (maxStackSize > item.amount / 2)
                        splitItemB = item.SplitItem(Convert.ToInt32(item.amount) / 2);
                    else
                        splitItemB = item.SplitItem(maxStackSize);
                    if (!splitItemB.MoveToContainer(container, slot, true))
                    {
                        item.amount += splitItemB.amount;
                        splitItemB.Remove(0f);
                    }

                    ItemManager.DoRemoves();
                    inventory.ServerUpdate(0f);
                    return true;
                }

                Item splitItemC = item.SplitItem(item.amount / 2);
                if ((item.amount + splitItemC.amount) % 2 != 0)
                {
                    splitItemC.amount++;
                    item.amount--;
                }

                if (!splitItemC.MoveToContainer(container, slot, true))
                {
                    item.amount += splitItemC.amount;
                    splitItemC.Remove(0f);
                }

                ItemManager.DoRemoves();
                inventory.ServerUpdate(0f);
                return true;
            }

            return null;
        }

        private static bool IsItemWithCondition(ItemDefinition itemDefinition)
        {
            if (itemDefinition == null) return false;
            return itemDefinition.condition.enabled && itemDefinition.condition.max > 0f;
        }

        private bool CanStackItemInContainer(ItemContainer container, Item item, int targetPos)
        {
            foreach (var currentItem in container.itemList.Where(x => x != null && (targetPos == -1 || targetPos == x.position)))
            {
                if (CanStackItem(currentItem, item) == true) return true;
            }

            return false;
        }

        private Dictionary<string, Dictionary<string, int>> GenerateItemStackSizes()
        {
            var stackSizes = new Dictionary<string, Dictionary<string, int>>();
            var currentCategory = ItemCategory.Weapon;
            var itemsInCategory = new Dictionary<string, int>();
            foreach (var item in ItemManager.itemList.OrderBy(r => r.category))
            {
                if (currentCategory != item.category && itemsInCategory.Count > 0)
                {
                    stackSizes.Add($"{currentCategory}", itemsInCategory.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value));
                    itemsInCategory.Clear();
                }

                if (!itemsInCategory.ContainsKey(item.displayName.english))
                    itemsInCategory.Add(item.displayName.english, item.stackable);
                currentCategory = item.category;
            }

            if (itemsInCategory.Count > 0)
                stackSizes.Add($"{currentCategory}", new Dictionary<string, int>(itemsInCategory));
            return stackSizes.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value);
        }

        private static ConfigData config;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Разрешить корректный перенос стаков больше 64К")]
            public bool AllowStackOver64K;

            [JsonProperty(PropertyName = "Стаки предметов по категориям")]
            public Dictionary<string, Dictionary<string, int>> ItemStackSizes;

            [JsonProperty(PropertyName = "Предметы которым принудительно разрешено стакаться в слотах быстрого доступа")]
            public List<string> Settings;

            [JsonProperty(PropertyName = "Предметы которым принудительно запрещено стакаться в слотах быстрого доступа")]
            public List<string> BlacklistedItems;

            [JsonProperty(PropertyName = "Скины предметов которые не нужно обрабатывать плагином при стаке и разделении (для исключения конфликтов)")]
            public List<ulong> ExcludedSkins;

            [JsonProperty(PropertyName = "Названия предметов которые не нужно обрабатывать плагином при стаке и разделении (для исключения конфликтов)")]
            public List<string> ExcludedItems;
        }

        private void LoadConfiguration() =>
            config = Config.ReadObject<ConfigData>();

        protected override void LoadDefaultConfig()
        {
            if (ItemManager.itemList == null)
            {
                timer.Once(5f, () => LoadDefaultConfig());
                return;
            }

            config = new ConfigData
            {
                AllowStackOver64K = true,
                ItemStackSizes = GenerateItemStackSizes(),
                Settings = new List<string>(),
                BlacklistedItems = new List<string> { "Blue Keycard", "Green Keycard", "Red Keycard" },
                ExcludedSkins = new List<ulong>(),
                ExcludedItems = new List<string>()
            };
            SaveConfig();
            timer.Once(0.1f, () => SaveConfig());
        }

        private void SaveConfig() =>
            Config.WriteObject(config, true);
    }
}
