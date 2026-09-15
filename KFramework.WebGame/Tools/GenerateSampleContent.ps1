<#
.SYNOPSIS
  为三个示例生成“样例内容”（Content/raw），让 kfc 能产出 content 包。
  精灵用纯文本 *.sprite.json（矢量形状，离线光栅化），音频用占位 PCM *.wav。
  运行：pwsh Tools/GenerateSampleContent.ps1
#>
$ErrorActionPreference = 'Stop'
$Base = Resolve-Path (Join-Path $PSScriptRoot '..')   # -> KFramework.WebGame

function Write-Sprite {
    param($RelPath, $W, $H, $Color, [string]$Shape='rect')
    $full = Join-Path $Base $RelPath
    New-Item -ItemType Directory -Force -Path (Split-Path $full) | Out-Null
    $shapeObj = if ($Shape -eq 'circle') {
        @{ type='circle'; cx=[int]($W/2); cy=[int]($H/2); r=[math]::Max(1,[int]($W/2)-1); color=$Color }
    } else {
        @{ type='rect'; x=1; y=1; w=$W-2; h=$H-2; color=$Color }
    }
    $obj = [ordered]@{ width=$W; height=$H; shapes=@($shapeObj) }
    $json = $obj | ConvertTo-Json -Depth 5
    [System.IO.File]::WriteAllText($full, $json, [System.Text.UTF8Encoding]::new($false))
}

function Write-Wav {
    param($RelPath, $Seconds=0.15, $SampleRate=8000)
    $full = Join-Path $Base $RelPath
    New-Item -ItemType Directory -Force -Path (Split-Path $full) | Out-Null
    $samples = [int]($Seconds * $SampleRate)
    $dataLen = $samples * 2
    $total = 44 + $dataLen
    $ms = New-Object System.IO.MemoryStream($total)
    $bw = New-Object System.IO.BinaryWriter($ms)
    $enc = [System.Text.Encoding]::ASCII
    $bw.Write($enc.GetBytes('RIFF')); $bw.Write([uint32]($total-8)); $bw.Write($enc.GetBytes('WAVE'))
    $bw.Write($enc.GetBytes('fmt ')); $bw.Write([uint32]16); $bw.Write([uint16]1)
    $bw.Write([uint16]1); $bw.Write([uint32]$SampleRate); $bw.Write([uint32]($SampleRate*2))
    $bw.Write([uint16]2); $bw.Write([uint16]16)
    $bw.Write($enc.GetBytes('data')); $bw.Write([uint32]$dataLen)
    for ($i=0; $i -lt $samples; $i++) { $bw.Write([int16]0) }
    $bw.Flush(); [System.IO.File]::WriteAllBytes($full, $ms.ToArray())
    $bw.Dispose(); $ms.Dispose()
}

$palette = @('#ff5555','#55ff55','#5555ff','#ffff55','#ff55ff','#55ffff','#ffaa00','#aaaaff',
             '#88aa55','#aa8855','#55aa88','#5588aa','#aa5588','#aaaa55','#55aaaa','#aa55aa')

# ---------------- Example1 ----------------
$e1 = 'KFramework.Example1/Content/raw'
$sprites1 = @('sprites/player','sprites/flame','sprites/bullet','sprites/enemy_bullet',
              'sprites/particle','sprites/star','sprites/powerup','sprites/heart',
              'sprites/enemy_fighter','sprites/enemy_scout','sprites/enemy_bomber','sprites/asteroid')
for ($i=0; $i -lt $sprites1.Count; $i++) {
    $n = $sprites1[$i]
    $circle = $n -like '*bullet*' -or $n -like '*particle*' -or $n -like '*star*' -or $n -like '*enemy_bullet*'
    $sz = if ($circle) {16} else {32}
    $leaf = Split-Path $n -Leaf
    Write-Sprite "$e1/Bundles/sprites/$leaf.sprite.json" $sz $sz $palette[$i % $palette.Count] $(if($circle){'circle'}else{'rect'})
}
$game = [ordered]@{
    design  = @{ width=480; height=720 }
    player  = @{ speed=300; bulletSpeed=700; fireInterval=0.15; maxLives=3; invulnerableTime=1.6; radius=11 }
    enemies = [ordered]@{
        fighter = @{ texture='sprites/enemy_fighter'; health=1; speed=100; score=100; radius=10 }
        scout   = @{ texture='sprites/enemy_scout';   health=1; speed=140; score=120; radius=9 }
        bomber  = @{ texture='sprites/enemy_bomber';  health=3; speed=60;  score=300; radius=14 }
        asteroid= @{ texture='sprites/asteroid';      health=5; speed=40;  score=500; radius=16 }
    }
    waves = @(
        @{ spawn=@(@{type='fighter';count=8}); interval=0.5; restAfter=1.5 },
        @{ spawn=@(@{type='scout';count=6});   interval=0.4; restAfter=1.5 },
        @{ spawn=@(@{type='bomber';count=3});  interval=0.8; restAfter=2.0 }
    )
    powerup = @{ dropChance=0.14; fallSpeed=90; duration=8 }
}
# 清理旧的原地 raw 布局（sprites/data 直接放在 raw 根），避免与新的 Bundles 目录混淆
if (Test-Path (Join-Path $Base "$e1/sprites")) { Remove-Item -Recurse -Force (Join-Path $Base "$e1/sprites") }
if (Test-Path (Join-Path $Base "$e1/data"))    { Remove-Item -Recurse -Force (Join-Path $Base "$e1/data") }
New-Item -ItemType Directory -Force -Path (Join-Path $Base "$e1/Bundles/data") | Out-Null
[System.IO.File]::WriteAllText((Join-Path $Base "$e1/Bundles/data/game.json"), ($game | ConvertTo-Json -Depth 6), [System.Text.UTF8Encoding]::new($false))
# 打包配置：指定 Bundles 为 AssetBundle 打包目录（每个含资源的子文件夹 = 一个 AssetBundle）
[System.IO.File]::WriteAllText((Join-Path $Base "$e1/bundles.json"), '{"bundlesDir":"Bundles"}', [System.Text.UTF8Encoding]::new($false))

# ---------------- Example2 ----------------
$e2 = 'KFramework.Example2/Content/raw'
$sprites2 = @('Map_0','Map_1','Map_2','Map_3','Map_5','Explode1','Explode2','Flag','Shield')
for ($i=0; $i -lt 32; $i++) { $sprites2 += "Player1_$i" }
for ($i=0; $i -lt 64; $i++) { $sprites2 += "Enemys_$i" }
for ($i=0; $i -lt 4;  $i++) { $sprites2 += "bullet_$i" }
for ($i=0; $i -lt 4;  $i++) { $sprites2 += "Born_$i" }
for ($i=0; $i -lt 6;  $i++) { $sprites2 += "Bonus_$i" }
for ($i=0; $i -lt $sprites2.Count; $i++) {
    $n = $sprites2[$i]
    $circle = $n -like 'bullet*' -or $n -like 'Born*' -or $n -like 'Explode*'
    $sz = if ($n -like 'Map_*') {64} elseif ($n -like 'Player1*' -or $n -like 'Enemys*') {32} else {24}
    Write-Sprite "$e2/sprites/$($n).sprite.json" $sz $sz $palette[$i % $palette.Count] $(if($circle){'circle'}else{'rect'})
}
foreach ($a in @('shoot','explosion','hit','pickup','powerup','gameover')) { Write-Wav "$e2/audio/$a.wav" }
New-Item -ItemType Directory -Force -Path (Join-Path $Base "$e2/levels") | Out-Null
for ($i=0; $i -lt 5; $i++) {
    $lvl = "level $i`n32 32`n" + ('#' * 32) + "`n" + ('.' * 32) + "`n"
    [System.IO.File]::WriteAllText((Join-Path $Base "$e2/levels/$($i.ToString('00')).txt"), $lvl, [System.Text.UTF8Encoding]::new($false))
}

# ---------------- Example3 ----------------
# 音频：让 SoundHelper 在运行时能取到（wav 为占位静音）。
$e3 = 'KFramework.Example3/Content/raw'
$audio3 = @('smb_coin','smb_jump-small','smb_jump-super','smb_stomp','smb_powerup','smb_pipe','smb_bump',
            'smb_1up','smb_bowser_fall','smb_bowserfire','smb_breakblock','smb_fireball','smb_fireworks',
            'smb_flagpole','smb_gameover','smb_jump','smb_pause','smb_lose_plife','smb_mariodie',
            'smb_passstage','smb_vine','smb_warning')
foreach ($a in $audio3) { Write-Wav "$e3/MyRes/Sounds/$a.wav" }
# 注意：Example3 的 SpriteSheetLoader 仍按“AtlasData JSON + 整张 PNG”的老格式取图，
# 与新管线（单精灵 -> 自动打包图集 -> 子图切片）不一致，待对齐（见 README 第 5 节）。
# 这里仅放置占位精灵，保证 kfc 能产出 content 包（构建通过）。

Write-Host "样例内容已生成："
Write-Host "  Example1 raw: $e1"
Write-Host "  Example2 raw: $e2  ($(($sprites2).Count) 个精灵 + 6 音频 + 5 关卡)"
Write-Host "  Example3 raw: $e3  ($($audio3.Count) 音频)"
