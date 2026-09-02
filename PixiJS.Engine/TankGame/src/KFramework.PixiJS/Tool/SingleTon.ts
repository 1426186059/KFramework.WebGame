//TS 直接 泛型支持不好
//这里写个模板
export class SingletonTemplate
{
    //类加载时立即创建
    private static m_Instance: SingletonTemplate;
    public static GetInstance(): SingletonTemplate 
    {
        if(SingletonTemplate.m_Instance == null)
        {
            SingletonTemplate.m_Instance = new SingletonTemplate();
        }
        return SingletonTemplate.m_Instance;
    }
    private constructor() {}
}
