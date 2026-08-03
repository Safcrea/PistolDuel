using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RecoilDuel.Editor
{
    public static class RecoilDuelSetupMenu
    {
        [MenuItem("Tools/Recoil Duel/Create or Select Game Manager")]
        [MenuItem("GameObject/Recoil Duel/Game Manager", false, 10)]
        public static void CreateGameManager()
        {
            RecoilDuelGame existing = Object.FindFirstObjectByType<RecoilDuelGame>();
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            GameObject root = new GameObject("Recoil Duel Game Manager");
            Undo.RegisterCreatedObjectUndo(root, "Create Recoil Duel Game Manager");
            Undo.AddComponent<RecoilDuelGame>(root);
            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        [MenuItem("Tools/Recoil Duel/Create First Playable Bootstrap")]
        public static void CreateBootstrap()
        {
            CreateGameManager();
        }
    }
}
