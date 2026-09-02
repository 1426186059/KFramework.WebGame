import { ObjPool } from "./ObjPool";
import { PoolItemContainer } from "./PoolItemContainer";

export class PoolContainer<T extends PoolItemContainer> extends ObjPool<T>
{
    public constructor(factory: () => T) 
    {
        super(factory);
    }

    public override push(obj: T) 
    {
        obj.visible = false;
        super.push(obj);
    }
    
    public pop(): T
    {
        let mItem = super.pop();
        mItem.visible = true;  
        return mItem;
    }
}
