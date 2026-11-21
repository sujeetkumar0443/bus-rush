using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BusRush.UI
{
    /// <summary>
    /// Grabs the current frame once (with an overlay canvas hidden for that frame), shrinks it to 1/4 size and softens
    /// it with a down/up blit chain: a cheap "blurred game behind" for full-screen screens and popups.
    /// </summary>
    public static class ScreenGrab
    {
        /// <summary>Fills <paramref name="target"/> with a blurred grab. The caller owns the texture (see <see cref="Release"/>).</summary>
        public static IEnumerator Blurred(RawImage target, Canvas hide, Action<RenderTexture> grabbed = null)
        {
            bool wasEnabled = hide != null && hide.enabled;
            if (hide != null) hide.enabled = false;
            target.enabled = false;
            yield return new WaitForEndOfFrame();
            try
            {
                int w = Mathf.Max(16, Screen.width), h = Mathf.Max(16, Screen.height);
                var full = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
                ScreenCapture.CaptureScreenshotIntoRenderTexture(full);
                var quarter = new RenderTexture(w / 4, h / 4, 0, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Bilinear };
                var eighth = RenderTexture.GetTemporary(w / 8, h / 8, 0, RenderTextureFormat.ARGB32);
                var sixteenth = RenderTexture.GetTemporary(w / 16, h / 16, 0, RenderTextureFormat.ARGB32);
                eighth.filterMode = sixteenth.filterMode = FilterMode.Bilinear;
                Graphics.Blit(full, quarter);
                Graphics.Blit(quarter, eighth);
                Graphics.Blit(eighth, sixteenth);
                Graphics.Blit(sixteenth, eighth);
                Graphics.Blit(eighth, quarter);
                RenderTexture.ReleaseTemporary(full);
                RenderTexture.ReleaseTemporary(eighth);
                RenderTexture.ReleaseTemporary(sixteenth);
                Release(target);
                target.texture = quarter;
                // The grab comes out upside down where UVs start at the top (D3D, Metal, Vulkan).
                target.uvRect = SystemInfo.graphicsUVStartsAtTop ? new Rect(0f, 1f, 1f, -1f) : new Rect(0f, 0f, 1f, 1f);
                target.enabled = true;
                grabbed?.Invoke(quarter);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BusRush] Background blur skipped: " + e.Message); // the dark overlay alone still works
            }
            if (hide != null) hide.enabled = wasEnabled;
        }

        /// <summary>Frees a grab shown on <paramref name="target"/>.</summary>
        public static void Release(RawImage target)
        {
            if (target == null || !(target.texture is RenderTexture rt)) return;
            target.texture = null;
            rt.Release();
            UnityEngine.Object.Destroy(rt);
        }
    }
}
