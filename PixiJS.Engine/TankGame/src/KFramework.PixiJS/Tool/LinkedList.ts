/**
 * 双向链表 —— 对齐 .NET 的 System.Collections.Generic.LinkedList\<T\> / LinkedListNode\<T\>。
 * 节点对象由外部持有（KTween 的 TweenItem.mEntry），因此可以做到 O(1) 插入/删除。
 */
export class LinkedListNode<T> 
{
  public list: LinkedList<T> | null = null;
  public previous: LinkedListNode<T> | null = null;
  public next: LinkedListNode<T> | null = null;

  public constructor(public readonly value: T) {}

  /** 该节点当前所属的链表，未挂载时为 null */
  public get List(): LinkedList<T> | null {
    return this.list;
  }

  public get Previous(): LinkedListNode<T> | null {
    return this.previous;
  }

  public get Next(): LinkedListNode<T> | null {
    return this.next;
  }

  public get Value(): T {
    return this.value;
  }
}

export class LinkedList<T> 
{
    private mFirst: LinkedListNode<T> | null = null;
    private mLast: LinkedListNode<T> | null = null;
    private mCount: number = 0;

    public get First(): LinkedListNode<T> | null 
    {
      return this.mFirst;
    }

    public get Last(): LinkedListNode<T> | null 
    {
      return this.mLast;
    }

  public get Count(): number 
  {
    return this.mCount;
  }

  /** 把节点挂到链表尾部；若节点已属于其它链表会先摘下来 */
  public AddLast(node: LinkedListNode<T>): LinkedListNode<T> {
    if (node.list !== null) 
    {
      node.list.Remove(node);
    }

    node.list = this;
    node.previous = this.mLast;
    node.next = null;

    if (this.mLast !== null) 
    {
      this.mLast.next = node;
    } 
    else 
    {
      this.mFirst = node;
    }

    this.mLast = node;
    this.mCount++;
    return node;
  }

  public Remove(node: LinkedListNode<T>): void 
  {
      if (node.list !== this) 
      {
        return;
      }

      if (node.previous !== null) 
      {
        node.previous.next = node.next;
      } 
      else 
      {
        this.mFirst = node.next;
      }

      if (node.next !== null) 
      {
        node.next.previous = node.previous;
      } 
      else 
      {
        this.mLast = node.previous;
      }

      node.list = null;
      node.previous = null;
      node.next = null;
      this.mCount--;
  }

  public Clear(): void 
  {
    let node: LinkedListNode<T> | null = this.mFirst;
    while (node !== null) 
    {
        const next: LinkedListNode<T> | null = node.next;
        node.list = null;
        node.previous = null;
        node.next = null;
        node = next;
    }
    this.mFirst = null;
    this.mLast = null;
    this.mCount = 0;
  }
}
