using System;

namespace KFramework.MonoGameExtend
{

    //public class FPSDisplay : MonoBehaviour
    //{
    //    // ����ƽ����ʾ FPS
    //    private float deltaTime = 0.0f;
    //    GUIStyle style = new GUIStyle();

    //    private void Start()
    //    {

    //    }

    //    void Update()
    //    {
    //        // ����ƽ����� deltaTime
    //        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
    //    }

    //    void OnGUI()
    //    {
    //        // ��ȡ��Ļ��ߣ����ڶ�̬���������С��λ��
    //        int w = Screen.width;
    //        int h = Screen.height;


    //        style.fontSize = h * 2 / 50;
    //        style.alignment = TextAnchor.UpperLeft; // ���ϽǶ���
    //        style.normal.textColor = Color.yellow; // ����������ɫΪ��ɫ

    //        // �����������֡��
    //        if (deltaTime <= 0.0f)
    //        {
    //            deltaTime = 0.001f;
    //        }
    //        float fps = 1.0f / deltaTime;
    //        string text = $"  {(int)Math.Floor(fps)} fps)";

    //        // �����Ͻǻ��Ʊ�ǩ (x=0, y=0 ��Ϊ���Ͻ�)
    //        Rect rect = new Rect(0, 0, w, h * 2 / 100);
    //        GUI.Label(rect, text, style);
    //    }
    //}
}