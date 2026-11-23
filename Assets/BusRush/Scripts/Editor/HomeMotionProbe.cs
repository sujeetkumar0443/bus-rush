using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BusRush.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BusRush.EditorTools
{
    /// <summary>
    /// Play-mode probe for the Home Level button motion: Boot -> Home, then samples for 5 s (unscaled time)
    ///   - the breathing pulse (LevelPulse localScale): min / max and the scale at several times,
    ///   - a synthetic press and release (pointer down / up, no click): min of the press squash and the release bounce max.
    /// Writes Temp/home_motion.txt and exits. Command line (batch is fine):
    ///   Unity.exe -batchmode -projectPath ... -executeMethod BusRush.EditorTools.HomeMotionProbe.Run -logFile probe.log
    /// </summary>
    [InitializeOnLoad]
    public static class HomeMotionProbe
    {
        private const string Flag = "BusRush.HomeMotion.Active";

        static HomeMotionProbe()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Flag, false)) return;
                SessionState.SetBool(Flag, false);
                var go = new GameObject("[HomeMotion]");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<Driver>();
            };
        }

        public static void Run()
        {
            SessionState.SetBool(Flag, true);
            EditorSceneManager.OpenScene("Assets/BusRush/Scenes/Boot.unity");
            EditorApplication.EnterPlaymode();
        }

        private sealed class Driver : MonoBehaviour
        {
            private IEnumerator Start()
            {
                var sb = new StringBuilder();
                bool ok = false;
                try
                {
                    // wait for Home
                    HomeAmbience home = null;
                    for (float w = 0f; home == null && w < 30f; w += Time.unscaledDeltaTime)
                    {
                        home = FindAnyObjectByType<HomeAmbience>();
                        yield return null;
                    }
                    if (home == null) { sb.AppendLine("FAIL: Home did not load"); goto done; }

                    Transform pulse = null;
                    Button level = null;
                    foreach (Button b in FindObjectsByType<Button>(FindObjectsInactive.Include))
                        if (b.name == "LevelButton") { level = b; pulse = b.transform.parent; }
                    if (level == null) { sb.AppendLine("FAIL: LevelButton not found"); goto done; }
                    var ui = level.GetComponent<UIButton>();
                    Transform target = (Transform)new SerializedObject(ui).FindProperty("target").objectReferenceValue;
                    sb.AppendLine($"pulse object: {pulse.name}   press target: {(target != null ? target.name : "-")}   scale at start: pulse {pulse.localScale.x:F4}, target {target.localScale.x:F4}");

                    // 1) idle: 5 s of the breathing pulse
                    float t0 = Time.unscaledTime, min = 9f, max = 0f, nextMark = 0f;
                    int frames = 0;
                    sb.AppendLine("breathing pulse (scale of LevelPulse):");
                    while (Time.unscaledTime - t0 < 5f)
                    {
                        float t = Time.unscaledTime - t0, s = pulse.localScale.x;
                        min = Mathf.Min(min, s);
                        max = Mathf.Max(max, s);
                        frames++;
                        if (t >= nextMark) { sb.AppendLine($"  t={t:F2}s  scale={s:F4}"); nextMark += 0.3f; }
                        yield return null;
                    }
                    sb.AppendLine($"  PULSE min={min:F4} max={max:F4} over {frames} frames (spec 1.00 .. 1.04, period 1.2 s)");

                    // 2) press, hold 0.35 s, release, watch 0.6 s
                    var data = new PointerEventData(EventSystem.current);
                    float pMin = 9f, rMax = 0f, rMin = 9f;
                    sb.AppendLine("press animation (scale of the button target):");
                    ExecuteEvents.Execute(level.gameObject, data, ExecuteEvents.pointerDownHandler);
                    float tp = Time.unscaledTime;
                    while (Time.unscaledTime - tp < 0.35f)
                    {
                        float s = target.localScale.x;
                        pMin = Mathf.Min(pMin, s);
                        sb.AppendLine($"  down +{Time.unscaledTime - tp:F2}s  target={s:F4}");
                        yield return null;
                    }
                    ExecuteEvents.Execute(level.gameObject, data, ExecuteEvents.pointerUpHandler);
                    float tr = Time.unscaledTime;
                    while (Time.unscaledTime - tr < 0.6f)
                    {
                        float s = target.localScale.x;
                        rMax = Mathf.Max(rMax, s);
                        rMin = Mathf.Min(rMin, s);
                        sb.AppendLine($"  up   +{Time.unscaledTime - tr:F2}s  target={s:F4}");
                        yield return null;
                    }
                    sb.AppendLine($"  PRESS min={pMin:F4} (spec 0.94)   RELEASE bounce max={rMax:F4} (OutBack overshoot, then settles to {target.localScale.x:F4})");
                    ok = true;
                }
                finally { }
            done:
                sb.Insert(0, ok ? "PASS\n" : "FAIL\n");
                File.WriteAllText("Temp/home_motion.txt", sb.ToString());
                Debug.Log("[HomeMotion]\n" + sb);
                EditorApplication.ExitPlaymode();
                EditorApplication.delayCall += () => EditorApplication.Exit(ok ? 0 : 1);
            }
        }
    }
}
