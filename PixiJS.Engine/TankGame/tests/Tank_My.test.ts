import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";
import { AnimatedSprite, Cache, Point, Texture, TextureSource, Ticker } from "pixi.js";

import { Tank_My } from "../src/app/Game/Tank_My";
import type { TankLevel } from "../src/app/Game/TankLevel";

/** 与 Tank_My 内部常量保持一致（私有字段，此处以字面量对齐） */
const MOVE_SPEED = 5;
const ANI_SPEED = 0.12;
const TILE_SIZE = 32;
const DEFAULT_SCALE = 2;
/** 出生格子 */
const SPAWN_X = 3;
const SPAWN_Y = 4;
/** 缓存的贴图数量，覆盖 Tank_My 所有 nType 可能用到的 key */
const TEXTURE_COUNT = 40;

/** 方向 -> 序列帧名称（源自 SwitchTankType 的取帧规则，nType = 0） */
const DIR_FRAMES = {
    UP: ["Player1_0", "Player1_1"], // +0 / +1
    RIGHT: ["Player1_8", "Player1_9"], // +8 / +9
    DOWN: ["Player1_16", "Player1_17"], // +16 / +17
    LEFT: ["Player1_24", "Player1_25"], // +24 / +25
} as const;

/** 创建一个独立的 1x1 贴图（不需要真实图片资源） */
function makeTexture(label: string): Texture {
    return new Texture({ source: new TextureSource({ width: 1, height: 1 }), label });
}

/**
 * 真实运行时 Player1_* 由 assetpack / Assets 写入 Cache，
 * 而 Texture.from() 内部只是 Cache.get()，未命中会返回 undefined。
 * 这里预先填充，保证 SwitchTankType 能取到有效贴图。
 */
function registerTankTextures(): void {
    for (let i = 0; i < TEXTURE_COUNT; i++) {
        const key = `Player1_${i}`;
        if (!Cache.has(key)) {
            Cache.set(key, makeTexture(key));
        }
    }
}

/** 最小可用的 TankLevel 替身：只提供 TileBase.resize 需要的两个成员 */
function createLevel(scale: number = DEFAULT_SCALE): TankLevel {
    const level = {
        fTileScaleCoef: scale,
        GetTilePos(x: number, y: number): Point {
            return new Point(x * TILE_SIZE * level.fTileScaleCoef, y * TILE_SIZE * level.fTileScaleCoef);
        },
    };
    return level as unknown as TankLevel;
}

/** 从显示列表中取回 Tank_My 内部创建的动画播放器（不依赖私有字段） */
function getAnimator(tank: Tank_My): AnimatedSprite {
    const found = tank.children.find((child): child is AnimatedSprite => child instanceof AnimatedSprite);
    if (!found) {
        throw new Error("Tank_My 未创建 AnimatedSprite");
    }
    return found;
}

/** 当前动画使用的序列帧名称，用于判定朝向与坦克类型 */
function frameLabels(tank: Tank_My): string[] {
    return getAnimator(tank).textures.map((texture) => texture.label);
}

/** 只提供 update 用到的 deltaTime 字段 */
function makeTicker(deltaTime = 1): Ticker {
    return { deltaTime } as Ticker;
}

function pressKey(key: string): void {
    window.dispatchEvent(new KeyboardEvent("keydown", { key }));
}

function releaseKey(key: string): void {
    window.dispatchEvent(new KeyboardEvent("keyup", { key }));
}

const createdTanks: Tank_My[] = [];

function createTank(level: TankLevel = createLevel()): Tank_My {
    const tank = new Tank_My(level, SPAWN_X, SPAWN_Y);
    createdTanks.push(tank);
    return tank;
}

beforeAll(() => {
    registerTankTextures();
});

beforeEach(() => {
    // update() 内部有 console.log("Tank_My update")，静音以免污染测试结果输出
    vi.spyOn(console, "log").mockImplementation(() => undefined);
});

afterEach(() => {
    // 复位所有按键：历史用例若遗留按下状态会污染后续用例
    (["w", "a", "s", "d"] as const).forEach(releaseKey);

    // 销毁坦克：AnimatedSprite.destroy 会把自己从共享 Ticker 上摘掉
    while (createdTanks.length > 0) {
        createdTanks.pop()?.destroy({ children: true });
    }

    vi.restoreAllMocks();
});

describe("Tank_My - 构造与初始化", () => {
    it("构造时应挂上 mSprite，并按关卡缩放系数定位到出生格子", () => {
        const tank = createTank();

        expect(tank.TileX).toBe(SPAWN_X);
        expect(tank.TileY).toBe(SPAWN_Y);
        expect(tank.children).toContain(tank.mSprite);
        expect(tank.scale.x).toBe(DEFAULT_SCALE);
        expect(tank.scale.y).toBe(DEFAULT_SCALE);
        expect(tank.position.x).toBe(SPAWN_X * TILE_SIZE * DEFAULT_SCALE);
        expect(tank.position.y).toBe(SPAWN_Y * TILE_SIZE * DEFAULT_SCALE);
    });

    it("构造时应创建 AnimatedSprite，并以朝上序列帧开始播放", () => {
        const tank = createTank();
        const animator = getAnimator(tank);

        expect(tank.children).toContain(animator);
        expect(animator.animationSpeed).toBe(ANI_SPEED);
        expect(animator.loop).toBe(true);
        expect(animator.playing).toBe(true);
        expect(animator.currentFrame).toBe(0);
        expect(frameLabels(tank)).toEqual([...DIR_FRAMES.UP]);
    });

    it("构造时应向 window 注册 keydown / keyup 监听", () => {
        const spy = vi.spyOn(window, "addEventListener");

        createTank();

        const types = spy.mock.calls.map(([type]) => type);
        expect(types).toContain("keydown");
        expect(types).toContain("keyup");
    });
});

describe("Tank_My - 移动输入 (update)", () => {
    it("按 W 向上移动：y -= 速度 * deltaTime", () => {
        const tank = createTank();
        pressKey("w");

        tank.update(makeTicker(1));

        expect(tank.position.y).toBe(SPAWN_Y * TILE_SIZE * DEFAULT_SCALE - MOVE_SPEED);
        expect(tank.position.x).toBe(SPAWN_X * TILE_SIZE * DEFAULT_SCALE);
    });

    it("按 S 向下移动：y += 速度 * deltaTime", () => {
        const tank = createTank();
        pressKey("s");

        tank.update(makeTicker(1));

        expect(tank.position.y).toBe(SPAWN_Y * TILE_SIZE * DEFAULT_SCALE + MOVE_SPEED);
    });

    it("按 A 向左移动：x -= 速度 * deltaTime", () => {
        const tank = createTank();
        pressKey("a");

        tank.update(makeTicker(1));

        expect(tank.position.x).toBe(SPAWN_X * TILE_SIZE * DEFAULT_SCALE - MOVE_SPEED);
    });

    it("按 D 向右移动：x += 速度 * deltaTime", () => {
        const tank = createTank();
        pressKey("d");

        tank.update(makeTicker(1));

        expect(tank.position.x).toBe(SPAWN_X * TILE_SIZE * DEFAULT_SCALE + MOVE_SPEED);
    });

    it("位移应随 deltaTime 线性缩放（帧率无关）", () => {
        const tank = createTank();
        pressKey("w");

        tank.update(makeTicker(2.5));

        expect(tank.position.y).toBe(SPAWN_Y * TILE_SIZE * DEFAULT_SCALE - MOVE_SPEED * 2.5);
    });

    it("未按下任何方向键时位置保持不变", () => {
        const tank = createTank();
        const origin = tank.position.clone();

        tank.update(makeTicker(1));
        tank.update(makeTicker(3));

        expect(tank.position.x).toBe(origin.x);
        expect(tank.position.y).toBe(origin.y);
    });

    it("反向键同时按下时位移相互抵消，朝向取最后判定的方向", () => {
        const tank = createTank();
        const origin = tank.position.clone();

        pressKey("w");
        pressKey("s");
        pressKey("a");
        pressKey("d");
        tank.update(makeTicker(1));

        // w/s 与 a/d 各自抵消
        expect(tank.position.x).toBe(origin.x);
        expect(tank.position.y).toBe(origin.y);
        // 判定顺序为 w -> s -> a -> d，最终朝向为 RIGHT
        expect(frameLabels(tank)).toEqual([...DIR_FRAMES.RIGHT]);
    });

    it("非 WASD 按键（含大写）不产生任何移动", () => {
        const tank = createTank();
        const origin = tank.position.clone();

        pressKey("q");
        pressKey("W");
        pressKey("ArrowUp");
        tank.update(makeTicker(1));

        expect(tank.position.x).toBe(origin.x);
        expect(tank.position.y).toBe(origin.y);
        expect(getAnimator(tank).playing).toBe(true); // 仍保持构造时的播放态
    });
});

describe("Tank_My - 朝向切换与动画控制", () => {
    it("四个方向应各自使用对应的序列帧", () => {
        const tank = createTank();
        expect(frameLabels(tank)).toEqual([...DIR_FRAMES.UP]);

        pressKey("d");
        tank.update(makeTicker());
        expect(frameLabels(tank)).toEqual([...DIR_FRAMES.RIGHT]);

        pressKey("s");
        releaseKey("d");
        tank.update(makeTicker());
        expect(frameLabels(tank)).toEqual([...DIR_FRAMES.DOWN]);

        pressKey("a");
        releaseKey("s");
        tank.update(makeTicker());
        expect(frameLabels(tank)).toEqual([...DIR_FRAMES.LEFT]);

        pressKey("w");
        releaseKey("a");
        tank.update(makeTicker());
        expect(frameLabels(tank)).toEqual([...DIR_FRAMES.UP]);
    });

    it("朝向变化时应从第 0 帧重新播放", () => {
        const tank = createTank();
        const animator = getAnimator(tank);

        // 手动推进一帧：animationSpeed(0.12) * deltaTime 超过 1 即切到下一帧
        animator.update(makeTicker(8.34));
        expect(animator.currentFrame).toBe(1);

        pressKey("s");
        tank.update(makeTicker(1));

        expect(animator.currentFrame).toBe(0);
        expect(animator.playing).toBe(true);
    });

    it("朝向不变时不应重建 AnimatedSprite，也不应重新加载帧数组", () => {
        const tank = createTank();
        const animator = getAnimator(tank);
        const texturesBefore = animator.textures;
        pressKey("w");

        tank.update(makeTicker(1));
        tank.update(makeTicker(1));

        expect(getAnimator(tank)).toBe(animator);
        expect(getAnimator(tank).textures).toBe(texturesBefore);
        expect(tank.children.length).toBe(2); // mSprite + AnimatedSprite
    });

    it("开始移动时播放动画，松开按键后停止", () => {
        const tank = createTank();
        const animator = getAnimator(tank);

        pressKey("w");
        tank.update(makeTicker(1));
        expect(animator.playing).toBe(true);

        releaseKey("w");
        tank.update(makeTicker(1));
        expect(animator.playing).toBe(false);

        pressKey("a");
        tank.update(makeTicker(1));
        expect(animator.playing).toBe(true);
    });

    it("松开按键后位置不再变化", () => {
        const tank = createTank();
        pressKey("w");
        tank.update(makeTicker(1));
        releaseKey("w");

        const stopped = tank.position.clone();
        tank.update(makeTicker(1));
        tank.update(makeTicker(1));

        expect(tank.position.x).toBe(stopped.x);
        expect(tank.position.y).toBe(stopped.y);
    });
});

describe("Tank_My - SwitchTankType", () => {
    it("切换类型后应立即套用该类型的序列帧（保持当前朝向）", () => {
        const tank = createTank();

        tank.SwitchTankType(2); // 朝上：2 * 2 + 0 / +1
        expect(frameLabels(tank)).toEqual(["Player1_4", "Player1_5"]);

        tank.SwitchTankType(1); // 朝上：1 * 2 + 0 / +1
        expect(frameLabels(tank)).toEqual(["Player1_2", "Player1_3"]);

        tank.SwitchTankType(0);
        expect(frameLabels(tank)).toEqual([...DIR_FRAMES.UP]);
    });

    it("切换类型后再次转向应沿用新类型的帧", () => {
        const tank = createTank();
        tank.SwitchTankType(2);

        pressKey("d"); // RIGHT: 2 * 2 + 8 / +9
        tank.update(makeTicker());
        expect(frameLabels(tank)).toEqual(["Player1_12", "Player1_13"]);

        pressKey("s"); // DOWN: 2 * 2 + 16 / +17
        tank.update(makeTicker());
        expect(frameLabels(tank)).toEqual(["Player1_20", "Player1_21"]);

        pressKey("a"); // LEFT: 2 * 2 + 24 / +25
        tank.update(makeTicker());
        expect(frameLabels(tank)).toEqual(["Player1_28", "Player1_29"]);
    });

    it("重复调用不会重复创建 AnimatedSprite", () => {
        const tank = createTank();
        const animator = getAnimator(tank);

        tank.SwitchTankType(1);
        tank.SwitchTankType(2);
        tank.SwitchTankType(0);

        expect(getAnimator(tank)).toBe(animator);
        expect(tank.children.length).toBe(2);
        expect(getAnimator(tank).playing).toBe(true);
    });

    it("边界：未缓存的类型（负数）取不到贴图，PlayAnimation 抛错", () => {
        const tank = createTank();
        const assertSpy = vi.spyOn(console, "assert").mockImplementation(() => undefined);

        expect(() => tank.SwitchTankType(-1)).toThrow();
        expect(assertSpy).toHaveBeenCalled();
    });
});

describe("Tank_My - PlayAnimation", () => {
    it("已存在播放器时只替换帧数组，并保持播放状态", () => {
        const tank = createTank();
        const animator = getAnimator(tank);
        const custom = [makeTexture("Custom_A"), makeTexture("Custom_B")];

        tank.PlayAnimation(custom);

        expect(getAnimator(tank)).toBe(animator);
        expect(frameLabels(tank)).toEqual(["Custom_A", "Custom_B"]);
        expect(animator.playing).toBe(true);
        expect(tank.children.length).toBe(2);
    });

    it("传入的帧数组长度不受限（单帧动画）", () => {
        const tank = createTank();
        const single = [makeTexture("Custom_Single")];

        tank.PlayAnimation(single);

        expect(frameLabels(tank)).toEqual(["Custom_Single"]);
        expect(getAnimator(tank).totalFrames).toBe(1);
    });
});

describe("Tank_My - 键盘绑定与释放", () => {
    it("Dispose 应尝试移除 keydown / keyup 监听", () => {
        const tank = createTank();
        const spy = vi.spyOn(window, "removeEventListener");

        tank.Dispose();

        expect(spy).toHaveBeenCalledTimes(2);
        expect(spy).toHaveBeenCalledWith("keydown", expect.any(Function));
        expect(spy).toHaveBeenCalledWith("keyup", expect.any(Function));
    });

    it("[已知缺陷] Dispose 之后监听器实际仍未被移除", () => {
        // AddKeyboard 用 this.OnKeyDown.bind(this) 注册（每次生成新函数引用），
        // 而 RemoveKeyboard 传入的是未绑定的 this.OnKeyDown，两者引用不同 -> 移除失败。
        // 修复 Tank_My 后本用例预期需要反转：Dispose 后不应再响应按键。
        const tank = createTank();
        tank.Dispose();

        pressKey("w");
        tank.update(makeTicker(1));

        expect(tank.position.y).toBe(SPAWN_Y * TILE_SIZE * DEFAULT_SCALE - MOVE_SPEED);
    });

    it("keyup 能正确复位对应按键，互不影响", () => {
        const tank = createTank();

        pressKey("w");
        pressKey("a");
        tank.update(makeTicker(1));
        const afterBoth = tank.position.clone();

        releaseKey("w");
        tank.update(makeTicker(1));

        // 只剩 a 生效：x 继续减少，y 不再变化
        expect(tank.position.x).toBe(afterBoth.x - MOVE_SPEED);
        expect(tank.position.y).toBe(afterBoth.y);
    });
});

describe("Tank_My - resize", () => {
    it("resize 应按当前关卡缩放系数重新定位", () => {
        const level = createLevel(DEFAULT_SCALE);
        const tank = createTank(level);

        level.fTileScaleCoef = 1.5;
        tank.resize();

        expect(tank.scale.x).toBe(1.5);
        expect(tank.scale.y).toBe(1.5);
        expect(tank.position.x).toBe(SPAWN_X * TILE_SIZE * 1.5);
        expect(tank.position.y).toBe(SPAWN_Y * TILE_SIZE * 1.5);
    });
});
