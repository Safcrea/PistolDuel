using UnityEngine;

namespace RecoilDuel
{
    internal sealed class RecoilDuelWavePlanner
    {
        private static readonly float[] LaneDefaults = { -2.45f, -1.2f, 0f, 1.2f, 2.45f };

        private readonly float[] sortedLanes = new float[LaneDefaults.Length];
        private readonly float[] selectedLanes = new float[LaneDefaults.Length];

        public float[] PickDropLanes(int count, float playerX)
        {
            LaneDefaults.CopyTo(sortedLanes, 0);
            SortFarthestFirst(playerX);

            int selectedCount = Mathf.Clamp(count, 0, selectedLanes.Length);
            for (int i = 0; i < selectedCount; i++)
            {
                selectedLanes[i] = sortedLanes[i % sortedLanes.Length];
            }

            return selectedLanes;
        }

        private void SortFarthestFirst(float playerX)
        {
            for (int i = 1; i < sortedLanes.Length; i++)
            {
                float lane = sortedLanes[i];
                float distance = Mathf.Abs(lane - playerX);
                int insertAt = i - 1;
                while (insertAt >= 0 && Mathf.Abs(sortedLanes[insertAt] - playerX) < distance)
                {
                    sortedLanes[insertAt + 1] = sortedLanes[insertAt];
                    insertAt--;
                }

                sortedLanes[insertAt + 1] = lane;
            }
        }
    }
}
