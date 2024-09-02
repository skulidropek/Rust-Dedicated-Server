using System;
using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("RaidBlock", "RustGPT", "1.0.6")]
    [Description("Adds a raid block system with a UI component to show the block duration, applying to all players in the raid zone.")]
    public class RaidBlock : RustPlugin
    {
        [PluginReference]
        private Plugin CombatBlock;

        private const string RaidBlockUI = "RaidBlockUI";
        private const string RaidBlockProgress = "RaidBlockProgress";
        private Dictionary<ulong, Timer> raidTimers = new Dictionary<ulong, Timer>();
        private HashSet<ulong> blockedPlayers = new HashSet<ulong>();
        private List<RaidZone> activeRaidZones = new List<RaidZone>();
        private List<SphereEntity> activeDomes = new List<SphereEntity>();

        private class PluginConfig
        {
            public float BlockDuration { get; set; }
            public bool BlockOnReceiveRaidDamage { get; set; }
            public bool RemoveBlockOnDeath { get; set; }
            public List<string> BlockedCommands { get; set; }
            public float RaidZoneRadius { get; set; }

            public bool IsSphereEnabled { get; set; } = true;
            public int SphereType { get; set; } = 0;
            public int DomeTransparencyLevel { get; set; } = 3;
        }

        private PluginConfig config;

        private class RaidZone
        {
            public Vector3 Position { get; set; }
            public float ExpirationTime { get; set; }
        }

        protected override void LoadDefaultConfig()
        {
            config = new PluginConfig
            {
                BlockDuration = 300.0f,
                BlockOnReceiveRaidDamage = true,
                RemoveBlockOnDeath = true,
                BlockedCommands = new List<string> { "/tpr", "/tpa", "/home" },
                RaidZoneRadius = 50.0f,
                IsSphereEnabled = true,
                SphereType = 0,
                DomeTransparencyLevel = 3
            };
            SaveConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
            if (config == null)
            {
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        private void Init()
        {
            LoadDefaultMessages();
            ClearAllRaidBlockUI();
        }

        private void Unload()
        {
            ClearAllRaidZonesAndDomes();
            Puts("Plugin unloaded, all active raid zones and domes have been cleared.");
        }

        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["RaidBlock.Active"] = "Блокировка рейда: {0} секунд",
                ["RaidBlock.BlockedCommand"] = "Вы не можете использовать эту команду во время блокировки рейда.",
                ["RaidBlock.UIMessage"] = "Вы не можете использовать эту команду, пока в блокировке рейда."
            }, this, "ru");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["RaidBlock.Active"] = "Raid Block: {0} seconds",
                ["RaidBlock.BlockedCommand"] = "You cannot use this command while in raid block.",
                ["RaidBlock.UIMessage"] = "You cannot use this command while in raid block."
            }, this, "en");
        }

        private string GetMessage(string key, BasePlayer player = null, params object[] args)
        {
            return string.Format(lang.GetMessage(key, this, player?.UserIDString), args);
        }

        private void ClearAllRaidBlockUI()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyRaidBlockUI(player);
            }
        }

        private void ClearAllRaidZonesAndDomes()
        {
            foreach (var dome in activeDomes)
            {
                if (dome != null && !dome.IsDestroyed)
                {
                    Puts($"Destroying dome at position {dome.transform.position}");
                    dome.Kill();
                }
            }
            activeDomes.Clear();

            activeRaidZones.Clear();
            Puts("All active raid zones and domes have been cleared.");
        }

        private void AddRaidBlock(BasePlayer player, float duration)
        {
            ulong playerId = player.userID;

            DestroyRaidBlockUI(player);

            if (raidTimers.ContainsKey(playerId))
            {
                raidTimers[playerId].Destroy();
                raidTimers.Remove(playerId);
            }

            blockedPlayers.Add(playerId);
            Puts($"Player {player.displayName} has been added to blocked players");

            CreateRaidBlockUI(player, duration);

            raidTimers[playerId] = timer.Repeat(1f, (int)duration, () =>
            {
                duration--;
                UpdateRaidBlockUI(player, duration);
            });

            timer.Once(duration, () =>
            {
                DestroyRaidBlockUI(player);
                raidTimers.Remove(playerId);
                blockedPlayers.Remove(playerId);
                Puts($"Player {player.displayName} has been removed from blocked players after block duration ended");
            });

            Puts($"Raid block applied to player {player.displayName} for {duration} seconds");
        }

        private void CreateRaidBlockUI(BasePlayer player, float duration)
        {
            Puts($"Creating RaidBlock UI for player {player.displayName}");

            CuiElementContainer container = new CuiElementContainer();
            string panelName = RaidBlockUI;

            // Determine position based on CombatBlock status
            string anchorMin = "0.3447913 0.1135";
            string anchorMax = "0.640625 0.1435";

            if (CombatBlock != null)
            {
                var hasCombatBlock = CombatBlock.Call<bool>("HasCombatBlock", player.userID.Get());
                if (hasCombatBlock)
                {
                    anchorMin = "0.3447913 0.1435";
                    anchorMax = "0.640625 0.1735";
                }
            }

            container.Add(new CuiPanel
            {
                Image = { Color = "0.97 0.92 0.88 0.18" },
                RectTransform = { AnchorMin = anchorMin, AnchorMax = anchorMax },
                CursorEnabled = false
            }, "Hud", panelName);

            container.Add(new CuiLabel
            {
                Text = { Text = GetMessage("RaidBlock.Active", player, duration), FontSize = 15, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, panelName);

            float progress = Mathf.Clamp01(duration / config.BlockDuration);
            container.Add(new CuiElement
            {
                Name = RaidBlockProgress,
                Parent = panelName,
                Components =
                {
                    new CuiImageComponent { Color = "0.60 0.80 0.20 0.8" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = $"{progress} 0.1" }
                }
            });

            CuiHelper.AddUi(player, container);
            Puts($"RaidBlock UI should now be visible for player {player.displayName}");
        }

        private void UpdateRaidBlockUI(BasePlayer player, float duration)
        {
            DestroyRaidBlockUI(player);
            CreateRaidBlockUI(player, duration);
        }

        private void DestroyRaidBlockUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, RaidBlockUI);
            Puts($"RaidBlock UI destroyed for player {player.displayName}");
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity != null && entity.OwnerID != 0)
            {
                BasePlayer attacker = info.Initiator as BasePlayer;

                if (attacker != null && (info.damageTypes.Has(DamageType.Explosion) || info.damageTypes.Has(DamageType.Bullet)))
                {
                    Puts($"Creating raid zone at position {entity.transform.position}");
                    CreateRaidZone(entity.transform.position);
                }

                if (entity is BasePlayer victim && config.BlockOnReceiveRaidDamage)
                {
                    AddRaidBlock(victim, config.BlockDuration);
                }
            }
        }

        private void CreateRaidZone(Vector3 position)
        {
            var raidZone = new RaidZone
            {
                Position = position,
                ExpirationTime = Time.realtimeSinceStartup + config.BlockDuration
            };

            activeRaidZones.Add(raidZone);
            Puts($"Raid zone created at {position} with expiration at {raidZone.ExpirationTime}");


            if (config.IsSphereEnabled)
            {
                CreateDome(position);
            }
        }

        private void CreateDome(Vector3 position)
        {
            string spherePrefab = config.SphereType == 0 ? "assets/prefabs/visualization/sphere.prefab" : "assets/prefabs/visualization/sphere_battleroyale.prefab";

            for (int i = 0; i < config.DomeTransparencyLevel; i++)
            {
                SphereEntity sphere = GameManager.server.CreateEntity(spherePrefab, position) as SphereEntity;
                if (sphere != null)
                {
                    sphere.enableSaving = false; // Отключение сохранения состояния сферы
                    sphere.currentRadius = config.RaidZoneRadius; // Установка радиуса сферы
                    sphere.lerpRadius = config.RaidZoneRadius; // Установка конечного радиуса для анимации
                    sphere.lerpSpeed = 0f; // Отключение плавного изменения размера

                    sphere.Spawn();
                    activeDomes.Add(sphere);
                    Puts($"Dome created at position {position} with radius {sphere.currentRadius}");
                }
            }
        }

        private bool IsPlayerInRaidZone(BasePlayer player)
        {
            foreach (var raidZone in activeRaidZones)
            {
                if (Time.realtimeSinceStartup > raidZone.ExpirationTime)
                {
                    continue;
                }

                float distance = Vector3.Distance(player.transform.position, raidZone.Position);
                Puts($"Player {player.displayName} is {distance} meters from raid zone at {raidZone.Position}");
                if (distance <= config.RaidZoneRadius)
                {
                    Puts($"Player {player.displayName} is in raid zone at {raidZone.Position}");
                    return true;
                }
            }

            return false;
        }

        private void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (config.RemoveBlockOnDeath)
            {
                DestroyRaidBlockUI(player);
                blockedPlayers.Remove(player.userID);
                Puts($"Player {player.displayName} has been removed from blocked players due to death");
            }
        }

        private object OnPlayerChat(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return null;

            string message = arg.GetString(0, "text").Trim();
            if (blockedPlayers.Contains(player.userID))
            {
                if (config.BlockedCommands.Exists(cmd => message.StartsWith(cmd, StringComparison.OrdinalIgnoreCase)))
                {
                    player.ChatMessage(GetMessage("RaidBlock.BlockedCommand", player));
                    return false;
                }
            }

            return null;
        }

        private object OnUserCommand(IPlayer player, string command, string[] args)
        {
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer == null) return null;

            if (blockedPlayers.Contains(basePlayer.userID))
            {
                command = "/" + command.ToLower();
                if (config.BlockedCommands.Contains(command))
                {
                    basePlayer.ChatMessage(GetMessage("RaidBlock.UIMessage", basePlayer));
                    return false;
                }
            }

            return null;
        }

        [HookMethod("HasRaidBlock")]
        public bool HasRaidBlock(ulong playerID)
        {
            return blockedPlayers.Contains(playerID);
        }

        private void OnServerInitialized()
        {
            timer.Every(5f, () =>
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (IsPlayerInRaidZone(player) && !blockedPlayers.Contains(player.userID))
                    {
                        Puts($"Applying raid block to player {player.displayName}");
                        AddRaidBlock(player, config.BlockDuration);
                    }
                }

                activeRaidZones.RemoveAll(zone => Time.realtimeSinceStartup > zone.ExpirationTime);
            });
        }
    }
}
