import { IDisposable } from "./IDisposable";

export interface IPoolItem extends IDisposable
{
    OnPoolPop(): void;
    OnPoolPush(): void;
}