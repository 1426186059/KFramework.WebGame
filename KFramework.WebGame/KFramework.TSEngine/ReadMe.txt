KFramework.TSEngine（浏览器层 TypeScript）开发规范
==================================================

本目录是引擎的浏览器层：所有代码都在 requestAnimationFrame 主循环上跑，
每帧都会被执行到。因此这里的第一原则不是"写得顺手"，而是**每帧零分配**。


一、禁止匿名函数（本目录硬性规定）
----------------------------------

不允许使用匿名函数，包括箭头函数（() => {}）与函数表达式（const f = function () {}）。

    // ✘ 禁止
    window.addEventListener('blur', () => { pending = -1; });
    list.forEach((item) => write(item));
    const tick = function () { ... };

    // ✔ 用具名函数
    function onBlur(): void { pending = -1; }
    window.addEventListener('blur', onBlur);

    for (let i = 0; i < list.length; i++) write(list[i]);

原因：匿名函数只要引用了外部变量，就会连同被捕获的变量一起构成一个闭包对象，
**每次执行到这一行都要新建一次**（函数对象 + 上下文对象）。在每帧路径上，
这就是每秒 60 个垃圾；在循环里则是每次迭代一个。WASM 的 GC 是停止世界的，
一次回收的停顿远大于省下的那点写法便利。

具名函数声明在模块顶层，函数对象在模块加载时创建一次、之后全程复用，
配合模块级变量（复用同一个对象 / 数组）即可做到每帧零分配。

连带规则：
  * 需要状态就用**模块级变量**，不要靠闭包捕获；
  * 需要遍历就用 for 循环，不要用 forEach / map / filter（它们每次都要建回调）；
  * 事件监听一律用具名函数 —— 顺带保证 removeEventListener 能精确解绑，
    匿名函数绑上去是解不掉的（这是另一类泄漏）。


二、每帧路径的其它约束
----------------------

  * 缓冲复用：每帧要用的数组 / 对象在模块加载时一次开好（如事件流的 scratch），
    之后只读写、不 new；
  * 跨界数据走 MemoryView：C#→JS 用 view.set(源, 偏移) 直接写托管内存，
    不要 new 一个数组再返回（那是一次分配 + 一次复制）；
  * 不要在每帧路径上做字符串拼接、JSON 解析、try/catch 这些隐式分配的操作；
  * 一次性初始化（模块顶层、绑定监听）不受"零分配"约束，但仍须遵守第一条 —— 用具名函数。

第一条的<b>判定标准是调用频率</b>，不是语法形式 —— 先问一句：这段代码每帧会跑吗？

  严格执行 —— <b>每帧路径</b>（帧回调、输入事件、每帧的数据汇总与跨界面写入）：
    一律具名函数，不留任何闭包。这里的闭包是"每秒 60 个"，是 GC 的主要来源。

  不强制 —— <b>异步 / 一次性路径</b>（IndexedDB 读写、WebSocket 连接、纹理解码、
    模块初始化、绑定监听）：
    这些一次运行只跑几次，闭包的分配量可以忽略，可读性优先，不必为形式统一去改写。
    尤其 <c>new Promise((resolve, reject) => ...)</c> 的 executor 必须拿到 resolve / reject，
    语言层面就只能是个闭包 —— 硬改只是把分配挪个地方，还可能引入并发问题。

  真要改时要这么改（收益极小，通常不必）：把 resolve / reject <b>挂到各自的
  request / transaction 上</b>，回调用具名函数 + this 取回。挂对象而不是模块级 ——
  IndexedDB 读写可并发，放模块级会被后一个操作覆盖掉前一个的 resolve，
  第一个 Promise 就永远不结算了，那是实打实的 bug。

拿不准就按"每帧会不会跑"来判：会 → 用具名函数；不会 → 随意。


三、跨界约定
------------

  * TS 导出的模块函数名 == C# [JSImport("函数名", "模块名")] 中的函数名，二者必须逐字一致；
  * JS→C# 传字节只能给 Uint8Array（运行时不认裸 ArrayBuffer）；
  * C#→JS 的大块数据优先 MemoryView（Span<byte> + JSMarshalAs<MemoryView>），零拷贝；
  * JS→C# 方向**没有** MemoryView：运行时要求它内部的 MemoryView 对象，
    浏览器端造不出来，传 Uint8Array 会被断言 "Expected MemoryViewType.Byte" 拒绝。
    这个方向要传字节就只能用 byte[]（每次一份新数组，C# 侧可长期持有）。

  * **改跨界的函数签名，C# 与 TS 必须同时改**（参数个数 / 类型 / 返回值）。
    这类不匹配<b>编译期不报错</b>，要等运行时 marshal 才炸，而且报错信息指不到源头：
    例 —— C# 声明 `int TakeFrameData(Span<byte> buffer)`，TS 侧却沿用了旧的
    `takeFrameData(): Uint8Array`，运行时就会抛
    "Assert failed: Value is not an integer: 0 (object)"（0 是那个 Uint8Array 转字符串的结果），
    从字面上完全看不出是哪个函数。改一侧之前先搜另一侧的对应声明。


四、事件编码
------------

所有 HTML 输入事件的类型编号与字节布局统一在 html_event_type.ts，
C# 侧的镜像常量在 KFramework.MonoGame/Input/Input_GameFrameData.cs。
改任何一边的编号或长度，另一边必须同步 —— 这是唯一的两处定义，
不允许在第三个文件里再写一份私有的事件编号。


五、现状（待整改）
------------------

（统计口径：正则 `\=\>|function\s*\(`，含绑定监听与一次性初始化路径上的）

src 下现存约 62 处匿名函数 / 箭头函数，集中在：

    storage_indexeddb.ts  20   main.ts                9
    input_html_ime.ts      8   net_websocket.ts       8
    render_webgpu.ts       4   texture.ts             4
    audio.ts               4   html_canvas.ts         2
    game_update.ts         1   render_webgl20.ts      1
    storage_cachestorage.ts 1

<b>每帧路径已全部清零</b> —— 这是本轮唯一的目标，已完成：

    input_mouse.ts、input_touch.ts、input_keyboard.ts、html_event_type.ts、
    game_frame_take_js_data.ts、input_common.ts、
    main.ts（帧回调 onFrame、四个网络回调、unlockAudio）

改法范例：
  * 多个事件共用一个具名函数，差异从 <c>ev.type</c> 推断（触摸的 onTouch）；
  * 回调需要的对象存成模块级变量，别让具名函数去捕获局部变量
    （main.ts 的 onFrame + frameHost、onNetOpen + netNs）；
  * 坐标这种"每次都要返回两个值"的，改用复用的对象（input_common 的 Point / pointOut）。

剩余 57 处 <c>=></c>（其中 9 处是接口方法签名、不是匿名函数；真箭头 48 处）全部落在
<b>异步 / 一次性</b>路径上 —— IndexedDB 读写 20、IME 8、网络 4、纹理解码 4、渲染初始化 4、
音频 4、画布 2、其它 2 —— 均非每帧调用，按上面的判定标准<b>维持现状，不再改写</b>。

新写的代码：先问"这段每帧会跑吗"，会就一律遵守第一条。
