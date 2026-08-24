using System;
using System.Collections.Generic;
using WebAssemblyBrowserApp.Engine;

namespace WebAssemblyBrowserApp.Games;

/// <summary>
/// FC《西游记》风格横版动作游戏核心逻辑：孙悟空挥棒闯关打妖怪，尽头挑战牛魔王。
/// 不依赖任何渲染后端，渲染由各端通过 IXyRenderer 调用 DrawScene 完成。
/// </summary>
public sealed class XyWorld
{
    public const float LevelWidth = 3200f;
    private const float MoveSpeed = 215f;
    private const float JumpSpeed = -470f;
    private const float Gravity = 1250f;
    private const float BossActivateX = LevelWidth - 620f;   // 进入 Boss 区域的触发线

    public readonly float ViewW, ViewH, GroundY;

    // 玩家
    public float Px, Py;
    public readonly float Pw = 40, Ph = 54;
    public float Pvx, Pvy;
    public int Facing = 1;
    public int Hp, MaxHp = 5;
    public float Invuln, AttackT, AttackCd;
    public int Score;
    public XyState State = XyState.Playing;
    public float StateTimer;
    public float CameraX;
    public bool BossActive;
    public XyEntity Boss;

    public readonly List<XyEntity> Entities = new();
    public readonly List<XyPlatform> Platforms = new();
    public readonly List<XyBg> Background = new();

    public XyWorld(float viewW, float viewH)
    {
        ViewW = viewW;
        ViewH = viewH;
        GroundY = viewH - 80f;
    }

    public void Reset()
    {
        Px = 90; Py = GroundY - Ph; Pvx = 0; Pvy = 0;
        Facing = 1; Hp = MaxHp; Invuln = 0; AttackT = 0; AttackCd = 0;
        Score = 0; State = XyState.Playing; StateTimer = 0;
        CameraX = 0; BossActive = false;

        Entities.Clear();
        Platforms.Clear();
        Background.Clear();

        // 砖台（顶部可站立）
        Platforms.Add(new XyPlatform(720,  GroundY - 100, 220, 24));
        Platforms.Add(new XyPlatform(1180, GroundY - 160, 180, 24));
        Platforms.Add(new XyPlatform(1680, GroundY - 90,  240, 24));
        Platforms.Add(new XyPlatform(2180, GroundY - 140, 160, 24));
        Platforms.Add(new XyPlatform(2620, GroundY - 120, 200, 24));

        // 妖兵（地面巡逻）
        AddZombie(520,  400, 720);
        AddZombie(960,  800, 1120);
        AddZombie(1420, 1280, 1600);
        AddZombie(1880, 1720, 2050);
        AddZombie(2320, 2200, 2480);

        // 飞妖（悬浮）
        AddBat(820, 330);
        AddBat(1310, 280);
        AddBat(1760, 350);
        AddBat(2120, 300);

        // 牛魔王（Boss）
        Boss = new XyEntity
        {
            Kind = XyEntityKind.Boss,
            X = LevelWidth - 380, Y = GroundY - 96,
            W = 90, H = 96,
            MinX = LevelWidth - 540, MaxX = LevelWidth - 140,
            Facing = -1,
            Hp = 12, MaxHp = 12,
        };
        Entities.Add(Boss);

        BuildBackground();
    }

    private void AddZombie(float startX, float minX, float maxX)
    {
        Entities.Add(new XyEntity
        {
            Kind = XyEntityKind.Zombie,
            X = startX, Y = GroundY - 46, W = 32, H = 46,
            MinX = minX, MaxX = maxX, Facing = 1,
            Hp = 2, MaxHp = 2,
        });
    }

    private void AddBat(float x, float y)
    {
        Entities.Add(new XyEntity
        {
            Kind = XyEntityKind.Bat,
            X = x, Y = y, W = 30, H = 20,
            BaseY = y, MinX = x - 60, MaxX = x + 60,
            Hp = 1, MaxHp = 1,
        });
    }

    private void BuildBackground()
    {
        var r = new Random(2026);

        // 云（视差 0.2）
        for (int i = 0; i < 18; i++)
        {
            float x = r.Next(-100, (int)LevelWidth + 200);
            float y = r.Next(50, 190);
            float w = r.Next(70, 130), h = 20 + r.Next(8);
            Background.Add(new XyBg(x, y, w, h, 0.2f, "#f1f3f5", 0.85f));
        }

        // 远山（视差 0.35）
        for (int i = 0; i < 13; i++)
        {
            float x = r.Next(0, (int)LevelWidth);
            float w = r.Next(220, 360);
            float h = r.Next(150, 210);
            string c = i % 3 == 0 ? "#5c7cfa" : (i % 3 == 1 ? "#748ffc" : "#9775fa");
            Background.Add(new XyBg(x, GroundY - h, w, h, 0.35f, c));
        }

        // 塔（视差 0.55）：塔身 + 金顶
        for (int i = 0; i < 8; i++)
        {
            float x = 200 + i * 430 + r.Next(-40, 40);
            float h = r.Next(120, 170);
            Background.Add(new XyBg(x, GroundY - h, 44, h, 0.55f, "#e8590c"));
            Background.Add(new XyBg(x - 6, GroundY - h - 18, 56, 18, 0.55f, "#ffd43b"));
        }

        // 树（视差 0.7）
        for (int i = 0; i < 10; i++)
        {
            float x = 120 + i * 320 + r.Next(-30, 30);
            float h = r.Next(70, 110);
            Background.Add(new XyBg(x - 20, GroundY - h - 26, 40, 26, 0.7f, "#51cf66"));
            Background.Add(new XyBg(x - 6, GroundY - h, 12, h, 0.7f, "#b08968"));
        }
    }

    public void Update(float dt)
    {
        StateTimer += dt;

        if (State == XyState.Playing)
        {
            UpdatePlaying(dt);
        }
        else if (Input.IsKeyPressed(Input.Enter) || Input.IsKeyPressed(Input.Space))
        {
            Reset();
            Audio.Beep(523, 0.06f, "square", 0.06f);
        }
    }

    private void UpdatePlaying(float dt)
    {
        bool left   = Input.IsKeyPressed("ArrowLeft") || Input.IsKeyPressed("a");
        bool right  = Input.IsKeyPressed("ArrowRight") || Input.IsKeyPressed("d");
        bool jump   = Input.IsKeyPressed(Input.Space) || Input.IsKeyPressed("ArrowUp") || Input.IsKeyPressed("w");
        bool attack = Input.IsKeyPressed("j") || Input.IsKeyPressed("z") || Input.IsKeyPressed("x");

        // ---- 玩家移动 ----
        float move = (right ? 1f : 0f) - (left ? 1f : 0f);
        if (move != 0) Facing = move > 0 ? 1 : -1;
        Pvx = move * MoveSpeed;

        if (jump && OnGround)
        {
            Pvy = JumpSpeed;
            OnGround = false;
            Audio.Beep(330, 0.07f, "square", 0.05f);
        }

        Pvy += Gravity * dt;
        Px += Pvx * dt;
        Py += Pvy * dt;

        // ---- 平台碰撞（地面 + 砖台顶部）----
        OnGround = false;
        if (Pvy >= 0 && Py + Ph >= GroundY && Py + Ph <= GroundY + 18)
        {
            Py = GroundY - Ph; Pvy = 0; OnGround = true;
        }
        foreach (var p in Platforms)
        {
            if (Pvy >= 0 && Py + Ph >= p.Y - 1 && Py + Ph <= p.Y + 14
                && Px + Pw > p.X + 4 && Px < p.X + p.W - 4)
            {
                Py = p.Y - Ph; Pvy = 0; OnGround = true;
            }
        }

        Px = Math.Clamp(Px, 0, LevelWidth - Pw);

        // ---- 攻击 ----
        AttackT -= dt;
        AttackCd -= dt;
        if (attack && AttackCd <= 0)
        {
            AttackT = 0.16f;
            AttackCd = 0.30f;
            Audio.Beep(240, 0.05f, "square", 0.06f);
            HitEnemies();
        }

        // ---- 敌人 AI ----
        UpdateEnemies(dt);

        // ---- 敌人碰撞玩家 ----
        Invuln -= dt;
        if (Invuln <= 0)
        {
            foreach (var e in Entities)
            {
                if (!e.Alive) continue;
                if (Overlap(Px, Py, Pw, Ph, e.X, e.Y, e.W, e.H)) { Hurt(e); break; }
            }
        }

        // ---- 相机跟随 ----
        CameraX = Math.Clamp(Px - ViewW * 0.38f, 0, LevelWidth - ViewW);

        // ---- Boss 激活 ----
        if (!BossActive && Boss is { Alive: true } && Px + Pw > BossActivateX)
        {
            BossActive = true;
            Audio.Beep(80, 0.5f, "square", 0.12f);
        }
    }

    private bool OnGround;

    private void HitEnemies()
    {
        float cx = Px + Pw / 2 + Facing * 46;
        float cy = Py + 18;
        const float hw = 34, hh = 24;

        foreach (var e in Entities)
        {
            if (!e.Alive || e.Flash > 0) continue;
            if (!Overlap(cx - hw, cy - hh, hw * 2, hh * 2, e.X, e.Y, e.W, e.H)) continue;

            e.Hp--;
            e.Flash = 0.25f;
            e.VX = Facing * 240;
            e.VY = -140;

            if (e.Kind == XyEntityKind.Boss)
            {
                Audio.Beep(120, 0.10f, "square", 0.10f);
            }
            else
            {
                Score += e.Kind == XyEntityKind.Zombie ? 100 : 150;
                Audio.Beep(150, 0.08f, "square", 0.08f);
            }

            if (e.Hp <= 0)
            {
                e.Alive = false;
                if (e.Kind == XyEntityKind.Boss)
                {
                    Score += 2000;
                    State = XyState.Victory;
                    StateTimer = 0;
                    Audio.Beep(523, 0.12f, "square", 0.10f);
                    Audio.Beep(659, 0.12f, "square", 0.10f);
                }
            }
        }
    }

    private void UpdateEnemies(float dt)
    {
        foreach (var e in Entities)
        {
            if (!e.Alive) continue;
            if (e.Flash > 0) e.Flash -= dt;

            switch (e.Kind)
            {
                case XyEntityKind.Zombie:
                    if (e.Flash > 0)
                    {
                        // 被击击退
                        e.X += e.VX * dt;
                        e.Y += e.VY * dt;
                        e.VY += Gravity * dt;
                        if (e.Y + e.H >= GroundY) { e.Y = GroundY - e.H; e.VY = 0; }
                    }
                    else
                    {
                        e.X += e.Facing * 55f * dt;
                        if (e.X < e.MinX) { e.X = e.MinX; e.Facing = 1; }
                        if (e.X + e.W > e.MaxX) { e.X = e.MaxX - e.W; e.Facing = -1; }
                        e.Y = GroundY - e.H;
                    }
                    break;

                case XyEntityKind.Bat:
                    e.Timer += dt;
                    e.Y = e.BaseY + MathF.Sin(e.Timer * 3f) * 26f;
                    e.X += (Px > e.X ? 1 : -1) * 22f * dt;
                    e.X = Math.Clamp(e.X, e.MinX, e.MaxX);
                    break;

                case XyEntityKind.Boss:
                    if (BossActive)
                    {
                        if (e.Flash > 0)
                        {
                            e.X += e.VX * dt;
                        }
                        else
                        {
                            e.X += e.Facing * 80f * dt;
                            if (e.X < e.MinX) { e.X = e.MinX; e.Facing = 1; }
                            if (e.X + e.W > e.MaxX) { e.X = e.MaxX - e.W; e.Facing = -1; }

                            e.Timer += dt;
                            if (e.Timer > 2.4f && e.Y + e.H >= GroundY - 1)
                            {
                                e.VY = -400;
                                e.Timer = 0;
                                Audio.Beep(100, 0.10f, "sawtooth", 0.08f);
                            }
                        }
                        e.VY += Gravity * dt;
                        e.Y += e.VY * dt;
                        if (e.Y + e.H >= GroundY) { e.Y = GroundY - e.H; e.VY = 0; }
                    }
                    break;
            }
        }
    }

    private void Hurt(XyEntity e)
    {
        Hp--;
        Invuln = 1.1f;
        Pvx = (Px + Pw / 2 < e.X + e.W / 2 ? -1 : 1) * 260;
        Pvy = -240;
        Audio.Beep(110, 0.16f, "sawtooth", 0.08f);
        if (Hp <= 0)
        {
            Hp = 0;
            State = XyState.GameOver;
            StateTimer = 0;
        }
    }

    private static bool Overlap(float ax, float ay, float aw, float ah, float bx, float by, float bw, float bh)
        => ax < bx + bw && ax + aw > bx && ay < by + bh && ay + ah > by;

    // ==================== 绘制 ====================

    public void DrawScene(IXyRenderer r)
    {
        r.Clear("#6ea8fe");
        float cam = CameraX;

        // 太阳
        float sunX = ViewW - 140 - cam * 0.05f;
        r.Alpha(0.35f);
        r.FillCircle(sunX, 92, 62, "#ffd43b");
        r.Alpha(1f);
        r.FillCircle(sunX, 92, 44, "#ffd43b");

        // 视差背景
        foreach (var bg in Background)
        {
            float x = bg.X - cam * bg.Parallax;
            if (x + bg.W < -60 || x > ViewW + 60) continue;
            if (bg.Alpha < 1f) r.Alpha(bg.Alpha);
            r.FillRect(x, bg.Y, bg.W, bg.H, bg.Color);
            if (bg.Alpha < 1f) r.Alpha(1f);
        }

        // 地面
        r.FillRect(-cam, GroundY, LevelWidth, ViewH - GroundY, "#8d6e63");
        r.FillRect(-cam, GroundY, LevelWidth, 8, "#51cf66");

        // 砖台
        foreach (var p in Platforms)
        {
            float x = p.X - cam;
            r.FillRect(x, p.Y, p.W, p.H, "#f59f00");
            r.FillRect(x, p.Y, p.W, 5, "#ffe066");
        }

        // 敌人
        foreach (var e in Entities)
        {
            if (!e.Alive) continue;
            if (e.Flash > 0)
            {
                r.Alpha(0.45f);
                DrawEntity(r, e, cam);
                r.Alpha(1f);
                r.FillRect(e.X - cam, e.Y, e.W, e.H, "#ffffff");
            }
            else
            {
                DrawEntity(r, e, cam);
            }
        }

        // 玩家
        bool blink = Invuln > 0 && (int)(Invuln * 12) % 2 == 0;
        if (blink) r.Alpha(0.45f);
        DrawMonkey(r, Px - cam, Py, Facing);
        if (blink) r.Alpha(1f);

        // 挥棒特效
        if (AttackT > 0)
        {
            float bx = Px + Pw / 2 - cam + Facing * 36;
            float by = Py + 16;
            r.FillRect(bx - 22, by, 44, 6, "#ffd43b");
            r.FillRect(bx - 22, by - 2, 44, 2, "#fff9db");
        }

        DrawHud(r);
        DrawOverlay(r);
    }

    private void DrawEntity(IXyRenderer r, XyEntity e, float cam)
    {
        float x = e.X - cam, y = e.Y;
        switch (e.Kind)
        {
            case XyEntityKind.Zombie: DrawZombie(r, x, y); break;
            case XyEntityKind.Bat:    DrawBat(r, x, y);    break;
            case XyEntityKind.Boss:   DrawBoss(r, x, y);   break;
        }
    }

    private static void DrawMonkey(IXyRenderer r, float x, float y, int facing)
    {
        // 腿部
        r.FillRect(x + 7, y + Ph1 - 16, 12, 16, "#7b4b2a");
        r.FillRect(x + Pw1 - 19, y + Ph1 - 16, 12, 16, "#7b4b2a");
        // 披风（身后）
        r.FillRect(facing > 0 ? x + 3 : x + Pw1 - 9, y + 22, 6, 24, "#e03131");
        // 身体金甲
        r.FillRect(x + 9, y + 22, Pw1 - 18, Ph1 - 36, "#f6c445");
        // 腰带
        r.FillRect(x + 8, y + Ph1 - 18, Pw1 - 16, 5, "#e03131");
        // 头（棕发）
        r.FillRect(x + 8, y + 4, Pw1 - 16, 20, "#b5651d");
        // 金箍
        r.FillRect(x + 6, y + 1, Pw1 - 12, 5, "#ffd43b");
        // 脸
        float faceX = facing > 0 ? x + 14 : x + Pw1 - 27;
        r.FillRect(faceX, y + 7, 13, 11, "#ff8787");
        // 眼睛
        float eyeX = facing > 0 ? x + 24 : x + Pw1 - 26;
        r.FillRect(eyeX, y + 10, 3, 3, "#212529");
    }

    private const float Pw1 = 40, Ph1 = 54;

    private static void DrawZombie(IXyRenderer r, float x, float y)
    {
        // 角
        r.FillRect(x + 12, y + 1, 8, 6, "#3b5bdb");
        // 头（绿）
        r.FillRect(x + 7, y + 7, 18, 14, "#63e6be");
        // 眼
        r.FillRect(x + 11, y + 11, 4, 4, "#e03131");
        r.FillRect(x + 17, y + 11, 4, 4, "#e03131");
        // 身体（蓝）
        r.FillRect(x + 9, y + 21, 14, 19, "#74c0fc");
        // 腿
        r.FillRect(x + 8, y + 40, 7, 6, "#495057");
        r.FillRect(x + 17, y + 40, 7, 6, "#495057");
    }

    private static void DrawBat(IXyRenderer r, float x, float y)
    {
        // 耳朵
        r.FillRect(x + 7, y, 6, 5, "#845ef7");
        r.FillRect(x + 17, y, 6, 5, "#845ef7");
        // 翅膀
        r.FillRect(x + 1, y + 4, 11, 6, "#b197fc");
        r.FillRect(x + 18, y + 4, 11, 6, "#b197fc");
        // 身体
        r.FillRect(x + 9, y + 2, 12, 12, "#845ef7");
        // 眼
        r.FillRect(x + 13, y + 5, 4, 3, "#ffffff");
    }

    private static void DrawBoss(IXyRenderer r, float x, float y)
    {
        // 角
        r.FillRect(x + 8,  y + 8, 16, 9, "#f8f9fa");
        r.FillRect(x + 66, y + 8, 16, 9, "#f8f9fa");
        // 头（红）
        r.FillRect(x + 20, y + 15, 50, 34, "#c92a2a");
        // 眼
        r.FillRect(x + 28, y + 24, 8, 6, "#ffd43b");
        r.FillRect(x + 54, y + 24, 8, 6, "#ffd43b");
        // 鼻环
        r.FillCircle(x + 45, y + 40, 6, "#ffd43b");
        // 身体（深红）
        r.FillRect(x + 14, y + 49, 62, 39, "#a61e1e");
        // 腰带
        r.FillRect(x + 12, y + 62, 66, 9, "#f8f9fa");
        // 腿（蹄）
        r.FillRect(x + 15, y + 88, 22, 8, "#343a40");
        r.FillRect(x + 53, y + 88, 22, 8, "#343a40");
    }

    private void DrawHud(IXyRenderer r)
    {
        // HP 心形
        string hearts = "";
        for (int i = 0; i < Hp; i++) hearts += "❤";
        if (hearts.Length > 0)
            r.FillText(hearts, 16, 34, "bold 20px system-ui, sans-serif", "#ff2b2b", "left");

        // 分数
        r.FillText("得分 " + Score.ToString("D7"), ViewW - 16, 34, "bold 20px system-ui, sans-serif", "#ffffff", "right");

        // 关卡标题
        r.FillText("西游记 · MONKEY KING", ViewW / 2, 30, "bold 18px system-ui, sans-serif", "#ffffff", "center");

        // Boss 血条
        if (BossActive && Boss is { Alive: true })
        {
            float bx = ViewW / 2 - 150;
            r.FillText("牛魔王", ViewW / 2, 56, "bold 16px system-ui, sans-serif", "#ffe066", "center");
            r.FillRect(bx, 64, 300, 12, "#343a40");
            r.FillRect(bx, 64, 300 * Boss.Hp / Boss.MaxHp, 12, "#ff6b6b");
        }
    }

    private void DrawOverlay(IXyRenderer r)
    {
        if (State == XyState.Playing) return;

        r.Alpha(0.62f);
        r.FillRect(0, 0, ViewW, ViewH, "#000000");
        r.Alpha(1f);

        if (State == XyState.GameOver)
        {
            r.FillText("游戏结束", ViewW / 2, ViewH / 2 - 40, "bold 46px system-ui, sans-serif", "#ff6b6b", "center");
            r.FillText("总分  " + Score, ViewW / 2, ViewH / 2 + 8, "bold 22px system-ui, sans-serif", "#ffe066", "center");
        }
        else
        {
            r.FillText("通关！降妖除魔，功德圆满", ViewW / 2, ViewH / 2 - 40, "bold 42px system-ui, sans-serif", "#ffd43b", "center");
            r.FillText("总分  " + Score, ViewW / 2, ViewH / 2 + 8, "bold 22px system-ui, sans-serif", "#ffe066", "center");
        }

        if ((int)(StateTimer * 2) % 2 == 0)
            r.FillText("按 回车 / 空格 重新开始 · ESC 返回菜单", ViewW / 2, ViewH / 2 + 60, "18px system-ui, sans-serif", "#ffffff", "center");
    }
}
