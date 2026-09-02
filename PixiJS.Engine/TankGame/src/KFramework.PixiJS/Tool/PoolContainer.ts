import { ObjPool } from "./ObjPool";
import { PoolItemContainer } from "./PoolItemContainer";

export class PoolContainer<T extends PoolItemContainer>
{
    private mPool:ObjPool<T>;
    public constructor(factory: () => T) 
    {
        this.mPool = new ObjPool<T>(factory);
    }

    public init(initNum: number = 0, maxSize: number = 0) 
    {
        this.mPool.init(initNum, maxSize);
    }
    
    public push(obj: T) 
    {
        obj.visible = false;
        this.mPool.push(obj);
    }
    
    public pop(): T
    {
        let mItem = this.mPool.pop();
        mItem.visible = true;  
        return mItem;
    }
    
    public Dispose():void
    {
        this.mPool.Dispose();
    }

}
