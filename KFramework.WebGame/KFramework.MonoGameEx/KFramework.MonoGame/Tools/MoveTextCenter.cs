//using System;
//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;
//using UnityEngine.UI;

//[ExecuteAlways]
//public class MoveTextCenter : MonoBehaviour
//{
//    public bool bAutoRun = false;
//    public RectMask2D parentMaskNode; //遮罩区域
//    public float scrollSpeed = 0.05f;
//    private int nAlignmentType = 0;
//    private bool isMove;
//    private bool bReJudgeMove;
    
//    private float offsetWidth;
//    private float fMinPos;
//    private float fMaxPos;
    
//    private Vector3 offsetVec;
//    private RectTransform mRectTransform;
//    private Vector2 oriPos;
//    private bool bInit = false;

//    Text mText = null;
//    private string LastTextStr;


//    private void Start()
//    {
//        Init();
//    }
    
//    void Init()
//    {
//        if (!bInit)
//        {
//            bInit = true;
//            mText = GetComponent<Text>();

//            mRectTransform = GetComponent<RectTransform>();
//            mRectTransform.sizeDelta = parentMaskNode.rectTransform.sizeDelta;
//            mRectTransform.anchorMin = Vector2.one * 0.5f;
//            mRectTransform.anchorMax = Vector2.one * 0.5f;
//            oriPos = mRectTransform.anchoredPosition;
//        }
//    }
    
//#if UNITY_EDITOR
//    private void EditorAdjust()
//    {
//        if (mRectTransform != null && parentMaskNode != null)
//        {
//            mRectTransform.sizeDelta = parentMaskNode.rectTransform.sizeDelta;
//            mRectTransform.anchorMin = Vector2.one * 0.5f;
//            mRectTransform.anchorMax = Vector2.one * 0.5f;
//        }
//    }
//#endif
    
//    public void ReBuild()
//    {
//        Init();
//        isMove = false;
//        bReJudgeMove = true;
//        mRectTransform.anchoredPosition = oriPos;
//    }
    
//    private void CalculateIfMove()
//    {
//        bReJudgeMove = false;
//        isMove = false;
//        if (parentMaskNode != null)
//        {
//            float parentWidh = parentMaskNode.rectTransform.rect.width;
//            Vector2 mPrefectSize = GetPreferredSize1(gameObject);
//            float textWidth = mPrefectSize.x;
//            if (textWidth >= parentWidh)
//            {
//                SetTextAlignmentType(gameObject);
//                if (nAlignmentType == 1)
//                {
//                    fMinPos = -textWidth;
//                    fMaxPos = parentWidh;
//                }
//                else if (nAlignmentType == 2)
//                {
//                    fMinPos = -parentWidh;
//                    fMaxPos = textWidth;
//                }
//                else
//                {
//                    fMinPos = -(textWidth + parentWidh) / 2.0f;
//                    fMaxPos = -fMinPos;
//                }
                
//                offsetVec = mRectTransform.anchoredPosition;
//                isMove = true;
//            }
//        }
//    }

//    private string GetTextStr()
//    {
//        return mText.text;
//    }

//    private void SetTextAlignmentType(GameObject obj)
//    {
//        if (mText != null)
//        {
//            if (mText.alignment == TextAnchor.LowerLeft || mText.alignment == TextAnchor.MiddleLeft ||
//                mText.alignment == TextAnchor.UpperLeft)
//            {
//                nAlignmentType = 1;
//            }
//            else if (mText.alignment == TextAnchor.LowerRight || mText.alignment == TextAnchor.MiddleRight ||
//                     mText.alignment == TextAnchor.UpperRight)
//            {
//                nAlignmentType = 2;
//            }
//            else
//            {
//                nAlignmentType = 0;
//            }
//        }
//    }

//    private Vector2 GetPreferredSize1(GameObject obj)
//    {
//        Text mText = obj.GetComponent<Text>();
//        if (mText != null)
//        {
//            return new Vector2(mText.preferredWidth, mText.preferredHeight);
//        }
        
//        return Vector2.zero;
//    }
    
//    private void ScrollHorizontal()
//    {
//        offsetVec.x -= scrollSpeed * Time.deltaTime;
//        transform.GetComponent<RectTransform>().anchoredPosition = offsetVec;
        
//        if (offsetVec.x <= fMinPos)
//        {
//            offsetVec.x = fMaxPos;
//        }
//    }
    
//    // Update is called once per frame
//    void Update()
//    {
//        if (Application.isPlaying)
//        {
//            if (bAutoRun)
//            {
//                string nowStr = GetTextStr();
//                if (LastTextStr != nowStr)
//                {
//                    ReBuild();
//                    LastTextStr = nowStr;
//                }
//            }

//            if (bReJudgeMove)
//            {
//                CalculateIfMove();
//            }

//            if (isMove)
//            {
//                ScrollHorizontal();
//            }
//        }
//        else
//        {
//#if UNITY_EDITOR
//            EditorAdjust();
//#endif
//        }
//    }
//}