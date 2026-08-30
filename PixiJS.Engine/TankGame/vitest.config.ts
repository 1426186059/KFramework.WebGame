import { defineConfig } from "vitest/config";

// 仅用于单元测试：vitest 会优先读取 vitest.config.ts，不会影响 vite 的应用构建配置
export default defineConfig({
    test: {
        // Tank_My 通过 window.addEventListener 注册键盘事件，需要 DOM 环境
        environment: "jsdom",
        include: ["tests/**/*.test.ts"],
        setupFiles: ["tests/setup.ts"],
        coverage: {
            provider: "v8",
            include: ["src/app/Game/Tank_My.ts"],
            reporter: ["text", "html"],
            reportsDirectory: "tests/.coverage",
        },
    },
});
