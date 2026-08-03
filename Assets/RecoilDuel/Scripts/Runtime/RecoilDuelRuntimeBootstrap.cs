using UnityEngine;

namespace RecoilDuel
{
    public static class RecoilDuelRuntimeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateFirstPlayable()
        {
            if (Object.FindFirstObjectByType<RecoilDuelGame>() != null)
            {
                return;
            }

            GameObject root = new GameObject("Recoil Duel Game Manager (Runtime)");
            root.AddComponent<RecoilDuelGame>();
        }
    }
}
