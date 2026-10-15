# RogueShooter · 箭骸行者

基于团结引擎开发的等距视角肉鸽动作射击游戏。玩家以弓箭蓄力、弱点射击和翻滚应对房间战斗，通过宝箱、祭坛和商店构筑单局能力，探索三阶段地牢并挑战最终 BOSS。

本仓库保存开发中的完整工程；当前默认流程为「三阶段连续关卡」。功能和数值仍在迭代，以工程内配置为准。

## 环境要求

- **团结引擎 1.6.13**，工程记录的编辑器版本为 `2022.3.61t14`（`3a70663b70e0`）。
- Windows 为现有发布脚本的目标平台；构建需要安装 Windows x64 支持模块。
- 首次打开需要联网下载 `Packages/manifest.json` 声明的包，包括团结 Codely、2D、TextMeshPro 和 UGUI 等依赖。

工程使用团结引擎的 `.scene` 场景格式，建议使用上述对应编辑器打开。

## 快速开始

```bash
git clone https://github.com/simple032/RogueShooter.git
```

1. 在团结 Hub 中添加克隆后的项目根目录，使用对应版本编辑器打开。
2. 等待包解析和资源导入完成。素材较多，首次导入需要一定时间。
3. 打开 `Assets/Scenes/StartMenu.scene` 并进入 Play 模式。
4. 点击「开始游戏」进入 `Stage1Maze`，按出生房教程熟悉移动、蓄力与弱点射击。

也可以直接打开 `Assets/Scenes/Stage1Maze.scene` 调试玩法。`RunFlow.DefaultMode` 控制默认流程，第一阶段试玩模式会在第一阶段连接区域结束。

## 基本操作

| 操作 | 输入 |
| --- | --- |
| 移动 | WASD / 方向键 |
| 瞄准 | 鼠标位置 |
| 蓄力射击 | 按住鼠标左键或 C，松开发射 |
| 翻滚 | Shift + 移动方向 |
| 近战 | F |
| 凝神窥机 | Q |
| 暂停 / 返回 | Esc |
| 重新开始 | R |

蓄力期间移动减慢；在绿色窗口内释放可触发弱点箭。交互、奖励选择和后续操作以游戏内提示为准。

## 工程内容

```text
Assets/
  Scripts/          玩家、敌人、关卡、构筑、BOSS、视觉与音频逻辑
  Scenes/           启动菜单、主玩法与开发场景
  Art/              导入后的游戏美术
  Audio/            音效、环境声和音乐
  Resources/        运行时加载的资源与图集
  StreamingAssets/  数值表与运行时数据
  Editor/           资源导入、场景工具、验证和构建脚本
  ArtSamples/       美术样例及预览场景
Art_Source/         与运行时资源目录逐文件匹配的在用美术原稿
Packages/           包依赖清单及锁定信息
ProjectSettings/    项目与编辑器配置
Tools/              美术辅助脚本
```

`Packages/manifest.json` 中的 TJGenerators 使用工程相对路径。仓库仅包含该路径所需的包源码：`.codely-cli/extensions/TJGenerators/Packages/cn.tuanjie.ai.generators/`，其余本机工具文件不随仓库分发。使用在线生成功能时需在本机自行登录相应服务。

## Windows 构建

在编辑器中选择 **JianHai → Build → Windows Release**。

构建脚本 `Assets/Editor/WindowsReleaseBuild.cs` 以 Windows x64 为目标，仅包含 `StartMenu` 和 `Stage1Maze` 两个面向玩家的场景，结果输出到 `Builds/Windows/`，同时生成 `build_result.json`。开发预览场景不包含在此发布流程中。

## 文件同步约定

- 同步完整源码、导入素材、音频、场景、项目配置及所需本地包源码；保留所有 Unity / 团结 `.meta` 文件及其 GUID。
- `Art_Source` 仅同步与 `Assets/Resources/Stage1PackedSprites.asset` 运行时资源目录中素材名称及 SHA-256 一致的 PNG 原稿；未匹配的候选素材、旧版本、Blender 备份和预览源文件不上传。
- `Library`、`Temp`、`Logs`、`UserSettings`、生成的 IDE 文件和 `Builds` 等由本机生成，不提交到 Git。
- 本机 `outputs`、截图、助手记忆、日志、临时文件和凭据不上传。

修改工程时应将资源与对应 `.meta` 文件一同提交。完整运行包可另行通过 GitHub Releases 分发。
