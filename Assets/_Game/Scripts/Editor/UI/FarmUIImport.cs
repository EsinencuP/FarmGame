using UnityEditor;
using UnityEngine;

namespace MyLittleFarm.Editor
{
    public sealed class FarmUIImport : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/_Game/Resources/FarmUI/") && assetPath != "Assets/_Game/Content/UI/MainMenuBackground.png") return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 2048;
            importer.spritePixelsPerUnit = 100;
            if (assetPath.Contains("/Skin/")) importer.spriteBorder = new Vector4(28, 28, 28, 28);
        }
    }
}
