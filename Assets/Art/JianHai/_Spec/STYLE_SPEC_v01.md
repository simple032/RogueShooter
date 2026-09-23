# 箭骸行者 · 2D 美术规格 v0.1（风格 + 命名）

**状态**：可开工首批 sprite  
**对标**：挺进地牢式清晰度（剪影清楚、轮廓硬、少脏噪）  
**引擎**：Unity RogueShooter · 俯视角 · 1 tile = 1 world unit（挂点锁版）

---

## 1. 风格板（文字版）

| 项 | 定稿 |
|----|------|
| 主题 | 暗色骸骨地牢；冷灰石 + 骨白 + 锈铜点缀 |
| 可读性 | 优先于写实：角色/交互点在暗底上 1 眼可辨 |
| 轮廓 | 硬边剪影；实体填色；极少软笔触 |
| 光源 | 顶光偏冷；祭坛/箱/Shop 用暖色识别灯（不改几何） |
| 色温 | 地板 `#1a1d24`–`#2a3038`；墙 `#3a424c`；骨 `#c8c0b0`；血/危 `#c4453a`；祭坛暖 `#d4a05a`；商店 `#5a8f7b`；宝箱 `#c9a227` |
| 禁止 | 低对比灰泥糊成一团；过细花纹；3D 透视贴片；依赖发光描边才看得清 |

**角色气质**
- 主角「箭骸行者」：骷髅弓手，披残甲/布条，弓轮廓夸张易读；蓄力时弓弦/箭簇可加暖色高光条（后续帧）
- E1 小怪「骸兵」：矮一档的近战骷髅，持骨刃/碎矛，剪影与主角区分（无大弓）
- BOSS「箭骸领主」：体量约 1.6–2×主角，巨弓或骨冠，P1/P2 可用色相差（P2 更红/更亮眼）

---

## 2. 技术规格（程序可直接吃）

| 项 | 值 | 备注 |
|----|-----|------|
| 格式 | PNG，RGBA，无预乘优先；透明底 | 导入 Unity 再设 Sprite |
| PPU | **32** | 32 px = 1 tile = 1u |
| 过滤 | Point（像素硬边）或 Bilinear+关 mip 试玩二选一；首版建议 **Point** |
| 压缩 | 源文件不压；工程内 Sprite 按平台再压 |
| 朝向 | 默认 **朝下/朝相机** 为 idle；四向或八向另表（首批先 1 向 idle + 可选 walk 4） |
| 碰撞参考 | 角色占位圆 ≈ 0.6–0.8u；BOSS ≈ 1.2–1.6u | 美术框对齐脚底 |

### 2.1 画布与锚点

| 资产类 | 画布 px | Pivot（归一化） | 落点 |
|--------|---------|-----------------|------|
| 主角 / E1 | 64×64 | `(0.5, 0.15)` 脚底中 | 实体中心略上 |
| BOSS | 128×128 | `(0.5, 0.12)` | 同 |
| 祭坛 / 箱 / Shop 台 | 64×64 或 96×96 | `(0.5, 0.2)` | 贴地 |
| 场景地砖 | 32×32 | `(0.5, 0.5)` | tile 中心 |
| 墙/柱切片 | 32×64 或 64×64 | `(0.5, 0.0)` 底边 | 墙脚对齐 tile |
| UI 图标 | 32×32 / 48×48 | `(0.5, 0.5)` | — |
| UI 条块 | 按控件 | 左中或左下 | 见 §4 |

### 2.2 分层（Sorting）

建议 Layer 名（程序建同名即可）：

| Order / Layer | 内容 |
|---------------|------|
| `Ground` 0 | 地砖、干道/死路/Hub 铺装差 |
| `Decal` 5 | 血迹、碎骨、路径提示 |
| `Prop` 10 | 箱、祭坛、Shop、柱 |
| `Entity` 20 | 主角、小怪、BOSS（同层靠 Y 排序） |
| `FX` 30 | 受击、蓄力、弹道 |
| `Overlay` 40 | 阴影可选、交互提示 |
| `UI` Canvas | HUD |

角色多图层文件（可选 PSD/分层 PNG）：`shadow` / `body` / `weapon` / `fx`；交付合并 sprite 亦可，但命名保留层意图。

---

## 3. 命名约定

**总规则**：`jh_<类>_<名>[_变体][_动作][_方向][_帧]`  
- 全小写 + 下划线  
- 与挂点 ID 对齐时 **保留挂点拼写**（如 `Shop_01` → 文件里用 `shop_01`）  
- 不写中文文件名

### 3.1 类前缀

| 前缀 | 用途 | 例 |
|------|------|-----|
| `jh_char_` | 主角 | `jh_char_archer_idle_00` |
| `jh_enemy_` | 小怪 | `jh_enemy_e1_skel_idle` / `jh_enemy_dog_idle_00` / `jh_enemy_mage_idle_00` |
| `jh_boss_` | BOSS | `jh_boss_lord_idle` |
| `jh_prop_` | 交互物 | `jh_prop_chest_closed` |
| `jh_tile_` | 铺装 | `jh_tile_floor_hub` |
| `jh_wall_` | 墙/柱 | `jh_wall_stone_n` |
| `jh_ui_` | UI | `jh_ui_icon_build` |
| `jh_fx_` | 特效 | `jh_fx_hit_spark_00` |

### 3.2 与挂点 ID 映射（交互点）

| 挂点 ID（关卡） | 资产名根 |
|-----------------|----------|
| `Chest_01`… | `jh_prop_chest_*`（开/关状态） |
| `A_Shared` / `A1`…`A6` | `jh_prop_altar_*`（静/激） |
| `Shop_01` | `jh_prop_shop_01_*`（台+棚） |
| `BOSS` 房标识 | `jh_prop_bossdoor_*`（可选） |
| DE 死路 | 不单独做门；用 `jh_tile_floor_deadend` 色差 |

### 3.3 动作 / 方向后缀

- 动作：`idle` `walk` `atk` `hurt` `die` `charge`（主角蓄力）  
- 方向（若有）：`n` `e` `s` `w` 或 `ne`…  
- 帧：`_00` `_01`… 两位

例：`jh_char_archer_walk_s_03.png`

---

## 4. 首批交付清单（本轮）

优先级 P0 → 先占位色块也可，但 **轮廓与色相必须符合风格板**。

### P0 角色 / 敌

| ID | 文件 | 尺寸 | 说明 |
|----|------|------|------|
| CHAR | `jh_char_archer_idle_00.png` | 64×64 | 主角 idle 首帧 |
| E1 | `jh_enemy_e1_skel_idle.png` | 64×64 | 骸兵（首类小怪） |
| BOSS | `jh_boss_lord_idle.png` | 128×128 | 箭骸领主 idle |

### P0 交互点

| ID | 文件 | 尺寸 |
|----|------|------|
| 祭坛 | `jh_prop_altar_idle.png` / `_active.png` | 64×64 |
| 宝箱 | `jh_prop_chest_closed.png` / `_open.png` | 64×64 |
| 商店 | `jh_prop_shop_01.png` | 96×96 |

### P0 场景切片

| ID | 文件 | 尺寸 | 用途 |
|----|------|------|------|
| 干道 | `jh_tile_floor_corridor.png` | 32×32 | 主廊 |
| 死路 | `jh_tile_floor_deadend.png` | 32×32 | 略暗/略脏 |
| Hub | `jh_tile_floor_hub.png` | 32×32 | 略亮石缝 |
| 墙 | `jh_wall_stone_s.png` | 32×64 | 南向墙示意一张即可扩 |

### P0 UI 占位

| ID | 文件 | 尺寸 | 用途 |
|----|------|------|------|
| Build | `jh_ui_icon_build.png` + `jh_ui_badge_build.png` | 32 / 48 | Build 数 |
| 时间段 | `jh_ui_icon_phase_t0.png`…`_t3.png` | 32×32 | T0–T3 压力可读 |
| 血量 | `jh_ui_bar_hp_fill.png` + `_back.png` | 128×16 | 血条 |

---

## 5. 交付路径（建议）

```
Assets/Art/JianHai/
  Characters/
  Enemies/
  Boss/
  Props/
  Tiles/
  UI/
  _Spec/STYLE_SPEC_v01.md
```

源文件同步：`/workspace/jianhai-art/`（本规格 + 后续 PNG）

---

## 6. 对接

- 进度/阻塞 → 游戏项目经理  
- 接入 → 玩法程序  
- 位点/铺装疑问 → 关卡  
- Slack 本轮不发  

**下一拍**：按本表出 P0 占位/正式稿 sprite（先角色+E1+祭坛/箱/Shop 色块轮廓）。
