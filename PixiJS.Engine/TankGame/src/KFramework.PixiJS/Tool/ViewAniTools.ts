
// export class ViewAniTools 
// {
//     public static PlayShowRightToLeftAni(viewNode:Node, bShow:boolean, finishFunc?:Function)
//     {
//         if(bShow)
//         {
//             let width = screen.windowSize.width;   
//             let mAniObj = NodeExtention.getChildByPath(viewNode, "n_root")
//             let mWidget = mAniObj.getComponent(Widget)
//             viewNode.active = true  
//             if (mWidget)
//             {    
//                 mWidget.alignMode = Widget.AlignMode.ALWAYS;
//                 let fTargetLeft = width
//                 let fTargetRight = -width
//                 let fOriLeft = 0;
//                 let fOriRight = 0;
                
//                 mWidget.top = 0
//                 mWidget.bottom = 0
//                 tween(mWidget).set({left:fTargetLeft, right:fTargetRight}).to(0.45,{left:fOriLeft, right:fOriRight},{easing:"sineOut"}).call(()=>
//                 {
//                     finishFunc?.()
//                 }).start();
//             }
//             else
//             {    
//                 tween(mAniObj).set({position:new Vec3(width, 0, 0)}).to(0.45,{position:new Vec3(0,0,0)},{easing:"sineOut"}).call(()=>
//                 {
//                     finishFunc?.()
//                 }).start();
//             }
//         }
//         else
//         {
//             let width = screen.windowSize.width;   
//             let mAniObj = NodeExtention.getChildByPath(viewNode, "n_root")
//             let mWidget = mAniObj.getComponent(Widget)
//             if (mWidget)
//             { 
//                 let fTargetLeft = width
//                 let fTargetRight = -width
//                 let fOriLeft = 0;
//                 let fOriRight = 0;
                
//                 tween(mWidget).set({left:fOriLeft, right:fOriRight}).to(0.34,{left:fTargetLeft, right:fTargetRight},{easing:"sineInOut"}).call(()=>
//                 {
//                     viewNode.active = false
//                     finishFunc?.()
//                 }).start();
//             }
//             else
//             {
//                 mAniObj.setPosition(0, 0, 0);      
//                 tween(mAniObj).to(0.34,{position:new Vec3(width,0,0)},{easing:"sineInOut"}).call(()=>
//                 {
//                     viewNode.active = false
//                     finishFunc?.()
//                 }).start();
//             }
//         }
//     }
    
//     public static PlayShowScaleAni(viewNode:Node, bShow:boolean, finishFunc?:Function)
//     {
//         if(bShow)
//         {
//             viewNode.active = true 
//             let mAniObj = NodeExtention.getChildByPath(viewNode, "n_root")    
//             tween(mAniObj).set({scale: new Vec3(0, 0, 0)}).to(0.25,{scale:new Vec3(1,1,1)}, {easing:"sineIn"}).call(()=>
//             {
//                 finishFunc?.()
//             }).start();
//         }
//         else
//         {
//             let mAniObj = NodeExtention.getChildByPath(viewNode, "n_root")    
//             tween(mAniObj).set({scale: new Vec3(1, 1, 1)}).to(0.25,{scale:new Vec3(0,0,0)}, {easing:"sineIn"}).call(()=>
//             {
//                 viewNode.active = false
//                 finishFunc?.()
//             }).start();
//         }
//     }
    
//     public static PlayShowAlphaAni(viewNode:Node, bShow:boolean, finishFunc?:Function)
//     {
//         if(bShow)
//         {
//             viewNode.active = true 
//             let mAniObj = NodeExtention.getChildByPath(viewNode, "n_root")
//             let mUIOpacity = mAniObj.getComponent(UIOpacity);    
//             tween(mUIOpacity).set({opacity: 0}).to(0.25,{opacity: 255}, {easing:"sineIn"}).call(()=>
//             {
//                 finishFunc?.()
//             }).start();
//         }
//         else
//         {
//             let mAniObj = NodeExtention.getChildByPath(viewNode, "n_root")    
//             let mUIOpacity = mAniObj.getComponent(UIOpacity);
//             tween(mUIOpacity).set({opacity: 255}).to(0.25,{opacity: 0}, {easing:"sineIn"}).call(()=>
//             {
//                 viewNode.active = false
//                 finishFunc?.()
//             }).start();
//         }
//     }

//     public static PlayShowDownToUpAni(viewNode:Node, bShow:boolean, finishFunc?:Function)
//     {
//         if(bShow)
//         {
//             let height = screen.windowSize.height * 2;   
//             let mAniObj = NodeExtention.getChildByPath(viewNode, "n_root")
//             let mWidget = mAniObj.getComponent(Widget)
//             viewNode.active = true  
//             if (mWidget)
//             {    
//                 mWidget.alignMode = Widget.AlignMode.ALWAYS;
//                 let fTargetTop = height
//                 let fTargetBottom = -height
//                 let fOriTop = 0;
//                 let fOriBottom = 0;

//                 mWidget.top = 0
//                 mWidget.bottom = 0
//                 tween(mWidget).set({top:fTargetTop, bottom:fTargetBottom}).to(0.3,{top:fOriTop, bottom:fOriBottom},{easing:"sineOut"}).call(()=>
//                 {
//                     finishFunc?.()
//                 }).start();
//             }
//             else
//             {    
//                 tween(mAniObj).delay(0.01).set({position:new Vec3(0, -height, 0)}).to(0.3,{position:new Vec3(0,0,0)},{easing:"sineOut"}).call(()=>
//                 {
//                     finishFunc?.()
//                 }).start();
//             }
//         }
//         else
//         {
//             let height = screen.windowSize.height * 2;   
//             let mAniObj = NodeExtention.getChildByPath(viewNode, "n_root")
//             let mWidget = mAniObj.getComponent(Widget)
//             if (mWidget)
//             { 
//                 let fTargetTop = height
//                 let fTargetBottom = -height
//                 let fOriTop = 0;
//                 let fOriBottom = 0;
                
//                 tween(mWidget).set({top:fOriTop, bottom:fOriBottom}).to(0.3,{top:fTargetTop, bottom:fTargetBottom},{easing:"sineOut"}).call(()=>
//                 {
//                     viewNode.active = false
//                     finishFunc?.()
//                 }).start();
//             }
//             else
//             {     
//                 tween(mAniObj).set({position:new Vec3(0, 0, 0)}).to(0.3,{position:new Vec3(0,-height,0)},{easing:"sineOut"}).call(()=>
//                 {
//                     viewNode.active = false
//                     finishFunc?.()
//                 }).start();
//             }
//         }
//     }

// }


