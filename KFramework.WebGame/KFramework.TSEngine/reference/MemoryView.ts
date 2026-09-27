// =============================================================================
// 参考文件（REFERENCE ONLY —— 不参与编译，放在 src/ 之外）。
//
// 来源：dotnet/runtime @ main
//   路径：src/mono/browser/runtime/marshal.ts   （注意：当前 main 已无独立 MemoryView.ts，
//         MemoryView 类是 marshal.ts 的一部分；旧文档里写的 src/mono/wasm/runtime/MemoryView.ts 已不存在）
// 许可：MIT（.NET Foundation）
//
// 这是 .NET WASM 运行时暴露给 JS 的 MemoryView 真实定义。TS 引擎里的 MemoryView_Span /
// MemoryView_ArraySegment 类型声明（src/types.d.ts）应当与此保持一致。
//
// 关键事实（与早期口头描述不同，以此为准）：
//   * 没有 getTypedArray 方法（内部叫 _unsafe_create_view，不对外）。
//   * copyTo(target, sourceOffset?) 只有 2 个参数（无 targetOffset / count）。
//   * set(source, targetOffset?) 2 个参数；且 source 的构造函数必须与视图元素类型一致，否则抛异常。
//   * slice(start?, end?) 返回的是 TypedArray【副本】（写入不会回写 C# 缓冲）。
//   * 没有 [] 索引器、没有单元素 get/set。
// =============================================================================

// 以下为从 marshal.ts 摘录的原文（含其内部依赖符号，无法独立编译，仅供对照）：

abstract class MemoryView implements IMemoryView {
	protected constructor (public _pointer: VoidPtr, public _length: number, public _viewType: MemoryViewType) {
		this._pointer = fixupPointer(_pointer, 0);
	}
	abstract dispose(): void;
	abstract get isDisposed(): boolean;
	_unsafe_create_view (): TypedArray {
		if (this._viewType == MemoryViewType.Byte) {
			return new Uint8Array(localHeapViewU8().buffer, this._pointer as any, this._length);
		} else if (this._viewType == MemoryViewType.Int32) {
			return new Int32Array(localHeapViewI32().buffer, this._pointer as any, this._length);
		} else if (this._viewType == MemoryViewType.Double) {
			return new Float64Array(localHeapViewF64().buffer, this._pointer as any, this._length);
		} else if (this._viewType == MemoryViewType.Single) {
			return new Float32Array(localHeapViewF32().buffer, this._pointer as any, this._length);
		} else {
			throw new Error("NotImplementedException");
		}
	}
	set (source: TypedArray, targetOffset?: number): void {
		mono_check(!this.isDisposed, "ObjectDisposedException");
		const targetView = this._unsafe_create_view();
		mono_check(source && targetView && source.constructor === targetView.constructor, () => `Expected ${targetView.constructor}`);
		targetView.set(source, targetOffset);
		// TODO consider memory write barrier
	}
	copyTo (target: TypedArray, sourceOffset?: number): void {
		mono_check(!this.isDisposed, "ObjectDisposedException");
		const sourceView = this._unsafe_create_view();
		mono_check(target && sourceView && target.constructor === sourceView.constructor, () => `Expected ${sourceView.constructor}`);
		const trimmedSource = sourceView.subarray(sourceOffset);
		// TODO consider memory read barrier
		target.set(trimmedSource);
	}
	slice (start?: number, end?: number): TypedArray {
		mono_check(!this.isDisposed, "ObjectDisposedException");
		const sourceView = this._unsafe_create_view();
		// TODO consider memory read barrier
		return sourceView.slice(start, end);
	}
	get length (): number {
		mono_check(!this.isDisposed, "ObjectDisposedException");
		return this._length;
	}
	get byteLength (): number {
		mono_check(!this.isDisposed, "ObjectDisposedException");
		return this._viewType == MemoryViewType.Byte ? this._length : this._viewType == MemoryViewType.Int32 ? this._length << 2 : this._viewType == MemoryViewType.Double ? this._length << 3 : 0;
	}
}
