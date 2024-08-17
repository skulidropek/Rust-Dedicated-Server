/// <summary>
/// XChatInfo Plugin for Rust - Provides chat information on your server.
/// 
/// This plugin was fully developed using RustGPT. It allows players to request information through chat using keywords, 
/// supports multiple languages (ru/en/uk/es), and automatically sends messages from the server 
/// at specified intervals. The plugin includes a global cooldown for requesting information 
/// and displays messages to all players.
/// 
/// You can customize the plugin's behavior through the configuration and language files.
/// 
/// For more information, visit the plugin's page: https://skyplugins.ru/resources/xchatinfo.680/
/// 
/// Discord: https://discord.gg/sv6nF3gNU3
/// RustGPT: https://chatgpt.com/g/g-xunzDbv9b-rustgpt
/// </summary>

using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    /// <summary>
    /// Main class for the XChatInfo plugin, providing chat information on the Rust server.
    /// </summary>
    [Info("XChatInfo", "RustGPT", "1.1.8")]
    [Description("Provides chat information for your server.")]
    public class XChatInfo : RustPlugin
    {
        #region Configuration

        /// <summary>
        /// Class for storing the plugin's configuration data.
        /// </summary>
        private ConfigData config;

        /// <summary>
        /// Main class for the configuration data.
        /// </summary>
        private class ConfigData
        {
            [JsonProperty("General Settings")]
            public GeneralSettings General { get; set; }

            [JsonProperty("Server Info Message Settings")]
            public ServerInfoSettings ServerInfo { get; set; }

            [JsonProperty("Text Message Settings")]
            public TextMessageSettings TextMessages { get; set; }

            [JsonProperty("Language Selection")]
            public LanguageSettings Languages { get; set; }
        }

        /// <summary>
        /// Class for storing general configuration settings.
        /// </summary>
        private class GeneralSettings
        {
            [JsonProperty("SteamID for custom avatar profile")]
            public ulong CustomAvatarSteamID { get; set; }
        }

        /// <summary>
        /// Class for storing server info message settings.
        /// </summary>
        private class ServerInfoSettings
        {
            [JsonProperty("Server sends message")]
            public bool SendServerMessage { get; set; }

            [JsonProperty("Message sending interval")]
            public float ServerMessageInterval { get; set; }

            [JsonProperty("Players can request info via keyword")]
            public bool AllowKeywordRequests { get; set; }

            [JsonProperty("List of keywords")]
            public List<string> Keywords { get; set; }
        }

        /// <summary>
        /// Class for storing text message settings.
        /// </summary>
        private class TextMessageSettings
        {
            [JsonProperty("Server sends random message")]
            public bool SendRandomMessage { get; set; }

            [JsonProperty("Random message sending interval")]
            public float RandomMessageInterval { get; set; }

            [JsonProperty("Players can request info via keyword")]
            public bool AllowKeywordRequests { get; set; }

            [JsonProperty("List of keywords and cooldowns for players")]
            public Dictionary<string, float> KeywordCooldowns { get; set; }
        }

        /// <summary>
        /// Class for storing language settings.
        /// </summary>
        private class LanguageSettings
        {
            [JsonProperty("Plugin language")]
            public string Language { get; set; } = "ru";
        }

        /// <summary>
        /// Method for loading the default configuration.
        /// </summary>
        protected override void LoadDefaultConfig()
        {
            ConfigData configData = new ConfigData
            {
                General = new GeneralSettings
                {
                    CustomAvatarSteamID = 0
                },
                ServerInfo = new ServerInfoSettings
                {
                    SendServerMessage = true,
                    ServerMessageInterval = 300.0f,
                    AllowKeywordRequests = true,
                    Keywords = new List<string> { "!pop", "!online" }
                },
                TextMessages = new TextMessageSettings
                {
                    SendRandomMessage = true,
                    RandomMessageInterval = 275.0f,
                    AllowKeywordRequests = true,
                    KeywordCooldowns = new Dictionary<string, float>
                    {
                        { "!discord", 180 },
                        { "!email", 180 },
                        { "!donate", 180 },
                        { "!paypal", 180 }
                    }
                },
                Languages = new LanguageSettings
                {
                    Language = "ru" // Default language set to Russian
                }
            };
            Config.WriteObject(configData, true);
        }

        #endregion

        #region Data Storage

        /// <summary>
        /// Stores the last time commands were used by players to track cooldowns.
        /// </summary>
        private Dictionary<ulong, Dictionary<string, double>> playerCommandCooldowns = new Dictionary<ulong, Dictionary<string, double>>();

        #endregion

        #region Hooks

        /// <summary>
        /// Initializes the plugin, loads the configuration, and sets timers for sending messages.
        /// </summary>
        private void Init()
        {
            config = Config.ReadObject<ConfigData>();

            LoadMessages();

            if (config.ServerInfo.SendServerMessage)
            {
                timer.Every(config.ServerInfo.ServerMessageInterval, BroadcastServerInfo);
            }

            if (config.TextMessages.SendRandomMessage)
            {
                timer.Every(config.TextMessages.RandomMessageInterval, BroadcastRandomMessage);
            }
        }

        /// <summary>
        /// Handles player chat messages.
        /// </summary>
        /// <param name="player">The player who sent the message.</param>
        /// <param name="chatMessage">The text of the message.</param>
        /// <returns>Returns null if the command is not handled.</returns>
        private object OnPlayerChat(BasePlayer player, string chatMessage)
        {
            if (string.IsNullOrEmpty(chatMessage)) return null;

            if (config.ServerInfo.AllowKeywordRequests && config.ServerInfo.Keywords.Contains(chatMessage))
            {
                HandleDynamicKeywordRequest(player, chatMessage);
                return true;
            }

            if (config.TextMessages.KeywordCooldowns.ContainsKey(chatMessage))
            {
                HandleDynamicKeywordRequest(player, chatMessage);
                return true;
            }

            if (!config.TextMessages.KeywordCooldowns.ContainsKey(chatMessage))
            {
                HandleDynamicKeywordRequest(player, chatMessage, noCooldown: true);
                return true;
            }

            return null;
        }

        #endregion

        #region Command Handlers

        /// <summary>
        /// Handles keyword requests, considering possible cooldowns.
        /// </summary>
        /// <param name="player">The player who made the request.</param>
        /// <param name="keyword">The keyword for the requested information.</param>
        /// <param name="noCooldown">Flag indicating that the command does not require a cooldown.</param>
        private void HandleDynamicKeywordRequest(BasePlayer player, string keyword, bool noCooldown = false)
        {
            if (noCooldown || !config.TextMessages.KeywordCooldowns.ContainsKey(keyword))
            {
                string responseMessage = lang.GetMessage(keyword, this, player.UserIDString);
                responseMessage = ReplaceKeywords(responseMessage, player);
                PrintToChat(responseMessage);
                return;
            }

            if (IsOnCooldown(player.userID, keyword))
            {
                SendReply(player, Lang("CooldownMessage", player.UserIDString));
                return;
            }

            SetCooldown(player.userID, keyword);

            string cooldownMessage = lang.GetMessage(keyword, this, player.UserIDString);
            cooldownMessage = ReplaceKeywords(cooldownMessage, player);
            PrintToChat(cooldownMessage);
        }

        /// <summary>
        /// Replaces keywords in the message with actual data.
        /// </summary>
        /// <param name="message">The message in which to replace keywords.</param>
        /// <param name="player">The player for whom the replacement is made.</param>
        /// <returns>The message with replaced keywords.</returns>
        private string ReplaceKeywords(string message, BasePlayer player)
        {
            if (player != null)
            {
                message = message.Replace("{playerName}", player.displayName)
                                 .Replace("{playerId}", player.UserIDString);
            }

            var onlinePlayers = BasePlayer.activePlayerList.Count.ToString();
            var connectingPlayers = BasePlayer.sleepingPlayerList.Count.ToString(); // Example
            var queuedPlayers = BasePlayer.sleepingPlayerList.Count.ToString(); // Example

            message = message.Replace("{onlinePlayers}", onlinePlayers)
                             .Replace("{connectingPlayers}", connectingPlayers)
                             .Replace("{queuedPlayers}", queuedPlayers);

            return message;
        }

        /// <summary>
        /// Checks whether the command is on cooldown.
        /// </summary>
        /// <param name="userID">The user's ID.</param>
        /// <param name="keyword">The keyword of the command.</param>
        /// <returns>True if the command is on cooldown; otherwise, False.</returns>
        private bool IsOnCooldown(ulong userID, string keyword)
        {
            if (!config.TextMessages.KeywordCooldowns.ContainsKey(keyword))
            {
                return false;
            }

            if (playerCommandCooldowns.ContainsKey(userID) && playerCommandCooldowns[userID].ContainsKey(keyword))
            {
                double lastUsage = playerCommandCooldowns[userID][keyword];
                bool onCooldown = Time.realtimeSinceStartup - lastUsage < config.TextMessages.KeywordCooldowns[keyword];
                return onCooldown;
            }
            return false;
        }

        /// <summary>
        /// Sets the last time a command was used to track cooldowns.
        /// </summary>
        /// <param name="userID">The user's ID.</param>
        /// <param name="keyword">The keyword of the command.</param>
        private void SetCooldown(ulong userID, string keyword)
        {
            if (!playerCommandCooldowns.ContainsKey(userID))
            {
                playerCommandCooldowns[userID] = new Dictionary<string, double>();
            }
            playerCommandCooldowns[userID][keyword] = Time.realtimeSinceStartup;
        }

        #endregion

        #region Message Broadcasts

        /// <summary>
        /// Broadcasts server status message to the chat.
        /// </summary>
        private void BroadcastServerInfo()
        {
            string serverInfoMessage = Lang("!online", null);
            serverInfoMessage = ReplaceKeywords(serverInfoMessage, null);
            PrintToChat(serverInfoMessage);
        }

        /// <summary>
        /// Broadcasts a random message to the chat.
        /// </summary>
        private void BroadcastRandomMessage()
        {
            string randomMessage = Lang("RandomMessage", null);
            randomMessage = ReplaceKeywords(randomMessage, null);
            PrintToChat(randomMessage);
        }

        #endregion

        #region Language Support

        /// <summary>
        /// Loads messages for different languages.
        /// </summary>
        private void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CooldownMessage"] = "Please wait before using this command again.",
                ["!online"] = "Currently, there are {onlinePlayers} players online, {connectingPlayers} connecting, and {queuedPlayers} in queue.",
                ["!discord"] = "Join our Discord at www.discord.com",
                ["!donate"] = "Visit our store at www.donate.com",
                ["!email"] = "Contact us at support@yourdomain.com",
                ["!paypal"] = "You can support us via PayPal - paypal.me/yourlink",
                ["RandomMessage"] = "Don't forget to check out our Discord!"
            }, this, "en");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CooldownMessage"] = "Подождите, перед использованием этой команды снова.",
                ["!online"] = "В настоящее время в онлайне {onlinePlayers} игроков, {connectingPlayers} подключаются и {queuedPlayers} в очереди.",
                ["!discord"] = "Наш дискорд - www.discord.com",
                ["!donate"] = "Наш магазин - www.donate.com",
                ["!email"] = "Наша почта - support@yourdomain.com",
                ["!paypal"] = "Вы можете поддержать нас через PayPal - paypal.me/yourlink",
                ["RandomMessage"] = "Не забывайте заходить на наш дискорд!"
            }, this, "ru");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CooldownMessage"] = "Будь ласка, зачекайте перед використанням цієї команди знову.",
                ["!online"] = "Зараз в мережі {onlinePlayers} гравців, {connectingPlayers} підключаються і {queuedPlayers} в черзі.",
                ["!discord"] = "Наш дискорд - www.discord.com",
                ["!donate"] = "Наш магазин - www.donate.com",
                ["!email"] = "Наша пошта - support@yourdomain.com",
                ["!paypal"] = "Ви можете підтримати нас через PayPal - paypal.me/yourlink",
                ["RandomMessage"] = "Не забувайте відвідувати наш дискорд!"
            }, this, "uk");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CooldownMessage"] = "Por favor, espera antes de usar este comando nuevamente.",
                ["!online"] = "Actualmente, hay {onlinePlayers} jugadores en línea, {connectingPlayers} conectándose y {queuedPlayers} en cola.",
                ["!discord"] = "Únete a nuestro Discord en www.discord.com",
                ["!donate"] = "Visita nuestra tienda en www.donate.com",
                ["!email"] = "Contáctanos en support@yourdomain.com",
                ["!paypal"] = "Puedes apoyarnos a través de PayPal - paypal.me/yourlink",
                ["RandomMessage"] = "¡No olvides visitar nuestro Discord!"
            }, this, "es");
        }

        /// <summary>
        /// Retrieves a localized message by key.
        /// </summary>
        /// <param name="key">The message key.</param>
        /// <param name="userId">The user's ID (optional).</param>
        /// <param name="args">Arguments for formatting the message (optional).</param>
        /// <returns>The localized message.</returns>
        private string Lang(string key, string userId = null, params object[] args)
        {
            var message = lang.GetMessage(key, this, userId);
            return args.Length > 0 ? string.Format(message, args) : message;
        }

        #endregion
    }
}
