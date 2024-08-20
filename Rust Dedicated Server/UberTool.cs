using Oxide.Core.Plugins;
using UnityEngine;
using Oxide.Game.Rust.Cui;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("UberTool", "RustGPT", "1.0.0")]
    [Description("Custom plugin for giving a pistol with an attached laser sight that is always on, destroys building objects with raycasts, tracks and restores destroyed objects.")]

    public class UberTool : RustPlugin
    {
        private const int SemiAutoPistolID = 818877484; // ID для Semi-Automatic Pistol
        private const int CustomSkinID = 1234567890;    // ID кастомного скина
        private const int LaserSightID = -132516482;    // Правильный ID для лазерного прицела
        private const string CuiPanelName = "DestroyedObjectsUI"; // Название CUI панели

        private Dictionary<ulong, List<DestroyedEntityInfo>> destroyedEntities = new Dictionary<ulong, List<DestroyedEntityInfo>>();

        public class DestroyedEntityInfo
        {
            public string PrefabName { get; set; }
            public Vector3 Position { get; set; }
            public Quaternion Rotation { get; set; }
            public int Grade { get; set; } // Уровень строительства
            public ulong OwnerID { get; set; } // Идентификатор владельца
            public List<DestroyedEntityInfo> DependentEntities { get; set; } = new List<DestroyedEntityInfo>(); // Список зависимых объектов
        }

        [ChatCommand("givegun")]
        private void GiveGunCommand(BasePlayer player, string command, string[] args)
        {
            GiveCustomGun(player);
            CreateDestroyedObjectsUI(player);
        }

        private void GiveCustomGun(BasePlayer player)
        {
            var item = ItemManager.CreateByItemID(SemiAutoPistolID, 1, CustomSkinID);
            if (item == null)
            {
                LogError("Failed to create Semi-Automatic Pistol item.");
                player.ChatMessage("Ошибка при создании пистолета.");
                return;
            }

            player.GiveItem(item);

            if (item.contents == null)
            {
                LogError("Item does not have a contents container.");
                player.ChatMessage("Ошибка: пистолет не содержит контейнер модификаций.");
                return;
            }

            LogInfo($"Container capacity: {item.contents.capacity}");
            LogInfo($"Container item count: {item.contents.itemList.Count}");

            var weapon = item.GetHeldEntity() as BaseProjectile;
            if (weapon == null)
            {
                LogError("Failed to get the weapon's held entity.");
                player.ChatMessage("Ошибка при получении сущности оружия.");
                return;
            }

            if (weapon.primaryMagazine != null)
            {
                weapon.primaryMagazine.contents = 0;
            }

            var laserSight = ItemManager.CreateByItemID(LaserSightID, 1);
            if (laserSight == null)
            {
                LogError("Failed to create laser sight item.");
                player.ChatMessage("Ошибка при создании лазерного прицела.");
                return;
            }

            if (item.contents.CanAcceptItem(laserSight, -1) == ItemContainer.CanAcceptResult.CanAccept)
            {
                item.contents.AddItem(laserSight.info, 1);
                item.contents.SetFlag(ItemContainer.Flag.IsLocked, true);

                var laserEntity = laserSight.GetHeldEntity() as BaseEntity;
                if (laserEntity != null)
                {
                    EnableLaserSight(laserEntity);
                }

                LogInfo("Laser sight added to the container, and the container is locked.");
            }
            else
            {
                LogError("Failed to add laser sight to the container.");
                return;
            }

            weapon.SendNetworkUpdateImmediate();
            item.MarkDirty();

            player.ChatMessage("Вы получили кастомный Semi-Automatic Pistol с лазерным прицелом и рейкастами!");
            LogInfo($"Gave {player.displayName} a Semi-Automatic Pistol with laser sight and raycast shooting.");
        }

        private void EnableLaserSight(BaseEntity laserEntity)
        {
            var laserComponent = laserEntity.GetComponent<ProjectileWeaponMod>();
            if (laserComponent != null)
            {
                LogInfo("Laser component found. Enabling laser...");
                laserComponent.needsOnForEffects = false;

                laserEntity.SetFlag(BaseEntity.Flags.On, true);
                laserEntity.SendNetworkUpdateImmediate();
                LogInfo("Flag 'On' set to automatically enable the laser.");

                if (laserEntity.HasFlag(BaseEntity.Flags.On))
                {
                    LogInfo("Laser sight is successfully turned ON.");
                }
                else
                {
                    LogError("Failed to turn ON the laser sight.");
                }
            }
            else
            {
                LogError("Laser component not found on the entity.");
            }
        }

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (input == null || player == null)
                return;

            var heldEntity = player.GetHeldEntity() as BaseProjectile;
            if (heldEntity == null)
                return;

            var item = heldEntity.GetItem();
            if (item == null || item.skin != CustomSkinID)
                return;

            var laserEntity = item.contents?.itemList?.FirstOrDefault(x => x.info.itemid == LaserSightID)?.GetHeldEntity() as BaseEntity;
            if (laserEntity != null && !laserEntity.HasFlag(BaseEntity.Flags.On))
            {
                EnableLaserSight(laserEntity);
            }

            if (input.WasJustPressed(BUTTON.FIRE_PRIMARY))
            {
                RaycastHit hit;
                if (Physics.Raycast(player.eyes.HeadRay(), out hit, 300f))
                {
                    var hitEntity = hit.GetEntity();
                    if (hitEntity != null)
                    {
                        if (hitEntity is BuildingBlock buildingBlock)
                        {
                            var prefabPath = hitEntity.PrefabName;
                            if (!prefabPath.StartsWith("assets/"))
                            {
                                prefabPath = $"assets/{prefabPath}";
                            }

                            var entityInfo = new DestroyedEntityInfo
                            {
                                PrefabName = prefabPath,
                                Position = hitEntity.transform.position,
                                Rotation = hitEntity.transform.rotation,
                                Grade = (int)buildingBlock.grade, // Сохраняем уровень строительства
                                OwnerID = buildingBlock.OwnerID
                            };

                            // Получаем все зависимые объекты, которые разрушатся
                            var dependentEntities = GetAllDependentEntities(buildingBlock);
                            entityInfo.DependentEntities.AddRange(dependentEntities);

                            hitEntity.Kill();
                            player.ChatMessage($"Вы разрушили {prefabPath} (материал: {buildingBlock.grade}).");
                            LogInfo($"Player {player.displayName} destroyed {prefabPath} (material: {buildingBlock.grade}) with a raycast.");
                            AddDestroyedEntity(player, entityInfo);
                            UpdateDestroyedObjectsUI(player);
                        }
                        else
                        {
                            player.ChatMessage($"Вы попали в {hitEntity.ShortPrefabName}, но это не строительный элемент.");
                            LogInfo($"Player {player.displayName} hit {hitEntity.ShortPrefabName} with a raycast.");
                        }
                    }
                    else
                    {
                        player.ChatMessage("Вы попали в пустоту.");
                        LogInfo($"Player {player.displayName} fired a raycast but hit nothing.");
                    }
                }
            }
        }

        private List<DestroyedEntityInfo> GetAllDependentEntities(BuildingBlock block)
        {
            List<DestroyedEntityInfo> dependentEntities = new List<DestroyedEntityInfo>();
            Queue<BuildingBlock> blocksToCheck = new Queue<BuildingBlock>();
            blocksToCheck.Enqueue(block);

            while (blocksToCheck.Count > 0)
            {
                var currentBlock = blocksToCheck.Dequeue();
                var entitiesInProximity = new List<BaseEntity>();
                Vis.Entities(currentBlock.transform.position, 5f, entitiesInProximity);

                foreach (var entity in entitiesInProximity)
                {
                    if (entity == block) continue;

                    if (entity is BuildingBlock dependentBlock)
                    {
                        if (!dependentBlock.IsDestroyed && !IsBlockSupported(dependentBlock))
                        {
                            if (WillCollapseAfterSupportRemoved(dependentBlock))
                            {
                                dependentEntities.Add(new DestroyedEntityInfo
                                {
                                    PrefabName = dependentBlock.PrefabName,
                                    Position = dependentBlock.transform.position,
                                    Rotation = dependentBlock.transform.rotation,
                                    Grade = (int)dependentBlock.grade,
                                    OwnerID = dependentBlock.OwnerID
                                });

                                // Также проверяем зависимые блоки от этого блока
                                blocksToCheck.Enqueue(dependentBlock);
                            }
                        }
                    }
                }
            }

            return dependentEntities;
        }

        private bool IsBlockSupported(BuildingBlock block)
        {
            RaycastHit hit;
            if (Physics.Raycast(block.transform.position, Vector3.down, out hit, 2f))
            {
                if (hit.collider.GetComponent<BuildingBlock>() != null)
                {
                    return true;
                }
            }
            return false;
        }

        private bool WillCollapseAfterSupportRemoved(BuildingBlock block)
        {
            // Предполагаемое поведение, имитирующее разрушение блока при удалении поддержки
            float originalHealth = block.health;
            block.health = 0;

            List<ItemAmount> upkeepCosts = new List<ItemAmount>();

            block.CalculateUpkeepCostAmounts(upkeepCosts, 1.0f);

            bool willCollapse = block.IsDestroyed;

            block.health = originalHealth;
            block.SendNetworkUpdateImmediate();

            return willCollapse;
        }

        private void AddDestroyedEntity(BasePlayer player, DestroyedEntityInfo entityInfo)
        {
            if (!destroyedEntities.ContainsKey(player.userID))
            {
                destroyedEntities[player.userID] = new List<DestroyedEntityInfo>();
            }
            destroyedEntities[player.userID].Add(entityInfo);
        }

        private void UndoLastDestroyedEntity(BasePlayer player)
        {
            if (destroyedEntities.ContainsKey(player.userID) && destroyedEntities[player.userID].Count > 0)
            {
                var lastEntity = destroyedEntities[player.userID][destroyedEntities[player.userID].Count - 1];
                destroyedEntities[player.userID].RemoveAt(destroyedEntities[player.userID].Count - 1);

                var newEntity = GameManager.server.CreateEntity(lastEntity.PrefabName, lastEntity.Position, lastEntity.Rotation);

                if (newEntity != null)
                {
                    newEntity.OwnerID = lastEntity.OwnerID;

                    newEntity.Spawn();

                    var buildingBlock = newEntity as BuildingBlock;
                    if (buildingBlock != null)
                    {
                        buildingBlock.SetGrade((BuildingGrade.Enum)lastEntity.Grade);
                        buildingBlock.SetHealthToMax();
                        buildingBlock.SendNetworkUpdateImmediate();

                        foreach (var dependent in lastEntity.DependentEntities)
                        {
                            RestoreDependentEntity(dependent);
                        }
                    }

                    player.ChatMessage($"Восстановлен объект {lastEntity.PrefabName} с уровнем строительства {((BuildingGrade.Enum)lastEntity.Grade)} и владельцем {lastEntity.OwnerID}.");
                    LogInfo($"Player {player.displayName} restored {lastEntity.PrefabName} at {lastEntity.Position} with grade {((BuildingGrade.Enum)lastEntity.Grade)} and owner {lastEntity.OwnerID}.");
                }
                else
                {
                    player.ChatMessage($"Не удалось восстановить объект {lastEntity.PrefabName}.");
                    LogError($"Failed to restore object: {lastEntity.PrefabName}");
                }
            }
        }

        private void RestoreDependentEntity(DestroyedEntityInfo dependentEntity)
        {
            var newEntity = GameManager.server.CreateEntity(dependentEntity.PrefabName, dependentEntity.Position, dependentEntity.Rotation);
            if (newEntity != null)
            {
                newEntity.OwnerID = dependentEntity.OwnerID;
                newEntity.Spawn();

                var buildingBlock = newEntity as BuildingBlock;
                if (buildingBlock != null)
                {
                    buildingBlock.SetGrade((BuildingGrade.Enum)dependentEntity.Grade);
                    buildingBlock.SetHealthToMax();
                    buildingBlock.SendNetworkUpdateImmediate();
                }
            }
        }

        private void CreateDestroyedObjectsUI(BasePlayer player)
        {
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.1 0.7" },
                RectTransform = { AnchorMin = "0.85 0.3", AnchorMax = "0.99 0.7" },
                CursorEnabled = false
            }, new CuiElement().Parent = "Overlay", CuiPanelName);

            container.Add(new CuiLabel
            {
                Text = { Text = "Destroyed Objects", FontSize = 14, Align = TextAnchor.UpperCenter },
                RectTransform = { AnchorMin = "0 0.9", AnchorMax = "1 1" }
            }, CuiPanelName);

            var yPos = 0.8f;
            if (destroyedEntities.ContainsKey(player.userID))
            {
                foreach (var obj in destroyedEntities[player.userID])
                {
                    container.Add(new CuiLabel
                    {
                        Text = { Text = obj.PrefabName, FontSize = 12, Align = TextAnchor.MiddleLeft },
                        RectTransform = { AnchorMin = $"0 {yPos}", AnchorMax = $"1 {yPos + 0.05f}" }
                    }, CuiPanelName);
                    yPos -= 0.05f;
                }
            }

            container.Add(new CuiButton
            {
                Button = { Command = "ubertools.undo", Color = "0.8 0.2 0.2 1.0" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.1" },
                Text = { Text = "Undo Last Action", FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, CuiPanelName);

            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("ubertools.undo")]
        private void CmdUndoLastDestroyedEntity(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player != null)
            {
                UndoLastDestroyedEntity(player);
                UpdateDestroyedObjectsUI(player);
            }
        }

        private void UpdateDestroyedObjectsUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, CuiPanelName);

            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.1 0.7" },
                RectTransform = { AnchorMin = "0.85 0.3", AnchorMax = "0.99 0.7" },
                CursorEnabled = false
            }, new CuiElement().Parent = "Overlay", CuiPanelName);

            container.Add(new CuiLabel
            {
                Text = { Text = "Destroyed Objects", FontSize = 14, Align = TextAnchor.UpperCenter },
                RectTransform = { AnchorMin = "0 0.9", AnchorMax = "1 1" }
            }, CuiPanelName);

            var yPos = 0.8f;
            if (destroyedEntities.ContainsKey(player.userID))
            {
                foreach (var obj in destroyedEntities[player.userID])
                {
                    container.Add(new CuiLabel
                    {
                        Text = { Text = obj.PrefabName, FontSize = 12, Align = TextAnchor.MiddleLeft },
                        RectTransform = { AnchorMin = $"0 {yPos}", AnchorMax = $"1 {yPos + 0.05f}" }
                    }, CuiPanelName);
                    yPos -= 0.05f;
                }
            }

            container.Add(new CuiButton
            {
                Button = { Command = "ubertools.undo", Color = "0.8 0.2 0.2 1.0" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.1" },
                Text = { Text = "Undo Last Action", FontSize = 14, Align = TextAnchor.MiddleCenter }
            }, CuiPanelName);

            CuiHelper.AddUi(player, container);
        }

        private void LogInfo(string message)
        {
            Puts($"[UberTool] {message}");
        }

        private void LogError(string message)
        {
            PrintError($"[UberTool] {message}");
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, CuiPanelName);
            }
        }
    }
}
