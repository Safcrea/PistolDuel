using UnityEngine;

namespace RecoilDuel
{
    public enum TeamId
    {
        Neutral,
        Player,
        Enemy
    }

    public enum RunState
    {
        Boot,
        Countdown,
        Combat,
        ArenaClear,
        EnemyDropWarning,
        EnemyDropping,
        EnemyActivation,
        PlayerHitStop,
        PowerupDropping,
        RunOver,
        Paused
    }

    public enum UpgradeEffectType
    {
        Damage,
        FireRate,
        Recoil,
        MultiShot,
        Ricochet,
        Penetration,
        MaxHealth,
        Repair,
        Shield,
        Stabilizer,
        Special
    }

    public readonly struct DamageInfo
    {
        public readonly float Damage;
        public readonly TeamId SourceTeam;
        public readonly GameObject Owner;
        public readonly Vector2 HitPoint;
        public readonly Vector2 Direction;

        public DamageInfo(float damage, TeamId sourceTeam, GameObject owner, Vector2 hitPoint, Vector2 direction)
        {
            Damage = damage;
            SourceTeam = sourceTeam;
            Owner = owner;
            HitPoint = hitPoint;
            Direction = direction;
        }
    }

    [CreateAssetMenu(menuName = "Recoil Duel/Gun Data")]
    public sealed class GunData : ScriptableObject
    {
        public string gunId = "standard";
        public float mass = 1f;
        public float linearDamping = 0.18f;
        public float angularDamping = 0.1f;
        public float recoilForce = 3.4f;
        public float fireCooldown = 0.36f;
        public float maxHealth = 2f;
        public BulletData bullet;
    }

    [CreateAssetMenu(menuName = "Recoil Duel/Bullet Data")]
    public sealed class BulletData : ScriptableObject
    {
        public float speed = 26f;
        public float damage = 1f;
        public float knockback = 1f;
        public int maxRicochets = 4;
        public int penetration = 0;
        public float lifetime = 4.5f;
        public float bounceRetention = 0.96f;
        public float ownerImmunityDuration = 0.12f;
        public float visualScale = 1f;
        public float shockForce;
        public bool splitOnFirstRicochet;
    }

    [CreateAssetMenu(menuName = "Recoil Duel/Enemy Archetype")]
    public sealed class EnemyArchetypeData : ScriptableObject
    {
        public string enemyId = "rookie";
        public int threatCost = 1;
        public GunData gun;
        public float minFireDelay = 0.65f;
        public float maxFireDelay = 1.4f;
        public float reactionDelay = 0.25f;
        public float requiredAimDot = 0.9f;
        public float predictionStrength = 0.15f;
        public float repositionShotChance = 0.2f;
    }

    [CreateAssetMenu(menuName = "Recoil Duel/Upgrade Data")]
    public sealed class UpgradeData : ScriptableObject
    {
        public string upgradeId = "rapid_chamber";
        public string displayName = "Rapid Chamber";
        public UpgradeEffectType effectType = UpgradeEffectType.FireRate;
        public int maxStacks = 3;
        public float valuePerStack = 0.18f;
        public Sprite icon;
    }

    [CreateAssetMenu(menuName = "Recoil Duel/Major Drop Timing")]
    public sealed class MajorDropTimingData : ScriptableObject
    {
        [Range(0f, 1f)] public float earlyTwoMinuteChance = 0.1f;
        public Vector2 earlyWindowMinutes = new Vector2(1.8f, 2.3f);
        public Vector2 normalWindowMinutes = new Vector2(5f, 15f);
        public float hardPityMinutes = 15f;
    }

    [CreateAssetMenu(menuName = "Recoil Duel/Difficulty Curve")]
    public sealed class DifficultyCurveData : ScriptableObject
    {
        public AnimationCurve threatBudgetByMinutes = AnimationCurve.Linear(0f, 2f, 15f, 8f);
        public AnimationCurve activationDelayByMinutes = AnimationCurve.Linear(0f, 0.7f, 15f, 0.28f);
        public AnimationCurve eliteChanceByMinutes = AnimationCurve.Linear(0f, 0f, 15f, 0.2f);
        public AnimationCurve bossChanceByMinutes = AnimationCurve.Linear(0f, 0f, 15f, 0.08f);
        public int maxActiveEnemiesLow = 4;
        public int maxActiveEnemiesMedium = 6;
        public int maxActiveEnemiesHigh = 8;
    }

    public static class MajorDropScheduler
    {
        public static float RollNextMajorDropDelaySeconds(MajorDropTimingData timing, System.Random random)
        {
            if (timing == null)
            {
                return 120f;
            }

            if (random.NextDouble() <= timing.earlyTwoMinuteChance)
            {
                return Range(timing.earlyWindowMinutes.x, timing.earlyWindowMinutes.y, random) * 60f;
            }

            double weightedRoll = random.NextDouble();
            float minutes;

            if (weightedRoll < 0.35)
            {
                minutes = Range(5f, 7f, random);
            }
            else if (weightedRoll < 0.75)
            {
                minutes = Range(8f, 11f, random);
            }
            else
            {
                minutes = Range(12f, 15f, random);
            }

            return Mathf.Min(minutes, timing.hardPityMinutes) * 60f;
        }

        private static float Range(float min, float max, System.Random random)
        {
            return min + (float)random.NextDouble() * (max - min);
        }
    }

    public static class ProgressionRules
    {
        public static int GetUpgradeTier(int enemyKills, int killsPerUpgrade = 6)
        {
            return killsPerUpgrade <= 0 ? 0 : Mathf.Max(0, enemyKills) / killsPerUpgrade;
        }

        public static int GetEnemyCountForWave(int wave)
        {
            return Mathf.Clamp(2 + Mathf.Max(0, wave - 1) / 2, 2, 5);
        }
    }
}
