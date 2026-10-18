using UnityEditor;
using UnityEngine;

namespace Tidebite.Editor
{
    public sealed class ArtImporter : AssetPostprocessor
    {
        public override uint GetVersion()=>2;
        private void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith("Assets/Tidebite/Resources/Art/")) return;
            var importer=(TextureImporter)assetImporter;
            bool floor=assetPath.EndsWith("lagoon-floor.png")||assetPath.EndsWith("profile-abyss-backdrop.png");
            bool pearl=assetPath.EndsWith("golden-pearl.png");
            int size=pearl?256:floor?2048:1024;
            importer.textureType=TextureImporterType.Default;
            importer.sRGBTexture=true;
            importer.alphaSource=floor?TextureImporterAlphaSource.None:TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency=!floor;
            importer.mipmapEnabled=true;
            importer.isReadable=false;
            importer.wrapMode=floor?TextureWrapMode.Mirror:TextureWrapMode.Clamp;
            importer.filterMode=FilterMode.Trilinear;
            importer.anisoLevel=1;
            importer.npotScale=floor?TextureImporterNPOTScale.None:TextureImporterNPOTScale.ToNearest;
            importer.maxTextureSize=size;
            importer.textureCompression=TextureImporterCompression.CompressedHQ;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings {
                name="Standalone",overridden=true,maxTextureSize=size,
                format=floor?TextureImporterFormat.DXT1:TextureImporterFormat.DXT5,compressionQuality=100
            });
            foreach(string platform in new[]{"Android","iPhone"})
                importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings {
                    name=platform,overridden=true,maxTextureSize=size,
                    format=TextureImporterFormat.ASTC_6x6,compressionQuality=100
                });
        }
    }
}
