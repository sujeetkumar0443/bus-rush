using UnityEngine;

namespace BusRush
{
    /// <summary>Android vibration wrapper. Respects SaveData.vibrationOn. No-op on other platforms.</summary>
    public static class Haptics
    {
        private const long ShortMs = 20;
        private const long MediumMs = 45;
        private const int ShortAmplitude = 90;   // 1..255
        private const int MediumAmplitude = 180;

        public static bool Enabled => SaveSystem.Data.vibrationOn;

        private const long LightMs = 12;
        private const int LightAmplitude = 60;

        /// <summary>Lightest tick (booster used, selection).</summary>
        public static void Light() => Vibrate(LightMs, LightAmplitude);

        public static void Short() => Vibrate(ShortMs, ShortAmplitude);

        public static void Medium() => Vibrate(MediumMs, MediumAmplitude);

#if UNITY_ANDROID && !UNITY_EDITOR
        private static bool initialized;
        private static AndroidJavaObject vibrator;
        private static int sdkInt;

        private static void Initialize()
        {
            initialized = true;
            try
            {
                using var version = new AndroidJavaClass("android.os.Build$VERSION");
                sdkInt = version.GetStatic<int>("SDK_INT");

                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                if (vibrator != null && !vibrator.Call<bool>("hasVibrator"))
                {
                    vibrator.Dispose();
                    vibrator = null;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[BusRush] Vibrator unavailable: {e.Message}");
                vibrator = null;
            }
        }

        private static void Vibrate(long milliseconds, int amplitude)
        {
            if (!Enabled)
            {
                return;
            }
            if (!initialized)
            {
                Initialize();
            }

            if (vibrator == null)
            {
                // Fallback; referencing Handheld.Vibrate also makes Unity add the VIBRATE permission.
                Handheld.Vibrate();
                return;
            }

            if (sdkInt >= 26)
            {
                using var effectClass = new AndroidJavaClass("android.os.VibrationEffect");
                using var effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", milliseconds, amplitude);
                vibrator.Call("vibrate", effect);
            }
            else
            {
                vibrator.Call("vibrate", milliseconds);
            }
        }
#else
        private static void Vibrate(long milliseconds, int amplitude)
        {
            // Vibration only exists on Android devices.
        }
#endif
    }
}
