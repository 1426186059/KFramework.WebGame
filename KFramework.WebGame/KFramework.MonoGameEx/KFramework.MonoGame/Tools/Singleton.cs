using System.Diagnostics;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 如果实现单例，就继承这个类
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class Singleton<T> where T : class, new()
    {
        protected Singleton()
        {
            PrintTool.Assert(instance == null, "单例模式, 不可以再 New(): " + this.GetType().ToString());
        }

        private static T instance = new T();
        public static T Instance
        {
            get
            {
                return instance;
            }
        }

        public static T readOnlyInstance
        {
            get
            {
                return instance;
            }
        }
    }

    public abstract class SingleTonMonoBehaviour<T> : MonoBehaviour where T : SingleTonMonoBehaviour<T>, new()
    {
        private static T m_Instance = null;
        public static T Instance
        {
            get
            {
                if (null == m_Instance)
                {
                    m_Instance = new T();
                    m_Instance.Parent = KSceneMgr.DontDestroyOnLoadRoot;
                }
                return m_Instance;
            }
        }

        // 在Destory中如果调用 instance 会报错，所以 加了一个 只读实例，避免在 Destroy 中 重复分配新的实例
        public static T readOnlyInstance
        {
            get
            {
                return m_Instance;
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            m_Instance = null;
        }

    }
}