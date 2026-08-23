using System;

namespace WebAssemblyBrowserApp.Engine;

/// <summary>常用数学工具。</summary>
public static class MathUtils
{
    public static double Clamp(double value, double min, double max)
        => value < min ? min : value > max ? max : value;

    public static int Clamp(int value, int min, int max)
        => value < min ? min : value > max ? max : value;

    public static double Lerp(double a, double b, double t) => a + (b - a) * t;

    public static double Rand(double min, double max) => min + (max - min) * Random.Shared.NextDouble();
}
