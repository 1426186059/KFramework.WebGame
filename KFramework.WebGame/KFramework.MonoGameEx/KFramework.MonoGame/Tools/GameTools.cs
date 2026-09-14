namespace KFramework.MonoGame
{
    public static class GameTools
    {
        //public static bool OrObjectExist(UnityEngine.Object obj)
        //{
        //    if (obj is GameObject)
        //    {
        //        return obj != null;
        //    }
        //    else if (obj is Transform)
        //    {
        //        return obj != null;
        //    }
        //    else
        //    {
        //        Debug.LogError("OrObjectExist Error use");
        //    }

        //    return obj != null;
        //}

        //public static Vector3 GetUIRelativePosByWorldPos(Vector3 worldPos, Camera mCamera = null)
        //{
        //    if (mCamera == null)
        //    {
        //        mCamera = Camera.main;
        //    }

        //    Vector3 mVec3 = mCamera.WorldToScreenPoint(worldPos);
        //    return new Vector3(mVec3.x - Screen.width / 2f, mVec3.y - Screen.height / 2f, 0);
        //}

        //public static Vector3 GetWorldPosByUIRelativePos(Vector3 mRelativePos, Camera mCamera = null)
        //{
        //    if (mCamera == null)
        //    {
        //        mCamera = Camera.main;
        //    }

        //    var mScreenPoint = new Vector3(mRelativePos.x + Screen.width / 2f, mRelativePos.y + Screen.height / 2f, 0);
        //    return mCamera.ScreenToWorldPoint(mScreenPoint);
        //}

        //public static Vector3 WorldToUILocalPos(Vector3 worldPos, RectTransform mParentRectTransform, Camera camera = null)
        //{
        //    if (camera == null)
        //    {
        //        camera = Camera.main;
        //    }

        //    Vector2 localPoint = RectTransformUtility.WorldToScreenPoint(camera, worldPos);
        //    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(mParentRectTransform, localPoint, camera, out localPoint))
        //    {
        //        return new Vector3(localPoint.x, localPoint.y, 0);
        //    }
        //    else
        //    {
        //        Debug.LogError("ScreenPointToLocalPointInRectangle false");
        //    }

        //    return Vector3.zero;
        //}

        //public static Vector3 ScreenToUILocalPos(Vector2 ScreenPoint, RectTransform mParentRectTransform, Camera camera = null)
        //{
        //    if (camera == null)
        //    {
        //        camera = Camera.main;
        //    }

        //    Vector2 localPoint = Vector2.zero;
        //    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(mParentRectTransform, ScreenPoint, camera, out localPoint))
        //    {
        //        return new Vector3(localPoint.x, localPoint.y, 0);
        //    }

        //    return Vector3.zero;
        //}

        //public static Sprite TextureToSprite(Texture2D texture2D)
        //{
        //    return Sprite.Create(texture2D, new Rect(0, 0, texture2D.width, texture2D.height), new Vector2(0.5f, 0.5f));
        //}

        //public static string GetDownLoadSizeStr(long nSumSize)
        //{
        //    if (nSumSize >= 1024 * 1024 * 1024)
        //    {
        //        return (nSumSize / 1024f / 1024f / 1024f).ToString("N1") + "Gb";
        //    }
        //    else if (nSumSize >= 1024 * 1024)
        //    {
        //        return (nSumSize / 1024f / 1024f).ToString("N1") + "Mb";
        //    }
        //    else if (nSumSize >= 1024)
        //    {
        //        return (nSumSize / 1024f).ToString("N1") + "Kb";
        //    }
        //    else
        //    {
        //        return nSumSize.ToString("N1") + "B";
        //    }
        //}
    }
}
