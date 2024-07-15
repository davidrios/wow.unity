using UnityEngine;

namespace WowUnity
{
    public class Utils
    {
        public static Texture2D RotateTexture180(Texture2D originalTexture)
        {
            var rotatedTexture = new Texture2D(originalTexture.width, originalTexture.height);

            // Get the original pixels from the texture
            var originalPixels = originalTexture.GetPixels();
            var rotatedPixels = new Color[originalPixels.Length];

            int width = originalTexture.width;
            var height = originalTexture.height;

            // Loop through each pixel and set it in the new position
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    // Calculate the index for the original and rotated positions
                    var originalIndex = y * width + x;
                    var rotatedIndex = (height - 1 - y) * width + (width - 1 - x);

                    // Set the rotated pixel
                    rotatedPixels[rotatedIndex] = originalPixels[originalIndex];
                }
            }

            // Apply the rotated pixels to the new texture
            rotatedTexture.SetPixels(rotatedPixels);
            rotatedTexture.Apply();

            return rotatedTexture;
        }
    }
}