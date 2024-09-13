using Oxide.Core.Plugins;
using Oxide.Core;
using UnityEngine;
using Rust;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Jump Rockets", "RustGPT", "1.0.2")]
    [Description("Запускает ракету при прыжке игрока.")]
    public class JumpRockets : RustPlugin
    {
        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            // Проверяем, нажал ли игрок кнопку прыжка
            if (input.WasJustPressed(BUTTON.JUMP))
            {
                // Путь к префабу ракеты
                string rocketPrefab = "assets/prefabs/ammo/rocket/rocket_hv.prefab";

                // Позиция и направление игрока
                Vector3 playerPosition = player.transform.position + new Vector3(0, 1.5f, 0);
                Quaternion playerRotation = Quaternion.Euler(player.serverInput.current.aimAngles);

                // Создаем объект ракеты
                BaseEntity rocketEntity = GameManager.server.CreateEntity(rocketPrefab, playerPosition, playerRotation);
                if (rocketEntity != null)
                {
                    // Устанавливаем владельца ракеты
                    rocketEntity.OwnerID = player.userID;

                    rocketEntity.Spawn();

                    // Задаем скорость ракете
                    ServerProjectile projectile = rocketEntity.GetComponent<ServerProjectile>();
                    if (projectile != null)
                    {
                        // Устанавливаем направление и скорость ракеты
                        projectile.InitializeVelocity(player.eyes.BodyForward() * projectile.speed);
                    }

                    // Дополнительно, если нужно установить источник урона
                    TimedExplosive timedExplosive = rocketEntity.GetComponent<TimedExplosive>();
                    if (timedExplosive != null)
                    {
                        timedExplosive.damageTypes = new List<DamageTypeEntry>
                        {
                            new DamageTypeEntry { type = DamageType.Explosion, amount = 200f } // Настройте урон по желанию
                        };
                        timedExplosive.creatorEntity = player;
                    }
                }
            }
        }
    }
}