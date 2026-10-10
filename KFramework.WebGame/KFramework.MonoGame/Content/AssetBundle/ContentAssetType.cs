namespace KFramework.MonoGame
{ 
    public enum ContentAssetType
    {
        //默认都是二进制资源
        Binary,

        //下面都是具体的资源格式, 特殊处理
        Text,
        Texture,
        Audio,
        Video,
    }
}
