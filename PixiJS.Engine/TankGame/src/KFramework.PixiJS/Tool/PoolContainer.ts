import { PoolItemContainer } from "./PoolItemContainer";

export class PoolContainer<T extends PoolItemContainer>
{
    private creteObjFunc:Function;
    private pool:T[] = [];
    private usedList:T[] = [];
    private poolMaxSize: number = 50;
    public constructor(factory: () => T) 
    {
        this.creteObjFunc = factory;
        this.pool.length = 0;
    }

    public init(initNum: number = 0, maxSize: number = 0) 
    {
        this.poolMaxSize = Math.max(maxSize, 0);
        this.preLoadObj(initNum);
    }
    
    public push(obj: T) 
    {
        obj.visible = false;
        console.assert(this.usedList.indexOf(obj) >= 0, "recyleObj Error: " + obj.toString());
        this.usedList.splice(this.usedList.indexOf(obj), 1);
        if(this.poolMaxSize > 0 && this.pool.length >= this.poolMaxSize)
        {
             obj.Dispose();
        }
        else
        {
            this.pool.push(obj);
        }
    }
    
    public pop(): T | undefined
    {
        let mItem:T | undefined;
        if(this.pool.length > 0)
        {
            mItem = this.pool.pop();
        }
        else
        {
            mItem = this.creteObjFunc();
        }
        
        if(mItem != undefined)
        {
            this.usedList.push(mItem);
            mItem.visible = true;
        }
          
        return mItem;
    }

    private preLoadObj(num: number) 
    {
        let size = num;
        while (this.pool.length + this.usedList.length <= size) 
        {
            let item = this.creteObjFunc();
            this.pool.push(item);
        }
    }

    public Dispose():void
    {
        for(let A of this.pool)
        {
            A.Dispose();
        }

        for(let A of this.usedList)
        {
            A.Dispose();
        }

        this.pool.length = 0;
        this.usedList.length = 0;
    }

}
