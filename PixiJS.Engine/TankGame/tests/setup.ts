import { Ticker } from "pixi.js";

// Pixi 的 AnimatedSprite.play() 会向共享 Ticker 注册回调，而 Ticker.shared / Ticker.system
// 默认 autoStart = true，会在 jsdom 中拉起 requestAnimationFrame 循环，
// 导致用例结束后仍有定时器在跑、帧号不可控。这里统一关闭自动启动，
// 使动画帧推进只能通过显式调用 animator.update(ticker) 触发，保证断言可预测。
Ticker.shared.autoStart = false;
Ticker.system.autoStart = false;
