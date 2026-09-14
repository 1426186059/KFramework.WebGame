//using System;

//namespace KFramework.MonoGame
//{
//    public class AndroidVibrateHelper
//    {
//        private static bool m_Open = true;
//        private static int _sdkVersion = -1;

//        public static void SetVibrateOpen(bool bOpen)
//        {
//            m_Open = bOpen;
//        }

//        public static void Call_VIBRATOR(long milliseconds, int amplitude = 1)
//        {
//            if (!m_Open)
//            {
//                return;
//            }

//            try
//            {
//#if UNITY_EDITOR
//            Handheld.Vibrate();
//#elif UNITY_ANDROID
//        // amplitude is only supported after API26
//        if ((AndroidSDKVersion() < 26))
//        {
//            Call_VIBRATOR_1(milliseconds);
//        }
//        else
//        {
//            Call_VIBRATOR_2(milliseconds, amplitude);
//        }
//#endif
//            }
//            catch (Exception e)
//            {
//                Debug.LogError(e.Message + " | " + e.StackTrace);
//            }
//        }

//        private static void Call_VIBRATOR_1(long milliseconds)
//        {
//            var jc = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
//            var activity = jc.GetStatic<AndroidJavaObject>("currentActivity");
//            var service = new AndroidJavaClass("android.app.Service");
//            var s = service.GetStatic<string>("VIBRATOR_SERVICE");
//            var vib = activity.Call<AndroidJavaObject>("getSystemService", s);
//            vib.Call("vibrate", milliseconds);
//        }

//        private static void Call_VIBRATOR_2(long milliseconds, int amplitude)
//        {
//            AndroidJavaClass UnityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
//            AndroidJavaObject CurrentActivity = UnityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
//            AndroidJavaObject AndroidVibrator = CurrentActivity.Call<AndroidJavaObject>("getSystemService", "vibrator");

//            var VibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect");
//            var VibrationEffect = VibrationEffectClass.CallStatic<AndroidJavaObject>("createOneShot", new object[] { milliseconds, amplitude });
//            AndroidVibrator.Call("vibrate", VibrationEffect);
//        }

//        private static int AndroidSDKVersion()
//        {
//            if (_sdkVersion == -1)
//            {
//                _sdkVersion = int.Parse(SystemInfo.operatingSystem.Substring(SystemInfo.operatingSystem.IndexOf("-") + 1, 3));
//            }
//            return _sdkVersion;
//        }
//    }
//}
