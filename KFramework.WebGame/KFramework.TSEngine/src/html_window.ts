const _cacheUint8Array = new Uint8Array(1);      // 每条事件的 data 只有 1 字节（Keys 序号），逐条 put
const _cacheDataView = new DataView(_cacheUint8Array.buffer);

export function getHTMLPageSize(view: MemoryView_Span | Int32Array): void 
{
    let _int32Scratch = new Int32Array(8);
    writeInts(view, [Math.round(window.innerWidth), Math.round(window.innerHeight)]);
}

export function CSExport_GetHTMLPageSize(view: MemoryView_Span): void 
{
    let offset = 0
    _cacheDataView.setInt16(offset, Math.round(window.innerWidth)); offset += 2;
    _cacheDataView.setInt16(offset, Math.round(window.innerHeight)); offset += 2;
    view.set(_cacheUint8Array.subarray(0, offset))
}