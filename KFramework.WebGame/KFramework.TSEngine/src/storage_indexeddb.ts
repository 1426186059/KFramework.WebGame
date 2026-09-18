// 【依赖 C#】由 KFramework.MonoGame.JSBind_IndexedDB 经 [JSImport(module: "indexeddb")] 调用；
// 产物 storage_indexeddb.js 由 SyncJsEngine 复制到 wwwroot/jsengine。
//
// 把「用户本地数据」持久化到浏览器 IndexedDB：账号、密码、用户存档、装备数据、设置等。
// IndexedDB 支持事务与键值/对象存储，适合需要可靠持久化、可被查询的用户数据；
// 不要在这里存资源包/纹理（那应走 Cache Storage，见 storage_cachestorage.ts）。
//
// 与 C# 交换字节采用「预分配缓冲 + 写回」模式：先 bytesSize() 探长度，C# 按长度分配 byte[] 后交给
// loadBytesInto() 写入，绕开 .NET WASM 不支持 byte[] 作为返回值的限制（SYSLIB1072）。

const DB_NAME = 'kframework-user';
const STORE = 'kv';
const DB_VERSION = 1;

let _dbPromise: Promise<IDBDatabase> | null = null;

function openDb(): Promise<IDBDatabase> {
    if (_dbPromise) return _dbPromise;
    _dbPromise = new Promise((resolve, reject) => {
        const req = indexedDB.open(DB_NAME, DB_VERSION);
        req.onupgradeneeded = () => {
            const db = req.result;
            if (!db.objectStoreNames.contains(STORE)) {
                db.createObjectStore(STORE);
            }
        };
        req.onsuccess = () => resolve(req.result);
        req.onerror = () => reject(req.error);
    });
    return _dbPromise;
}

function txDone(tx: IDBTransaction): Promise<void> {
    return new Promise((resolve, reject) => {
        tx.oncomplete = () => resolve();
        tx.onerror = () => reject(tx.error);
        tx.onabort = () => reject(tx.error);
    });
}

// ---- 字符串 KV（账号/密码/设置等） ----

export async function setString(key: string, value: string): Promise<void> {
    const db = await openDb();
    const tx = db.transaction(STORE, 'readwrite');
    tx.objectStore(STORE).put(value, key);
    await txDone(tx);
}

// 取字符串；缺失返回空字符串（注意：空字符串与缺失不可区分，需要区分请用 hasKey）。
export async function getString(key: string): Promise<string> {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction(STORE, 'readonly');
        const req = tx.objectStore(STORE).get(key);
        req.onsuccess = () => resolve((req.result as string) ?? '');
        req.onerror = () => reject(req.error);
    });
}

export async function hasKey(key: string): Promise<boolean> {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction(STORE, 'readonly');
        const req = tx.objectStore(STORE).getKey(key);
        req.onsuccess = () => resolve(req.result !== undefined);
        req.onerror = () => reject(req.error);
    });
}

export async function removeKey(key: string): Promise<void> {
    const db = await openDb();
    const tx = db.transaction(STORE, 'readwrite');
    tx.objectStore(STORE).delete(key);
    await txDone(tx);
}

// ---- 字节 KV（用户存档、序列化 blob 等） ----

export async function setBytes(key: string, bytes: Uint8Array): Promise<void> {
    const db = await openDb();
    const tx = db.transaction(STORE, 'readwrite');
    tx.objectStore(STORE).put(bytes, key);
    await txDone(tx);
}

export async function bytesSize(key: string): Promise<number> {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction(STORE, 'readonly');
        const req = tx.objectStore(STORE).get(key);
        req.onsuccess = () => {
            const v = req.result;
            if (v instanceof Uint8Array) resolve(v.byteLength);
            else if (v instanceof ArrayBuffer) resolve(v.byteLength);
            else if (typeof v === 'string') resolve(new TextEncoder().encode(v).byteLength);
            else resolve(-1);
        };
        req.onerror = () => reject(req.error);
    });
}

export async function loadBytesInto(key: string, buffer: Uint8Array): Promise<number> {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction(STORE, 'readonly');
        const req = tx.objectStore(STORE).get(key);
        req.onsuccess = () => {
            const v = req.result;
            let bytes: Uint8Array | null = null;
            if (v instanceof Uint8Array) bytes = v;
            else if (v instanceof ArrayBuffer) bytes = new Uint8Array(v);
            else if (typeof v === 'string') bytes = new TextEncoder().encode(v);
            if (!bytes) { resolve(-1); return; }
            buffer.set(bytes.subarray(0, buffer.length));
            resolve(bytes.byteLength);
        };
        req.onerror = () => reject(req.error);
    });
}
