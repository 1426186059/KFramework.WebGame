#nullable enable
using System;

namespace WebAssemblyBrowserApp.Engine;

/// <summary>常用数学工具。</summary>
public static class MathUtils
{
    public const double Deg2Rad = Math.PI / 180.0;
    public const double Rad2Deg = 180.0 / Math.PI;
    public const double TwoPi = Math.PI * 2.0;

    public static double Clamp(double value, double min, double max)
        => value < min ? min : value > max ? max : value;

    public static int Clamp(int value, int min, int max)
        => value < min ? min : value > max ? max : value;

    public static float Clamp(float value, float min, float max)
        => value < min ? min : value > max ? max : value;

    public static double Lerp(double a, double b, double t) => a + (b - a) * t;

    /// <summary>带死区的线性插值。</summary>
    public static double LerpClamped(double a, double b, double t) => a + (b - a) * Clamp(t, 0, 1);

    /// <summary>角度（弧度）平滑插值，自动取最短路径。</summary>
    public static double LerpAngle(double a, double b, double t)
    {
        double diff = Math.IEEERemainder(b - a, TwoPi);
        return a + diff * Clamp(t, 0, 1);
    }

    public static double Rand(double min, double max) => min + (max - min) * Random.Shared.NextDouble();

    public static double RandAngle() => Random.Shared.NextDouble() * TwoPi;

    /// <summary>min（含）~ max（含）随机整数。</summary>
    public static int RandInt(int min, int max) => Random.Shared.Next(min, max + 1);

    public static double MoveTowards(double current, double target, double maxDelta)
    {
        if (Math.Abs(target - current) <= maxDelta) return target;
        return current + Math.Sign(target - current) * maxDelta;
    }

    /// <summary>平滑阻尼插值（帧率无关，lambda 为响应速度）。</summary>
    public static double Damp(double current, double target, double lambda, double dt)
        => Lerp(current, target, 1 - Math.Exp(-lambda * dt));
}
