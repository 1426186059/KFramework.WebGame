using System;

namespace KFramework.MonoGame
{
    public static class GameConst
    {
        public const int DesignWidth = 1280;
        public const int DesignHeight = 720;

        public static bool IsMobile =>
            OperatingSystem.IsAndroid() || OperatingSystem.IsIOS();

        public static bool IsDesktop =>
            OperatingSystem.IsMacOS() || OperatingSystem.IsLinux() || OperatingSystem.IsWindows();
    }
}
