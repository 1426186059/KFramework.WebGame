import { KTime } from "../GameEngine/KTime";

export class KTimeOutGenerator
{
    private fLastUpdateTime:number = 0;
    private fInternalTime:number = 0;

    public static New(fInternalTime:number):KTimeOutGenerator
    {
        var temp = new KTimeOutGenerator();
        temp.Init(fInternalTime);
        return temp;
    }

    public Init(fInternalTime:number = 1.0):void
    {
        this.fInternalTime = fInternalTime;
        this.Reset();
    }
    
    public Reset():void
    {
        this.fLastUpdateTime = KTime.time;
    }

    public orTimeOut():boolean
    {
        if ((KTime.time - this.fLastUpdateTime) > this.fInternalTime)
        {
            this.Reset();
            return true;
        }

        return false;
    }

    public orTimeOutWithSpecialTime(fInternalTime:number):boolean
    {
        if (KTime.time - this.fLastUpdateTime > fInternalTime)
        {
            this.Reset();
            return true;
        }

        return false;
    }
    
}
