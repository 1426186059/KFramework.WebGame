import { beforeEach, describe, expect, it } from "vitest";
import {
  LinkedList,
  LinkedListNode,
} from "../src/KFramework.PixiJS/Tool/LinkedList";

describe("LinkedList - 基础", () => {
  let list: LinkedList<number>;

  beforeEach(() => {
    list = new LinkedList<number>();
  });

  it("空链表：Count=0，首尾都为 null", () => {
    expect(list.Count).toBe(0);
    expect(list.First).toBeNull();
    expect(list.Last).toBeNull();
  });

  it("AddLast 单个节点：首尾都指向它", () => {
    const node = new LinkedListNode<number>(1);
    list.AddLast(node);

    expect(list.Count).toBe(1);
    expect(list.First).toBe(node);
    expect(list.Last).toBe(node);

    expect(node.List).toBe(list);
    expect(node.Value).toBe(1);
    expect(node.Previous).toBeNull();
    expect(node.Next).toBeNull();
  });

  it("AddLast 多个节点：双向链接正确", () => {
    const a = new LinkedListNode<number>(1);
    const b = new LinkedListNode<number>(2);
    const c = new LinkedListNode<number>(3);
    list.AddLast(a);
    list.AddLast(b);
    list.AddLast(c);

    expect(list.Count).toBe(3);
    expect(list.First).toBe(a);
    expect(list.Last).toBe(c);

    expect(a.Next).toBe(b);
    expect(b.Next).toBe(c);
    expect(c.Next).toBeNull();

    expect(c.Previous).toBe(b);
    expect(b.Previous).toBe(a);
    expect(a.Previous).toBeNull();
  });

  it("按 Next 正序遍历的结果与插入顺序一致", () => {
    const nodes = [1, 2, 3, 4, 5].map((v) => new LinkedListNode<number>(v));
    nodes.forEach((node) => list.AddLast(node));

    const walked: number[] = [];
    for (let node = list.First; node !== null; node = node.Next) {
      walked.push(node.Value);
    }

    expect(walked).toEqual([1, 2, 3, 4, 5]);
  });

  it("按 Previous 逆序遍历的结果与插入顺序相反", () => {
    const nodes = [1, 2, 3].map((v) => new LinkedListNode<number>(v));
    nodes.forEach((node) => list.AddLast(node));

    const walked: number[] = [];
    for (let node = list.Last; node !== null; node = node.Previous) {
      walked.push(node.Value);
    }

    expect(walked).toEqual([3, 2, 1]);
  });
});

describe("LinkedList - 删除", () => {
  let list: LinkedList<number>;
  let a: LinkedListNode<number>;
  let b: LinkedListNode<number>;
  let c: LinkedListNode<number>;

  beforeEach(() => {
    list = new LinkedList<number>();
    a = new LinkedListNode<number>(1);
    b = new LinkedListNode<number>(2);
    c = new LinkedListNode<number>(3);
    list.AddLast(a);
    list.AddLast(b);
    list.AddLast(c);
  });

  it("删头节点：First 后移，Count 递减，节点状态清空", () => {
    list.Remove(a);

    expect(list.Count).toBe(2);
    expect(list.First).toBe(b);
    expect(list.Last).toBe(c);
    expect(b.Previous).toBeNull();

    expect(a.List).toBeNull();
    expect(a.Previous).toBeNull();
    expect(a.Next).toBeNull();
  });

  it("删尾节点：Last 前移", () => {
    list.Remove(c);

    expect(list.Count).toBe(2);
    expect(list.First).toBe(a);
    expect(list.Last).toBe(b);
    expect(b.Next).toBeNull();

    expect(c.List).toBeNull();
  });

  it("删中间节点：前后邻居重新相连", () => {
    list.Remove(b);

    expect(list.Count).toBe(2);
    expect(a.Next).toBe(c);
    expect(c.Previous).toBe(a);

    expect(b.List).toBeNull();
  });

  it("删到空：首尾归 null", () => {
    list.Remove(a);
    list.Remove(b);
    list.Remove(c);

    expect(list.Count).toBe(0);
    expect(list.First).toBeNull();
    expect(list.Last).toBeNull();
  });

  it("删除不属于本链表的节点：无任何影响", () => {
    const other = new LinkedList<number>();
    const stranger = new LinkedListNode<number>(99);
    other.AddLast(stranger);

    list.Remove(stranger);

    expect(list.Count).toBe(3);
    expect(other.Count).toBe(1);
    expect(stranger.List).toBe(other);
  });

  it("重复删除同一节点：第二次无操作", () => {
    list.Remove(b);
    list.Remove(b);

    expect(list.Count).toBe(2);
  });
});

describe("LinkedList - AddLast 重挂载", () => {
  it("已挂载的节点会被先从旧链表摘下来", () => {
    const oldList = new LinkedList<number>();
    const newList = new LinkedList<number>();

    const a = new LinkedListNode<number>(1);
    const b = new LinkedListNode<number>(2);
    oldList.AddLast(a);
    oldList.AddLast(b);

    newList.AddLast(a);

    expect(oldList.Count).toBe(1);
    expect(oldList.First).toBe(b);
    expect(b.Previous).toBeNull();

    expect(newList.Count).toBe(1);
    expect(newList.First).toBe(a);
    expect(newList.Last).toBe(a);
    expect(a.List).toBe(newList);
    expect(a.Previous).toBeNull();
    expect(a.Next).toBeNull();
  });

  it("同一节点反复 AddLast 到同一链表：不会重复计数", () => {
    const list = new LinkedList<number>();
    const node = new LinkedListNode<number>(1);

    list.AddLast(node);
    list.AddLast(node);
    list.AddLast(node);

    expect(list.Count).toBe(1);
    expect(list.First).toBe(node);
    expect(list.Last).toBe(node);
    expect(node.Next).toBeNull();
    expect(node.Previous).toBeNull();
  });
});

describe("LinkedList - Clear", () => {
  it("清空后所有节点脱离链表", () => {
    const list = new LinkedList<number>();
    const nodes = [1, 2, 3].map((v) => new LinkedListNode<number>(v));
    nodes.forEach((node) => list.AddLast(node));

    list.Clear();

    expect(list.Count).toBe(0);
    expect(list.First).toBeNull();
    expect(list.Last).toBeNull();

    for (const node of nodes) {
      expect(node.List).toBeNull();
      expect(node.Previous).toBeNull();
      expect(node.Next).toBeNull();
      expect(node.Value).toBeGreaterThan(0); // Value 本身不受影响
    }
  });

  it("Clear 之后可以重新 AddLast", () => {
    const list = new LinkedList<number>();
    const node = new LinkedListNode<number>(1);
    list.AddLast(node);
    list.Clear();
    list.AddLast(node);

    expect(list.Count).toBe(1);
    expect(list.First).toBe(node);
  });
});
