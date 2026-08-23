using System;

namespace KFramework.MonoGame
{

    //public class FPSDisplay : MonoBehaviour
    //{
    //    // 用于平滑显示 FPS
    //    private float deltaTime = 0.0f;
    //    GUIStyle style = new GUIStyle();

    //    private void Start()
    //    {

    //    }

    //    void Update()
    //    {
    //        // 计算平滑后的 deltaTime
    //        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
    //    }

    //    void OnGUI()
    //    {
    //        // 获取屏幕宽高，用于动态调整字体大小和位置
    //        int w = Screen.width;
    //        int h = Screen.height;


    //        style.fontSize = h * 2 / 50;
    //        style.alignment = TextAnchor.UpperLeft; // 左上角对齐
    //        style.normal.textColor = Color.yellow; // 设置文字颜色为黄色

    //        // 计算毫秒数和帧率
    //        if (deltaTime <= 0.0f)
    //        {
    //            deltaTime = 0.001f;
    //        }
    //        float fps = 1.0f / deltaTime;
    //        string text = $"  {(int)Math.Floor(fps)} fps)";

    //        // 在左上角绘制标签 (x=0, y=0 即为左上角)
    //        Rect rect = new Rect(0, 0, w, h * 2 / 100);
    //        GUI.Label(rect, text, style);
    //    }
    //}
}