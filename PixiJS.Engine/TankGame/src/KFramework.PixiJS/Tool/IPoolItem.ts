import { IDisposable } from "./IDisposable";

export interface IPoolItem extends IDisposable
{
    Reset(): void;
}