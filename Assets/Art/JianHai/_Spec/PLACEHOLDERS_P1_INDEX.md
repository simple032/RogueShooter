# P1 占位 Sprite 索引

输出根路径：`/workspace/jianhai-art/placeholders_p1/`  
工程副本：`Assets/Art/JianHai/Characters/`（`jh_char_*`）与 `Assets/Art/JianHai/Enemies/`（`jh_enemy_*`）。

这些 PNG 是一阶段可接的硬边几何占位图（64×64、RGBA、透明底、脚底锚点约为 `(0.5, 0.15)`）。可直接接入状态机，后续可被真图按同名文件替换。

## 文件清单与动作对照

### `jh_char_archer`
- **idle**（4 帧）：`jh_char_archer_idle_00.png`, `jh_char_archer_idle_01.png`, `jh_char_archer_idle_02.png`, `jh_char_archer_idle_03.png`
- **walk_s**（6 帧）：`jh_char_archer_walk_s_00.png`, `jh_char_archer_walk_s_01.png`, `jh_char_archer_walk_s_02.png`, `jh_char_archer_walk_s_03.png`, `jh_char_archer_walk_s_04.png`, `jh_char_archer_walk_s_05.png`
- **charge**（6 帧）：`jh_char_archer_charge_00.png`, `jh_char_archer_charge_01.png`, `jh_char_archer_charge_02.png`, `jh_char_archer_charge_03.png`, `jh_char_archer_charge_04.png`, `jh_char_archer_charge_05.png`
- **atk**（4 帧）：`jh_char_archer_atk_00.png`, `jh_char_archer_atk_01.png`, `jh_char_archer_atk_02.png`, `jh_char_archer_atk_03.png`
- **roll**（8 帧）：`jh_char_archer_roll_00.png`, `jh_char_archer_roll_01.png`, `jh_char_archer_roll_02.png`, `jh_char_archer_roll_03.png`, `jh_char_archer_roll_04.png`, `jh_char_archer_roll_05.png`, `jh_char_archer_roll_06.png`, `jh_char_archer_roll_07.png`
- **hurt**（3 帧）：`jh_char_archer_hurt_00.png`, `jh_char_archer_hurt_01.png`, `jh_char_archer_hurt_02.png`
- **die**（6 帧）：`jh_char_archer_die_00.png`, `jh_char_archer_die_01.png`, `jh_char_archer_die_02.png`, `jh_char_archer_die_03.png`, `jh_char_archer_die_04.png`, `jh_char_archer_die_05.png`

### `jh_enemy_e1_skel`
- **idle**（4 帧）：`jh_enemy_e1_skel_idle_00.png`, `jh_enemy_e1_skel_idle_01.png`, `jh_enemy_e1_skel_idle_02.png`, `jh_enemy_e1_skel_idle_03.png`
- **walk**（6 帧）：`jh_enemy_e1_skel_walk_00.png`, `jh_enemy_e1_skel_walk_01.png`, `jh_enemy_e1_skel_walk_02.png`, `jh_enemy_e1_skel_walk_03.png`, `jh_enemy_e1_skel_walk_04.png`, `jh_enemy_e1_skel_walk_05.png`
- **alert**（3 帧）：`jh_enemy_e1_skel_alert_00.png`, `jh_enemy_e1_skel_alert_01.png`, `jh_enemy_e1_skel_alert_02.png`
- **chase**（6 帧）：`jh_enemy_e1_skel_chase_00.png`, `jh_enemy_e1_skel_chase_01.png`, `jh_enemy_e1_skel_chase_02.png`, `jh_enemy_e1_skel_chase_03.png`, `jh_enemy_e1_skel_chase_04.png`, `jh_enemy_e1_skel_chase_05.png`
- **atk**（8 帧）：`jh_enemy_e1_skel_atk_00.png`, `jh_enemy_e1_skel_atk_01.png`, `jh_enemy_e1_skel_atk_02.png`, `jh_enemy_e1_skel_atk_03.png`, `jh_enemy_e1_skel_atk_04.png`, `jh_enemy_e1_skel_atk_05.png`, `jh_enemy_e1_skel_atk_06.png`, `jh_enemy_e1_skel_atk_07.png`
- **hurt**（3 帧）：`jh_enemy_e1_skel_hurt_00.png`, `jh_enemy_e1_skel_hurt_01.png`, `jh_enemy_e1_skel_hurt_02.png`
- **die**（5 帧）：`jh_enemy_e1_skel_die_00.png`, `jh_enemy_e1_skel_die_01.png`, `jh_enemy_e1_skel_die_02.png`, `jh_enemy_e1_skel_die_03.png`, `jh_enemy_e1_skel_die_04.png`

### `jh_enemy_dog`
- **idle**（4 帧）：`jh_enemy_dog_idle_00.png`, `jh_enemy_dog_idle_01.png`, `jh_enemy_dog_idle_02.png`, `jh_enemy_dog_idle_03.png`
- **walk**（6 帧）：`jh_enemy_dog_walk_00.png`, `jh_enemy_dog_walk_01.png`, `jh_enemy_dog_walk_02.png`, `jh_enemy_dog_walk_03.png`, `jh_enemy_dog_walk_04.png`, `jh_enemy_dog_walk_05.png`
- **alert**（2 帧）：`jh_enemy_dog_alert_00.png`, `jh_enemy_dog_alert_01.png`
- **chase**（6 帧）：`jh_enemy_dog_chase_00.png`, `jh_enemy_dog_chase_01.png`, `jh_enemy_dog_chase_02.png`, `jh_enemy_dog_chase_03.png`, `jh_enemy_dog_chase_04.png`, `jh_enemy_dog_chase_05.png`
- **atk**（6 帧）：`jh_enemy_dog_atk_00.png`, `jh_enemy_dog_atk_01.png`, `jh_enemy_dog_atk_02.png`, `jh_enemy_dog_atk_03.png`, `jh_enemy_dog_atk_04.png`, `jh_enemy_dog_atk_05.png`
- **hurt**（2 帧）：`jh_enemy_dog_hurt_00.png`, `jh_enemy_dog_hurt_01.png`
- **die**（4 帧）：`jh_enemy_dog_die_00.png`, `jh_enemy_dog_die_01.png`, `jh_enemy_dog_die_02.png`, `jh_enemy_dog_die_03.png`

### `jh_enemy_mage`
- **idle**（4 帧）：`jh_enemy_mage_idle_00.png`, `jh_enemy_mage_idle_01.png`, `jh_enemy_mage_idle_02.png`, `jh_enemy_mage_idle_03.png`
- **walk**（6 帧）：`jh_enemy_mage_walk_00.png`, `jh_enemy_mage_walk_01.png`, `jh_enemy_mage_walk_02.png`, `jh_enemy_mage_walk_03.png`, `jh_enemy_mage_walk_04.png`, `jh_enemy_mage_walk_05.png`
- **alert**（3 帧）：`jh_enemy_mage_alert_00.png`, `jh_enemy_mage_alert_01.png`, `jh_enemy_mage_alert_02.png`
- **chase**（1 帧）：`jh_enemy_mage_chase_00.png`
- **cast**（8 帧）：`jh_enemy_mage_cast_00.png`, `jh_enemy_mage_cast_01.png`, `jh_enemy_mage_cast_02.png`, `jh_enemy_mage_cast_03.png`, `jh_enemy_mage_cast_04.png`, `jh_enemy_mage_cast_05.png`, `jh_enemy_mage_cast_06.png`, `jh_enemy_mage_cast_07.png`
- **hurt**（3 帧）：`jh_enemy_mage_hurt_00.png`, `jh_enemy_mage_hurt_01.png`, `jh_enemy_mage_hurt_02.png`
- **die**（5 帧）：`jh_enemy_mage_die_00.png`, `jh_enemy_mage_die_01.png`, `jh_enemy_mage_die_02.png`, `jh_enemy_mage_die_03.png`, `jh_enemy_mage_die_04.png`

## 剪影对照

- `jh_char_archer`：主角骷髅弓手，持大弓；`charge_04` 用暖铜峰值提示，并带略暖绿窗提示。
- `jh_enemy_e1_skel`：矮一档普怪，持短刃/碎矛，无大弓。
- `jh_enemy_dog`：低姿四足骨犬。
- `jh_enemy_mage`：瘦高邪法，持杖；`cast_04` 标示法球生成占位。

## 规格对齐

- 动作名与帧数对照 `ACTION_SPEC_P1_v01.md`；翻滚总长 0.40s，建议无敌窗 0.04–0.28s。
- 颜色为骨白 `#c8c0b0`、危红 `#c4453a`；暖铜 `#d4a05a` 仅用于蓄力峰提示。
- 当前生成文件总数：**132**。
