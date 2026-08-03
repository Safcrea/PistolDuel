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
    public sealed partial class RecoilDuelGame : MonoBehaviour
    {
        private const float ArenaHalfWidth = 3.25f;
        private const float ArenaHalfHeight = 5.8f;
        private const float AimLockCastDistance = ArenaHalfHeight * 3f;
        private const float HitStopTimeScale = 0.18f;
        private const float GameOverTimeScale = 0.45f;

        [Header("Player Settings")]
        [SerializeField, Min(0.1f)] private float playerMaxHealth = 2f;
        [SerializeField, Range(0, 5)] private int playerStartingShieldHits;
        [SerializeField, Min(0.1f)] private float playerBulletDamage = 1f;
        [SerializeField, Min(1f)] private float playerBulletSpeed = 25f;
        [SerializeField, Min(0.05f)] private float playerFireCooldown = 0.34f;
        [SerializeField, Min(0f)] private float playerRecoilForce = 4.1f;
        [SerializeField, Range(0, 2)] private int playerStartingRicochets = 2;
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

        [Header("Gun Gravity After Shot")]
        [SerializeField] private bool enablePlayerShotGravity = true;
        [SerializeField] private bool enableEnemyShotGravity = true;
        [SerializeField, Min(0f)] private float playerShotGravityAcceleration = 7.5f;
        [SerializeField, Min(0f)] private float enemyShotGravityAcceleration = 5.5f;
        [SerializeField, Min(0f)] private float gunShotGravityDuration = 0.75f;
        [SerializeField, Range(0.1f, 8f)] private float maximumGunFallSpeed = 7.5f;

        [Header("Bullet Size Settings")]
        [SerializeField, Min(0.01f)] private float playerBulletVisualSize = 0.18f;
        [SerializeField, Min(0.01f)] private float enemyBulletVisualSize = 0.18f;
        [SerializeField, Min(0.001f)] private float playerBulletHitboxRadius = 0.065f;
        [SerializeField, Min(0.001f)] private float enemyBulletHitboxRadius = 0.065f;

        [Header("Aim-Lock Slow Motion")]
        [SerializeField] private bool enableAimLockSlowMotion = true;
        [SerializeField, Range(0.05f, 1f)] private float aimLockTimeScale = 0.35f;
        [SerializeField, Min(0.01f)] private float aimLockDuration = 0.45f;
        [SerializeField, Min(0f)] private float aimLockCooldown = 1.2f;

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

        private readonly List<PistolController> enemyPool = new List<PistolController>(8);
        private readonly List<GameObject> dropWarnings = new List<GameObject>(5);
        private readonly Dictionary<PowerupId, int> upgradeStacks = new Dictionary<PowerupId, int>();
        private readonly HashSet<PistolController> activeAimLocks = new HashSet<PistolController>();
        private readonly HashSet<PistolController> currentAimLocks = new HashSet<PistolController>();
        private readonly HashSet<HealthComponent> explosionDamagedTargets = new HashSet<HealthComponent>();
        private RaycastHit2D[] aimLockCastHits = new RaycastHit2D[64];
        private Collider2D[] explosionOverlapHits = new Collider2D[16];
        private readonly System.Random majorDropRandom = new System.Random(7261);
        private readonly RecoilDuelRunSession session = new RecoilDuelRunSession();
        private readonly RecoilDuelRuntimeCatalog catalog = new RecoilDuelRuntimeCatalog();
        private readonly RecoilDuelFeedbackSystem feedbackSystem = new RecoilDuelFeedbackSystem();
        private readonly RecoilDuelWavePlanner wavePlanner = new RecoilDuelWavePlanner();
        private readonly RecoilDuelProjectilePool projectilePool = new RecoilDuelProjectilePool();

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

        private List<UpgradeData> upgrades => catalog.Upgrades;
        private List<UpgradeData> killMilestoneUpgrades => catalog.KillMilestoneUpgrades;
        private List<EnemyArchetypeData> enemyArchetypes => catalog.EnemyArchetypes;
        private List<GunData> enemyGunDefinitions => catalog.EnemyGuns;
        private List<BulletData> enemyBulletDefinitions => catalog.EnemyBullets;
        private GunData playerGunData { get => catalog.PlayerGun; set => catalog.PlayerGun = value; }
        private BulletData playerBulletData { get => catalog.PlayerBullet; set => catalog.PlayerBullet = value; }
        private MajorDropTimingData majorDropTiming { get => catalog.MajorDropTiming; set => catalog.MajorDropTiming = value; }

        private PistolController player;
        private RunState state { get => session.State; set => session.State = value; }
        private int waveIndex { get => session.WaveIndex; set => session.WaveIndex = value; }
        private int score { get => session.Score; set => session.Score = value; }
        private int friendlyFireKills { get => session.FriendlyFireKills; set => session.FriendlyFireKills = value; }
        private int ricochetKills { get => session.RicochetKills; set => session.RicochetKills = value; }
        private int totalEnemyKills { get => session.TotalEnemyKills; set => session.TotalEnemyKills = value; }
        private int lastMilestoneTier { get => session.LastMilestoneTier; set => session.LastMilestoneTier = value; }
        private int nextDebugUpgradeIndex { get => session.NextDebugUpgradeIndex; set => session.NextDebugUpgradeIndex = value; }
        private int activePowerups { get => session.ActivePowerups; set => session.ActivePowerups = value; }
        private float runTime { get => session.RunTime; set => session.RunTime = value; }
        private float nextPowerupTime { get => session.NextPowerupTime; set => session.NextPowerupTime = value; }
        private float lastHapticTime;
        private bool clearSequenceRunning { get => session.ClearSequenceRunning; set => session.ClearSequenceRunning = value; }
        private bool infiniteHealth;
        private bool magnetPowerups;
        private bool enemiesFrozen;
        private bool enemiesConfused;
        private bool gameOverSlowMotionActive;
        private WeaponMode activeWeaponMode;
        private Coroutine freezeRoutine;
        private Coroutine confusionRoutine;
        private ContactFilter2D aimLockContactFilter;
        private ContactFilter2D explosionContactFilter;
        private RunState stateBeforeHitStop;
        private float aimLockPulseEndsAtUnscaled;
        private float nextAimLockPulseAllowedAtUnscaled;
        private float hitStopEndsAtUnscaled;

        public TeamId PlayerTeam => TeamId.Player;
        public bool IsCombatActive => state == RunState.Combat || state == RunState.PowerupDropping;
        public PistolController PlayerPistol => player;
        public bool InfiniteHealth => infiniteHealth;
        public bool MagnetPowerups => magnetPowerups;
        public bool EnemiesFrozen => enemiesFrozen;
        public bool EnemiesConfused => enemiesConfused;
        public float PlayerInvulnerabilityDuration => playerInvulnerabilityDuration;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Physics2D.gravity = Vector2.zero;
            aimLockContactFilter = ContactFilter2D.noFilter;
            aimLockContactFilter.useTriggers = false;
            explosionContactFilter = ContactFilter2D.noFilter;
            explosionContactFilter.useTriggers = false;
            infiniteHealth = startWithInfiniteHealth;
            CreateRuntimeData();
            CreateSprites();
            CreateRoots();
            SetupCamera();
            SetupAudio();
            SetupArena();
            SetupUi();
            feedbackSystem.Initialize(transform, vfxRoot, canvas, mainCamera, audioSource, circleSprite);
            CreatePools();
        }

        private void OnDestroy()
        {
            feedbackSystem.Dispose();
            catalog.Dispose();
        }

        private void OnValidate()
        {
            playerMaxHealth = Mathf.Max(0.1f, playerMaxHealth);
            killsPerUpgrade = Mathf.Max(1, killsPerUpgrade);
            enemyMaximumHealth = Mathf.Max(enemyBaseHealth, enemyMaximumHealth);
            enemyMaximumFireDelay = Mathf.Max(enemyMinimumFireDelay, enemyMaximumFireDelay);
            maximumEnemiesPerWave = Mathf.Max(initialEnemyCount, maximumEnemiesPerWave);
            wavesPerExtraEnemy = Mathf.Max(1, wavesPerExtraEnemy);
            playerBulletVisualSize = Mathf.Max(0.01f, playerBulletVisualSize);
            enemyBulletVisualSize = Mathf.Max(0.01f, enemyBulletVisualSize);
            playerBulletHitboxRadius = Mathf.Max(0.001f, playerBulletHitboxRadius);
            enemyBulletHitboxRadius = Mathf.Max(0.001f, enemyBulletHitboxRadius);
            aimLockTimeScale = Mathf.Clamp(aimLockTimeScale, 0.05f, 1f);
            aimLockDuration = Mathf.Max(0.01f, aimLockDuration);
            aimLockCooldown = Mathf.Max(0f, aimLockCooldown);
            playerShotGravityAcceleration = Mathf.Max(0f, playerShotGravityAcceleration);
            enemyShotGravityAcceleration = Mathf.Max(0f, enemyShotGravityAcceleration);
            gunShotGravityDuration = Mathf.Max(0f, gunShotGravityDuration);
            maximumGunFallSpeed = Mathf.Clamp(maximumGunFallSpeed, 0.1f, 8f);
        }

        private void Start()
        {
            StartCoroutine(StartRun());
        }

        private void Update()
        {
            UpdateTimeEffects();

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

            EvaluateAimLocks();
        }
    }
}
