import { _decorator,Color,tween,v2,v3,UITransform,Node, EventHandler,Vec3, sys, assetManager, math, Vec2, UIOpacity, Tween } from 'cc';
const { ccclass, property } = _decorator;

@ccclass('TimeTools')
export class TimeTools
{
    public static GetNowTimeStamp():number
    {
       return Date.now()
    }
    
    /*
    timestamp: 13位时间戳 | new Date() | Date()
    console.log(dateFormat(1714528800000, 'YY-MM-DD HH:mm:ss'))
    format => YY：年，M：月，D：日，H：时，m：分钟，s：秒，SSS：毫秒
        
    console.log(dateFormat(1680227496788, 'YYYY-MM-DD HH:mm:ss')) // 2023-03-31 09:51:36:788
    console.log(dateFormat(new Date(), 'YYYY年MM月DD日 HH时mm分ss秒SSS毫秒')) // 2023年03月31日 09时53分41秒730毫秒
    */
    static dateFormat (timestamp: number|string|Date, format = 'YYYY-MM-DD HH:mm:ss'): string {
    var date = new Date(timestamp)
    function fixedTwo (value: number): string {
      return value < 10 ? '0' + value : String(value)
    }
    var showTime = format
    if (showTime.includes('SSS')) {
      const S = date.getMilliseconds()
      showTime = showTime.replace('SSS', '0'.repeat(3 - String(S).length) + S)
    }
    if (showTime.includes('YY')) {
      const Y = date.getFullYear()
      showTime = showTime.includes('YYYY') ? showTime.replace('YYYY', String(Y)) : showTime.replace('YY', String(Y).slice(2, 4))
    }
    if (showTime.includes('M')) {
      const M = date.getMonth() + 1
      showTime = showTime.includes('MM') ? showTime.replace('MM', fixedTwo(M)) : showTime.replace('M', String(M))
    }
    if (showTime.includes('D')) {
      const D = date.getDate()
      showTime = showTime.includes('DD') ? showTime.replace('DD', fixedTwo(D)) : showTime.replace('D', String(D))
    }
    if (showTime.includes('H')) {
      const H = date.getHours()
      showTime = showTime.includes('HH') ? showTime.replace('HH', fixedTwo(H)) : showTime.replace('H', String(H))
    }
    if (showTime.includes('m')) {
      var m = date.getMinutes()
      showTime = showTime.includes('mm') ? showTime.replace('mm', fixedTwo(m)) : showTime.replace('m', String(m))
    }
    if (showTime.includes('s')) {
      var s = date.getSeconds()
      showTime = showTime.includes('ss') ? showTime.replace('ss', fixedTwo(s)) : showTime.replace('s', String(s))
    }
    return showTime
  }
}

/*
cc.log(Date.parse(new Date().toString()))   //获取当前的时间戳     为:1568259372000

cc.log(new Date(Date.parse(new Date().toString())))// 打印为 :Thu Sep 12 2019 11:36:12 GMT+0800 (中国标准时间)

let nowTime = new Date(Date.parse(new Date().toString()));    //然后我用nowTime接住

cc.log(nowTime.getDate())     //获取今天是几号，我现在是2019年9月12号，所以打印的是12

cc.log(nowTime.getUTCDate())    //同上一样，都是打印的12

cc.log(nowTime.getDay())    //打印的是4，因为今天星期四

cc.log(nowTime.getFullYear())    //打印2019
*/
