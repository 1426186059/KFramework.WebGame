import { defineConfig } from "vitest/config";

// 仅用于单元测试：vitest 会优先读取 vitest.config.ts，不会影响 vite 的应用构建配置
export default defineConfig({
    test: {
        // Tank_My 通过 window.addEventListener 注册键盘事件，需要 DOM 环境
        environment: "jsdom",
        include: ["tests/**/*.test.ts"],
        // 原来这里挂着 tests/setup.ts（关掉 Ticker.shared.autoStart，避免 AnimatedSprite.play()
        // 在 jsdom 里拉起 rAF 循环）。文件已删除，且现有用例都不涉及动画，故先不启用。
        // 以后若要测 AnimatedSprite / Ticker 相关代码，把 setup 加回来再打开这一行。
        coverage: {
            provider: "v8",
            reporter: ["text", "html"],
            reportsDirectory: "tests/.coverage",
        },
    },
});
