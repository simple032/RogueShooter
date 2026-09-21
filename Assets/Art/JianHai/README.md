# 箭骸 · JianHai 2D import scaffold (STYLE_SPEC_v01)

Drop PNG here. **Interact GameObject names stay HOOKS IDs** (`Chest_01`, `A_Shared`, `Shop_01`). Sprites bind through art roots — replacing a file does not rewrite E-interact IDs.

## Import

| Item | Value |
|------|--------|
| Format | PNG RGBA (no premul preferred), transparent bg |
| PPU | **32** (32 px = 1 tile = 1 world unit) |
| Filter | **Point** (hard edge). No mip for sprites. |
| Naming | `jh_<cat>_<name>[_action][_dir][_frame]` lowercase + underscore |
| Engine | Unity 2D ortho; 1 tile = 1u |

## Pivots

| Kind | Canvas | Pivot |
|------|--------|-------|
| Player / E1 | 64×64 | **(0.5, 0.15)** feet |
| BOSS | 128×128 | **(0.5, 0.12)** |
| Props (chest / altar / shop) | 64×64 or 96×96 | **(0.5, 0.2)** |
| Tiles | 32×32 | **(0.5, 0.5)** center |
| Walls | 32×64 | **(0.5, 0.0)** bottom |

`.meta` on the P0 placeholders already pin PPU / Point / these pivots. Overwrite the PNG, keep the `.meta`.

## Sorting layers

`Ground` → `Decal` → `Prop` → `Entity` → `FX` → `UI`

Props (chest/altar/shop) = **Prop**. Player / E1 / BOSS = **Entity**.

## Hook → art (do not rename interact IDs)

| Hook ID | Art root | Files |
|---------|----------|--------|
| `Chest_*` | `jh_prop_chest` | `jh_prop_chest_closed.png` / `_open.png` |
| `A_*` / `A_Shared` | `jh_prop_altar` | `jh_prop_altar_idle.png` / `_active.png` |
| `Shop_01` | `jh_prop_shop_01` | `jh_prop_shop_01.png` |

Runtime: `JianHaiArtCatalog` + `JianHaiSpriteSlot` on the HOOKS-named object. Play Mode / player builds load PNGs from `Assets/Art/JianHai/` (`File.ReadAllBytes` + `Texture2D.LoadImage`, PPU 32 Point, spec pivot). Editor still prefers the imported Sprite when Unity has it. Color-block placeholders are last-resort only.

Stage-1 maze floors/walls/doors use `SpriteDrawMode.Tiled` at `localScale=1`; collision AABBs pass explicit half-extents so tiling does not inflate volumes.

## Folders

```
Characters/  Enemies/  Boss/  Props/  Tiles/  UI/  _Spec/
```

See `_Spec/STYLE_SPEC_v01.md` and `hook_map.csv`.
