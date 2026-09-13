namespace WebLib;

/// <summary>
/// 目标平台（对齐 Unity <c>BuildTarget</c>，仅作签名占位；本库产出与平台无关）。
/// </summary>
public enum BuildTarget
{
    StandaloneWindows64 = 19,
    WebGL = 14,
}
