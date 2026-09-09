// =====================================================================
// 原生渲染层：C++ 直接调用 emscripten 的 WebGL2 绑定。
// 由 emcc 编译为静态库，C# 侧通过 P/Invoke 调用。
//
// 这里同时提供两个探针：
//   native_test()  仅验证 P/Invoke 通路是否打通（不碰 GL）
//   gfx_init()     验证 WebGL2 上下文能否在 .NET WASM 中建立
// =====================================================================

#include <emscripten/emscripten.h>
#include <emscripten/html5.h>
#include <GLES3/gl3.h>
#include <stdio.h>

extern "C" {

// 最简单的通路探针：不涉及任何 GL
EMSCRIPTEN_KEEPALIVE
int native_test(void) {
    return 42;
}

static EMSCRIPTEN_WEBGL_CONTEXT_HANDLE g_ctx = 0;

// 在指定 canvas 上创建 WebGL2 上下文并置为当前
EMSCRIPTEN_KEEPALIVE
int gfx_init(const char* selector) {
    EmscriptenWebGLContextAttributes attrs;
    emscripten_webgl_init_context_attributes(&attrs);
    attrs.alpha = 0;
    attrs.depth = 0;
    attrs.stencil = 0;
    attrs.antialias = 0;
    attrs.premultipliedAlpha = 0;
    attrs.preserveDrawingBuffer = 0;
    attrs.majorVersion = 2;
    attrs.minorVersion = 0;

    g_ctx = emscripten_webgl_create_context(selector, &attrs);
    if (g_ctx <= 0) {
        printf("[gfx] create_context failed\n");
        return 0;
    }
    if (emscripten_webgl_make_context_current(g_ctx) != EMSCRIPTEN_RESULT_SUCCESS) {
        printf("[gfx] make_context_current failed\n");
        return 0;
    }

    printf("[gfx] WebGL2 context ready, GL_VERSION=%s\n", glGetString(GL_VERSION));
    return 1;
}

// 用指定颜色清屏，验证 GL 指令确实生效
EMSCRIPTEN_KEEPALIVE
void gfx_clear(float r, float g, float b, float a) {
    glClearColor(r, g, b, a);
    glClear(GL_COLOR_BUFFER_BIT);
}

} // extern "C"
