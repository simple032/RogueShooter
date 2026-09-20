# Overlay Chinese annotations onto map_full_annotated.png -> map_full_对照标注.png
Add-Type -AssemblyName System.Drawing

$srcPath = "D:\Game Provide\RogueShooter\screenshots\map_full_annotated.png"
$outPath = "D:\Game Provide\RogueShooter\screenshots\map_full_对照标注.png"

$src = [System.Drawing.Bitmap]::FromFile($srcPath)
$marginL = 170; $marginTB = 70
$bmp = New-Object System.Drawing.Bitmap ($src.Width + $marginL), ($src.Height + $marginTB * 2)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
$bg = [System.Drawing.Color]::FromArgb(255, 13, 14, 17)
$g.Clear($bg)
$g.DrawImage($src, $marginL, $marginTB, $src.Width, $src.Height)

$fontTitle = New-Object System.Drawing.Font "Microsoft YaHei", 15, ([System.Drawing.FontStyle]::Bold)
$fontSub   = New-Object System.Drawing.Font "Microsoft YaHei", 9
$fontBand  = New-Object System.Drawing.Font "Microsoft YaHei", 10, ([System.Drawing.FontStyle]::Bold)
$fontSmall = New-Object System.Drawing.Font "Microsoft YaHei", 8

$white = [System.Drawing.Brushes]::White
$gold  = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 220, 180, 90))
$gray  = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 160, 160, 170))

# title
$g.DrawString("箭骸行者 · 地牢地图 v6 落位对照全景", $fontTitle, $white, $marginL, 14)
$g.DrawString("出生三选一 → 西/中/东三折线主路 → O/M/I 三带（6 座阶段石拱门）→ 汇合大祭坛 → 商人前厅 → BOSS 房", $fontSub, $gray, $marginL, 40)

# world->image helpers (render: cam x=31 y=53.5 size=57, 800x1472)
function WX([float]$x) { return $marginL + (($x + 0.04) * (800.0 / 62.08)) }
function WY([float]$y) { return $marginTB + ((110.5 - $y) * (1472.0 / 114.0)) }

# band labels in left margin with bracket lines
$bands = @(
    @{ Name = "BOSS 房";        Y1 = 96;  Y2 = 106; Color = [System.Drawing.Color]::FromArgb(255, 200, 80, 80) },
    @{ Name = "商人前厅";        Y1 = 88;  Y2 = 95;  Color = [System.Drawing.Color]::FromArgb(255, 200, 160, 90) },
    @{ Name = "内带 I · 阶段三"; Y1 = 68;  Y2 = 87;  Color = [System.Drawing.Color]::FromArgb(255, 150, 130, 190) },
    @{ Name = "中带 M · 阶段二"; Y1 = 40;  Y2 = 67;  Color = [System.Drawing.Color]::FromArgb(255, 130, 170, 200) },
    @{ Name = "外带 O · 阶段一"; Y1 = 12;  Y2 = 39;  Color = [System.Drawing.Color]::FromArgb(255, 110, 190, 220) },
    @{ Name = "出生枢纽";        Y1 = 0;   Y2 = 11;  Color = [System.Drawing.Color]::FromArgb(255, 220, 200, 140) }
)
$pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(230, 220, 220, 230)), 2
foreach ($b in $bands) {
    $y1 = WY $b.Y2; $y2 = WY $b.Y1
    $midY = ($y1 + $y2) / 2
    $g.DrawLine($pen, 18, $y1, 18, $y2)
    $g.DrawLine($pen, 18, $y1, 26, $y1)
    $g.DrawLine($pen, 18, $y2, 26, $y2)
    $br = New-Object System.Drawing.SolidBrush ($b.Color)
    $g.DrawString($b.Name, $fontBand, $br, 30, $midY - 8)
}

# route legend (bottom area)
$ly = $marginTB + $src.Height + 10
$legend = @(
    @{ N = "西路 W";  C = [System.Drawing.Color]::FromArgb(255, 77, 217, 243) },
    @{ N = "中路 C";  C = [System.Drawing.Color]::FromArgb(255, 243, 204, 102) },
    @{ N = "东路 E";  C = [System.Drawing.Color]::FromArgb(255, 243, 107, 107) },
    @{ N = "汇合→BOSS"; C = [System.Drawing.Color]::FromArgb(255, 235, 235, 250) },
    @{ N = "道路小箱×12(1/2/1)"; C = [System.Drawing.Color]::FromArgb(255, 220, 180, 70) },
    @{ N = "死路大箱×3"; C = [System.Drawing.Color]::FromArgb(255, 190, 120, 50) }
)
$lx = $marginL
foreach ($it in $legend) {
    $br = New-Object System.Drawing.SolidBrush ($it.C)
    $g.FillRectangle($br, $lx, $ly + 3, 14, 14)
    $g.DrawString($it.N, $fontSmall, $white, $lx + 19, $ly)
    $lx += 19 + ($it.N.Length * 10) + 22
}
$g.DrawString("祭坛×3（小/中/大·必经·非死路）｜ 单向门=不可回出生 ｜ 回出生死路=不刷怪 ｜ 墙骨饰/岔口箭头按 L3 §2.2-§3", $fontSmall, $gray, $marginL, $ly + 24)

$g.Dispose()
$bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose(); $src.Dispose()
Write-Host ("saved: " + $outPath)
