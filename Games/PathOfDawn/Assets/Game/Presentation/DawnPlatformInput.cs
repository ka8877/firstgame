using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace PathOfDawn.Presentation
{
    internal static class DawnPlatformInput
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern int PathOfDawn_HasTouchInterface();

        static bool browserQueried, browserTouch;
        public static bool UseTouchControls
        {
            get
            {
                // WebGL can expose a synthetic Input System Touchscreen on a mouse-only PC.
                // Browser capabilities are available before Unity starts and do not require
                // a JS bridge call on every gameplay frame.
                if (!browserQueried)
                {
                    browserTouch = PathOfDawn_HasTouchInterface() != 0;
                    browserQueried = true;
                }
                return browserTouch;
            }
        }
#else
        public static bool UseTouchControls => Application.isMobilePlatform || Touchscreen.current != null;
#endif
    }
}
