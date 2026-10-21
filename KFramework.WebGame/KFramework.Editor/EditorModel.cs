using KFramework.MonoGame;

namespace KFramework.Editor
{
    /// <summary>物体类型（决定怎么渲染 / 在层级里怎么显示）。</summary>
    public enum EditorObjectType
    {
        Cube,
        Empty,
        Camera,
        Light,
    }

    /// <summary>编辑器里的物体：对标 Unity 的 GameObject + Transform。</summary>
    public sealed class EditorObject
    {
        public string Name;
        public EditorObjectType Type;
        public Vector3 Position;
        public Vector3 Rotation;                       // 欧拉角（弧度）
        public Vector3 Scale = new Vector3(1f, 1f, 1f);
        public bool Visible = true;

        public EditorObject(string name, EditorObjectType type)
        {
            Name = name;
            Type = type;
        }
    }

    /// <summary>编辑器场景图（极简版）：一个物体列表 + 当前选中。</summary>
    public sealed class EditorSceneModel
    {
        public readonly List<EditorObject> Objects = new();
        public EditorObject? Selected;
        public EditorObject MainCamera;                // 游戏视图使用的相机（其 Transform 决定游戏视角）

        public EditorSceneModel()
        {
            MainCamera = new EditorObject("Main Camera", EditorObjectType.Camera)
            {
                Position = Vector3.Zero,
            };
            Objects.Add(MainCamera);
            Objects.Add(new EditorObject("Directional Light", EditorObjectType.Light)
            {
                Position = new Vector3(5f, 8f, -5f),
            });
            // 默认就放一个 Cube，在相机正前方（屏幕正中）
            Objects.Add(new EditorObject("Cube", EditorObjectType.Cube)
            {
                Position = new Vector3(0f, 0f, -10f),
            });
            Selected = Objects[2];
        }

        public void AddObject(EditorObjectType type)
        {
            int cubeN = 0, emptyN = 0;
            foreach (EditorObject o in Objects)
            {
                if (o.Type == EditorObjectType.Cube) cubeN++;
                else if (o.Type == EditorObjectType.Empty) emptyN++;
            }
            EditorObject newObj = type switch
            {
                EditorObjectType.Cube => new EditorObject("Cube " + (cubeN + 1), EditorObjectType.Cube),
                _ => new EditorObject("Empty " + (emptyN + 1), EditorObjectType.Empty),
            };
            newObj.Position = new Vector3(0f, 0f, -10f);
            Objects.Add(newObj);
            Selected = newObj;
        }
    }
}
