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
        private const float PixelsPerUnit = 100f;
        private static readonly Vector2 centerPivot = new Vector2(0.5f, 0.5f);

        /// <summary>The sprite, or null with a warning when the resource is missing or isn't a valid image.</summary>
        public static Sprite Load(string resourceName)
        {
            byte[] png;
            using (Stream stream = typeof(EmbeddedSprites).Assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    DroneImprovementsPlugin.Log.LogWarning($"The embedded image {resourceName} is missing.");
                    return null;
                }
                using (MemoryStream buffer = new MemoryStream())
                {
                    stream.CopyTo(buffer);
                    png = buffer.ToArray();
                }
            }

            // LoadImage replaces the placeholder size and format with the image's. The pixels are only drawn, never
            // read back, so the CPU copy can be dropped.
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            if (!texture.LoadImage(png, markNonReadable: true))
            {
                Object.Destroy(texture);
                DroneImprovementsPlugin.Log.LogWarning($"The embedded image {resourceName} couldn't be decoded.");
                return null;
            }
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.name = resourceName;
            // FullRect: a tight mesh would have to read the (non-readable) pixels.
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), centerPivot, PixelsPerUnit,
                0, SpriteMeshType.FullRect);
        }
    }
}
