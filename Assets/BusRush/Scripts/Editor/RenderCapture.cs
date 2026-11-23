using System.Collections;
using System.Collections.Generic;
using System.IO;
using BusRush.Gameplay;
using UnityEngine;

namespace BusRush.EditorTools
{
    /// <summary>
    /// Play-mode capture of camera + UI at an exact resolution: overlay canvases are switched to camera space for one
    /// render into a RenderTexture, then restored. Must run in the windowed editor (batch mode renders wrong colours).
    /// </summary>
    public static class RenderCapture
    {
        public static IEnumerator Capture(string directory, string name, int width, int height)
        {
            // BUSRUSH_SHOTDIR redirects every screenshot tool to one folder (e.g. unity_shots/text_fix).
            string redirect = System.Environment.GetEnvironmentVariable("BUSRUSH_SHOTDIR");
            if (!string.IsNullOrEmpty(redirect)) directory = redirect;
            // BUSRUSH_SHOTSCALE=2 renders at 2x (780x1688) and adds "@2x" to nothing else: same names, bigger files.
            string scaleEnv = System.Environment.GetEnvironmentVariable("BUSRUSH_SHOTSCALE");
            if (int.TryParse(scaleEnv, out int scale) && scale > 1)
            {
                width *= scale;
                height *= scale;
                name = name.Replace("390x844", $"{width}x{height}");
            }
            Directory.CreateDirectory(directory);
            Camera cam = Camera.main;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            rt.Create();

            var switched = new List<Canvas>();
            foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
            {
                if (!c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceOverlay || c.GetComponent<ScreenFader>() != null) continue;
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = 1f;
                switched.Add(c);
            }

            cam.targetTexture = rt;
            CameraFitter fitter = cam.GetComponent<CameraFitter>();
            yield return null;
            if (fitter != null) fitter.Fit(width / (float)height);
            yield return null;
            Canvas.ForceUpdateCanvases();
            cam.Render();

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes($"{directory}/{name}.png", tex.EncodeToPNG());
            Object.Destroy(tex);

            cam.targetTexture = null;
            rt.Release();
            Object.Destroy(rt);
            foreach (Canvas c in switched)
            {
                if (c != null) c.renderMode = RenderMode.ScreenSpaceOverlay;
            }
            if (fitter != null) fitter.Fit();
            yield return null;
            Debug.Log($"[RenderCapture] saved {name} ({width}x{height})");
        }
    }
}
