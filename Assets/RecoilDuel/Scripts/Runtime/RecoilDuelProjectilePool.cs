using System.Collections.Generic;
using UnityEngine;

namespace RecoilDuel
{
    internal sealed class RecoilDuelProjectilePool
    {
        private readonly List<BulletController> bullets = new List<BulletController>(48);

        public void Initialize(Transform parent, Sprite fallbackSprite, PhysicsMaterial2D bounceMaterial, int initialSize)
        {
            for (int i = bullets.Count; i < initialSize; i++)
            {
                GameObject bulletObject = new GameObject("Pooled Bullet");
                bulletObject.transform.SetParent(parent);
                BulletController bullet = bulletObject.AddComponent<BulletController>();
                bullet.Build(fallbackSprite, bounceMaterial);
                bulletObject.SetActive(false);
                bullets.Add(bullet);
            }
        }

        public BulletController Rent(bool allowRecycle)
        {
            for (int i = 0; i < bullets.Count; i++)
            {
                if (!bullets[i].gameObject.activeSelf)
                {
                    return bullets[i];
                }
            }

            if (!allowRecycle || bullets.Count == 0)
            {
                return null;
            }

            BulletController recycled = bullets[0];
            recycled.gameObject.SetActive(false);
            return recycled;
        }

        public void DeactivateAll()
        {
            for (int i = 0; i < bullets.Count; i++)
            {
                bullets[i].gameObject.SetActive(false);
            }
        }

        public void DeactivateTeam(TeamId team)
        {
            for (int i = 0; i < bullets.Count; i++)
            {
                if (bullets[i].gameObject.activeSelf && bullets[i].OwnerTeam == team)
                {
                    bullets[i].FadeAndDisable();
                }
            }
        }
    }
}
