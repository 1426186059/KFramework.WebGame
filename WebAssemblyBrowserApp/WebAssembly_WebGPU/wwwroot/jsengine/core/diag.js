// ===========================================================
// 诊断工具：让 C# 通过 JSImport 直接写入浏览器 console.info，
// 避免经过 Mono put_char / fd_write 管道（可能导致消息延迟或丢失），
// 并且确保与 JS 侧 console 消息顺序一致。
// ===========================================================

export const diag = {
    step(label) {
        console.info('[CS-step] ' + String(label ?? ''));
    },
    fail(label, message, stack) {
        console.error('[CS-fail] ' + String(label ?? '') + ' : ' + String(message ?? ''));
        if (stack) console.error('  stack> ' + String(stack).substring(0, 500));
    },
};
