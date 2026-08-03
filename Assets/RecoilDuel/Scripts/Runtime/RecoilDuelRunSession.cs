namespace RecoilDuel
{
    internal sealed class RecoilDuelRunSession
    {
        public RunState State = RunState.Boot;
        public int WaveIndex;
        public int Score;
        public int FriendlyFireKills;
        public int RicochetKills;
        public int TotalEnemyKills;
        public int LastMilestoneTier;
        public int NextDebugUpgradeIndex;
        public int ActivePowerups;
        public float RunTime;
        public float NextPowerupTime;
        public bool ClearSequenceRunning;
    }
}
