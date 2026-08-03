using System;
using System.Collections.Generic;
using UnityEngine;

namespace RecoilDuel
{
    public static class RecoilDuelArtLibrary
    {
        private const string Root = "RecoilDuelArt/";
        private static readonly Dictionary<string, Sprite[]> SpriteSets = new Dictionary<string, Sprite[]>();

        public static Sprite GetPlayerChassis(PlayerChassisId id)
        {
            return GetFromAtlas("Player/player_chassis_atlas", (int)id);
        }

        public static Sprite GetEnemyChassis(EnemyArchetypeId id)
        {
            return GetFromAtlas("Enemies/enemy_chassis_atlas", (int)id);
        }

        public static Sprite GetBullet(BulletArtId id)
        {
            return GetFromAtlas("Bullets/bullet_atlas", (int)id);
        }

        public static Sprite GetPowerup(PowerupId id)
        {
            return GetFromAtlas("Powerups/powerup_atlas", (int)id);
        }

        public static Sprite GetAttachment(AttachmentArtId id)
        {
            return GetFromAtlas("Attachments/attachment_atlas", (int)id);
        }

        public static Sprite GetArenaBackground()
        {
            return Resources.Load<Sprite>(Root + "Arena/portrait_arena");
        }

        public static void ClearCache()
        {
            SpriteSets.Clear();
        }

        private static Sprite GetFromAtlas(string resourcePath, int index)
        {
            if (!SpriteSets.TryGetValue(resourcePath, out Sprite[] sprites))
            {
                sprites = Resources.LoadAll<Sprite>(Root + resourcePath);
                Array.Sort(sprites, CompareSpriteNames);
                SpriteSets.Add(resourcePath, sprites);
            }

            return index >= 0 && index < sprites.Length ? sprites[index] : null;
        }

        private static int CompareSpriteNames(Sprite left, Sprite right)
        {
            return string.CompareOrdinal(left.name, right.name);
        }
    }
}
