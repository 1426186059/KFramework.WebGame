using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace KFramework.MonoGame
{
    public class CommonResSerialization
    {
        public readonly List<Texture2D> m_TextureList = new List<Texture2D>();

        //public TextAsset FindTextAsset(string name)
        //{
        //    return m_TextAssetList.Find((x) => x != null && x.name == name);
        //}

        //public GameObject FindPrefab(string name)
        //{
        //    return m_PrefabList.Find((x) => x != null && x.name == name);
        //}

        //public GameObject FindPrefabByPrefixName(string name)
        //{
        //    return m_PrefabList.Find((x) => x != null && x.name.StartsWith(name));
        //}

        //public Sprite FindSprite(string name)
        //{
        //    return m_SpriteList.Find((x) => x != null && x.name == name);
        //}

        public Texture2D FindTexture(string name)
        {
            return m_TextureList.Find((x) => x != null && x.Name == name);
        }

        //public AudioClip FindAudioClip(string name)
        //{
        //    return m_AudoClipList.Find((x) => x != null && x.name == name);
        //}

        //public Shader FindShader(string name)
        //{
        //    return m_ShaderList.Find((x) => x != null && x.name == name);
        //}

        //public Material FindMaterial(string name)
        //{
        //    return m_MaterialList.Find((x) => x != null && x.name == name);
        //}

        //public SpriteAtlas GetAtlas(string atlasName)
        //{
        //    return m_AtlasList.Find((x) => x != null && x.name == atlasName);
        //}

        //public Sprite GetSpriteByAtlas(string atlasName, string spriteName)
        //{
        //    return GetAtlas(atlasName).GetSprite(spriteName);
        //}
    }
}