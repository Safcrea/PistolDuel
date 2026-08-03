using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace RecoilDuel
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Recoil Duel/Game Manager")]
    public sealed class RecoilDuelGame : MonoBehaviour
    {
        private const float ArenaHalfWidth = 3.25f;
        private const float ArenaHalfHeight = 5.8f;
        private const float BulletRadius = 0.065f;

        [Header("Player Settings")]
        [SerializeField, Min(0.1f)] private float playerMaxHealth = 2f;
        [SerializeField, Range(0, 5)] private int playerStartingShieldHits;
        [SerializeField, Min(0.1f)] private float playerBulletDamage = 1f;
        [SerializeField, Min(1f)] private float playerBulletSpeed = 25f;
        [SerializeField, Min(0.05f)] private float playerFireCooldown = 0.34f;
        [SerializeField, Min(0f)] private float playerRecoilForce = 4.1f;
        [SerializeField, Range(0, 12)] private int playerStartingRicochets = 4;
        [SerializeField, Min(0f)] private float playerInvulnerabilityDuration = 0.22f;
        [SerializeField, Min(1)] private int killsPerUpgrade = 6;

        [Header("Enemy Settings")]
        [SerializeField, Min(0.1f)] private float enemyBaseHealth = 1f;
        [SerializeField, Min(0.1f)] private float enemyMaximumHealth = 3f;
        [SerializeField, Min(0f)] private float enemyHealthIncrease = 0.5f;
        [SerializeField, Min(1)] private int enemyHealthIncreaseEveryWaves = 4;
        [SerializeField, Min(0.1f)] private float enemyBulletDamage = 1f;
        [SerializeField, Min(1f)] private float enemyBulletSpeed = 19f;
        [SerializeField, Min(0.05f)] private float enemyMinimumFireDelay = 0.9f;
        [SerializeField, Min(0.05f)] private float enemyMaximumFireDelay = 1.55f;
        [SerializeField, Range(0f, 0.2f)] private float enemyFireDelayReductionPerWave = 0.035f;
        [SerializeField, Range(0.1f, 1f)] private float enemyMinimumFireDelayMultiplier = 0.55f;
        [SerializeField, Min(0f)] private float enemyRecoilForce = 2.6f;
        [SerializeField, Range(0f, 0.2f)] private float enemyRecoilIncreasePerWave = 0.025f;

        [Header("Wave Settings")]
        [SerializeField, Range(1, 5)] private int initialEnemyCount = 2;
        [SerializeField, Range(1, 5)] private int maximumEnemiesPerWave = 5;
        [SerializeField, Min(1)] private int wavesPerExtraEnemy = 2;
        [SerializeField, Min(0f)] private float arenaClearDuration = 1.1f;
        [SerializeField, Min(0f)] private float dropWarningDuration = 0.5f;
        [SerializeField, Min(0.1f)] private float enemyDropSpeed = 5.4f;
        [SerializeField, Min(0f)] private float enemyActivationDelay = 0.04f;
        [SerializeField, Min(0f)] private float enemyActivationStagger = 0.06f;

        [Header("Power-up Settings")]
        [SerializeField, Min(0f)] private float firstPowerupDelay = 22f;
        [SerializeField, Min(1f)] private float powerupIntervalAfterCollection = 40f;

        [Header("Debug Settings")]
        [SerializeField] private bool showDebugButtons = true;
        [SerializeField] private bool startWithInfiniteHealth;

        private readonly List<BulletController> bulletPool = new List<BulletController>(48);
        private readonly List<PistolController> enemyPool = new List<PistolController>(8);
        private readonly List<GameObject> dropWarnings = new List<GameObject>(5);
        private readonly List<UpgradeData> upgrades = new List<UpgradeData>(14);
        private readonly List<UpgradeData> killMilestoneUpgrades = new List<UpgradeData>(10);
        private readonly System.Random majorDropRandom = new System.Random(7261);

        private Camera mainCamera;
        private Canvas canvas;
        private Text statusText;
        private Text scoreText;
        private Text hpText;
        private Text centerText;
        private Text infiniteHealthButtonText;
        private Text powerupButtonText;
        private Transform dynamicRoot;
        private Transform bulletRoot;
        private Transform enemyRoot;
        private Transform vfxRoot;
        private PhysicsMaterial2D bounceMaterial;
        private Sprite squareSprite;
        private Sprite circleSprite;
        private Sprite triangleSprite;
        private AudioSource audioSource;

        private GunData playerGunData;
        private GunData enemyGunData;
        private BulletData playerBulletData;
        private BulletData enemyBulletData;
        private EnemyArchetypeData enemyArchetype;
        private MajorDropTimingData majorDropTiming;

        private PistolController player;
        private RunState state = RunState.Boot;
        private int waveIndex;
        private int score;
        private int friendlyFireKills;
        private int ricochetKills;
        private int totalEnemyKills;
        private int lastMilestoneTier;
        private int nextDebugUpgradeIndex;
        private int activePowerups;
        private float runTime;
        private float nextPowerupTime;
        private float lastHapticTime;
        private bool clearSequenceRunning;
        private bool infiniteHealth;
        private bool magnetPowerups;

        public TeamId PlayerTeam => TeamId.Player;
        public bool IsCombatActive => state == RunState.Combat || state == RunState.PowerupDropping;
        public PistolController PlayerPistol => player;
        public bool InfiniteHealth => infiniteHealth;
        public bool MagnetPowerups => magnetPowerups;
        public float PlayerInvulnerabilityDuration => playerInvulnerabilityDuration;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Physics2D.gravity = Vector2.zero;
            infiniteHealth = startWithInfiniteHealth;
            CreateRuntimeData();
            CreateSprites();
            CreateRoots();
            SetupCamera();
            SetupAudio();
            SetupArena();
            SetupUi();
            CreatePools();
        }

        private void OnValidate()
        {
            playerMaxHealth = Mathf.Max(0.1f, playerMaxHealth);
            killsPerUpgrade = Mathf.Max(1, killsPerUpgrade);
            enemyMaximumHealth = Mathf.Max(enemyBaseHealth, enemyMaximumHealth);
            enemyMaximumFireDelay = Mathf.Max(enemyMinimumFireDelay, enemyMaximumFireDelay);
            maximumEnemiesPerWave = Mathf.Max(initialEnemyCount, maximumEnemiesPerWave);
            wavesPerExtraEnemy = Mathf.Max(1, wavesPerExtraEnemy);
        }

        private void Start()
        {
            StartCoroutine(StartRun());
        }

        private void Update()
        {
            if (state == RunState.RunOver)
            {
                if (WasPrimaryPressed())
                {
                    RestartRun();
                }
                return;
            }

            if (state == RunState.Countdown)
            {
                return;
            }

            if (WasPrimaryPressed() && player != null && player.IsAlive)
            {
                player.TryFire();
            }

            if (IsCombatActive)
            {
                runTime += Time.deltaTime;
                if (activePowerups == 0 && runTime >= nextPowerupTime)
                {
                    StartCoroutine(DropPowerup(GetRandomUpgrade()));
                }
            }

            UpdateUi();
        }

        private void FixedUpdate()
        {
            if (player != null && player.IsAlive)
            {
                ClampBody(player.Body);
            }

            for (int i = 0; i < enemyPool.Count; i++)
            {
                if (enemyPool[i].gameObject.activeSelf)
                {
                    ClampBody(enemyPool[i].Body);
                }
            }
        }

        public BulletController SpawnBullet(PistolController source, BulletData bulletData)
        {
            return SpawnBullet(source, bulletData, source.MuzzleRight, true);
        }

        public BulletController SpawnBullet(PistolController source, BulletData bulletData, Vector2 direction, bool playFeedback)
        {
            BulletController bullet = GetInactiveBullet();
            bullet.Launch(
                source.Muzzle.position,
                direction,
                source.Team,
                source.gameObject,
                bulletData,
                source.Team == TeamId.Player ? new Color(0.25f, 0.75f, 1f) : new Color(1f, 0.22f, 0.13f),
                this);

            if (playFeedback)
            {
                FeedbackShot(source);
            }

            return bullet;
        }

        public void OnBulletExpired(BulletController bullet)
        {
            bullet.gameObject.SetActive(false);
        }

        public void SpawnSplitRicochets(BulletController source, Vector2 reflectedDirection)
        {
            for (int i = -1; i <= 1; i += 2)
            {
                BulletController split = GetInactiveBulletWithoutRecycling();
                if (split == null)
                {
                    return;
                }

                Vector2 direction = Quaternion.Euler(0f, 0f, i * 17f) * reflectedDirection;
                split.Launch(
                    source.transform.position + (Vector3)(direction.normalized * 0.12f),
                    direction,
                    source.OwnerTeam,
                    source.SourceOwner,
                    source.Data,
                    new Color(0.45f, 0.9f, 1f),
                    this,
                    false,
                    0.65f);
            }
        }

        public void ApplyShockPulse(Vector2 hitPoint, float force)
        {
            if (force <= 0f)
            {
                return;
            }

            if (player != null && player.IsAlive)
            {
                PushFromPoint(player.Body, hitPoint, force);
            }

            for (int i = 0; i < enemyPool.Count; i++)
            {
                if (enemyPool[i].gameObject.activeSelf && enemyPool[i].IsAlive)
                {
                    PushFromPoint(enemyPool[i].Body, hitPoint, force);
                }
            }

            SpawnBurst(hitPoint, Color.cyan, 8);
        }

        private static void PushFromPoint(Rigidbody2D body, Vector2 point, float force)
        {
            Vector2 offset = body.position - point;
            float distance = offset.magnitude;
            if (distance < 0.05f || distance > 2.4f)
            {
                return;
            }

            body.AddForce(offset.normalized * force * (1f - distance / 2.4f), ForceMode2D.Impulse);
        }

        public void OnGunDamaged(HealthComponent target, DamageInfo damage)
        {
            StartCoroutine(HitStop(damage.Damage >= 2f ? 0.08f : 0.035f));
            ShakeCamera(0.08f + damage.Damage * 0.04f);

            if (target.Team == TeamId.Player)
            {
                Vibrate(0.28f);
            }
        }

        public void OnGunDestroyed(HealthComponent target, DamageInfo damage)
        {
            SpawnBurst(target.transform.position, target.Team == TeamId.Player ? Color.cyan : Color.red, 14);

            if (target.Team == TeamId.Player)
            {
                StartCoroutine(GameOver());
                return;
            }

            PistolController pistol = target.GetComponent<PistolController>();
            if (pistol != null)
            {
                pistol.DeactivateForPool();
            }

            if (damage.SourceTeam == TeamId.Enemy)
            {
                friendlyFireKills++;
                score += 150;
                ShowFloatingLabel("CROSSFIRE", target.transform.position, new Color(1f, 0.72f, 0.22f));
            }
            else
            {
                score += 100;
                if (target.LastHitRicochetCount > 0)
                {
                    ricochetKills++;
                    score += 50;
                    ShowFloatingLabel("RICOCHET", target.transform.position, Color.cyan);
                }
            }

            totalEnemyKills++;
            int earnedTier = ProgressionRules.GetUpgradeTier(totalEnemyKills, killsPerUpgrade);
            if (earnedTier > lastMilestoneTier)
            {
                lastMilestoneTier = earnedTier;
                UpgradeData milestoneUpgrade = killMilestoneUpgrades[(earnedTier - 1) % killMilestoneUpgrades.Count];
                ApplyUpgrade(milestoneUpgrade, true);
                ShowFloatingLabel("6 KILLS - WEAPON MK " + earnedTier, player.transform.position + Vector3.up * 0.85f, Color.cyan);
            }

            if (!clearSequenceRunning && CountActiveEnemies() == 0 && state != RunState.RunOver)
            {
                StartCoroutine(ClearAndDropNextWave());
            }
        }

        public void ApplyUpgrade(UpgradeData upgrade, bool milestone = false)
        {
            if (player == null || upgrade == null)
            {
                return;
            }

            switch (upgrade.upgradeId)
            {
                case "damage_core":
                    playerBulletData.damage = Mathf.Min(3f, playerBulletData.damage + 0.35f);
                    break;
                case "rapid_chamber":
                    player.ApplyFireRateMultiplier(0.82f);
                    break;
                case "heavy_recoil":
                    player.ApplyRecoilMultiplier(1.2f);
                    break;
                case "multi_shot":
                    player.AddProjectile();
                    break;
                case "ricochet_core":
                    playerBulletData.maxRicochets = Mathf.Min(10, playerBulletData.maxRicochets + 1);
                    playerBulletData.bounceRetention = Mathf.Min(1f, playerBulletData.bounceRetention + 0.01f);
                    break;
                case "penetrator":
                    playerBulletData.penetration = Mathf.Min(3, playerBulletData.penetration + 1);
                    break;
                case "large_caliber":
                    playerBulletData.damage = Mathf.Min(3.5f, playerBulletData.damage + 0.55f);
                    playerBulletData.speed = Mathf.Max(17f, playerBulletData.speed * 0.92f);
                    playerBulletData.visualScale = Mathf.Min(1.8f, playerBulletData.visualScale + 0.25f);
                    player.ApplyRecoilMultiplier(1.14f);
                    break;
                case "repair_module":
                    if (!player.Health.Repair(1f))
                    {
                        player.Health.AddShield(1);
                    }
                    break;
                case "armor_plate":
                    player.Health.AddShield(1);
                    break;
                case "stabilizer":
                    player.ApplyStabilizer(0.78f);
                    break;
                case "wild_spinner":
                    player.ApplyFireRateMultiplier(0.88f);
                    player.ApplyRecoilMultiplier(1.12f);
                    player.AddSpinKick(0.24f);
                    break;
                case "split_ricochet":
                    playerBulletData.splitOnFirstRicochet = true;
                    break;
                case "shock_round":
                    playerBulletData.shockForce = Mathf.Min(4.5f, playerBulletData.shockForce + 1.8f);
                    break;
                case "magnet_pickup":
                    magnetPowerups = true;
                    break;
            }

            player.AdvanceUpgradeTier(upgrade.displayName);
            score += 250;
            ShowFloatingLabel(upgrade.displayName.ToUpperInvariant(), player.transform.position + Vector3.up * 0.55f, new Color(1f, 0.8f, 0.18f));
            ShakeCamera(0.18f);
            Vibrate(0.45f);
            if (!milestone)
            {
                nextPowerupTime = runTime + powerupIntervalAfterCollection;
            }
        }

        public void OnPowerupRemoved()
        {
            activePowerups = Mathf.Max(0, activePowerups - 1);
        }

        public void AwardRicochet(BulletController bullet)
        {
            if (bullet.OwnerTeam == TeamId.Player)
            {
                score += 5;
            }
        }

        private IEnumerator StartRun()
        {
            state = RunState.Countdown;
            ClearRuntimeObjects();
            waveIndex = 0;
            score = 0;
            friendlyFireKills = 0;
            ricochetKills = 0;
            totalEnemyKills = 0;
            lastMilestoneTier = 0;
            nextDebugUpgradeIndex = 0;
            activePowerups = 0;
            runTime = 0f;
            clearSequenceRunning = false;
            magnetPowerups = false;
            nextPowerupTime = firstPowerupDelay;
            ResetPlayerProgressionData();
            UpdateDebugButtonLabels();

            CreatePlayer();

            string[] beats = { "3", "2", "1", "DUEL" };
            for (int i = 0; i < beats.Length; i++)
            {
                centerText.text = beats[i];
                centerText.enabled = true;
                yield return new WaitForSeconds(i == beats.Length - 1 ? 0.55f : 0.45f);
            }

            centerText.enabled = false;
            SpawnEnemyWave(initialEnemyCount, true);
            state = RunState.Combat;
        }

        private void RestartRun()
        {
            StopAllCoroutines();
            Time.timeScale = 1f;
            StartCoroutine(StartRun());
        }

        private IEnumerator GameOver()
        {
            state = RunState.RunOver;
            Time.timeScale = 0.45f;
            centerText.enabled = true;
            centerText.text = "RUN OVER\nTAP TO RESTART";
            yield return new WaitForSecondsRealtime(0.6f);
            Time.timeScale = 1f;
        }

        private IEnumerator ClearAndDropNextWave()
        {
            clearSequenceRunning = true;
            state = RunState.ArenaClear;
            ClearHostileBullets();
            centerText.enabled = true;
            centerText.text = "ARENA CLEAR";
            score += 200 + waveIndex * 25;
            ShakeCamera(0.12f);
            yield return new WaitForSeconds(arenaClearDuration);

            int nextCount = GetEnemyCountForWave(waveIndex + 1);
            yield return StartCoroutine(SpawnDroppingWave(nextCount));

            centerText.enabled = false;
            clearSequenceRunning = false;
            state = RunState.Combat;

            // Player ricochets can clear a whole incoming group before it activates.
            if (CountActiveEnemies() == 0)
            {
                StartCoroutine(ClearAndDropNextWave());
            }
        }

        private IEnumerator SpawnDroppingWave(int count)
        {
            state = RunState.EnemyDropWarning;
            waveIndex++;
            float[] lanes = PickDropLanes(count);

            for (int i = 0; i < lanes.Length; i++)
            {
                GameObject warning = GetDropWarning(i);
                warning.transform.position = new Vector3(lanes[i], ArenaHalfHeight - 0.35f, 0f);
                warning.SetActive(true);
            }

            centerText.text = "WARNING";
            yield return new WaitForSeconds(dropWarningDuration);

            state = RunState.EnemyDropping;
            for (int i = 0; i < lanes.Length; i++)
            {
                PistolController enemy = SpawnEnemy(new Vector2(lanes[i], ArenaHalfHeight + 1f), false);
                FallingEnemy falling = enemy.GetComponent<FallingEnemy>();
                if (falling == null)
                {
                    falling = enemy.gameObject.AddComponent<FallingEnemy>();
                }

                falling.Begin(
                    enemy,
                    ArenaHalfHeight - 1.15f - i * 0.32f,
                    enemyDropSpeed,
                    enemyActivationDelay + i * enemyActivationStagger);
                yield return new WaitForSeconds(0.1f);
            }

            for (int i = 0; i < dropWarnings.Count; i++)
            {
                dropWarnings[i].SetActive(false);
            }

            yield return new WaitUntil(() => CountActivatingEnemies() == 0);
            state = RunState.EnemyActivation;
            yield return null;
        }

        private IEnumerator DropPowerup(UpgradeData upgrade)
        {
            if (upgrade == null)
            {
                yield break;
            }

            activePowerups++;
            state = RunState.PowerupDropping;
            float lane = PickSafeLane();
            GameObject warning = GetDropWarning(0);
            warning.transform.position = new Vector3(lane, ArenaHalfHeight - 0.35f, 0f);
            warning.GetComponent<SpriteRenderer>().color = new Color(1f, 0.78f, 0.16f, 0.8f);
            warning.SetActive(true);

            yield return new WaitForSeconds(0.55f);

            GameObject powerup = new GameObject("Major Powerup - " + upgrade.displayName);
            powerup.transform.SetParent(dynamicRoot);
            powerup.transform.position = new Vector3(lane, ArenaHalfHeight + 0.9f, 0f);
            PowerupPickup pickup = powerup.AddComponent<PowerupPickup>();
            pickup.Initialize(this, squareSprite, upgrade);
            warning.SetActive(false);
            warning.GetComponent<SpriteRenderer>().color = new Color(1f, 0.1f, 0.05f, 0.65f);

            if (state == RunState.PowerupDropping)
            {
                state = RunState.Combat;
            }
        }

        private void CreateRuntimeData()
        {
            playerBulletData = ScriptableObject.CreateInstance<BulletData>();
            playerBulletData.speed = playerBulletSpeed;
            playerBulletData.damage = playerBulletDamage;
            playerBulletData.maxRicochets = playerStartingRicochets;
            playerBulletData.lifetime = 4.5f;
            playerBulletData.ownerImmunityDuration = 0.12f;

            enemyBulletData = ScriptableObject.CreateInstance<BulletData>();
            enemyBulletData.speed = enemyBulletSpeed;
            enemyBulletData.damage = enemyBulletDamage;
            enemyBulletData.maxRicochets = 3;
            enemyBulletData.lifetime = 4.2f;
            enemyBulletData.ownerImmunityDuration = 0.16f;

            playerGunData = ScriptableObject.CreateInstance<GunData>();
            playerGunData.gunId = "player_standard";
            playerGunData.mass = 1f;
            playerGunData.recoilForce = playerRecoilForce;
            playerGunData.fireCooldown = playerFireCooldown;
            playerGunData.maxHealth = playerMaxHealth;
            playerGunData.bullet = playerBulletData;

            enemyGunData = ScriptableObject.CreateInstance<GunData>();
            enemyGunData.gunId = "enemy_rookie";
            enemyGunData.mass = 1.05f;
            enemyGunData.recoilForce = enemyRecoilForce;
            enemyGunData.fireCooldown = 1f;
            enemyGunData.maxHealth = 1f;
            enemyGunData.bullet = enemyBulletData;

            enemyArchetype = ScriptableObject.CreateInstance<EnemyArchetypeData>();
            enemyArchetype.enemyId = "rookie";
            enemyArchetype.gun = enemyGunData;
            enemyArchetype.minFireDelay = enemyMinimumFireDelay;
            enemyArchetype.maxFireDelay = enemyMaximumFireDelay;
            enemyArchetype.requiredAimDot = 0.78f;
            enemyArchetype.predictionStrength = 0.1f;

            upgrades.Add(CreateUpgrade("damage_core", "Damage Core", UpgradeEffectType.Damage));
            upgrades.Add(CreateUpgrade("rapid_chamber", "Rapid Chamber", UpgradeEffectType.FireRate));
            upgrades.Add(CreateUpgrade("heavy_recoil", "Heavy Recoil", UpgradeEffectType.Recoil));
            upgrades.Add(CreateUpgrade("multi_shot", "Multi-Shot", UpgradeEffectType.MultiShot));
            upgrades.Add(CreateUpgrade("ricochet_core", "Ricochet Core", UpgradeEffectType.Ricochet));
            upgrades.Add(CreateUpgrade("penetrator", "Penetrator", UpgradeEffectType.Penetration));
            upgrades.Add(CreateUpgrade("large_caliber", "Large Caliber", UpgradeEffectType.Special));
            upgrades.Add(CreateUpgrade("repair_module", "Repair Module", UpgradeEffectType.Repair));
            upgrades.Add(CreateUpgrade("armor_plate", "Armor Plate", UpgradeEffectType.Shield));
            upgrades.Add(CreateUpgrade("stabilizer", "Stabilizer", UpgradeEffectType.Stabilizer));
            upgrades.Add(CreateUpgrade("wild_spinner", "Wild Spinner", UpgradeEffectType.Recoil));
            upgrades.Add(CreateUpgrade("split_ricochet", "Split Ricochet", UpgradeEffectType.Ricochet));
            upgrades.Add(CreateUpgrade("shock_round", "Shock Round", UpgradeEffectType.Special));
            upgrades.Add(CreateUpgrade("magnet_pickup", "Magnet Pickup", UpgradeEffectType.Special));

            int[] milestoneOrder = { 0, 1, 3, 4, 2, 5, 6, 9, 10, 11, 12 };
            for (int i = 0; i < milestoneOrder.Length; i++)
            {
                killMilestoneUpgrades.Add(upgrades[milestoneOrder[i]]);
            }

            majorDropTiming = ScriptableObject.CreateInstance<MajorDropTimingData>();
            _ = MajorDropScheduler.RollNextMajorDropDelaySeconds(majorDropTiming, majorDropRandom);
        }

        private static UpgradeData CreateUpgrade(string id, string displayName, UpgradeEffectType effectType)
        {
            UpgradeData upgrade = ScriptableObject.CreateInstance<UpgradeData>();
            upgrade.upgradeId = id;
            upgrade.displayName = displayName;
            upgrade.effectType = effectType;
            return upgrade;
        }

        private void ResetPlayerProgressionData()
        {
            playerBulletData.speed = playerBulletSpeed;
            playerBulletData.damage = playerBulletDamage;
            playerBulletData.maxRicochets = playerStartingRicochets;
            playerBulletData.penetration = 0;
            playerBulletData.bounceRetention = 0.96f;
            playerBulletData.visualScale = 1f;
            playerBulletData.shockForce = 0f;
            playerBulletData.splitOnFirstRicochet = false;
        }

        private UpgradeData GetRandomUpgrade()
        {
            return upgrades[UnityEngine.Random.Range(0, upgrades.Count)];
        }

        private int GetEnemyCountForWave(int wave)
        {
            int growth = Mathf.Max(0, wave - 1) / wavesPerExtraEnemy;
            return Mathf.Clamp(initialEnemyCount + growth, initialEnemyCount, maximumEnemiesPerWave);
        }

        private void CreateSprites()
        {
            squareSprite = CreateSprite("RecoilDuelSquare", 32, (texture, x, y) => Color.white);
            circleSprite = CreateSprite("RecoilDuelCircle", 32, (texture, x, y) =>
            {
                float dx = x - texture.width * 0.5f;
                float dy = y - texture.height * 0.5f;
                return dx * dx + dy * dy <= 14f * 14f ? Color.white : Color.clear;
            });
            triangleSprite = CreateSprite("RecoilDuelTriangle", 32, (texture, x, y) =>
            {
                float normalizedX = Mathf.Abs((x / 31f) - 0.5f);
                float normalizedY = y / 31f;
                return normalizedX < normalizedY * 0.45f + 0.08f ? Color.white : Color.clear;
            });
        }

        private static Sprite CreateSprite(string name, int size, Func<Texture2D, int, int, Color> colorFunc)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = name;
            texture.filterMode = FilterMode.Point;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    texture.SetPixel(x, y, colorFunc(texture, x, y));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 32f);
        }

        private void CreateRoots()
        {
            dynamicRoot = new GameObject("Runtime Objects").transform;
            dynamicRoot.SetParent(transform);
            bulletRoot = new GameObject("Bullet Pool").transform;
            bulletRoot.SetParent(transform);
            enemyRoot = new GameObject("Enemy Pool").transform;
            enemyRoot.SetParent(transform);
            vfxRoot = new GameObject("VFX Pool").transform;
            vfxRoot.SetParent(transform);
            bounceMaterial = new PhysicsMaterial2D("Recoil Duel Bounce") { bounciness = 1f, friction = 0f };
        }

        private void SetupCamera()
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                mainCamera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
                cameraObject.AddComponent<AudioListener>();
            }

            mainCamera.transform.position = new Vector3(0f, 0f, -10f);
            mainCamera.orthographic = true;
            mainCamera.orthographicSize = ArenaHalfHeight + 0.45f;
            mainCamera.backgroundColor = new Color(0.045f, 0.055f, 0.075f);
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
        }

        private void SetupAudio()
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.volume = 0.35f;
        }

        private void SetupArena()
        {
            GameObject arena = new GameObject("Portrait Arena");
            arena.transform.SetParent(transform);

            CreateWall(arena.transform, "Left Wall", new Vector2(-ArenaHalfWidth - 0.12f, 0f), new Vector2(0.24f, ArenaHalfHeight * 2.05f));
            CreateWall(arena.transform, "Right Wall", new Vector2(ArenaHalfWidth + 0.12f, 0f), new Vector2(0.24f, ArenaHalfHeight * 2.05f));
            CreateWall(arena.transform, "Top Wall", new Vector2(0f, ArenaHalfHeight + 0.12f), new Vector2(ArenaHalfWidth * 2.25f, 0.24f));
            CreateWall(arena.transform, "Bottom Wall", new Vector2(0f, -ArenaHalfHeight - 0.12f), new Vector2(ArenaHalfWidth * 2.25f, 0.24f));

            GameObject floor = new GameObject("Dark Arena Floor");
            floor.transform.SetParent(arena.transform);
            floor.transform.localPosition = Vector3.zero;
            SpriteRenderer floorRenderer = floor.AddComponent<SpriteRenderer>();
            floorRenderer.sprite = squareSprite;
            floorRenderer.color = new Color(0.08f, 0.095f, 0.12f);
            floorRenderer.sortingOrder = -10;
            floor.transform.localScale = new Vector3(ArenaHalfWidth * 2f, ArenaHalfHeight * 2f, 1f);
        }

        private void CreateWall(Transform parent, string name, Vector2 position, Vector2 size)
        {
            GameObject wall = new GameObject(name);
            wall.transform.SetParent(parent);
            wall.transform.position = position;
            wall.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = wall.AddComponent<SpriteRenderer>();
            renderer.sprite = squareSprite;
            renderer.color = new Color(0.18f, 0.2f, 0.25f);
            renderer.sortingOrder = -5;
            BoxCollider2D collider = wall.AddComponent<BoxCollider2D>();
            collider.sharedMaterial = bounceMaterial;
        }

        private void SetupUi()
        {
            GameObject canvasObject = new GameObject("Recoil Duel UI");
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObject.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1080f, 1920f);
            canvasObject.AddComponent<GraphicRaycaster>();

            scoreText = CreateText("Score", new Vector2(32f, -30f), TextAnchor.UpperLeft, 42, Color.white);
            hpText = CreateText("HP", new Vector2(-32f, -30f), TextAnchor.UpperRight, 42, new Color(0.34f, 0.84f, 1f));
            statusText = CreateText("Status", new Vector2(0f, -30f), TextAnchor.UpperCenter, 32, new Color(0.86f, 0.9f, 1f));
            centerText = CreateText("Center", Vector2.zero, TextAnchor.MiddleCenter, 78, Color.white);
            centerText.enabled = false;

            if (showDebugButtons)
            {
                Button infiniteHealthButton = CreateButton("Infinite Health", new Vector2(32f, 36f), new Vector2(330f, 94f), TextAnchor.LowerLeft);
                infiniteHealthButtonText = infiniteHealthButton.GetComponentInChildren<Text>();
                infiniteHealthButton.onClick.AddListener(ToggleInfiniteHealth);

                Button powerupButton = CreateButton("Drop Powerup", new Vector2(-32f, 36f), new Vector2(410f, 94f), TextAnchor.LowerRight);
                powerupButtonText = powerupButton.GetComponentInChildren<Text>();
                powerupButton.onClick.AddListener(DropNextDebugPowerup);

                if (EventSystem.current == null)
                {
                    GameObject eventSystemObject = new GameObject("Recoil Duel Event System");
                    eventSystemObject.AddComponent<EventSystem>();
                    InputSystemUIInputModule inputModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
                    inputModule.AssignDefaultActions();
                }

                UpdateDebugButtonLabels();
            }
        }

        private Button CreateButton(string name, Vector2 anchoredPosition, Vector2 size, TextAnchor anchor)
        {
            GameObject buttonObject = new GameObject(name + " Button");
            buttonObject.transform.SetParent(canvas.transform, false);
            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.08f, 0.12f, 0.17f, 0.94f);
            Button button = buttonObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.72f, 0.9f, 1f);
            colors.pressedColor = new Color(0.42f, 0.72f, 0.9f);
            button.colors = colors;

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            Vector2 anchorPoint = anchor == TextAnchor.LowerLeft ? Vector2.zero : new Vector2(1f, 0f);
            rect.anchorMin = anchorPoint;
            rect.anchorMax = anchorPoint;
            rect.pivot = anchorPoint;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            GameObject labelObject = new GameObject("Label");
            labelObject.transform.SetParent(buttonObject.transform, false);
            Text label = labelObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 26;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            RectTransform labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(12f, 6f);
            labelRect.offsetMax = new Vector2(-12f, -6f);
            return button;
        }

        private void ToggleInfiniteHealth()
        {
            infiniteHealth = !infiniteHealth;
            UpdateDebugButtonLabels();
            if (player != null)
            {
                ShowFloatingLabel(infiniteHealth ? "INFINITE HEALTH ON" : "INFINITE HEALTH OFF", player.transform.position + Vector3.up * 0.7f, Color.cyan);
            }
        }

        private void DropNextDebugPowerup()
        {
            if (!IsCombatActive || state == RunState.PowerupDropping || upgrades.Count == 0)
            {
                return;
            }

            UpgradeData upgrade = upgrades[nextDebugUpgradeIndex];
            nextDebugUpgradeIndex = (nextDebugUpgradeIndex + 1) % upgrades.Count;
            UpdateDebugButtonLabels();
            StartCoroutine(DropPowerup(upgrade));
        }

        private void UpdateDebugButtonLabels()
        {
            if (infiniteHealthButtonText != null)
            {
                infiniteHealthButtonText.text = infiniteHealth ? "INFINITE HEALTH: ON" : "INFINITE HEALTH: OFF";
                infiniteHealthButtonText.color = infiniteHealth ? Color.cyan : Color.white;
            }

            if (powerupButtonText != null && upgrades.Count > 0)
            {
                powerupButtonText.text = "DROP: " + upgrades[nextDebugUpgradeIndex].displayName.ToUpperInvariant();
            }
        }

        private Text CreateText(string name, Vector2 anchoredPosition, TextAnchor anchor, int size, Color color)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(canvas.transform, false);
            Text text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.raycastTarget = false;
            RectTransform rect = text.GetComponent<RectTransform>();
            rect.anchorMin = AnchorFor(anchor);
            rect.anchorMax = AnchorFor(anchor);
            rect.pivot = AnchorFor(anchor);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = name == "Center" ? new Vector2(900f, 420f) : new Vector2(520f, 120f);
            return text;
        }

        private static Vector2 AnchorFor(TextAnchor anchor)
        {
            switch (anchor)
            {
                case TextAnchor.UpperLeft:
                    return new Vector2(0f, 1f);
                case TextAnchor.UpperRight:
                    return new Vector2(1f, 1f);
                case TextAnchor.UpperCenter:
                    return new Vector2(0.5f, 1f);
                default:
                    return new Vector2(0.5f, 0.5f);
            }
        }

        private void CreatePools()
        {
            for (int i = 0; i < 48; i++)
            {
                GameObject bulletObject = new GameObject("Pooled Bullet");
                bulletObject.transform.SetParent(bulletRoot);
                BulletController bullet = bulletObject.AddComponent<BulletController>();
                bullet.Build(circleSprite, bounceMaterial, BulletRadius);
                bulletObject.SetActive(false);
                bulletPool.Add(bullet);
            }

            for (int i = 0; i < 8; i++)
            {
                PistolController enemy = CreatePistol("Enemy Gun", TeamId.Enemy, enemyGunData, new Color(0.95f, 0.1f, 0.08f), enemyRoot);
                enemy.gameObject.SetActive(false);
                enemyPool.Add(enemy);
            }
        }

        private void CreatePlayer()
        {
            player = CreatePistol("Blue Player Gun", TeamId.Player, playerGunData, new Color(0.05f, 0.55f, 1f), dynamicRoot);
            player.transform.position = new Vector3(0f, -2.4f, 0f);
            player.transform.rotation = Quaternion.Euler(0f, 0f, 80f);
            player.Initialize(this, playerGunData, TeamId.Player, true);
            player.Health.AddShield(playerStartingShieldHits);
        }

        private PistolController CreatePistol(string name, TeamId team, GunData gunData, Color color, Transform parent)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent);
            root.transform.localScale = Vector3.one;

            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.mass = gunData.mass;
            body.linearDamping = gunData.linearDamping;
            body.angularDamping = gunData.angularDamping;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
            collider.radius = 0.34f;
            collider.sharedMaterial = bounceMaterial;

            HealthComponent health = root.AddComponent<HealthComponent>();
            PistolController pistol = root.AddComponent<PistolController>();

            GameObject bodySprite = CreateSpriteChild(root.transform, "Body", squareSprite, color, new Vector2(0f, 0f), new Vector2(0.66f, 0.28f), 4);
            GameObject barrelSprite = CreateSpriteChild(root.transform, "Barrel", squareSprite, color * 1.18f, new Vector2(0.38f, 0f), new Vector2(0.46f, 0.16f), 5);
            GameObject gripSprite = CreateSpriteChild(root.transform, "Grip", squareSprite, color * 0.75f, new Vector2(-0.18f, -0.28f), new Vector2(0.22f, 0.42f), 3);
            GameObject shieldVisual = CreateSpriteChild(root.transform, "Shield", circleSprite, new Color(0.1f, 0.82f, 1f, 0.24f), Vector2.zero, new Vector2(1.05f, 1.05f), 2);
            shieldVisual.SetActive(false);

            GameObject muzzle = new GameObject("MuzzlePoint");
            muzzle.transform.SetParent(root.transform);
            muzzle.transform.localPosition = new Vector3(0.68f, 0f, 0f);

            health.Initialize(this, team, gunData.maxHealth, new[]
            {
                bodySprite.GetComponent<SpriteRenderer>(),
                barrelSprite.GetComponent<SpriteRenderer>(),
                gripSprite.GetComponent<SpriteRenderer>()
            });
            health.SetShieldVisual(shieldVisual);
            pistol.SetReferences(body, health, muzzle.transform, new[]
            {
                bodySprite.GetComponent<SpriteRenderer>(),
                barrelSprite.GetComponent<SpriteRenderer>(),
                gripSprite.GetComponent<SpriteRenderer>()
            });
            pistol.Initialize(this, gunData, team, team == TeamId.Player);
            return pistol;
        }

        private GameObject CreateSpriteChild(Transform parent, string name, Sprite sprite, Color color, Vector2 localPosition, Vector2 localScale, int sortingOrder)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent);
            child.transform.localPosition = localPosition;
            child.transform.localScale = new Vector3(localScale.x, localScale.y, 1f);
            SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return child;
        }

        private void SpawnEnemyWave(int count, bool immediate)
        {
            waveIndex++;
            for (int i = 0; i < count; i++)
            {
                float x = Mathf.Lerp(-1.8f, 1.8f, count == 1 ? 0.5f : i / (float)(count - 1));
                float y = immediate ? 1.3f + i * 0.8f : ArenaHalfHeight + 1f;
                PistolController enemy = SpawnEnemy(new Vector2(x, y), immediate);
                if (immediate)
                {
                    enemy.transform.rotation = Quaternion.Euler(0f, 0f, -90f + i * 60f);
                    enemy.ActivateEnemy(0.8f + i * 0.3f);
                }
            }
        }

        private PistolController SpawnEnemy(Vector2 position, bool active)
        {
            PistolController enemy = GetInactiveEnemy();
            enemy.transform.position = position;
            enemy.transform.rotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(210f, 330f));
            enemy.gameObject.SetActive(true);
            enemy.Initialize(this, enemyGunData, TeamId.Enemy, false);
            EnemyBrain brain = enemy.GetComponent<EnemyBrain>();
            if (brain == null)
            {
                brain = enemy.gameObject.AddComponent<EnemyBrain>();
            }

            brain.Initialize(enemy, this, enemyArchetype);
            brain.SetDifficulty(waveIndex, enemyFireDelayReductionPerWave, enemyMinimumFireDelayMultiplier);
            brain.SetActiveBrain(active);
            enemy.ApplyEnemyWaveScaling(
                waveIndex,
                enemyBaseHealth,
                enemyMaximumHealth,
                enemyHealthIncrease,
                enemyHealthIncreaseEveryWaves,
                enemyFireDelayReductionPerWave,
                enemyMinimumFireDelayMultiplier,
                enemyRecoilIncreasePerWave);
            return enemy;
        }

        private BulletController GetInactiveBullet()
        {
            for (int i = 0; i < bulletPool.Count; i++)
            {
                if (!bulletPool[i].gameObject.activeSelf)
                {
                    return bulletPool[i];
                }
            }

            BulletController recycled = bulletPool[0];
            recycled.gameObject.SetActive(false);
            return recycled;
        }

        private BulletController GetInactiveBulletWithoutRecycling()
        {
            for (int i = 0; i < bulletPool.Count; i++)
            {
                if (!bulletPool[i].gameObject.activeSelf)
                {
                    return bulletPool[i];
                }
            }

            return null;
        }

        private PistolController GetInactiveEnemy()
        {
            for (int i = 0; i < enemyPool.Count; i++)
            {
                if (!enemyPool[i].gameObject.activeSelf)
                {
                    return enemyPool[i];
                }
            }

            PistolController recycled = enemyPool[0];
            recycled.DeactivateForPool();
            return recycled;
        }

        private GameObject GetDropWarning(int index)
        {
            while (dropWarnings.Count <= index)
            {
                GameObject warning = new GameObject("Drop Warning");
                warning.transform.SetParent(dynamicRoot);
                warning.transform.localScale = new Vector3(0.55f, 0.55f, 1f);
                SpriteRenderer renderer = warning.AddComponent<SpriteRenderer>();
                renderer.sprite = triangleSprite;
                renderer.color = new Color(1f, 0.1f, 0.05f, 0.65f);
                renderer.sortingOrder = 8;
                warning.SetActive(false);
                dropWarnings.Add(warning);
            }

            return dropWarnings[index];
        }

        private float[] PickDropLanes(int count)
        {
            List<float> candidates = new List<float> { -2.45f, -1.2f, 0f, 1.2f, 2.45f };
            float playerX = player != null ? player.transform.position.x : 0f;
            candidates.Sort((a, b) => Mathf.Abs(b - playerX).CompareTo(Mathf.Abs(a - playerX)));

            float[] lanes = new float[count];
            for (int i = 0; i < count; i++)
            {
                lanes[i] = candidates[i % candidates.Count];
            }

            return lanes;
        }

        private float PickSafeLane()
        {
            float[] lanes = PickDropLanes(1);
            return lanes[0];
        }

        private int CountActiveEnemies()
        {
            int count = 0;
            for (int i = 0; i < enemyPool.Count; i++)
            {
                if (enemyPool[i].gameObject.activeSelf && enemyPool[i].IsAlive)
                {
                    count++;
                }
            }

            return count;
        }

        private int CountActivatingEnemies()
        {
            int count = 0;
            for (int i = 0; i < enemyPool.Count; i++)
            {
                FallingEnemy falling = enemyPool[i].GetComponent<FallingEnemy>();
                if (enemyPool[i].gameObject.activeSelf && falling != null && falling.IsDropping)
                {
                    count++;
                }
            }

            return count;
        }

        private void ClearRuntimeObjects()
        {
            if (player != null)
            {
                Destroy(player.gameObject);
                player = null;
            }

            for (int i = 0; i < enemyPool.Count; i++)
            {
                enemyPool[i].DeactivateForPool();
            }

            for (int i = 0; i < bulletPool.Count; i++)
            {
                bulletPool[i].gameObject.SetActive(false);
            }

            for (int i = dynamicRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = dynamicRoot.GetChild(i);
                if (child.name.StartsWith("Major Powerup", StringComparison.Ordinal))
                {
                    Destroy(child.gameObject);
                }
            }
        }

        private void ClearHostileBullets()
        {
            for (int i = 0; i < bulletPool.Count; i++)
            {
                if (bulletPool[i].gameObject.activeSelf && bulletPool[i].OwnerTeam == TeamId.Enemy)
                {
                    bulletPool[i].FadeAndDisable();
                }
            }
        }

        private void ClampBody(Rigidbody2D body)
        {
            Vector2 position = body.position;
            position.x = Mathf.Clamp(position.x, -ArenaHalfWidth + 0.35f, ArenaHalfWidth - 0.35f);
            position.y = Mathf.Clamp(position.y, -ArenaHalfHeight + 0.35f, ArenaHalfHeight - 0.35f);
            body.position = position;
            body.linearVelocity = Vector2.ClampMagnitude(body.linearVelocity, 8f);
            body.angularVelocity = Mathf.Clamp(body.angularVelocity, -520f, 520f);
        }

        private bool WasPrimaryPressed()
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return false;
            }

            bool mousePressed = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            bool touchPressed = Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
            bool keyboardRestart = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
            return mousePressed || touchPressed || keyboardRestart;
        }

        private void FeedbackShot(PistolController source)
        {
            SpawnBurst(source.Muzzle.position, source.Team == TeamId.Player ? new Color(0.4f, 0.85f, 1f) : new Color(1f, 0.3f, 0.16f), 5);
            ShakeCamera(source.Team == TeamId.Player ? 0.055f : 0.035f);

            if (source.Team == TeamId.Player)
            {
                PlayTone(680f, 0.035f);
                Vibrate(0.08f);
            }
        }

        private void SpawnBurst(Vector3 position, Color color, int count)
        {
            for (int i = 0; i < count; i++)
            {
                GameObject particle = new GameObject("Burst Spark");
                particle.transform.SetParent(vfxRoot);
                particle.transform.position = position;
                particle.transform.localScale = Vector3.one * UnityEngine.Random.Range(0.045f, 0.09f);
                SpriteRenderer renderer = particle.AddComponent<SpriteRenderer>();
                renderer.sprite = circleSprite;
                renderer.color = color;
                renderer.sortingOrder = 12;
                BurstSpark spark = particle.AddComponent<BurstSpark>();
                spark.Begin(UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(1.4f, 4.5f), 0.22f);
            }
        }

        private void ShowFloatingLabel(string text, Vector3 worldPosition, Color color)
        {
            GameObject labelObject = new GameObject("Floating Label");
            labelObject.transform.SetParent(canvas.transform, false);
            Text label = labelObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 34;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = color;
            label.raycastTarget = false;
            RectTransform rect = label.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(420f, 80f);
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(mainCamera, worldPosition);
            rect.position = screenPoint;
            label.text = text;
            labelObject.AddComponent<FloatingLabel>().Begin(label);
        }

        private void ShakeCamera(float amount)
        {
            StartCoroutine(CameraShake(amount, 0.12f));
        }

        private IEnumerator CameraShake(float amount, float duration)
        {
            Vector3 basePosition = new Vector3(0f, 0f, -10f);
            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                Vector2 offset = UnityEngine.Random.insideUnitCircle * amount * (1f - timer / duration);
                mainCamera.transform.position = basePosition + new Vector3(offset.x, offset.y, 0f);
                yield return null;
            }

            mainCamera.transform.position = basePosition;
        }

        private IEnumerator HitStop(float duration)
        {
            if (state == RunState.RunOver)
            {
                yield break;
            }

            RunState previous = state;
            state = RunState.PlayerHitStop;
            Time.timeScale = 0.18f;
            yield return new WaitForSecondsRealtime(duration);
            Time.timeScale = 1f;
            if (state == RunState.PlayerHitStop)
            {
                state = previous == RunState.PlayerHitStop ? RunState.Combat : previous;
            }
        }

        private void PlayTone(float frequency, float duration)
        {
            AudioClip clip = AudioClip.Create("tone", Mathf.CeilToInt(44100f * duration), 1, 44100, false);
            float[] samples = new float[clip.samples];
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * i / 44100f) * (1f - i / (float)samples.Length) * 0.18f;
            }

            clip.SetData(samples, 0);
            audioSource.PlayOneShot(clip);
        }

        private void Vibrate(float cooldown)
        {
            if (Time.unscaledTime - lastHapticTime < cooldown)
            {
                return;
            }

            lastHapticTime = Time.unscaledTime;
#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
        }

        private void UpdateUi()
        {
            scoreText.text = "Score " + score;
            statusText.text = "WAVE " + waveIndex + "  KILLS " + totalEnemyKills + "  MK " + (player != null ? player.UpgradeTier : 0);
            if (player != null)
            {
                string shield = player.Health.ShieldHits > 0 ? " +" + player.Health.ShieldHits + " SH" : string.Empty;
                hpText.text = infiniteHealth
                    ? "HP INF" + shield
                    : "HP " + Mathf.Max(0f, player.Health.CurrentHealth).ToString("0.#") + "/" + player.Health.MaxHealth.ToString("0.#") + shield;
            }
        }
    }

    public sealed class PistolController : MonoBehaviour
    {
        private RecoilDuelGame game;
        private GunData gunData;
        private float fireCooldownMultiplier = 1f;
        private float recoilMultiplier = 1f;
        private float stabilizerMultiplier = 1f;
        private float spinKick;
        private float nextFireTime;
        private int projectileCount = 1;
        private SpriteRenderer[] renderers;

        public Rigidbody2D Body { get; private set; }
        public HealthComponent Health { get; private set; }
        public Transform Muzzle { get; private set; }
        public TeamId Team { get; private set; }
        public bool IsPlayer { get; private set; }
        public bool IsAlive => Health != null && Health.IsAlive;
        public Vector2 MuzzleRight => Muzzle.right;
        public int UpgradeTier { get; private set; }

        public void SetReferences(Rigidbody2D body, HealthComponent health, Transform muzzle, SpriteRenderer[] spriteRenderers)
        {
            Body = body;
            Health = health;
            Muzzle = muzzle;
            renderers = spriteRenderers;
        }

        public void Initialize(RecoilDuelGame owner, GunData data, TeamId team, bool playerControlled)
        {
            game = owner;
            gunData = data;
            Team = team;
            IsPlayer = playerControlled;
            fireCooldownMultiplier = 1f;
            recoilMultiplier = 1f;
            stabilizerMultiplier = 1f;
            spinKick = 0f;
            projectileCount = 1;
            UpgradeTier = 0;
            nextFireTime = Time.time + 0.15f;
            Body.mass = data.mass;
            Body.linearDamping = data.linearDamping;
            Body.angularDamping = data.angularDamping;
            Body.linearVelocity = Vector2.zero;
            Body.angularVelocity = 0f;
            Health.ResetHealth(team, data.maxHealth);
        }

        public bool TryFire()
        {
            if (!IsAlive || Time.time < nextFireTime)
            {
                return false;
            }

            nextFireTime = Time.time + gunData.fireCooldown * fireCooldownMultiplier;
            float spread = projectileCount == 1 ? 0f : projectileCount == 2 ? 8f : 10f;
            for (int i = 0; i < projectileCount; i++)
            {
                float t = projectileCount == 1 ? 0.5f : i / (float)(projectileCount - 1);
                float angle = Mathf.Lerp(-spread, spread, t);
                Vector2 direction = Quaternion.Euler(0f, 0f, angle) * MuzzleRight;
                game.SpawnBullet(this, gunData.bullet, direction, i == 0);
            }

            Body.AddForceAtPosition(-MuzzleRight * gunData.recoilForce * recoilMultiplier, Muzzle.position, ForceMode2D.Impulse);
            Body.AddTorque(UnityEngine.Random.Range(-0.18f - spinKick, 0.18f + spinKick), ForceMode2D.Impulse);
            Body.angularVelocity *= stabilizerMultiplier;
            return true;
        }

        public void ApplyFireRateMultiplier(float multiplier)
        {
            fireCooldownMultiplier = Mathf.Clamp(fireCooldownMultiplier * multiplier, 0.35f, 1f);
        }

        public void ApplyRecoilMultiplier(float multiplier)
        {
            recoilMultiplier = Mathf.Clamp(recoilMultiplier * multiplier, 0.65f, 2.4f);
        }

        public void ApplyStabilizer(float multiplier)
        {
            stabilizerMultiplier = Mathf.Clamp(stabilizerMultiplier * multiplier, 0.35f, 1f);
            Body.angularDamping = Mathf.Min(2.2f, Body.angularDamping + 0.35f);
        }

        public void AddSpinKick(float amount)
        {
            spinKick = Mathf.Min(0.8f, spinKick + amount);
        }

        public void AddProjectile()
        {
            projectileCount = Mathf.Min(3, projectileCount + 1);
        }

        public void AdvanceUpgradeTier(string upgradeName)
        {
            UpgradeTier++;
            renderers[1].transform.localScale = new Vector3(
                Mathf.Min(0.72f, renderers[1].transform.localScale.x + 0.025f),
                renderers[1].transform.localScale.y,
                1f);
            Muzzle.localPosition = new Vector3(Mathf.Min(0.9f, Muzzle.localPosition.x + 0.018f), 0f, 0f);

            GameObject accent = new GameObject(upgradeName + " Accent");
            accent.transform.SetParent(transform);
            accent.transform.localPosition = new Vector3(0.02f + (UpgradeTier % 3) * 0.12f, 0.18f + (UpgradeTier % 2) * 0.07f, 0f);
            accent.transform.localScale = new Vector3(0.2f + Mathf.Min(UpgradeTier, 6) * 0.035f, 0.055f, 1f);
            SpriteRenderer renderer = accent.AddComponent<SpriteRenderer>();
            renderer.sprite = renderers[0].sprite;
            renderer.color = Color.Lerp(new Color(1f, 0.76f, 0.15f), Color.cyan, (UpgradeTier % 5) / 4f);
            renderer.sortingOrder = 7;
        }

        public void ApplyEnemyWaveScaling(
            int wave,
            float baseHealth,
            float maximumHealth,
            float healthIncrease,
            int healthIncreaseEveryWaves,
            float fireDelayReductionPerWave,
            float minimumFireDelayMultiplier,
            float recoilIncreasePerWave)
        {
            if (Team != TeamId.Enemy)
            {
                return;
            }

            int completedWaveSteps = Mathf.Max(0, wave - 1) / Mathf.Max(1, healthIncreaseEveryWaves);
            fireCooldownMultiplier = Mathf.Clamp(
                1f - Mathf.Max(0, wave - 1) * fireDelayReductionPerWave,
                minimumFireDelayMultiplier,
                1f);
            recoilMultiplier = Mathf.Min(2.4f, 1f + Mathf.Max(0, wave - 1) * recoilIncreasePerWave);
            float health = baseHealth + completedWaveSteps * healthIncrease;
            Health.ResetHealth(TeamId.Enemy, Mathf.Min(maximumHealth, health));
        }

        public void ActivateEnemy(float delay)
        {
            EnemyBrain brain = GetComponent<EnemyBrain>();
            if (brain != null)
            {
                brain.SetActiveBrain(false);
                StartCoroutine(EnableEnemyAfterDelay(brain, delay));
            }
        }

        public void DeactivateForPool()
        {
            StopAllCoroutines();
            EnemyBrain brain = GetComponent<EnemyBrain>();
            if (brain != null)
            {
                brain.SetActiveBrain(false);
            }

            FallingEnemy falling = GetComponent<FallingEnemy>();
            if (falling != null)
            {
                falling.CancelDrop();
            }

            gameObject.SetActive(false);
        }

        private IEnumerator EnableEnemyAfterDelay(EnemyBrain brain, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (brain != null && gameObject.activeSelf)
            {
                brain.EnableBrain();
            }
        }
    }

    public sealed class HealthComponent : MonoBehaviour
    {
        private RecoilDuelGame game;
        private SpriteRenderer[] renderers;
        private Color[] originalColors;
        private GameObject shieldVisual;
        private float invulnerableUntil;
        private Coroutine flashRoutine;

        public TeamId Team { get; private set; }
        public float CurrentHealth { get; private set; }
        public float MaxHealth { get; private set; }
        public bool IsAlive => CurrentHealth > 0f;
        public int LastHitRicochetCount { get; private set; }
        public int ShieldHits { get; private set; }

        public void Initialize(RecoilDuelGame owner, TeamId team, float maxHealth, SpriteRenderer[] spriteRenderers)
        {
            game = owner;
            renderers = spriteRenderers;
            originalColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                originalColors[i] = renderers[i].color;
            }

            ResetHealth(team, maxHealth);
        }

        public void SetShieldVisual(GameObject visual)
        {
            shieldVisual = visual;
            UpdateShieldVisual();
        }

        public void ResetHealth(TeamId team, float maxHealth)
        {
            Team = team;
            MaxHealth = maxHealth;
            CurrentHealth = maxHealth;
            ShieldHits = 0;
            LastHitRicochetCount = 0;
            invulnerableUntil = 0f;
            if (renderers != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    renderers[i].color = originalColors[i];
                }
            }

            UpdateShieldVisual();
        }

        public bool ApplyDamage(DamageInfo damage, int ricochetCount)
        {
            if (!IsAlive || Time.time < invulnerableUntil)
            {
                return false;
            }

            if (Team == TeamId.Player && game.InfiniteHealth)
            {
                StartDamageFlash();
                game.OnGunDamaged(this, damage);
                return true;
            }

            if (ShieldHits > 0)
            {
                ShieldHits--;
                invulnerableUntil = Time.time + 0.18f;
                UpdateShieldVisual();
                StartDamageFlash();
                game.OnGunDamaged(this, damage);
                return true;
            }

            CurrentHealth -= damage.Damage;
            LastHitRicochetCount = ricochetCount;
            invulnerableUntil = Team == TeamId.Player ? Time.time + game.PlayerInvulnerabilityDuration : 0f;

            StartDamageFlash();
            game.OnGunDamaged(this, damage);

            if (CurrentHealth <= 0f)
            {
                game.OnGunDestroyed(this, damage);
            }

            return true;
        }

        public bool Repair(float amount)
        {
            if (CurrentHealth >= MaxHealth)
            {
                return false;
            }

            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
            return true;
        }

        public void AddShield(int hits)
        {
            ShieldHits = Mathf.Min(5, ShieldHits + Mathf.Max(0, hits));
            UpdateShieldVisual();
        }

        private void StartDamageFlash()
        {
            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
            }

            flashRoutine = StartCoroutine(FlashWhite());
        }

        private void UpdateShieldVisual()
        {
            if (shieldVisual != null)
            {
                shieldVisual.SetActive(ShieldHits > 0);
            }
        }

        private IEnumerator FlashWhite()
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].color = Color.white;
            }

            yield return new WaitForSecondsRealtime(0.055f);

            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].color = originalColors[i];
            }
        }
    }

    public sealed class BulletController : MonoBehaviour
    {
        private Rigidbody2D body;
        private CircleCollider2D circleCollider;
        private SpriteRenderer spriteRenderer;
        private RecoilDuelGame game;
        private BulletData data;
        private GameObject owner;
        private float spawnTime;
        private float expiresAt;
        private int bouncesRemaining;
        private int ricochetCount;
        private int penetrationsRemaining;
        private float damageMultiplier;
        private bool canSplit;
        private Collider2D ignoredCollider;

        public TeamId OwnerTeam { get; private set; }
        public GameObject SourceOwner => owner;
        public BulletData Data => data;

        public void Build(Sprite sprite, PhysicsMaterial2D bounceMaterial, float radius)
        {
            body = gameObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            circleCollider = gameObject.AddComponent<CircleCollider2D>();
            circleCollider.radius = radius;
            circleCollider.sharedMaterial = bounceMaterial;
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingOrder = 10;
            transform.localScale = Vector3.one * 0.18f;
        }

        public void Launch(
            Vector3 position,
            Vector2 direction,
            TeamId team,
            GameObject source,
            BulletData bulletData,
            Color color,
            RecoilDuelGame ownerGame,
            bool allowSplit = true,
            float launchDamageMultiplier = 1f)
        {
            StopAllCoroutines();
            RestoreIgnoredCollision();
            game = ownerGame;
            data = bulletData;
            owner = source;
            OwnerTeam = team;
            spawnTime = Time.time;
            expiresAt = Time.time + bulletData.lifetime;
            bouncesRemaining = bulletData.maxRicochets;
            penetrationsRemaining = bulletData.penetration;
            ricochetCount = 0;
            damageMultiplier = launchDamageMultiplier;
            canSplit = allowSplit;
            transform.position = position;
            transform.rotation = Quaternion.FromToRotation(Vector3.right, direction);
            transform.localScale = Vector3.one * (0.18f * bulletData.visualScale);
            spriteRenderer.color = color;
            gameObject.SetActive(true);
            body.linearVelocity = direction.normalized * bulletData.speed;
            body.angularVelocity = 0f;
        }

        private void Update()
        {
            if (Time.time >= expiresAt)
            {
                game.OnBulletExpired(this);
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            HealthComponent health = collision.collider.GetComponentInParent<HealthComponent>();
            if (health != null)
            {
                TryDamage(health, collision.GetContact(0).point, collision.collider);
                return;
            }

            if (canSplit && ricochetCount == 0 && data.splitOnFirstRicochet)
            {
                Vector2 reflected = Vector2.Reflect(body.linearVelocity.normalized, collision.GetContact(0).normal);
                game.SpawnSplitRicochets(this, reflected);
                canSplit = false;
            }

            bouncesRemaining--;
            ricochetCount++;
            game.AwardRicochet(this);
            if (bouncesRemaining < 0)
            {
                game.OnBulletExpired(this);
                return;
            }

            body.linearVelocity *= data.bounceRetention;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PowerupPickup pickup = other.GetComponentInParent<PowerupPickup>();
            if (pickup != null && OwnerTeam == TeamId.Player)
            {
                pickup.Collect();
                game.OnBulletExpired(this);
            }
        }

        private void TryDamage(HealthComponent health, Vector2 hitPoint, Collider2D hitCollider)
        {
            if (health.gameObject == owner && Time.time - spawnTime < data.ownerImmunityDuration)
            {
                return;
            }

            if (OwnerTeam == TeamId.Player && health.Team == TeamId.Player)
            {
                return;
            }

            Vector2 travelVelocity = body.linearVelocity;
            DamageInfo damage = new DamageInfo(data.damage * damageMultiplier, OwnerTeam, owner, hitPoint, travelVelocity.normalized);
            bool damaged = health.ApplyDamage(damage, ricochetCount);
            if (damaged)
            {
                if (data.shockForce > 0f)
                {
                    game.ApplyShockPulse(hitPoint, data.shockForce);
                }

                if (penetrationsRemaining > 0 && health.Team == TeamId.Enemy)
                {
                    penetrationsRemaining--;
                    StartCoroutine(PassThrough(hitCollider, travelVelocity));
                }
                else
                {
                    game.OnBulletExpired(this);
                }
            }
        }

        private IEnumerator PassThrough(Collider2D hitCollider, Vector2 travelVelocity)
        {
            ignoredCollider = hitCollider;
            Physics2D.IgnoreCollision(circleCollider, hitCollider, true);
            transform.position += (Vector3)(travelVelocity.normalized * 0.18f);
            yield return new WaitForFixedUpdate();
            body.linearVelocity = travelVelocity * 0.82f;
            yield return new WaitForSeconds(0.08f);
            if (circleCollider != null && hitCollider != null)
            {
                Physics2D.IgnoreCollision(circleCollider, hitCollider, false);
            }
            ignoredCollider = null;
        }

        private void OnDisable()
        {
            RestoreIgnoredCollision();
        }

        private void RestoreIgnoredCollision()
        {
            if (circleCollider != null && ignoredCollider != null)
            {
                Physics2D.IgnoreCollision(circleCollider, ignoredCollider, false);
            }

            ignoredCollider = null;
        }

        public void FadeAndDisable()
        {
            game.OnBulletExpired(this);
        }
    }

    public sealed class EnemyBrain : MonoBehaviour
    {
        private PistolController pistol;
        private RecoilDuelGame game;
        private EnemyArchetypeData archetype;
        private bool activeBrain;
        private float nextShotTime;
        private float fireDelayMultiplier = 1f;

        public void Initialize(PistolController owner, RecoilDuelGame ownerGame, EnemyArchetypeData data)
        {
            pistol = owner;
            game = ownerGame;
            archetype = data;
            nextShotTime = Time.time + UnityEngine.Random.Range(archetype.minFireDelay, archetype.maxFireDelay);
        }

        public void SetDifficulty(int wave, float reductionPerWave, float minimumMultiplier)
        {
            fireDelayMultiplier = Mathf.Clamp(
                1f - Mathf.Max(0, wave - 1) * reductionPerWave,
                minimumMultiplier,
                1f);
        }

        public void SetActiveBrain(bool active)
        {
            activeBrain = active;
        }

        public void EnableBrain()
        {
            activeBrain = true;
            nextShotTime = Time.time + UnityEngine.Random.Range(0.12f, 0.32f) * fireDelayMultiplier;
        }

        private void FixedUpdate()
        {
            if (!activeBrain || !game.IsCombatActive || pistol == null || !pistol.IsAlive)
            {
                return;
            }

            PistolController player = game.PlayerPistol;
            if (player == null || !player.IsAlive)
            {
                return;
            }

            Vector2 toPlayer = player.transform.position - transform.position;
            float targetAngle = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg;
            float nextAngle = Mathf.MoveTowardsAngle(pistol.Body.rotation, targetAngle, 140f * Time.fixedDeltaTime);
            pistol.Body.MoveRotation(nextAngle);
        }

        private void Update()
        {
            if (!activeBrain || !game.IsCombatActive || Time.time < nextShotTime || !pistol.IsAlive)
            {
                return;
            }

            PistolController target = game.PlayerPistol;
            if (target == null || !target.IsAlive)
            {
                return;
            }

            Vector2 toPlayer = (target.transform.position - transform.position).normalized;
            float aimDot = Vector2.Dot(pistol.MuzzleRight, toPlayer);
            if (aimDot >= archetype.requiredAimDot || UnityEngine.Random.value < 0.25f)
            {
                pistol.TryFire();
                nextShotTime = Time.time + UnityEngine.Random.Range(archetype.minFireDelay, archetype.maxFireDelay) * fireDelayMultiplier;
            }
        }
    }

    public sealed class FallingEnemy : MonoBehaviour
    {
        private PistolController pistol;
        private float landingY;
        private float fallSpeed;
        private float activationDelay;
        private bool landed;
        private SpriteRenderer[] renderers;
        private Color[] landingColors;

        public bool IsDropping { get; private set; }

        public void Begin(PistolController owner, float targetY, float speed, float delay)
        {
            StopAllCoroutines();
            pistol = owner;
            landingY = targetY;
            fallSpeed = speed;
            activationDelay = delay;
            landed = false;
            IsDropping = true;
            renderers = GetComponentsInChildren<SpriteRenderer>();
            landingColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                landingColors[i] = renderers[i].color;
            }

            pistol.Body.linearVelocity = Vector2.down * fallSpeed;
            pistol.Body.angularVelocity = UnityEngine.Random.Range(-140f, 140f);
        }

        public void CancelDrop()
        {
            StopAllCoroutines();
            IsDropping = false;
            landed = true;
        }

        private void Update()
        {
            if (!IsDropping || landed || pistol == null)
            {
                return;
            }

            if (transform.position.y <= landingY)
            {
                landed = true;
                pistol.Body.linearVelocity = Vector2.zero;
                pistol.Body.angularVelocity = 0f;
                StartCoroutine(ActivateAfterDelay());
            }
        }

        private IEnumerator ActivateAfterDelay()
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].color = Color.white;
            }

            yield return new WaitForSeconds(0.07f);

            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].color = landingColors[i];
            }

            yield return new WaitForSeconds(activationDelay);
            EnemyBrain brain = GetComponent<EnemyBrain>();
            if (brain != null)
            {
                brain.EnableBrain();
            }

            IsDropping = false;
        }
    }

    public sealed class PowerupPickup : MonoBehaviour
    {
        private RecoilDuelGame game;
        private Rigidbody2D body;
        private UpgradeData upgrade;
        private bool collected;
        private bool initialized;

        public void Initialize(RecoilDuelGame owner, Sprite sprite, UpgradeData upgrade)
        {
            game = owner;
            this.upgrade = upgrade;
            initialized = true;
            body = gameObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.linearVelocity = Vector2.down * 2.2f;
            body.angularVelocity = 90f;
            CircleCollider2D collider = gameObject.AddComponent<CircleCollider2D>();
            collider.radius = 0.32f;
            collider.isTrigger = true;
            SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = GetUpgradeColor(upgrade.effectType);
            renderer.sortingOrder = 9;
            transform.localScale = new Vector3(0.5f, 0.5f, 1f);
            Destroy(gameObject, 28f);
        }

        private void Update()
        {
            if (game.MagnetPowerups && game.PlayerPistol != null)
            {
                Vector2 toPlayer = game.PlayerPistol.transform.position - transform.position;
                if (toPlayer.sqrMagnitude < 25f)
                {
                    body.linearVelocity = Vector2.Lerp(body.linearVelocity, toPlayer.normalized * 4.5f, Time.deltaTime * 4f);
                    return;
                }
            }

            if (transform.position.y <= -4.8f)
            {
                body.linearVelocity = Vector2.zero;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponentInParent<PistolController>()?.Team == TeamId.Player)
            {
                Collect();
            }
        }

        public void Collect()
        {
            if (collected)
            {
                return;
            }

            collected = true;
            game.ApplyUpgrade(upgrade);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (initialized && game != null)
            {
                initialized = false;
                game.OnPowerupRemoved();
            }
        }

        private static Color GetUpgradeColor(UpgradeEffectType effectType)
        {
            switch (effectType)
            {
                case UpgradeEffectType.Damage:
                case UpgradeEffectType.Recoil:
                    return new Color(1f, 0.35f, 0.18f);
                case UpgradeEffectType.Repair:
                case UpgradeEffectType.Shield:
                    return new Color(0.15f, 1f, 0.62f);
                case UpgradeEffectType.Ricochet:
                case UpgradeEffectType.Penetration:
                    return new Color(0.2f, 0.85f, 1f);
                default:
                    return new Color(1f, 0.76f, 0.15f);
            }
        }
    }

    public sealed class BurstSpark : MonoBehaviour
    {
        private Vector2 velocity;
        private float lifetime;
        private float age;
        private SpriteRenderer spriteRenderer;

        public void Begin(Vector2 startVelocity, float duration)
        {
            velocity = startVelocity;
            lifetime = duration;
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            age += Time.unscaledDeltaTime;
            transform.position += (Vector3)(velocity * Time.unscaledDeltaTime);
            if (spriteRenderer != null)
            {
                Color color = spriteRenderer.color;
                color.a = Mathf.Clamp01(1f - age / lifetime);
                spriteRenderer.color = color;
            }

            if (age >= lifetime)
            {
                Destroy(gameObject);
            }
        }
    }

    public sealed class FloatingLabel : MonoBehaviour
    {
        private Text label;
        private float age;

        public void Begin(Text text)
        {
            label = text;
        }

        private void Update()
        {
            age += Time.unscaledDeltaTime;
            transform.position += Vector3.up * (45f * Time.unscaledDeltaTime);
            if (label != null)
            {
                Color color = label.color;
                color.a = Mathf.Clamp01(1f - age / 0.8f);
                label.color = color;
            }

            if (age >= 0.8f)
            {
                Destroy(gameObject);
            }
        }
    }
}
