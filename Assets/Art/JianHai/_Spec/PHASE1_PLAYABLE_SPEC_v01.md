# 箭骸行者 · 一阶段可玩 2D 规格 v0.1.1

**派单**：游戏项目经理  
**范围**：角色 / S1 怪 / 箭矢 / 法球 / 翻滚 —— 尺寸·锚点·命名  
**不做**：场景/地牢大套（外包）；不重做已交 P0 蓄力 FX  
**对齐**：
- 动作表：`ACTION_SPEC_P1_v01.md`（帧数/时长归角色动作）
- 风格：`STYLE_SPEC_v01.md`（PPU **32**，Point，暗骸可读）
- 制作人外包包：`D:\Game Provide\02_design\外包_一阶段可玩_美术_v01.md`（收件仍 JianHai）

---

## 0. 共用

| 项 | 值 |
|----|-----|
| 命名 | `jh_<类>_<名>_<动作>[_方向][_帧]` 全小写 |
| PPU | 32 |
| 主角/S1 画布 | **64×64**；Pivot **`(0.5, 0.15)`** |
| Sorting | Entity **20**；投射物/预警 FX **30** |
| 朝向首批 | 默认 `s`；缺向可镜像 |

### 0.1 与外包包命名映射（收件时）

| 外包包（制作人） | 内部（本规格 / 动作规格） |
|------------------|---------------------------|
| `jh_player_idle` / `jh_player_atk_*` / `jh_player_roll_*` | `jh_char_archer_idle` / `jh_char_archer_atk_*` / `jh_char_archer_roll_*` |
| `jh_enemy_<id>_<state>` | 见 §2（`e1_skel` / `dog` / `mage`） |
| `jh_proj_arrow` | `jh_proj_arrow_fly`（可双文件名同图） |
| `jh_proj_mage_orb` | `jh_proj_orb_mage_fly` |

收件核对：风格/PPU/Pivot/清单；冲突以 **内部 jh_char_ / ACTION 表** 为准，外包文件可复制改名入库。

---

## 1. 角色 `jh_char_archer`

| 资产 | 文件根 | 画布 | Pivot | Layer | 说明 |
|------|--------|------|-------|-------|------|
| Idle | `jh_char_archer_idle` | 64×64 | (0.5,0.15) | 20 | P0 已有；可作动作底图 |
| Walk | `jh_char_archer_walk_{s\|n\|e\|w}_##` | 64×64 | 同 | 20 | 帧数见 ACTION |
| Charge | `jh_char_archer_charge_##` | 64×64 | 同 | 20 | 蓄力；弦光可叠已交 FX |
| Atk | `jh_char_archer_atk_##` | 64×64 | 同 | 20 | 松手；`OnFire` @ atk_01 |
| **Roll** | `jh_char_archer_roll_##` | 64×64 | 同 | 20 | 8 帧 / ~0.40s（ACTION） |
| Hurt / Die | `jh_char_archer_hurt_##` / `_die_##` | 64×64 | 同 | 20 | |

**挂点（相对脚底，tile）**：`muzzle≈(0.15,0.55)` · `bow_string≈(0,0.50)` · `head≈(0,0.85)` · `foot=(0,0)`

**翻滚弱 FX（2D）**

| 文件 | 画布 | Pivot | 说明 |
|------|------|-------|------|
| `jh_fx_roll_afterimage` | 48×32 | (0.5,0.2) | α≤40% 骨灰剪影 |
| `jh_fx_roll_dust_##` | 32×16 | (0.5,0.1) | 脚底 2–3 帧 |

---

## 2. S1 怪（与 ACTION 同名）

| ID | 文件根 | 画布 | Pivot | 剪影要点 |
|----|--------|------|-------|----------|
| 普近战 | `jh_enemy_e1_skel_*` | 64×64 | (0.5,0.15) | 无大弓；骨刃；可复用/演进 P0 e1 |
| 狗 | `jh_enemy_dog_*` | 64×64 | (0.5,0.15) | 低姿扁长；快 |
| 邪法 | `jh_enemy_mage_*` | 64×64 | (0.5,0.15) | 瘦高持杖；cast 前摇可读 |

每怪动作槽：`idle/walk/alert/chase|cast/atk/hurt/die`（详表见 ACTION）。  
强化：同帧表 + `_enh` 色差/描边，不另做动作集。

**预警**：`jh_fx_warn_bang` 16×16 Pivot (0.5,0.0) 挂 `head`。

---

## 3. 箭矢

| 资产 | 文件名 | 画布 | Pivot | 说明 |
|------|--------|------|-------|------|
| 飞行 | `jh_proj_arrow_fly.png`（别名 `jh_proj_arrow`） | **32×8** 或 **48×16** | **箭身中后** ≈(0.2, 0.5)；朝 +X | 骨白+暖簇；程序旋转 |
| 尾迹可选 | `jh_proj_arrow_trail_##` | 同高×短宽 | (0.0,0.5) | 1–2 帧；勿糊 |
| 命中 | `jh_fx_arrow_hit_##` | 32×32 | (0.5,0.5) | 硬边火花 |

弱蓄/暴击可选变体：`_weak` / `_crit`（簇尖色差）。  
蓄力弦上 tip 仍用已交 `jh_fx_charge_arrow_tip*`（非飞行体）。

---

## 4. 法球（邪法）

| 资产 | 文件名 | 画布 | Pivot | 说明 |
|------|--------|------|-------|------|
| 飞行 | `jh_proj_orb_mage_fly.png`（别名 `jh_proj_mage_orb`） | **32×32** | (0.5,0.5) | 高对比紫核+白芯 |
| 生成闪可选 | `jh_proj_orb_mage_spawn` | 32×32 | 同 | |
| 命中/消散 | `jh_proj_mage_orb_impact_##` 或 `jh_fx_orb_fade_##` | 32×32 | 同 | 2–4 帧 |

数值（速/射程）归玩法；美术保 **色相与尺寸可读**。

---

## 5. 碰撞参考（美术备注，非碰撞资产）

交付 README 行：每图 **建议碰撞圆半径（u）** 相对 pivot —— 角色≈0.30–0.40；狗≈0.28；法师≈0.32；箭≈半宽；球≈0.20。

---

## 6. 目录

```
Assets/Art/JianHai/
  Characters/   jh_char_archer_*
  Enemies/      jh_enemy_e1_skel_*  jh_enemy_dog_*  jh_enemy_mage_*
  Projectiles/  jh_proj_arrow*  jh_proj_orb*  jh_proj_mage*
  FX/           jh_fx_roll_*  jh_fx_warn_*  jh_fx_arrow_hit_*
  _Spec/        PHASE1_PLAYABLE_SPEC_v01.md  ACTION_SPEC_P1_v01.md
```

收件根（制作人）：`D:\Game Provide\RogueShooter\Assets\Art\JianHai\`

---

## 7. 验收

- [x] 命名与 ACTION / 外包映射写清  
- [x] 画布·Pivot·PPU32 可进工程  
- [x] S1 三种 + 箭/球/滚 规格齐全  
- [x] 未新开场景大套  

**出图顺序（等项目经理下一拍再画）**：箭矢 → 法球 → warn → roll_00 占位 → dog/mage idle（skel 可沿用 P0）

---

## 8. 对接

| 谁 | 事 |
|----|-----|
| 游戏项目经理 | 本规格结单 / 下发出图 |
| 角色动作 | 帧时长权威；冲突回改本表命名 |
| 玩法程序 | 挂载与飞行朝向；路径见上 |
| 制作人外包 | 收件核对风格/命名/PPU/Pivot/清单 |

**路径**：`/workspace/jianhai-art/PHASE1_PLAYABLE_SPEC_v01.md`  
工程：`Assets/Art/JianHai/_Spec/PHASE1_PLAYABLE_SPEC_v01.md`
