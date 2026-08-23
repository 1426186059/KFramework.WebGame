namespace KFramework.MonoGame
{
    //public class SafeAreaFit : MonoBehaviour
    //{
    //    private Camera mainCamera;
    //    private Rect lastSafeArea = new Rect(0, 0, 0, 0);

    //    void Awake()
    //    {
    //        mainCamera = GetComponent<Camera>();
    //        Update();
    //    }

    //    void Update()
    //    {
    //        // 实时检测安全区域是否发生变化（比如横竖屏切换）
    //        Rect safeArea = SafeAreaFit.GetSafeArea();
    //        if (safeArea == lastSafeArea) return;
    //        lastSafeArea = safeArea;

    //        //将像素坐标转换为归一化坐标（0~1之间）
    //        float x = safeArea.x / Screen.width;
    //        float y = safeArea.y / Screen.height;
    //        float width = safeArea.width / Screen.width;
    //        float height = safeArea.height / Screen.height;

    //        //修改相机的渲染区域
    //        if (Screen.width > Screen.height) //横屏
    //        {
    //            mainCamera.rect = new Rect(x, 0, width, 1);
    //        }
    //        else
    //        {
    //            mainCamera.rect = new Rect(0, y, 1, height);
    //        }

    //        Debug.Log($"相机 渲染区域已适配: {mainCamera.rect}");
    //    }

    //    public static Rect GetSafeArea()
    //    {
    //        //return new Rect(100, 0, Screen.width - 200, Screen.height);
    //        //return new Rect(0, 100, Screen.width, Screen.height - 200);
    //        return Screen.safeArea;
    //    }
    //}
}