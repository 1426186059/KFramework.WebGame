import { Assets, Spritesheet } from "pixi.js";

export class ResCenter
{
    private constructor() {}
    private static readonly m_Instance:ResCenter = new ResCenter();
    public static GetSingleton():ResCenter
    {
        return ResCenter.m_Instance;
    }
    
    public mapAtlas = new Map<string, Spritesheet>();;
    public Init() : void
    {
        this.LoadAtlas();
    }

    private LoadAtlas():void
    {
        let mAtlasList:string[] =[];
        mAtlasList[0] = "MyRes/Atlas/Bonus.atlas";
        mAtlasList[1] = "MyRes/Atlas/Born.atlas";
        mAtlasList[2] = "MyRes/Atlas/characters.atlas";
        mAtlasList[3] = "MyRes/Atlas/Enemys.atlas";
        mAtlasList[4] = "MyRes/Atlas/Map.atlas";
        mAtlasList[5] = "MyRes/Atlas/misc-3.atlas";
        mAtlasList[6] = "MyRes/Atlas/Player1.atlas";
        mAtlasList[7] = "MyRes/Atlas/Player2.atlas";
        mAtlasList[8] = "MyRes/Atlas/Shield.atlas";
        mAtlasList[9] = "MyRes/Atlas/UIView.atlas";

        let nType:number = 2;
        if(nType == 1)
        {
            for(let key in mAtlasList)  
            {
                //this.mapAtlas.set(key, await Assets.load(mAtlasList[key])); 
            }
        }
        else
        {
            for(let key in mAtlasList)  
            {
                this.mapAtlas.set(key, Assets.get(mAtlasList[key])); 
            }
        }
    }

        
}