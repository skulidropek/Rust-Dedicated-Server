using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using UnityEngine;
using static BasePlayer;

namespace Oxide.Plugins
{
    [Info("Dance Plugin", "RustGPT", "1.0.0")]
    [Description("Позволяет игрокам танцевать без необходимости покупать DLC.")]

    public class Dancing : RustPlugin
    {
        // Присваиваем идентификаторы жестов
        private const uint RAISE_THE_ROOF_ID = 478760625;
        private const uint CABBAGE_PATCH_ID = 1855420636;
        private const uint THE_TWIST_ID = 1702547860;

        // Метод для запуска жеста
        private void Server_StartGesture(BasePlayer player, uint gestureId)
        {
            GestureConfig toPlay = player.gestureList.IdToGesture(gestureId);
            if (toPlay == null)
            {
                Puts($"Gesture with ID {gestureId} not found.");
                return;
            }
            toPlay.hideInWheel = false;
            toPlay.forceUnlock = true;
            player.Server_StartGesture(toPlay);
        }

        private void ShowDanceMenu(BasePlayer player)
        {
            var container = new CuiElementContainer();

            // Полупрозрачный задний фон на весь экран с эффектом размытия
            container.Add(new CuiPanel
            {
                Image = { Material = "assets/content/ui/uibackgroundblur.mat", Color = "0.0 0.0 0.0 0.75" },
                RectTransform =
        {
            AnchorMin = "0 0",
            AnchorMax = "1 1"
        },
                CursorEnabled = true
            }, "Overlay", "BackgroundPanel");

            // Закрытие GUI при клике на задний фон
            container.Add(new CuiButton
            {
                Button = { Color = "0 0 0 0", Close = "BackgroundPanel" },
                RectTransform =
        {
            AnchorMin = "0 0",
            AnchorMax = "1 1"
        },
                Text = { Text = "" }
            }, "BackgroundPanel");

            // Основная панель с размерами 469x268 пикселей
            container.Add(new CuiPanel
            {
                Image = { Color = "0.85 0.75 0.6 0.85" },
                RectTransform =
        {
            AnchorMin = "0.375 0.375",
            AnchorMax = "0.625 0.625"
        },
                CursorEnabled = true
            }, "BackgroundPanel", "MainPanel");

            // Панель для заголовка и кнопки закрытия
            container.Add(new CuiPanel
            {
                Image = { Color = "0.9 0.85 0.8 1" },
                RectTransform =
        {
            AnchorMin = "0 0.75",
            AnchorMax = "1 1"
        }
            }, "MainPanel", "HeaderPanel");

            // Заголовок
            container.Add(new CuiLabel
            {
                Text = { Color = "0.2 0.2 0.2 1", Text = "Выберите танец", FontSize = 24, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf" },
                RectTransform =
        {
            AnchorMin = "0.05 0",
            AnchorMax = "0.7 1"
        }
            }, "HeaderPanel");

            // Кнопка закрытия
            container.Add(new CuiButton
            {
                Button = { Color = "0.9 0.5 0.5 1", Close = "BackgroundPanel" },
                Text = { Text = "ЗАКРЫТЬ", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform =
        {
            AnchorMin = "0.75 0.2",
            AnchorMax = "0.95 0.8"
        }
            }, "HeaderPanel");

            // Первая кнопка танца "Снос крыши"
            container.Add(new CuiButton
            {
                Button = { Color = "0.85 0.75 0.6 0.5", Command = "dance.perform raise", Close = "BackgroundPanel" },
                RectTransform =
        {
            AnchorMin = "0.05 0.1",
            AnchorMax = "0.3 0.65"
        },
                Text = { Text = "<size=22>Снос крыши</size>", Align = TextAnchor.MiddleCenter, Color = "0 0 0 1", Font = "robotocondensed-regular.ttf" }
            }, "MainPanel", "Dance1Button");

            // Вторая кнопка танца "Кочанная капуста"
            container.Add(new CuiButton
            {
                Button = { Color = "0.85 0.75 0.6 0.5", Command = "dance.perform cabbage", Close = "BackgroundPanel" },
                RectTransform =
        {
            AnchorMin = "0.35 0.1",
            AnchorMax = "0.65 0.65"
        },
                Text = { Text = "<size=22>Кочанная капуста</size>", Align = TextAnchor.MiddleCenter, Color = "0 0 0 1", Font = "robotocondensed-regular.ttf" }
            }, "MainPanel", "Dance2Button");

            // Третья кнопка танца "Твист"
            container.Add(new CuiButton
            {
                Button = { Color = "0.85 0.75 0.6 0.5", Command = "dance.perform twist", Close = "BackgroundPanel" },
                RectTransform =
        {
            AnchorMin = "0.7 0.1",
            AnchorMax = "0.95 0.65"
        },
                Text = { Text = "<size=22>Твист</size>", Align = TextAnchor.MiddleCenter, Color = "0 0 0 1", Font = "robotocondensed-regular.ttf" }
            }, "MainPanel", "Dance3Button");

            // Отображение созданного интерфейса
            CuiHelper.AddUi(player, container);
        }

        // Консольная команда для запуска танца
        [ConsoleCommand("dance.perform")]
        private void PerformDanceCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null || arg.Args == null || arg.Args.Length == 0)
            {
                SendReply(player, "Некорректная команда. Используйте GUI или команду: /dance [raise/cabbage/twist]");
                return;
            }

            // Выполняем жест
            switch (arg.Args[0].ToLower())
            {
                case "raise":
                    Server_StartGesture(player, RAISE_THE_ROOF_ID);
                    break;
                case "cabbage":
                    Server_StartGesture(player, CABBAGE_PATCH_ID);
                    break;
                case "twist":
                    Server_StartGesture(player, THE_TWIST_ID);
                    break;
                default:
                    SendReply(player, "Некорректная команда. Используйте GUI или команду: /dance [raise/cabbage/twist]");
                    break;
            }
        }

        // Основная команда /dance (оставлена для вызова GUI)
        [ChatCommand("dance")]
        private void DanceCommand(BasePlayer player, string command, string[] args)
        {
            ShowDanceMenu(player);
        }

        // Вспомогательный метод для преобразования HEX цвета в формат Rust
        private string HexToRustFormat(string hex)
        {
            if (string.IsNullOrEmpty(hex))
                return "1 1 1 1"; // белый цвет по умолчанию, если строка пустая или null

            if (hex.StartsWith("#"))
                hex = hex.Substring(1);

            if (hex.Length != 6 && hex.Length != 8)
                throw new ArgumentOutOfRangeException(nameof(hex), "HEX color string must be 6 or 8 characters long.");

            if (hex.Length == 6)
                hex += "FF"; // добавляем альфа-канал, если его нет

            var r = int.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
            var g = int.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
            var b = int.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
            var a = int.Parse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber) / 255f;

            return $"{r} {g} {b} {a}";
        }

        // Метод, вызываемый при выгрузке плагина
        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, "BackgroundPanel");
                CuiHelper.DestroyUi(player, "MainPanel");
            }
        }
    }
}
