WASM 基准测试 · 结果参考 ReadMe
================================

本文件汇总这套 C# / WASM 基准测试（JS↔C# 跨界互操作、MemoryView 零拷贝、反射性能等）
各模块的【目的、方法、关注点/结论要点】，方便以后不翻代码就能回忆每个测试在讲什么。

注意：本文是「设计意图与预期关注点」的汇总（取自代码注释与总纲说明），并非某一次运行的实测数值。
真实耗时数值请按下方「如何查看结果」在浏览器运行对应页面后，从结果区复制补充到本文对应小节。


一、如何查看结果
----------------
- 总纲页：浏览器打开 index.html，列出全部测试条目；点「运行」可在本页看该模块结果，点「运行全部」跑全部。
- 独立页：每个测试有同名 .html（见第三节），进入即自动运行该模块，顶部有「← 返回总纲」按钮。
- 运行方式：用支持 .NET WASM 的 http 服务器托管 wwwroot（Blazor/WASM 不能用 file:// 直接打开），
  例如 `dotnet run` 起来后用浏览器访问对应页面。

阅读结果的统一约定（来自 BenchKit / 总纲说明）：
- 抗干扰：每轮开始前强制 GC.Collect()；跑 Rounds = 5 轮取【最快的一轮】作为代表（干扰只会让某轮变慢）。
  - 「抖动(ms)」= 最慢轮 − 最快轮。
  - 「GC(次)」= 计时窗口里实际发生的回收次数；非 0（界面标橙）表示该行被回收拖过，数字别当真。
- 固定次数（次数不自适应，便于横比）：
  - 标量组（int / string）：Times = 20000 次。
  - 字节块组（byte[] / MemoryView）：TimesBytes = 2000 次；按 16 / 256 / 2048 三档长度看「固定成本 vs 线性增长」。
- 唯一通用判据是「ns/操作」（行按它升序排）；同组内「耗时(ms)」可直接横比，跨组请比 ns/操作。


二、测试总览（模块清单）
------------------------
序号  模块                     页面(.html)              一句话目的
 1   反射性能                  reflection               模拟 ORM/封包的反射操作开销
 2   JS→C# 跨界                jstocs                   JS 调 C# 的代价（int/string/byte[]/MemoryView）
 3   C#→JS 跨界                cstojs                   C# 调 JS 的代价（同上四类）
 4   跨界横向对比              crossboundary            同一操作在 基线/C#→JS/JS→C# 三走法下的耗时
 5   帧循环取舍                frameloop               输入事件「随帧推进」vs「C# 回头取」的取舍
 6   MemoryView 行为探测       memoryview              MemoryView 几种写法是否真动到托管内存（不计时）
 7   异步期 MemoryView 稳定性  memoryviewasync         await 期间视图内存会不会变（Span vs ArraySegment）
 8   GCHandle 裸堆视图         heapview                GCHandle 钉住+裸地址给 JS 建 TypedArray 行不行
 9   公开内存 API 探测         runtimeapi              localHeapViewU8 等公开 API 逐个实测
10   零拷贝的代价              zerocopy                把一次 memcpy 的价钱量成数字（4KB→4MB 分档）
11   跨界频率/帧率             crosscall               每帧 N 次空跨界调用的频率（滑块 0→100 万）
12   JS→C# 返回值能力          jsreturn                同步/异步各能返回什么类型、多值怎么传
13   像素回传性能              bytearrayreturn         MemoryView 写入 vs 直接返回 byte[] 的对比


三、各测试详解
--------------

1) 反射性能  reflection.html  (Bench_Reflection)
   目的：模拟 ORM 的 Load/Save 与封包的 Write/Read —— 属性读写、对象创建、方法调用、特性查询。
   关注：它内部会按场景输出多张结果表（不必拆成多个条目），衡量各类反射操作相对直接调用的开销。

2) JS→C# 跨界  jstocs.html  (Bench_JsToCs)
   目的：按 int / string / byte[] / MemoryView 四类分组，测 JS 函数调用 C# 的代价。
   关注：以 C#→C# 为基线横比，看清一次「JS 进 C#」的真实票价。

3) C#→JS 跨界  cstojs.html  (Bench_CsToJs)
   目的：同样按 int / string / byte[] / MemoryView 四类分组，测 C# 调 JS 的代价。
   关注：与 JS→C# 对照，方向不同成本往往也不同。

4) 跨界横向对比  crossboundary.html  (Bench_CrossBoundary)
   目的：同一操作在【基线 / C#→JS / JS→C#】三种走法下的耗时，同样四类分组。
   关注：直接回答「该不该为省一次跨界而引入拷贝」—— 配合第五节零拷贝代价一起看。

5) 帧循环取舍  frameloop.html  (Bench_FrameLoop)
   目的：一帧的输入事件是「随帧回调一起推进来」还是「C# 回头去取」两种实现。
   关注：这是引擎宿主那行代码的取舍依据；两种写法在逐帧输入下开销差异明显。

6) MemoryView 行为探测  memoryview.html  (Bench_MemoryView)
   目的：MemoryView 在 JS 侧有哪几种写法、每种是不是真动到托管内存 —— 不计时，只做行为探测。
   关注：起因是「slice() 拿到的是零拷贝视图」这个猜测：副本还是视图，跑一遍就知道。

7) 异步期 MemoryView 稳定性  memoryviewasync.html  (Bench_MemoryViewAsync)
   目的：用「存视图 → 强制 GC 间隙 → 读回」的同步手段，实测 await 期间 MemoryView 的内存会不会变。
   关注：对比两种钉法 —— 默认 Span（unpinned，调用结束即解除 pin）与 ArraySegment（pinned）。

8) GCHandle 裸堆视图  heapview.html  (Bench_HeapView)
   目的：另一条零拷贝路线：GCHandle 钉住数组 + 把裸地址交给 JS 建 TypedArray。
   关注：前提是 JS 拿得到 WASM 的 memory.buffer；.NET 不像 Emscripten 那样把堆挂全局，
         实测到底行不行（不行就得走绕道）。

9) 公开内存 API 探测  runtimeapi.html  (Bench_RuntimeApi)
   目的：按 dotnet/runtime 源码找出的整套【公开】内存 API：
         localHeapViewU8() 系列、setHeapU8/getHeapU8 系列、以及 create() 返回值上挂着的 Module。
   关注：⑨ 那句「全局扫不到裸堆」其实只是候选名单没扫到 getDotnetRuntime；
         本模块把它们逐个实测，并回答 HeapView 那条 _unsafe_create_view 绕道能不能换成公开 API。

10) 零拷贝的代价  zerocopy.html  (Bench_ZeroCopy)
    目的：把「一次 memcpy」的价钱量成数字，按 4KB→4MB 分档看它是固定成本还是随体积线性增长。
    关注：前面几页回答「能不能零拷贝」，这一页回答「值不值」。

11) 跨界频率/帧率  crosscall.html  (Bench_CrossCall)
    目的：只测频率 —— 每帧做 N 次空跨界调用（滑块 0 → 100 万）。
    关注：一次空跨界 ≈ 拷贝 47 KB，次数往往比数据量更决定帧率 —— 回答「帧率到底被什么吃掉」。

12) JS→C# 返回值能力  jsreturn.html  (Bench_JsReturn)
    目的：JS→C# 的「返回值能力」探测：同步 / 异步各能返回什么类型（int/double/bool/string/byte[]/JSObject），
          以及「一次调用能不能返回多个值」。

13) 像素回传性能  bytearrayreturn.html  (Bench_ByteArrayReturn)
    目的：对比把 RGBA 像素字节交回 C# 的两条路线：
          A) JS 写入 C# 预分配的 MemoryView（零拷贝）；
          B) JS 直接 return Uint8Array，C# 同步收到 byte[]（复制一次）。
    关注：哪种在「像素回传」这种典型负载下更划算。


四、约定速查（BenchKit 常量）
----------------------------
- PayloadSizes = [16, 256, 2048]     字节块分档长度（看固定成本 vs 线性增长）
- TextLength   = 256                  string 测试固定长度
- Times        = 20000               标量组固定次数（int / string）
- TimesBytes   = 2000                字节块组固定次数（byte[] / MemoryView）
- Rounds       = 5                   固定次数计时的轮数，取最快一轮
- 判据：ns/操作 为唯一通用横比标准；行按 ns/操作 升序排。


五、各模块结论（待运行后补充）
------------------------------
（下列为空，待在浏览器运行各页面后，把结果区的「耗时(ms)/ns操作/抖动/GC」粘贴到此）

1) reflection.html  ：
2) jstocs.html      ：
3) cstojs.html      ：
4) crossboundary.html：
5) frameloop.html   ：
6) memoryview.html  ：
7) memoryviewasync.html：
8) heapview.html    ：
9) runtimeapi.html  ：
10) zerocopy.html    ：
11) crosscall.html   ：
12) jsreturn.html    ：
13) bytearrayreturn.html：
