using System.IO;
using UnityEngine;

namespace DroneImprovements
{
    /// <summary>
    /// Loads PNG images embedded in this assembly as sprites: the skill icons (see the project file) and the store
    /// icon (see Directory.Build.targets).
    /// </summary>
    internal static class EmbeddedSprites
    {
        public static Sprite Load(string resourceName)
        {
            byte[] png;
            using (Stream stream = typeof(EmbeddedSprites).Assembly.GetManifestResourceStream(resourceName))
            using (MemoryStream buffer = new MemoryStream())
            {
                stream.CopyTo(buffer);
                png = buffer.ToArray();
            }
            // LoadImage replaces the placeholder size and format with the image's. No mipmaps: these are UI icons.
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            texture.LoadImage(png);
            // With the default Repeat, the edges of a scaled-down icon would blend with the opposite edges.
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.name = resourceName;
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }
    }
}
