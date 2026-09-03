import { KTime } from "../GameEngine/KTime";

export class KTimeOutGenerator
{
    private fTime:number = 0;
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
        this.fTime = 0;
    }

    public orTimeOut():boolean
    {
        this.fTime += KTime.deltaTime;
        if (this.fTime > this.fInternalTime)
        {
            this.Reset();
            return true;
        }

        return false;
    }
    
    public orTimeOutWithSpecialTime(fInternalTime:number):boolean
    {
        this.fTime += KTime.deltaTime;
        if (this.fTime > fInternalTime)
        {
            this.Reset();
            return true;
        }

        return false;
    }
    
}
