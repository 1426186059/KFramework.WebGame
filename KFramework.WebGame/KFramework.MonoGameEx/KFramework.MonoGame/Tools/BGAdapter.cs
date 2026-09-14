namespace KFramework.MonoGame
{
    //public class BGAdapter : MonoBehaviour
    //{
    //    private RectTransform mRectTransform;
    //    private RectTransform mCanvasRectTransform;
    //    private Image mImage;
    //    private void Start()
    //    {
    //        mImage = GetComponent<Image>();
    //        mRectTransform = GetComponent<RectTransform>();
    //        var mParentCanvas = this.GetComponentInParent<Canvas>();
    //        mCanvasRectTransform = mParentCanvas.GetComponent<RectTransform>();
    //    }

    //    void Do()
    //    {
    //        float fScreenWidth = mCanvasRectTransform.rect.width;
    //        float fScreenHeight = mCanvasRectTransform.rect.height;
    //        float fCoef1 = fScreenHeight / fScreenWidth;

    //        float fWidth = mImage.sprite.rect.width;
    //        float fHeight = mImage.sprite.rect.height;
    //        float fCoef = fHeight / fWidth;
    //        if (fCoef1 >= fCoef)
    //        {
    //            fWidth = fScreenHeight / fCoef;
    //            fHeight = fScreenHeight;
    //        }
    //        else
    //        {
    //            fWidth = fScreenWidth;
    //            fHeight = fScreenWidth * fCoef;
    //        }

    //        mRectTransform.sizeDelta = new Vector2(fWidth, fHeight);
    //    }

    //    private Rect oldRect;
    //    void LateUpdate()
    //    {
    //        if (mCanvasRectTransform.rect != oldRect)
    //        {
    //            oldRect = mCanvasRectTransform.rect;
    //            this.Do();
    //        }
    //    }
    //}
}
