using UnityEditor;
using UnityEngine;

namespace RecoilDuel.Editor
{
    public sealed class RecoilDuelArtPostprocessor : AssetPostprocessor
    {
        private const string ArtRoot = "Assets/RecoilDuel/Resources/RecoilDuelArt/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtRoot, System.StringComparison.Ordinal))
            {
                return;
            }

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = IsAtlas(assetPath) ? SpriteImportMode.Multiple : SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 4096;
            importer.spritePixelsPerUnit = 512f;

            if (!IsAtlas(assetPath))
            {
                return;
            }

            GetGrid(assetPath, out int columns, out int rows);
            importer.GetSourceTextureWidthAndHeight(out int textureWidth, out int textureHeight);
            SpriteMetaData[] sprites = new SpriteMetaData[columns * rows];
            int cellWidth = textureWidth / columns;
            int cellHeight = textureHeight / rows;
            string baseName = System.IO.Path.GetFileNameWithoutExtension(assetPath);

            int index = 0;
            for (int topRow = 0; topRow < rows; topRow++)
            {
                int y = textureHeight - (topRow + 1) * cellHeight;
                for (int column = 0; column < columns; column++)
                {
                    sprites[index] = new SpriteMetaData
                    {
                        name = baseName + "_" + index.ToString("00"),
                        rect = new Rect(column * cellWidth, y, cellWidth, cellHeight),
                        alignment = (int)SpriteAlignment.Center,
                        pivot = new Vector2(0.5f, 0.5f)
                    };
                    index++;
                }
            }

#pragma warning disable 618
            importer.spritesheet = sprites;
#pragma warning restore 618
        }

        [MenuItem("Recoil Duel/Art/Reimport Generated Art")]
        private static void ReimportGeneratedArt()
        {
            AssetDatabase.ImportAsset(ArtRoot, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            RecoilDuelArtLibrary.ClearCache();
        }

        private static bool IsAtlas(string path)
        {
            return path.EndsWith("_atlas.png", System.StringComparison.OrdinalIgnoreCase);
        }

        private static void GetGrid(string path, out int columns, out int rows)
        {
            columns = 1;
            rows = 1;
            if (path.Contains("player_chassis_atlas"))
            {
                columns = 3;
                rows = 2;
            }
            else if (path.Contains("enemy_chassis_atlas"))
            {
                columns = 4;
                rows = 2;
            }
            else if (path.Contains("bullet_atlas"))
            {
                columns = 7;
            }
            else if (path.Contains("powerup_atlas"))
            {
                columns = 4;
                rows = 4;
            }
            else if (path.Contains("attachment_atlas"))
            {
                columns = 4;
            }
        }
    }
}
