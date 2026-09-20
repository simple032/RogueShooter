# JianHai pixel-art placeholder generator (P1 upgrade of P0 flat blocks)
# Contract: Assets/Art/JianHai/, PPU=32, Point filter, hard edges, jh_* naming.
# Overwrites existing P0 PNGs in place (their .meta pins PPU/pivot - keep metas).
# New PNGs get importer settings fixed by JianHaiDungeonMapBuilder in-editor.
Add-Type -AssemblyName System.Drawing

$TilesDir = "D:\Game Provide\RogueShooter\Assets\Art\JianHai\Tiles"
$PropsDir = "D:\Game Provide\RogueShooter\Assets\Art\JianHai\Props"
$PreviewPath = "D:\Game Provide\RogueShooter\Temp\jianhai_px_preview.png"

# ---------- deterministic RNG (LCG) ----------
$script:rstate = 1
function New-Seed([int]$s) { $script:rstate = $s }
function Rnd([int]$max) {
    $script:rstate = ([int64]$script:rstate * 1103515245 + 12345) % 2147483648
    if ($script:rstate -lt 0) { $script:rstate += 2147483648 }
    return [int](($script:rstate -shr 16) % $max)
}

# ---------- canvas ----------
function New-Canvas([int]$w, [int]$h) {
    return New-Object System.Drawing.Bitmap $w, $h, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
}
function SetPx($bmp, [int]$x, [int]$y, [int64]$argb) {
    if ($x -lt 0 -or $y -lt 0 -or $x -ge $bmp.Width -or $y -ge $bmp.Height) { return }
    if ($argb -lt 0x1000000) { $argb = $argb -bor 0xFF000000 }  # bare 0xRRGGBB -> opaque
    $bmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(
        [int](($argb -shr 24) -band 255), [int](($argb -shr 16) -band 255), [int](($argb -shr 8) -band 255), [int]($argb -band 255)))
}
function ClearPx($bmp, [int]$x, [int]$y) {
    $bmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(0, 0, 0, 0))
}
function FillPx($bmp, [int]$x0, [int]$y0, [int]$x1, [int]$y1, [int]$argb) {
    for ($y = $y0; $y -le $y1; $y++) { for ($x = $x0; $x -le $x1; $x++) { SetPx $bmp $x $y $argb } }
}
function FillRectOutline($bmp, [int]$x0, [int]$y0, [int]$x1, [int]$y1, [int]$argb) {
    for ($x = $x0; $x -le $x1; $x++) { SetPx $bmp $x $y0 $argb; SetPx $bmp $x $y1 $argb }
    for ($y = $y0; $y -le $y1; $y++) { SetPx $bmp $x0 $y $argb; SetPx $bmp $x1 $y $argb }
}

# ---------- color helpers (0xRRGGBB ints) ----------
function Cl([int]$v) { if ($v -lt 0) { 0 } elseif ($v -gt 255) { 255 } else { $v } }
function Jit([int]$c, [int]$amp) {
    $d = (Rnd (2 * $amp + 1)) - $amp
    $r = Cl ((($c -shr 16) -band 255) + $d); $g = Cl ((($c -shr 8) -band 255) + $d); $b = Cl (($c -band 255) + $d)
    return (0xFF000000 -bor ($r -shl 16) -bor ($g -shl 8) -bor $b)
}
function A([int]$c, [int]$alpha) { return (($alpha -shl 24) -bor ($c -band 0xFFFFFF)) }
function Sh([int]$c, [int]$t) { # darken toward black by t/255
    $r = ((($c -shr 16) -band 255) * (255 - $t) / 255); $g = ((($c -shr 8) -band 255) * (255 - $t) / 255); $b = (($c -band 255) * (255 - $t) / 255)
    return (0xFF000000 -bor ([int]$r -shl 16) -bor ([int]$g -shl 8) -bor [int]$b)
}
function Br([int]$c, [int]$t) { # brighten toward white by t/255
    $r = ((($c -shr 16) -band 255) + (255 - (($c -shr 16) -band 255)) * $t / 255)
    $g = ((($c -shr 8) -band 255) + (255 - (($c -shr 8) -band 255)) * $t / 255)
    $b = (($c -band 255) + (255 - ($c -band 255)) * $t / 255)
    return (0xFF000000 -bor ([int]$r -shl 16) -bor ([int]$g -shl 8) -bor [int]$b)
}

# ---------- shared palettes ----------
$P = @{
    floorO_base = 0x5c544a; floorO_mortar = 0x4b443c; floorO_dark = 0x514a42; floorO_light = 0x6b6257
    floorM_base = 0x3c3a46; floorM_mortar = 0x2e2c38; floorM_dark = 0x34323e; floorM_light = 0x474550
    floorI_base = 0x2b2734; floorI_mortar = 0x1f1c28; floorI_dark = 0x232029; floorI_light = 0x363240
    floorN_base = 0x4a4550; floorN_mortar = 0x3c3843; floorN_dark = 0x413d48; floorN_light = 0x575260
    spawn_base = 0x635a4f; spawn_mortar = 0x524a40; spawn_dark = 0x564e44; spawn_light = 0x706658
    hub_base = 0x51504a; hub_mortar = 0x43403a; hub_dark = 0x474440; hub_light = 0x5d5a54
    dead_base = 0x1a1722; dead_mortar = 0x110f1a; dead_dark = 0x141220; dead_light = 0x232030
    gold = 0x9a7a3a; gold_hi = 0xc8a860; gold_deep = 0x7a5f2e
    boss_base = 0x3d2f36; boss_mortar = 0x2e2329; boss_dark = 0x352830; boss_light = 0x4a3a42
    boss_rune = 0x8a3644; boss_rune_hi = 0xc05a66
    merch_base = 0x5a5044; merch_mortar = 0x4a4136; merch_dark = 0x4e453a; merch_light = 0x6b5f4f
    faceO = 0x777063; faceO_hi = 0x8b8276; faceO_mortar = 0x585044
    faceM = 0x5b5550; faceM_hi = 0x6d6660; faceM_mortar = 0x423d38
    faceI = 0x484240; faceI_hi = 0x59534e; faceI_mortar = 0x322e2a
    topO = 0x3a3540; topO_ghost = 0x322d38; topO_speck = 0x43404a
    topM = 0x222028; topM_ghost = 0x1c1a21; topM_speck = 0x2a2630
    topI = 0x1b1820; topI_ghost = 0x16131b; topI_speck = 0x232028
    bone = 0xd8cfae; bone_sh = 0xa89a78; bone_line = 0x6a5f45
    wood = 0x6a5232; wood_hi = 0x7a6240; wood_dark = 0x3a2a14
    iron = 0x4a4a52; iron_hi = 0x5c5c66; iron_dark = 0x33333b
    stone = 0x55504c; stone_hi = 0x6a6560; stone_dark = 0x3c3833
    outline = 0x25252b; shadow = 0x1c1820
    ember = 0xe87830; flame_mid = 0xf0a040; flame_core = 0xf8e070
    smallChest = 0xb08a2e; smallChest_hi = 0xd4a94a; smallChest_band = 0x8a6a1e; smallChest_out = 0x4a3810
    smallChest_lid = 0xc9a244; smallChest_lock = 0x6a5218; keyhole = 0x241a08
    bigChest = 0x8a5f22; bigChest_hi = 0xb08434; bigChest_band = 0x5f3f14; bigChest_out = 0x3a2610
    bigChest_lid = 0xa87c2e; bigChest_plate = 0x6a4a1a
    altar_mono = 0x6a5a3a; altar_mono_hi = 0x827048; altar_mono_sh = 0x554828; altar_out = 0x3a3020
    altar_stone = 0x4a4438; altar_stone_hi = 0x5c554a;     altar_rune_off = 0x7a6a42; altar_rune_on = 0xf0cc70
    bowl = 0x3f382c; bowl_rim = 0x565040; bowl_in = 0x2a2418
    awnA = 0x5a8f7b; awnB = 0x48796a; awn_out = 0x2e5248
    counter = 0x7a6a4a; counter_top = 0x93805a; counter_out = 0x3a3020
    lampC = 0xe8b050; lamp_glow = 0xf0d080; lamp_out = 0x8a5f20
    hood = 0x2e2a31; hood_out = 0x18151a; hood_eye = 0xd4a05a
    gem = 0x8a3030; gem_hi = 0xc05050
    potion = 0xb04040
}

# ============================================================
# 32x32 stone floor painter (slab grid 16px, mortar, speckle)
# ============================================================
function Paint-Floor([int]$base, [int]$mortar, [int]$dark, [int]$light, [int]$seed) {
    New-Seed $seed
    $bmp = New-Canvas 32 32
    $jit = @(0,0,0,0)
    for ($q = 0; $q -lt 4; $q++) { $jit[$q] = (Rnd 11) - 5 }
    for ($y = 0; $y -lt 32; $y++) {
        for ($x = 0; $x -lt 32; $x++) {
            $c = Jit $base $jit[[int](($y -shr 4) * 2 + ($x -shr 4))]
            SetPx $bmp $x $y $c
        }
    }
    # speckle dust
    for ($i = 0; $i -lt 26; $i++) {
        $x = Rnd 32; $y = Rnd 32
        $c = $dark; if ((Rnd 3) -eq 0) { $c = $light }
        SetPx $bmp $x $y (Jit $c 3)
    }
    # mortar slab grid
    for ($y = 0; $y -lt 32; $y++) { SetPx $bmp 0 $y (Jit $mortar 3); SetPx $bmp 16 $y (Jit $mortar 3); SetPx $bmp 31 $y (Jit $mortar 2) }
    for ($x = 0; $x -lt 32; $x++) { SetPx $bmp $x 0 (Jit $mortar 3); SetPx $bmp $x 16 (Jit $mortar 3); SetPx $bmp $x 31 (Jit $mortar 2) }
    # intersection nicks
    SetPx $bmp 1 1 $dark; SetPx $bmp 17 1 $dark; SetPx $bmp 1 17 $dark; SetPx $bmp 17 17 $dark
    # small cracks
    for ($i = 0; $i -lt 3; $i++) {
        $x = 3 + (Rnd 24); $y = 3 + (Rnd 24)
        SetPx $bmp $x $y $dark; SetPx $bmp ($x + 1) ($y + 1) (Sh $dark 20); SetPx $bmp ($x + 2) ($y + 1) (Sh $dark 40)
    }
    return $bmp
}

function Paint-FloorSpawn {
    $bmp = Paint-Floor $P.spawn_base $P.spawn_mortar $P.spawn_dark $P.spawn_light 901
    # faint inlaid triangle mark at center (three-way choice hint)
    $g = $P.gold_deep
    for ($i = 0; $i -le 4; $i++) { SetPx $bmp (16 - $i) (14 + [int]($i / 2)) $g; SetPx $bmp (15 + $i) (14 + [int]($i / 2)) $g }
    return $bmp
}

function Paint-FloorHub {
    $bmp = Paint-Floor $P.hub_base $P.hub_mortar $P.hub_dark $P.hub_light 902
    # bone-dust ring in center (room marker, readable but not loud)
    $g = $P.bone_sh; $h = $P.bone
    SetPx $bmp 13 16 $g; SetPx $bmp 18 16 $g; SetPx $bmp 12 17 $g; SetPx $bmp 19 17 $g; SetPx $bmp 12 18 $g; SetPx $bmp 19 18 $g
    SetPx $bmp 13 19 $g; SetPx $bmp 18 19 $g; SetPx $bmp 14 20 $g; SetPx $bmp 17 20 $g; SetPx $bmp 15 21 $h; SetPx $bmp 16 21 $h
    SetPx $bmp 15 20 $h; SetPx $bmp 16 20 $h
    return $bmp
}

function Paint-FloorDeadEnd {
    $bmp = Paint-Floor $P.dead_base $P.dead_mortar $P.dead_dark $P.dead_light 903
    # rubble chunks
    for ($i = 0; $i -lt 6; $i++) {
        $x = 2 + (Rnd 26); $y = 2 + (Rnd 26)
        SetPx $bmp $x $y $P.dead_light; SetPx $bmp ($x + 1) $y (Sh $P.dead_light 30); SetPx $bmp $x ($y + 1) (Sh $P.dead_light 50)
    }
    return $bmp
}

function Paint-FloorAltar {
    $bmp = Paint-Floor $P.floorM_base $P.floorM_mortar $P.floorM_dark $P.floorM_light 904
    # dark-gold corner brackets (frame reads across tiled plazas)
    $g = $P.gold; $h = $P.gold_hi
    # TL
    for ($i = 2; $i -le 6; $i++) { SetPx $bmp $i 2 $g }; SetPx $bmp 7 2 (Br $g 30)
    for ($i = 2; $i -le 5; $i++) { SetPx $bmp 2 $i $g }; SetPx $bmp 2 6 (Br $g 30)
    SetPx $bmp 2 2 $h
    # TR
    for ($i = 25; $i -le 29; $i++) { SetPx $bmp $i 2 $g }; SetPx $bmp 24 2 (Br $g 30)
    for ($i = 2; $i -le 5; $i++) { SetPx $bmp 29 $i $g }; SetPx $bmp 29 6 (Br $g 30)
    SetPx $bmp 29 2 $h
    # BL
    for ($i = 2; $i -le 6; $i++) { SetPx $bmp $i 29 $g }; SetPx $bmp 7 29 (Br $g 30)
    for ($i = 26; $i -le 29; $i++) { SetPx $bmp 2 $i $g }; SetPx $bmp 2 25 (Br $g 30)
    # BR
    for ($i = 25; $i -le 29; $i++) { SetPx $bmp $i 29 $g }; SetPx $bmp 24 29 (Br $g 30)
    for ($i = 26; $i -le 29; $i++) { SetPx $bmp 29 $i $g }; SetPx $bmp 29 25 (Br $g 30)
    return $bmp
}

function Paint-FloorBoss([bool]$rune) {
    New-Seed 905
    $bmp = Paint-Floor $P.boss_base $P.boss_mortar $P.boss_dark $P.boss_light 905
    # dark red accents along mortar
    for ($y = 0; $y -lt 32; $y++) { if ((Rnd 4) -eq 0) { SetPx $bmp 0 $y (Jit $P.boss_rune 8) } }
    for ($x = 0; $x -lt 32; $x++) { if ((Rnd 4) -eq 0) { SetPx $bmp $x 0 (Jit $P.boss_rune 8) } }
    if ($rune) {
        # down-pointing arrow glyph (箭骸 motif)
        $r = $P.boss_rune; $rh = $P.boss_rune_hi
        FillPx $bmp 14 8 17 14 $r          # shaft (thick)
        for ($i = 0; $i -lt 5; $i++) {
            FillPx $bmp (14 - $i) (15 + $i) (17 - $i) (15 + $i) $r   # head rows (V)
            FillPx $bmp (14 + $i) (15 + $i) (17 + $i) (15 + $i) $r
        }
        FillPx $bmp 15 8 16 8 $rh
        SetPx $bmp 13 19 $rh; SetPx $bmp 18 19 $rh
        SetPx $bmp 12 20 $r; SetPx $bmp 19 20 $r; SetPx $bmp 12 21 $r; SetPx $bmp 19 21 $r
    }
    return $bmp
}

function Paint-FloorMerchant {
    # warm wooden plank floor (前厅安全岛感，与石质走廊强区分)
    New-Seed 906
    $bmp = New-Canvas 32 32
    $wood = 0x5a4c38; $woodHi = 0x6e5c44; $groove = 0x403424; $hi = 0x7a6650
    for ($y = 0; $y -lt 32; $y++) {
        for ($x = 0; $x -lt 32; $x++) { SetPx $bmp $x $y (Jit $wood 5) }
    }
    for ($b = 0; $b -lt 4; $b++) {
        $y0 = $b * 8
        for ($x = 0; $x -lt 32; $x++) {
            SetPx $bmp $x $y0 (Jit $hi 3)             # plank top catchlight
            SetPx $bmp $x ($y0 + 7) (Jit $groove 3)    # groove
        }
        $jx = 8 + ($b % 2) * 16                        # staggered vertical joint
        for ($y = $y0; $y -lt ($y0 + 7); $y++) { SetPx $bmp $jx $y (Jit $groove 6) }
        SetPx $bmp ($jx + 1) ($y0 + 2) $P.gold         # gold nails
        SetPx $bmp ($jx + 1) ($y0 + 4) $P.gold
    }
    for ($i = 0; $i -lt 14; $i++) { SetPx $bmp (Rnd 32) (Rnd 32) (Jit $woodHi 4) }
    return $bmp
}

# ============================================================
# 32x32 wall painters
# ============================================================
function Paint-WallFace([int]$base, [int]$hi, [int]$mortar, [int]$seed) {
    New-Seed $seed
    $bmp = New-Canvas 32 32
    $sh = (Sh $base 70)
    for ($y = 0; $y -lt 32; $y++) {
        $row = [int]($y -shr 3)          # 4 brick courses of 8px
        $off = if ($row % 2 -eq 0) { 0 } else { 8 }
        for ($x = 0; $x -lt 32; $x++) {
            $bx = ($x + $off) % 16      # position inside brick
            $c = Jit $base 5
            if ($bx -eq 15 -or $y % 8 -eq 7) { $c = Jit $mortar 3 }   # right/bottom mortar
            elseif ($bx -eq 0 -or $y % 8 -eq 0) { $c = Jit (Br $base 18) 3 }  # top/left edge catch light
            if ($bx -eq 1 -and $y % 8 -eq 1) { $c = $hi }             # corner highlight
            SetPx $bmp $x $y $c
        }
    }
    # course shadow + top/bottom separation
    for ($x = 0; $x -lt 32; $x++) { SetPx $bmp $x 0 (Jit (Sh $base 90) 4); SetPx $bmp $x 31 (Jit $sh 4) }
    return $bmp
}

function Paint-WallTop([int]$base, [int]$ghost, [int]$speck, [int]$seed) {
    New-Seed $seed
    $bmp = New-Canvas 32 32
    for ($y = 0; $y -lt 32; $y++) {
        for ($x = 0; $x -lt 32; $x++) { SetPx $bmp $x $y (Jit $base 3) }
    }
    # faint ghost bricks
    for ($y = 0; $y -lt 32; $y++) { SetPx $bmp 0 $y (Jit $ghost 2); SetPx $bmp 16 $y (Jit $ghost 2) }
    for ($x = 0; $x -lt 32; $x++) { SetPx $bmp $x 15 (Jit $ghost 2); SetPx $bmp $x 31 (Jit $ghost 2) }
    # sparse rubble speckle
    for ($i = 0; $i -lt 8; $i++) { SetPx $bmp (Rnd 32) (Rnd 32) (Jit $speck 3) }
    return $bmp
}

# ============================================================
# 32x32 decor overlays (transparent bg)
# ============================================================
function Paint-DecoBones {
    $bmp = New-Canvas 32 32
    $b = $P.bone; $s = $P.bone_sh; $l = $P.bone_line
    # crossed bone 1 (diagonal, knobs at ends)
    for ($i = 0; $i -lt 9; $i++) { SetPx $bmp (8 + $i) (22 - $i) $b; SetPx $bmp (8 + $i) (23 - $i) $s }
    SetPx $bmp 6 24 $b; SetPx $bmp 7 23 $b; SetPx $bmp 7 25 $s; SetPx $bmp 5 23 $s
    SetPx $bmp 17 14 $b; SetPx $bmp 18 13 $b; SetPx $bmp 18 15 $s; SetPx $bmp 17 12 $s
    # bone 2 horizontal
    for ($i = 0; $i -lt 7; $i++) { SetPx $bmp (18 + $i) 24 $b; SetPx $bmp (18 + $i) 25 $s }
    SetPx $bmp 16 23 $b; SetPx $bmp 16 24 $b; SetPx $bmp 16 25 $s
    SetPx $bmp 25 23 $b; SetPx $bmp 25 24 $b; SetPx $bmp 26 25 $s
    # tiny rib fragments
    SetPx $bmp 11 27 $l; SetPx $bmp 12 27 $l; SetPx $bmp 22 20 $l; SetPx $bmp 23 21 $l
    return $bmp
}

function Paint-DecoArrow {
    $bmp = New-Canvas 32 32
    $shaft = 0x8a7a5a; $head = 0xc0b088; $fl = 0x6a5a3a; $hole = 0x211d28
    # arrow stuck into ground, tilted
    for ($i = 0; $i -lt 11; $i++) { SetPx $bmp (9 + $i) (21 - $i) $shaft; SetPx $bmp (10 + $i) (21 - $i) (Sh $shaft 40) }
    # head (tip down in ground)
    SetPx $bmp 7 23 $hole; SetPx $bmp 8 22 $hole; SetPx $bmp 8 23 $head
    # fletching
    SetPx $bmp 20 10 $fl; SetPx $bmp 21 9 $fl; SetPx $bmp 19 9 $fl; SetPx $bmp 21 8 $fl; SetPx $bmp 20 11 $fl; SetPx $bmp 22 10 $fl
    # shadow nick
    SetPx $bmp 6 24 $hole; SetPx $bmp 9 24 $hole
    return $bmp
}

function Paint-DecoCrack {
    $bmp = New-Canvas 32 32
    $d = 0x1e1a22; $e = 0x312c38
    $pts = @(@(5, 26), @(8, 23), @(11, 22), @(14, 18), @(17, 17), @(20, 13), @(23, 11), @(26, 7))
    for ($i = 0; $i -lt $pts.Count; $i++) {
        SetPx $bmp $pts[$i][0] $pts[$i][1] $d
        if ($i -lt $pts.Count - 1) {
            $dx = [int](($pts[$i + 1][0] - $pts[$i][0]) / 2); $dy = [int](($pts[$i + 1][1] - $pts[$i][1]) / 2)
            SetPx $bmp ($pts[$i][0] + $dx) ($pts[$i][1] + $dy) $d
        }
    }
    # branch
    SetPx $bmp 14 19 $d; SetPx $bmp 15 20 $d; SetPx $bmp 16 22 $d
    # edge highlight
    SetPx $bmp 6 25 $e; SetPx $bmp 12 21 $e; SetPx $bmp 18 16 $e; SetPx $bmp 24 10 $e
    return $bmp
}

function Paint-DecoSkull {
    $bmp = New-Canvas 32 32
    $b = $P.bone; $s = $P.bone_sh; $e = 0x241d12
    # small skull 8x7 centered
    FillPx $bmp 12 8 19 14 $b
    FillPx $bmp 13 7 18 7 $b
    FillPx $bmp 13 15 18 15 $s
    # eye sockets
    FillPx $bmp 14 10 15 11 $e; FillPx $bmp 17 10 18 11 $e
    # nose
    SetPx $bmp 16 12 $e
    # jaw teeth line
    for ($x = 13; $x -le 18; $x++) { if ($x % 2 -eq 1) { SetPx $bmp $x 14 $e } }
    # shadow under
    for ($x = 12; $x -le 19; $x++) { SetPx $bmp $x 16 (A $P.shadow 120) }
    # chips
    SetPx $bmp 11 13 $s; SetPx $bmp 20 12 $s; SetPx $bmp 21 18 $s
    return $bmp
}

# ============================================================
# 32x32 route inlay arrows (岔口路线箭头, carved-stone look)
# ============================================================
function Paint-DecoRoute([int]$dir) {   # 0=up 1=left 2=right
    $bmp = New-Canvas 32 32
    $c = 0x6a6358; $h = 0x8a8178; $sd = 0x45403b
    $pts = @(
        @(13, 13, $c), @(14, 12, $c), @(18, 12, $c), @(19, 13, $c),
        @(15, 11, $c), @(17, 11, $c), @(16, 10, $h),
        @(16, 14, $h), @(16, 15, $c), @(16, 16, $c), @(16, 17, $sd),
        @(12, 13, $sd), @(20, 13, $sd), @(13, 17, $sd), @(19, 17, $sd)
    )
    foreach ($p in $pts) {
        $x = $p[0]; $y = $p[1]; $cc = $p[2]
        switch ($dir) {
            1 { $nx = $y; $ny = 31 - $x; $nx2 = $y + 1; $ny2 = 31 - $x }   # left
            2 { $nx = 31 - $y; $ny = $x; $nx2 = 31 - ($y + 1); $ny2 = $x } # right
            default { $nx = $x; $ny = $y; $nx2 = $x; $ny2 = $y + 1 }       # up
        }
        SetPx $bmp $nx $ny $cc
        SetPx $bmp $nx2 $ny2 $cc
    }
    return $bmp
}

# ============================================================
# 32x32 wall ornaments (骨饰: 嵌墙骷髅 / 插墙箭矢)
# ============================================================
function Paint-DecoWallSkull {
    $bmp = New-Canvas 32 32
    $b = $P.bone; $s = $P.bone_sh; $e = 0x241d12; $crack = 0x3a352f
    # recessed plate
    FillPx $bmp 8 8 23 22 0x494440
    FillRectOutline $bmp 8 8 23 22 0x36322e
    SetPx $bmp 9 9 0x565150; SetPx $bmp 22 9 0x565150
    # skull 10x9 centered on plate
    FillPx $bmp 11 10 20 18 $b
    FillPx $bmp 12 9 19 9 $b
    FillPx $bmp 12 19 19 19 $s
    FillPx $bmp 13 21 18 21 $s
    # sockets
    FillPx $bmp 13 12 15 14 $e; FillPx $bmp 17 12 19 14 $e
    SetPx $bmp 15 13 0x100c06; SetPx $bmp 17 13 0x100c06
    SetPx $bmp 16 15 $e
    # jaw teeth
    for ($x = 12; $x -le 19; $x++) { if ($x % 2 -eq 0) { SetPx $bmp $x 18 $e } }
    # cracks radiating from plate
    SetPx $bmp 6 10 $crack; SetPx $bmp 5 11 $crack; SetPx $bmp 25 16 $crack; SetPx $bmp 26 17 $crack
    SetPx $bmp 12 6 $crack; SetPx $bmp 13 5 $crack; SetPx $bmp 20 24 $crack; SetPx $bmp 21 25 $crack
    SetPx $bmp 7 22 $crack; SetPx $bmp 6 23 $crack
    return $bmp
}

function Paint-DecoWallArrow {
    $bmp = New-Canvas 32 32
    $shaft = 0x8a7a5a; $head = 0xc0b088; $fl = 0x6a5a3a; $hole = 0x1e1a20; $dust = 0x55504a
    # arrow 1 stuck diagonally (entry at lower-left of wall)
    for ($i = 0; $i -lt 13; $i++) { SetPx $bmp (6 + $i) (24 - $i) $shaft; SetPx $bmp (7 + $i) (24 - $i) (Sh $shaft 45) }
    SetPx $bmp 5 26 $hole; SetPx $bmp 6 25 $hole; SetPx $bmp 7 26 $hole; SetPx $bmp 5 24 $hole
    SetPx $bmp 4 27 $dust; SetPx $bmp 8 27 $dust
    # fletching at upper end
    SetPx $bmp 19 11 $fl; SetPx $bmp 20 10 $fl; SetPx $bmp 18 10 $fl
    SetPx $bmp 20 9 $fl; SetPx $bmp 19 13 $fl; SetPx $bmp 21 11 $fl
    SetPx $bmp 17 12 $head; SetPx $bmp 18 11 $head
    # arrow 2 shorter, opposite side
    for ($i = 0; $i -lt 9; $i++) { SetPx $bmp (22 - $i) (6 + $i) $shaft }
    SetPx $bmp 23 5 $hole; SetPx $bmp 24 4 $hole; SetPx $bmp 22 4 $hole
    SetPx $bmp 21 4 $fl; SetPx $bmp 25 6 $fl; SetPx $bmp 20 6 $fl
    # dust specks
    SetPx $bmp 12 28 $dust; SetPx $bmp 13 28 $dust; SetPx $bmp 26 20 $dust
    return $bmp
}

# ============================================================
# 32x64 wall sprite (face bottom / cap top, bottom anchor)
# ============================================================
function Paint-WallStoneS {
    $face = Paint-WallFace $P.faceO $P.faceO_hi $P.faceO_mortar 21
    $top = Paint-WallTop $P.topO $P.topO_ghost $P.topO_speck 22
    $bmp = New-Canvas 32 64
    for ($y = 0; $y -lt 32; $y++) { for ($x = 0; $x -lt 32; $x++) { $bmp.SetPixel($x, $y, $top.GetPixel($x, $y)) } }
    for ($y = 0; $y -lt 32; $y++) { for ($x = 0; $x -lt 32; $x++) { $bmp.SetPixel($x, (32 + $y), $face.GetPixel($x, $y)) } }
    # separation line between cap and face
    for ($x = 0; $x -lt 32; $x++) { SetPx $bmp $x 31 (Jit (Sh $P.faceO 90) 4) }
    $face.Dispose(); $top.Dispose()
    return $bmp
}

# ============================================================
# props: small chest 64x64
# ============================================================
function Paint-ChestSmall([bool]$open) {
    $bmp = New-Canvas 64 64
    $body = $P.smallChest; $hi = $P.smallChest_hi; $band = $P.smallChest_band; $out = $P.smallChest_out
    $lid = $P.smallChest_lid; $lock = $P.smallChest_lock; $kh = $P.keyhole
    # contact shadow
    FillPx $bmp 12 55 51 58 (A $P.shadow 110)
    FillPx $bmp 14 59 49 59 (A $P.shadow 70)
    if ($open) {
        # open lid leaning behind
        FillPx $bmp 10 8 53 22 $band
        FillRectOutline $bmp 10 8 53 22 $out
        FillPx $bmp 12 10 51 12 $lid
        # interior box
        FillPx $bmp 10 23 53 44 $body
        FillRectOutline $bmp 10 23 53 44 $out
        FillPx $bmp 12 25 51 42 (Sh $out 30)
        # gold heap + sparkles
        for ($x = 14; $x -le 49; $x++) {
            $t = [Math]::Abs($x - 31.5) / 17.5
            $top = [int](25 + 4 * $t * $t)
            for ($y = $top; $y -le 42; $y++) { SetPx $bmp $x $y 0xf0d070 }
        }
        SetPx $bmp 20 28 0xf8e898; SetPx $bmp 36 30 0xf8e898; SetPx $bmp 44 33 0xf8e898; SetPx $bmp 26 36 0xf8e898
        # bands continue down
        foreach ($bx in @(22, 38)) {
            FillPx $bmp $bx 44 ($bx + 3) 54 $band
            FillRectOutline $bmp $bx 44 ($bx + 3) 54 $out
        }
        FillPx $bmp 10 45 53 54 $body
        FillRectOutline $bmp 10 45 53 54 $out
    } else {
        # closed: lid on top of body
        # body
        FillPx $bmp 10 30 53 56 $body
        FillRectOutline $bmp 10 30 53 56 $out
        # rounded corners (capsule silhouette)
        ClearPx $bmp 10 30; ClearPx $bmp 53 30; ClearPx $bmp 10 56; ClearPx $bmp 53 56
        SetPx $bmp 11 29 $out; SetPx $bmp 52 29 $out; SetPx $bmp 11 57 $out; SetPx $bmp 52 57 $out
        # lid
        FillPx $bmp 10 18 53 33 $lid
        FillRectOutline $bmp 10 18 53 33 $out
        SetPx $bmp 10 18 (A 0 0); SetPx $bmp 53 18 (A 0 0)
        SetPx $bmp 11 17 $out; SetPx $bmp 52 17 $out
        FillPx $bmp 12 20 51 21 $hi      # lid highlight
        FillPx $bmp 12 32 51 33 $band    # seam
        # metal bands
        foreach ($bx in @(21, 39)) {
            FillPx $bmp $bx 18 ($bx + 4) 56 $band
            FillRectOutline $bmp $bx 18 ($bx + 4) 56 $out
            SetPx $bmp ($bx + 1) 26 $hi; SetPx $bmp ($bx + 1) 48 $hi
        }
        # lock plate
        FillPx $bmp 26 26 37 40 $lock
        FillRectOutline $bmp 26 26 37 40 $out
        SetPx $bmp 27 27 $hi; SetPx $bmp 36 27 $hi
        FillPx $bmp 30 31 33 32 $kh      # keyhole
        SetPx $bmp 31 33 $kh; SetPx $bmp 32 33 $kh
        # base shade
        FillPx $bmp 12 54 51 55 (Sh $body 30)
    }
    return $bmp
}

# ============================================================
# props: large chest 96x96
# ============================================================
function Paint-ChestLarge([bool]$open) {
    $bmp = New-Canvas 96 96
    $body = $P.bigChest; $hi = $P.bigChest_hi; $band = $P.bigChest_band; $out = $P.bigChest_out
    $lid = $P.bigChest_lid; $plate = $P.bigChest_plate
    # contact shadow
    FillPx $bmp 20 84 75 88 (A $P.shadow 110)
    FillPx $bmp 24 89 71 89 (A $P.shadow 70)
    if ($open) {
        # lid leaning behind
        FillPx $bmp 16 10 79 28 $band
        FillRectOutline $bmp 16 10 79 28 $out
        FillPx $bmp 18 12 77 16 $lid
        # interior
        FillPx $bmp 16 30 79 60 $body
        FillRectOutline $bmp 16 30 79 60 $out
        FillPx $bmp 18 32 77 58 (Sh $out 20)
        for ($x = 20; $x -le 75; $x++) {
            $t = [Math]::Abs($x - 47.5) / 27.5
            $top = [int](32 + 6 * $t * $t)
            for ($y = $top; $y -le 58; $y++) { SetPx $bmp $x $y 0xe8c060 }
        }
        SetPx $bmp 30 36 0xf8e898; SetPx $bmp 52 38 0xf8e898; SetPx $bmp 66 42 0xf8e898; SetPx $bmp 40 48 0xf8e898
        # lower body
        FillPx $bmp 16 61 79 82 $body
        FillRectOutline $bmp 16 61 79 82 $out
        foreach ($bx in @(30, 60)) {
            FillPx $bmp $bx 60 ($bx + 5) 82 $band
            FillRectOutline $bmp $bx 60 ($bx + 5) 82 $out
        }
        FillPx $bmp 18 79 77 81 (Sh $body 40)
    } else {
        # body
        FillPx $bmp 16 44 79 84 $body
        FillRectOutline $bmp 16 44 79 84 $out
        ClearPx $bmp 16 44; ClearPx $bmp 79 44; ClearPx $bmp 16 84; ClearPx $bmp 79 84
        SetPx $bmp 17 43 $out; SetPx $bmp 78 43 $out; SetPx $bmp 17 85 $out; SetPx $bmp 78 85 $out
        # lid
        FillPx $bmp 16 28 79 48 $lid
        FillRectOutline $bmp 16 28 79 48 $out
        SetPx $bmp 16 28 (A 0 0); SetPx $bmp 79 28 (A 0 0)
        SetPx $bmp 17 27 $out; SetPx $bmp 78 27 $out
        FillPx $bmp 18 30 77 32 $hi
        FillPx $bmp 18 46 77 48 $band
        # bands
        foreach ($bx in @(28, 62)) {
            FillPx $bmp $bx 28 ($bx + 5) 84 $band
            FillRectOutline $bmp $bx 28 ($bx + 5) 84 $out
            SetPx $bmp ($bx + 1) 38 $hi; SetPx $bmp ($bx + 2) 70 $hi
        }
        # skull plate
        FillPx $bmp 37 46 58 64 $plate
        FillRectOutline $bmp 37 46 58 64 $out
        SetPx $bmp 38 47 $hi; SetPx $bmp 57 47 $hi
        # skull emblem 10x9
        FillPx $bmp 42 50 53 57 $P.bone
        FillPx $bmp 43 49 52 49 $P.bone
        FillPx $bmp 43 58 52 58 $P.bone_sh
        FillPx $bmp 44 52 46 54 0x241a08; FillPx $bmp 49 52 51 54 0x241a08
        SetPx $bmp 47 55 0x241a08
        for ($x = 43; $x -le 52; $x++) { if ($x % 2 -eq 1) { SetPx $bmp $x 57 0x241a08 } }
        # feet
        FillPx $bmp 24 82 31 86 $band; FillRectOutline $bmp 24 82 31 86 $out
        FillPx $bmp 64 82 71 86 $band; FillRectOutline $bmp 64 82 71 86 $out
        # base shade
        FillPx $bmp 18 81 77 83 (Sh $body 40)
    }
    return $bmp
}

# ============================================================
# props: altar 96x96 (idle / active)
# ============================================================
function Paint-Altar([bool]$active) {
    $bmp = New-Canvas 96 96
    $mono = $P.altar_mono; $monoHi = $P.altar_mono_hi; $monoSh = $P.altar_mono_sh; $out = $P.altar_out
    $st = $P.altar_stone; $stHi = $P.altar_stone_hi
    $rune = if ($active) { $P.altar_rune_on } else { $P.altar_rune_off }
    # contact shadow
    FillPx $bmp 28 86 68 89 (A $P.shadow 110)
    # base tier
    FillPx $bmp 24 72 71 86 $st
    FillRectOutline $bmp 24 72 71 86 (Sh $st 60)
    FillPx $bmp 26 72 69 74 $stHi
    SetPx $bmp 25 76 $P.gold; SetPx $bmp 70 76 $P.gold
    SetPx $bmp 25 82 $P.gold; SetPx $bmp 70 82 $P.gold
    # mid tier
    FillPx $bmp 30 56 65 72 $st
    FillRectOutline $bmp 30 56 65 72 (Sh $st 60)
    FillPx $bmp 32 56 63 58 $stHi
    # monolith
    FillPx $bmp 38 24 57 56 $mono
    FillRectOutline $bmp 38 24 57 56 $out
    FillPx $bmp 39 25 40 55 $monoHi
    FillPx $bmp 56 25 56 55 $monoSh
    FillPx $bmp 40 24 55 26 (Br $mono 22)
    # rune strip
    FillPx $bmp 42 30 53 46 (Sh $mono 35)
    FillRectOutline $bmp 42 30 53 46 (Sh $mono 55)
    # three glyphs (eye / arrow / bone)
    # glyph 1: eye
    FillPx $bmp 45 33 49 33 $rune; SetPx $bmp 44 34 $rune; SetPx $bmp 50 34 $rune
    FillPx $bmp 45 35 49 35 $rune; SetPx $bmp 47 34 (Br $rune 25)
    # glyph 2: arrow down
    SetPx $bmp 47 38 $rune; SetPx $bmp 47 39 $rune; SetPx $bmp 47 40 $rune
    SetPx $bmp 46 40 $rune; SetPx $bmp 48 40 $rune; SetPx $bmp 45 41 $rune; SetPx $bmp 49 41 $rune
    # glyph 3: two dots + line
    SetPx $bmp 45 44 $rune; SetPx $bmp 49 44 $rune; FillPx $bmp 46 43 48 43 $rune
    if ($active) {
        SetPx $bmp 44 32 (Br $rune 20); SetPx $bmp 50 42 (Br $rune 20)
        SetPx $bmp 47 37 0xf8e898; SetPx $bmp 46 44 0xf8e898
    }
    # bowl top
    FillPx $bmp 32 18 63 26 $P.bowl
    FillRectOutline $bmp 32 18 63 26 (Sh $P.bowl 50)
    FillPx $bmp 34 18 61 19 $P.bowl_rim
    FillPx $bmp 36 21 59 23 $P.bowl_in
    if ($active) {
        # flames
        FillPx $bmp 44 14 51 20 $P.ember
        FillPx $bmp 46 12 49 16 $P.flame_mid
        FillPx $bmp 47 10 48 13 $P.flame_core
        SetPx $bmp 43 16 $P.ember; SetPx $bmp 52 15 $P.ember
        SetPx $bmp 46 8 $P.flame_mid; SetPx $bmp 49 9 $P.flame_mid
        # dithered glow
        SetPx $bmp 38 16 $P.lamp_glow; SetPx $bmp 57 14 $P.lamp_glow; SetPx $bmp 47 6 $P.lamp_glow; SetPx $bmp 42 22 $P.lamp_glow; SetPx $bmp 54 20 $P.lamp_glow
        SetPx $bmp 34 20 $P.lamp_glow; SetPx $bmp 61 22 $P.lamp_glow
    } else {
        # cold embers
        SetPx $bmp 42 21 0x7a4020; SetPx $bmp 47 22 0x7a4020; SetPx $bmp 52 21 0x7a4020; SetPx $bmp 55 22 0x6a3818
    }
    # gold trims on monolith corners
    SetPx $bmp 40 27 $P.gold_hi; SetPx $bmp 55 27 $P.gold_hi; SetPx $bmp 40 53 $P.gold; SetPx $bmp 55 53 $P.gold
    return $bmp
}

# ============================================================
# props: shop stall 96x96
# ============================================================
function Paint-Shop {
    $bmp = New-Canvas 96 96
    # contact shadow
    FillPx $bmp 16 80 80 84 (A $P.shadow 110)
    # posts
    foreach ($px in @(14, 76)) {
        FillPx $bmp $px 14 ($px + 5) 78 $P.wood
        FillRectOutline $bmp $px 14 ($px + 5) 78 $P.wood_dark
        FillPx $bmp ($px + 1) 15 ($px + 1) 77 $P.wood_hi
    }
    # awning stripes
    for ($x = 8; $x -le 87; $x++) {
        $c = if ([int](($x - 8) / 8) % 2 -eq 0) { $P.awnA } else { $P.awnB }
        for ($y = 8; $y -le 20; $y++) { SetPx $bmp $x $y $c }
    }
    FillRectOutline $bmp 8 8 87 20 $P.awn_out
    # scalloped bottom edge
    for ($x = 8; $x -le 87; $x += 4) {
        $c = if ([int](($x - 8) / 8) % 2 -eq 0) { $P.awnA } else { $P.awnB }
        SetPx $bmp $x 21 $c; SetPx $bmp ($x + 1) 21 $c; SetPx $bmp ($x + 1) 22 (Sh $c 25)
    }
    # counter
    FillPx $bmp 18 48 78 64 $P.counter
    FillRectOutline $bmp 18 48 78 64 $P.counter_out
    FillPx $bmp 19 48 77 50 $P.counter_top
    for ($x = 22; $x -le 74; $x += 8) { SetPx $bmp $x 58 $P.counter_out; SetPx $bmp $x 59 $P.counter_out }
    # goods: potion / scroll / bow
    FillPx $bmp 24 40 29 47 $P.potion
    FillRectOutline $bmp 24 40 29 47 (Sh $P.potion 60)
    FillPx $bmp 26 38 27 39 $P.bone
    SetPx $bmp 25 43 0xd87070
    FillPx $bmp 40 41 48 47 $P.bone
    FillRectOutline $bmp 40 41 48 47 $P.bone_line
    SetPx $bmp 43 43 $P.smallChest_band; SetPx $bmp 44 43 $P.smallChest_band
    for ($i = 0; $i -lt 9; $i++) { SetPx $bmp (58 + $i) (46 - [int]($i * $i / 8)) 0x8a7a5a }
    for ($i = 0; $i -lt 9; $i++) { SetPx $bmp (58 + $i) (46 - [int]($i * $i / 8) + 1) (Sh 0x8a7a5a 40) }
    SetPx $bmp 58 46 0xd8cfae; SetPx $bmp 66 36 0xd8cfae
    # hanging lamp
    FillPx $bmp 46 22 49 30 $P.lamp_out
    FillPx $bmp 47 24 48 28 $P.lampC
    SetPx $bmp 47 25 $P.lamp_glow; SetPx $bmp 48 27 $P.lamp_glow
    SetPx $bmp 44 26 $P.lamp_glow; SetPx $bmp 51 27 $P.lamp_glow; SetPx $bmp 47 32 $P.lamp_glow
    # hooded merchant silhouette behind counter
    FillPx $bmp 40 34 55 48 $P.hood
    FillPx $bmp 42 30 53 36 $P.hood
    FillRectOutline $bmp 40 34 55 48 $P.hood_out
    for ($i = 0; $i -lt 4; $i++) {
        SetPx $bmp (42 - $i) (34 + $i) $P.hood; SetPx $bmp (53 + $i) (34 + $i) $P.hood
    }
    SetPx $bmp 45 34 $P.hood_eye; SetPx $bmp 50 34 $P.hood_eye
    return $bmp
}

# ============================================================
# props: one-way hub gate 64x64 (arrow points = allowed direction)
# ============================================================
function Paint-GateHub {
    $bmp = New-Canvas 64 64
    # frame posts + lintel
    FillPx $bmp 4 4 10 60 $P.stone
    FillRectOutline $bmp 4 4 10 60 $P.outline
    FillPx $bmp 5 5 6 59 $P.stone_hi
    FillPx $bmp 54 4 60 60 $P.stone
    FillRectOutline $bmp 54 4 60 60 $P.outline
    FillPx $bmp 55 5 56 59 $P.stone_hi
    FillPx $bmp 4 4 60 10 $P.stone
    FillRectOutline $bmp 4 4 60 10 $P.outline
    FillPx $bmp 5 5 59 6 $P.stone_hi
    # bars
    foreach ($bx in @(14, 22, 30, 38, 46)) {
        FillPx $bmp $bx 10 ($bx + 3) 58 $P.iron
        FillRectOutline $bmp $bx 10 ($bx + 3) 58 $P.outline
        SetPx $bmp ($bx + 1) 11 $P.iron_hi
    }
    # horizontal brace
    FillPx $bmp 14 32 50 35 $P.iron
    FillRectOutline $bmp 14 32 50 35 $P.outline
    SetPx $bmp 15 33 $P.iron_hi
    # gold up-arrow marker (passable direction)
    $g = 0xd4a05a; $gh = 0xe8c880; $go = 0x8a6a2e
    FillPx $bmp 28 44 35 58 $g
    FillRectOutline $bmp 28 44 35 58 $go
    for ($i = 0; $i -lt 8; $i++) {
        FillPx $bmp (28 - [int]($i * 0.8)) (44 - $i) (35 + [int]($i * 0.8)) (44 - $i) $g
    }
    FillRectOutline $bmp 22 36 41 44 $go
    FillPx $bmp 29 45 34 46 $gh
    SetPx $bmp 30 40 $gh
    return $bmp
}

# ============================================================
# props: boss gate 64x64
# ============================================================
function Paint-GateBoss {
    $bmp = New-Canvas 64 64
    # frame
    FillPx $bmp 3 3 10 61 $P.stone
    FillRectOutline $bmp 3 3 10 61 $P.outline
    FillPx $bmp 4 4 5 60 $P.stone_hi
    FillPx $bmp 54 3 61 61 $P.stone
    FillRectOutline $bmp 54 3 61 61 $P.outline
    FillPx $bmp 55 4 56 60 $P.stone_hi
    FillPx $bmp 3 3 61 9 0x4a4642
    FillRectOutline $bmp 3 3 61 9 $P.outline
    # heavy bars
    foreach ($bx in @(13, 21, 29, 37, 45)) {
        FillPx $bmp $bx 9 ($bx + 4) 59 0x3a3a44
        FillRectOutline $bmp $bx 9 ($bx + 4) 59 $P.outline
        SetPx $bmp ($bx + 1) 10 0x4c4c58
    }
    # brace
    FillPx $bmp 13 34 51 38 0x3a3a44
    FillRectOutline $bmp 13 34 51 38 $P.outline
    # skull hub
    FillPx $bmp 25 22 38 36 0x4a4642
    FillRectOutline $bmp 25 22 38 36 $P.outline
    FillPx $bmp 27 24 36 31 $P.bone
    FillPx $bmp 28 23 35 23 $P.bone
    FillPx $bmp 28 32 35 32 $P.bone_sh
    FillPx $bmp 29 26 31 28 0x201810; FillPx $bmp 33 26 35 28 0x201810
    SetPx $bmp 31 29 0x201810; SetPx $bmp 32 29 0x201810
    for ($x = 28; $x -le 35; $x++) { if ($x % 2 -eq 0) { SetPx $bmp $x 31 0x201810 } }
    # red gems on lintel
    SetPx $bmp 6 6 $P.gem_hi; SetPx $bmp 7 6 $P.gem; SetPx $bmp 7 7 $P.gem
    SetPx $bmp 57 6 $P.gem_hi; SetPx $bmp 56 6 $P.gem; SetPx $bmp 56 7 $P.gem
    SetPx $bmp 31 6 $P.gem_hi; SetPx $bmp 30 6 $P.gem; SetPx $bmp 32 6 $P.gem
    return $bmp
}

# ============================================================
# props: brazier 32x32
# ============================================================
function Paint-Brazier {
    $bmp = New-Canvas 32 32
    # contact shadow
    FillPx $bmp 7 28 24 30 (A $P.shadow 110)
    # stand
    FillPx $bmp 13 22 18 26 $P.stone
    FillRectOutline $bmp 13 22 18 26 (Sh $P.stone 60)
    FillPx $bmp 10 26 21 27 $P.stone
    FillRectOutline $bmp 10 26 21 27 (Sh $P.stone 60)
    # bowl
    FillPx $bmp 7 15 24 22 $P.stone
    FillRectOutline $bmp 7 15 24 22 (Sh $P.stone 60)
    FillPx $bmp 8 14 23 15 $P.stone_hi
    FillPx $bmp 9 16 22 19 $P.stone_dark
    # embers in bowl
    SetPx $bmp 11 16 0xa05020; SetPx $bmp 16 17 0xa05020; SetPx $bmp 20 16 0xc05828
    # flames (hard-edged cluster)
    FillPx $bmp 10 10 21 15 $P.ember
    FillPx $bmp 12 7 19 12 $P.flame_mid
    FillPx $bmp 14 5 17 9 $P.flame_core
    SetPx $bmp 9 12 $P.ember; SetPx $bmp 22 11 $P.ember
    SetPx $bmp 12 5 $P.flame_mid; SetPx $bmp 20 6 $P.flame_mid
    SetPx $bmp 15 3 $P.flame_core
    return $bmp
}

# ============================================================
function FillClear($bmp, [int]$x0, [int]$y0, [int]$x1, [int]$y1) {
    for ($y = $y0; $y -le $y1; $y++) { for ($x = $x0; $x -le $x1; $x++) { ClearPx $bmp $x $y } }
}

# ============================================================
# props: stone archway 128x64 (阶段带边界里程碑, 跨满 4-tile 门洞, 中空可通行)
# ============================================================
function Paint-ArchStone {
    $bmp = New-Canvas 128 64
    $st = 0x5c5750; $hi = 0x746e64; $mort = 0x443f39; $out = 0x282520
    # pillars (span x6..121)
    foreach ($px0 in @(6, 110)) {
        FillPx $bmp $px0 6 ($px0 + 11) 62 $st
        FillRectOutline $bmp $px0 6 ($px0 + 11) 62 $out
        FillPx $bmp ($px0 + 1) 7 ($px0 + 2) 61 $hi
        for ($y = 14; $y -lt 60; $y += 8) { for ($x = $px0; $x -le ($px0 + 11); $x++) { SetPx $bmp $x $y (Jit $mort 3) } }
        SetPx $bmp ($px0 + 5) 26 $P.gold; SetPx $bmp ($px0 + 5) 42 $P.gold
        SetPx $bmp ($px0 + 6) 26 $P.gold_deep
    }
    # arch top band
    FillPx $bmp 6 6 121 20 $st
    FillRectOutline $bmp 6 6 121 20 $out
    FillPx $bmp 7 7 120 8 $hi
    # clear open passage below band (broad)
    FillClear $bmp 18 21 109 63
    for ($x = 18; $x -le 109; $x++) { for ($y = 9; $y -le 20; $y++) { ClearPx $bmp $x $y } }
    # arched inner edge: stepped curve
    for ($i = 0; $i -lt 7; $i++) {
        $x1 = 18 + $i; $x2 = 109 - $i
        $yTop = 20 - $i
        for ($y = 9; $y -le $yTop; $y++) { ClearPx $bmp $x1 $y; ClearPx $bmp $x2 $y }
        SetPx $bmp $x1 ($yTop + 1) $out; SetPx $bmp $x2 ($yTop + 1) $out
    }
    # threshold strip at passage mouth
    FillPx $bmp 24 21 103 22 0x494440
    # keystone with gold rune
    FillPx $bmp 58 4 69 10 $st
    FillRectOutline $bmp 58 4 69 10 $out
    FillPx $bmp 59 5 68 6 $hi
    SetPx $bmp 62 7 $P.gold_hi; SetPx $bmp 63 7 $P.gold; SetPx $bmp 62 8 $P.gold; SetPx $bmp 63 8 $P.gold_hi
    # base blocks
    FillPx $bmp 4 56 17 62 $st; FillRectOutline $bmp 4 56 17 62 $out
    FillPx $bmp 110 56 123 62 $st; FillRectOutline $bmp 110 56 123 62 $out
    SetPx $bmp 5 57 $hi; SetPx $bmp 111 57 $hi
    # contact shadow at feet
    FillPx $bmp 4 63 123 63 (A $P.shadow 90)
    return $bmp
}

# emit + preview
# ============================================================
$made = New-Object System.Collections.ArrayList
function Emit($bmp, [string]$dir, [string]$name) {
    $path = Join-Path $dir $name
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    [void]$made.Add(@{ Bmp = $bmp; Name = $name })
    Write-Host ("wrote " + $path)
}

# tiles
Emit (Paint-Floor $P.floorN_base $P.floorN_mortar $P.floorN_dark $P.floorN_light 101) $TilesDir "jh_tile_floor_corridor.png"
Emit (Paint-Floor $P.floorO_base $P.floorO_mortar $P.floorO_dark $P.floorO_light 102) $TilesDir "jh_tile_floor_corridor_o.png"
Emit (Paint-Floor $P.floorM_base $P.floorM_mortar $P.floorM_dark $P.floorM_light 103) $TilesDir "jh_tile_floor_corridor_m.png"
Emit (Paint-Floor $P.floorI_base $P.floorI_mortar $P.floorI_dark $P.floorI_light 104) $TilesDir "jh_tile_floor_corridor_i.png"
Emit (Paint-FloorSpawn) $TilesDir "jh_tile_floor_spawn.png"
Emit (Paint-FloorHub) $TilesDir "jh_tile_floor_hub.png"
Emit (Paint-FloorDeadEnd) $TilesDir "jh_tile_floor_deadend.png"
Emit (Paint-FloorAltar) $TilesDir "jh_tile_floor_altar.png"
Emit (Paint-FloorBoss $false) $TilesDir "jh_tile_floor_boss.png"
Emit (Paint-FloorBoss $true) $TilesDir "jh_tile_floor_boss_rune.png"
Emit (Paint-FloorMerchant) $TilesDir "jh_tile_floor_merchant.png"
Emit (Paint-WallFace $P.faceO $P.faceO_hi $P.faceO_mortar 111) $TilesDir "jh_tile_wall_face_o.png"
Emit (Paint-WallFace $P.faceM $P.faceM_hi $P.faceM_mortar 112) $TilesDir "jh_tile_wall_face_m.png"
Emit (Paint-WallFace $P.faceI $P.faceI_hi $P.faceI_mortar 113) $TilesDir "jh_tile_wall_face_i.png"
Emit (Paint-WallTop $P.topO $P.topO_ghost $P.topO_speck 121) $TilesDir "jh_tile_wall_top_o.png"
Emit (Paint-WallTop $P.topM $P.topM_ghost $P.topM_speck 122) $TilesDir "jh_tile_wall_top_m.png"
Emit (Paint-WallTop $P.topI $P.topI_ghost $P.topI_speck 123) $TilesDir "jh_tile_wall_top_i.png"
Emit (Paint-DecoBones) $TilesDir "jh_tile_deco_bones.png"
Emit (Paint-DecoArrow) $TilesDir "jh_tile_deco_arrow.png"
Emit (Paint-DecoCrack) $TilesDir "jh_tile_deco_crack.png"
Emit (Paint-DecoSkull) $TilesDir "jh_tile_deco_skull.png"
Emit (Paint-DecoRoute 0) $TilesDir "jh_tile_deco_route_up.png"
Emit (Paint-DecoRoute 1) $TilesDir "jh_tile_deco_route_left.png"
Emit (Paint-DecoRoute 2) $TilesDir "jh_tile_deco_route_right.png"
Emit (Paint-DecoWallSkull) $TilesDir "jh_tile_deco_wall_skull.png"
Emit (Paint-DecoWallArrow) $TilesDir "jh_tile_deco_wall_arrow.png"
Emit (Paint-WallStoneS) $TilesDir "jh_wall_stone_s.png"

# props
Emit (Paint-ChestSmall $false) $PropsDir "jh_prop_chest_closed.png"
Emit (Paint-ChestSmall $true) $PropsDir "jh_prop_chest_open.png"
Emit (Paint-ChestLarge $false) $PropsDir "jh_prop_chest_large_closed.png"
Emit (Paint-ChestLarge $true) $PropsDir "jh_prop_chest_large_open.png"
Emit (Paint-Altar $false) $PropsDir "jh_prop_altar_idle.png"
Emit (Paint-Altar $true) $PropsDir "jh_prop_altar_active.png"
Emit (Paint-Shop) $PropsDir "jh_prop_shop_01.png"
Emit (Paint-GateHub) $PropsDir "jh_prop_gate_hub.png"
Emit (Paint-GateBoss) $PropsDir "jh_prop_gate_boss.png"
Emit (Paint-Brazier) $PropsDir "jh_prop_brazier.png"
Emit (Paint-ArchStone) $PropsDir "jh_prop_arch_stone.png"

# preview sheet: 5 per row, x2 nearest scale, mid-gray bg (dark & light art both visible)
$cols = 5; $scale = 2; $cell = 96 * $scale + 24
$rows = [int](($made.Count + $cols - 1) / $cols)
$sheet = New-Object System.Drawing.Bitmap ($cols * $cell), ($rows * ($cell + 18))
$g = [System.Drawing.Graphics]::FromImage($sheet)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
$bg = [System.Drawing.Color]::FromArgb(255, 70, 66, 76)
$g.Clear($bg)
$font = New-Object System.Drawing.Font "Consolas", 7
$brush = [System.Drawing.Brushes]::White
for ($i = 0; $i -lt $made.Count; $i++) {
    $cx = ($i % $cols) * $cell + 12; $cy = [int]($i / $cols) * ($cell + 18) + 8
    $b = $made[$i].Bmp
    $g.DrawImage($b, $cx, $cy, $b.Width * $scale, $b.Height * $scale)
    $g.DrawString($made[$i].Name, $font, $brush, $cx, $cy + 96 * $scale + 4)
}
$g.Dispose()
New-Item -ItemType Directory -Force -Path (Split-Path $PreviewPath) | Out-Null
$sheet.Save($PreviewPath, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Host ("preview: " + $PreviewPath)
Write-Host ("total: " + $made.Count)
