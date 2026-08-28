export class Singleton 
{
    //类加载时立即创建
    private static m_Instance: Singleton = new Singleton();
    private constructor() {}
    public static GetInstance(): Singleton 
    {
        return Singleton.m_Instance;
    }

}
