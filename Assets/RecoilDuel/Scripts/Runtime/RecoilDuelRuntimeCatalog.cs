using System.Collections.Generic;
using UnityEngine;

namespace RecoilDuel
{
    internal sealed class RecoilDuelRuntimeCatalog
    {
        public readonly List<UpgradeData> Upgrades = new List<UpgradeData>(16);
        public readonly List<UpgradeData> KillMilestoneUpgrades = new List<UpgradeData>(5);
        public readonly List<EnemyArchetypeData> EnemyArchetypes = new List<EnemyArchetypeData>(8);
        public readonly List<GunData> EnemyGuns = new List<GunData>(8);
        public readonly List<BulletData> EnemyBullets = new List<BulletData>(8);

        public GunData PlayerGun;
        public BulletData PlayerBullet;
        public MajorDropTimingData MajorDropTiming;

        public void Dispose()
        {
            DestroyAll(Upgrades);
            DestroyAll(EnemyArchetypes);
            DestroyAll(EnemyGuns);
            DestroyAll(EnemyBullets);
            KillMilestoneUpgrades.Clear();

            DestroyObject(PlayerGun);
            DestroyObject(PlayerBullet);
            DestroyObject(MajorDropTiming);
            PlayerGun = null;
            PlayerBullet = null;
            MajorDropTiming = null;
        }

        private static void DestroyAll<T>(List<T> items) where T : Object
        {
            for (int i = 0; i < items.Count; i++)
            {
                DestroyObject(items[i]);
            }

            items.Clear();
        }

        private static void DestroyObject(Object item)
        {
            if (item != null)
            {
                Object.Destroy(item);
            }
        }
    }
}
