'use strict';

// 总览页：点卡片跳到对应资源页（真实页面跳转，而非视图切换）
$$('.ov-card').forEach(c => c.addEventListener('click', () => {
    const v = c.dataset.go;
    location.href = v + '.html';
}));
