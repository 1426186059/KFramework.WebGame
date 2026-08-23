using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace KFramework.MonoGame
{
    public class KSceneBase:IDisposable
    {
        public readonly LinkedListNode<KSceneBase> SceneEntry;
        public readonly KTransform SceneNodeRoot = new KTransform(); 

        /// <summary>场景默认字体（由游戏层在初始化时设置）</summary>
        public SpriteFont Font2 { get; set; }

        public KSceneBase()
        {
            SceneEntry = new LinkedListNode<KSceneBase>(this);
        }

        public virtual void LoadContent()
        {

        }
        
        public virtual void Update()
        {
            KTransformHelper.Do_Update_AllChildList(SceneNodeRoot);
        }
        
        public virtual void Draw()
        {
            KTransformHelper.Do_Draw_AllChildList(SceneNodeRoot);
        }

        public virtual void Dispose()
        {
            
        }
    }
}