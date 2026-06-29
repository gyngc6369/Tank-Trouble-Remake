# Pygame 到 Unity + C# 迁移计划

> 分析对象：当前工作区中的 Python/Pygame 项目源码。本文只规划迁移，不引入未经确认的新玩法。

## 1. 当前代码结构

### 1.1 文件职责

| 文件 | 职责 |
|------|------|
| `main.py` | 初始化 Pygame、窗口、音频、主循环、全局键鼠事件，并调用 `Game.update()` / `Game.draw()` |
| `settings.py` | 集中定义屏幕、网格、坦克、炮弹、规则、键位、颜色和字体路径等常量 |
| `game.py` | 游戏状态机、模式选择、菜单输入、回合流程、玩家/AI 输入分发、炮弹更新、碰撞协调和绘制分发 |
| `menu.py` | 菜单扩展预留文件，当前没有实际运行逻辑 |
| `entities/tank.py` | 坦克实体、旋转车身几何、移动/转向、墙体碰撞、射击、弹药回复和圆形炮弹命中车身工具 |
| `entities/bullet.py` | 炮弹实体、移动、墙体反射、命中坦克和点到线段距离工具 |
| `map/grid.py` | 薄墙网格数据结构、墙体移除、邻居查询、BFS 连通、出生点选择、坐标转换和墙线段缓存 |
| `map/presets.py` | 5 张预设地图构造和连通性修复 |
| `map/generator.py` | 随机地图生成，使用 DFS 回溯生成迷宫并额外打通内部墙 |
| `ai/controller.py` | AI 决策控制器，管理弹道计算、路径规划、躲避和连射状态 |
| `ai/pathfinding.py` | BFS 寻路、危险格预测、最近安全格搜索 |
| `ai/ballistics.py` | AI 弹道解计算，当前支持直线和单次反弹命中解 |
| `render/renderer.py` | 所有 Pygame 渲染，包括地图、坦克、炮弹、UI、菜单、遮罩和地图缩略图 |
| `sound/sounds.py` | 使用 Python 合成并播放射击和爆炸音效 |
| `test/*.py` | 针对移动、炮弹、AI 的轻量脚本测试 |

### 1.2 核心类与函数

| 名称 | 作用 |
|------|------|
| `GameState` | 主菜单、地图选择、难度选择、游戏中、回合结束、游戏结束、暂停 |
| `GameMode` | 双人、单人 vs AI、双人 + AI |
| `Game` | 最高层游戏控制器，持有地图、坦克、AI、炮弹、分数和状态 |
| `Game.start_game()` | 根据模式和地图选项创建地图、坦克、AI 控制器并开始第一回合 |
| `Game.start_round()` | 清空炮弹、选择出生点、重置坦克、开启倒计时 |
| `Game.end_round()` | 处理死亡、计分、胜利检查和回合结束状态 |
| `Game._update_playing()` | 更新玩家输入、AI、炮弹、坦克碰撞和安全修正 |
| `Tank` | 坦克状态和行为，包括几何、移动、转向、射击、弹药回复 |
| `Tank.get_body_corners()` | 计算旋转后车身四角，用于渲染和碰撞 |
| `Tank._check_collision_at()` | 判断给定位置的车身边是否与墙线段相交 |
| `Tank.shoot()` | 检查弹药和冷却，生成 `Bullet` |
| `Bullet.update()` | 移动炮弹、处理墙体反射和坦克命中 |
| `Grid` | 薄墙迷宫数据模型 |
| `Grid.get_wall_segments()` | 生成并缓存所有墙线段 |
| `Grid.pick_spawn_points()` | 随机选择距离足够远且连通的出生点 |
| `generate_random_map()` | 生成随机连通迷宫 |
| `get_preset()` | 返回指定预设地图 |
| `AIController.update()` | 输出 AI 的移动、转向和射击指令 |
| `bfs_path()` | 基于网格邻居的最短路径搜索 |
| `get_danger_cells()` | 简化模拟炮弹未来轨迹并标记危险格 |
| `find_hit_angles()` | 为 AI 查找直线或单次反弹命中角 |
| `Renderer` | Pygame 绘制入口和所有界面绘制方法 |

### 1.3 当前设计问题和技术债

- `Game` 同时负责状态机、输入分发、回合流程、实体协调和菜单键盘输入，职责偏重。
- `Renderer` 在菜单按钮点击时直接修改 `Game` 状态，UI 与游戏逻辑耦合。
- 当前源码参数与旧文档参数不完全一致，例如坦克尺寸、炮弹速度、炮弹半径、弹药回复时间。
- 炮弹反弹规则存在实现与文字描述差异：源码允许完成 7 次反弹后，在下一次碰墙时消失。
- `Grid.check_all_reachable()` 中有不可达的重复 `return True`。
- `map/presets.py` 中保留了早期尝试函数和绕路注释，地图构造逻辑可读性一般。
- `AIController.shoot_cooldown`、`shoot_timer`、`dodge_reaction_delay` 等字段目前没有完整参与决策。
- `bfs_path()` 接收 `avoid_cells`，但当前没有真正避开危险格。
- `ai/ballistics.py` 的 `max_bounces` 参数没有扩展到多次反弹，实际只计算直线和单次反弹。
- AI 单次反弹验证函数较简化，存在近似判断。
- 出生点选择失败时 fallback 只返回两个点，对三坦克模式不够稳健。
- 碰撞均为手写几何算法，迁移到 Unity 时需要决定是复刻几何逻辑，还是使用 Unity Physics2D 并通过测试校准。
- 中文注释和文本在当前终端读取时出现乱码，迁移前应统一编码和文本资源管理。
- 测试是脚本式验证，不是标准 Unity Test Runner 风格，覆盖有限。

## 2. Unity 目标结构建议

建议使用 Unity 2D 项目和正交摄像机，按“数据、规则、表现”分离。迁移顺序应先复刻可验证的规则，再补齐菜单、AI 和表现。

```text
Assets/
  Scripts/
    Config/
    Core/
    Map/
    Entities/
    AI/
    UI/
    Audio/
    Tests/
  Prefabs/
  Scenes/
  Materials/
  Audio/
```

建议核心脚本：

| Unity 脚本 | 对应 Pygame 职责 |
|-----------|------------------|
| `GameConfig.cs` | `settings.py` 常量 |
| `GameEnums.cs` | `GameState`、`GameMode` |
| `GameManager.cs` | 游戏状态、模式、主流程 |
| `RoundManager.cs` | 回合开始、回合结束、倒计时 |
| `ScoreManager.cs` | 分数和胜利判定 |
| `InputRouter.cs` | 玩家输入读取 |
| `TankController.cs` | 坦克移动、转向、弹药、射击 |
| `TankView.cs` | 坦克车身和炮管表现 |
| `BulletController.cs` | 炮弹移动、反弹、命中 |
| `GridMap.cs` | `Grid` 数据结构 |
| `WallSegment.cs` | 墙线段数据 |
| `MapBuilder.cs` | 预设/随机地图生成入口 |
| `PresetMaps.cs` | `map/presets.py` |
| `RandomMapGenerator.cs` | `map/generator.py` |
| `SpawnService.cs` | 出生点选择 |
| `CollisionService.cs` | 几何碰撞与反射逻辑 |
| `AIController.cs` | AI 决策状态机 |
| `AIPathfinding.cs` | BFS、危险格、安全格 |
| `AIBallistics.cs` | 命中角计算 |
| `HudController.cs` | 顶部分数和弹药 |
| `MenuController.cs` | 主菜单、地图选择、难度选择 |
| `RoundOverlayController.cs` | 倒计时、回合结束、游戏结束、暂停 |
| `AudioManager.cs` | 射击和爆炸音效 |

## 3. 分阶段迁移计划

### 阶段 0：Unity 项目骨架与参数冻结

目标：建立 Unity 2D 项目基础结构，将当前 Pygame 源码参数冻结到 Unity 配置，明确坐标系换算和像素单位策略。

需要创建/修改的 Unity 脚本：`GameConfig.cs`、`GameEnums.cs`、`GameManager.cs`、`CoordinateUtil.cs`。

完成标准：Unity 场景可运行；正交摄像机能显示 960 x 744 设计区域或等比例区域；当前源码常量都能在 `GameConfig` 中找到对应项；参数歧义不在代码中隐式改动。

测试方法：Play Mode 启动无报错；编辑器中显示空白游戏区域和顶部 UI 预留区域；Unity Test Runner 验证关键配置值等于当前 Pygame 源码值。

### 阶段 1：地图数据与薄墙渲染

目标：迁移 `Grid` 数据结构、5 张预设地图和随机地图生成，并在 Unity 中绘制薄墙线段。

需要创建/修改的 Unity 脚本：`GridMap.cs`、`WallSegment.cs`、`PresetMaps.cs`、`RandomMapGenerator.cs`、`MapBuilder.cs`、`MapRenderer.cs`。

完成标准：可选择并生成 5 张预设地图和 1 张随机地图；墙体表现为格子边界上的 4 px 等效线段；所有地图通过 BFS 连通性检查；`cell <-> world` 坐标转换稳定。

测试方法：单元测试验证 `GetNeighbors()`、`CheckAllReachable()`、`CellToWorld()`、`WorldToCell()`；对每张预设地图运行连通性测试；Play Mode 中逐张显示地图。

### 阶段 2：坦克实体与玩家移动

目标：实现坦克车身、炮管、车辆式移动和墙体碰撞，暂不实现炮弹和完整回合，只验证移动手感。

需要创建/修改的 Unity 脚本：`TankController.cs`、`TankView.cs`、`InputRouter.cs`、`CollisionService.cs`、`PlayerInputProfile.cs`。

完成标准：玩家 1 可用 WASD 操作，玩家 2 可用方向键操作；转向速度 150 度/s；移动速度等效当前源码 110 px/s；坦克不能穿墙；炮管可见但不参与碰撞。

测试方法：单元测试验证旋转车身四角计算；Play Mode 测试靠墙移动、旋转、后退；调试场景验证炮管碰墙不阻挡移动，车身碰墙会阻挡。

### 阶段 3：炮弹、反弹与命中

目标：实现炮弹发射、弹药、冷却、反弹、自伤和命中。

需要创建/修改的 Unity 脚本：`BulletController.cs`、`BulletPool.cs`、`AmmoComponent.cs`、`CollisionService.cs`、`RoundHitResolver.cs`。

完成标准：炮弹从炮管尖端生成；方向与坦克朝向一致；每次射击消耗 1 发弹药；弹药按 0.75 秒每发回复；射击冷却 0.15 秒；炮弹可反弹并命中任意坦克；命中只检测车身。

测试方法：单元测试验证反射向量；Play Mode 测试水平墙、垂直墙、角落附近反弹；测试炮弹命中目标和自伤；针对反弹次数写明确测试。

### 阶段 4：回合流程、计分与游戏状态机

目标：迁移完整游戏流程，先支持 PVP 的可玩闭环。

需要创建/修改的 Unity 脚本：`GameManager.cs`、`RoundManager.cs`、`ScoreManager.cs`、`SpawnService.cs`、`GameStateMachine.cs`、`HudController.cs`、`RoundOverlayController.cs`。

完成标准：开始游戏后创建地图和两辆玩家坦克；回合开始清空炮弹并重置坦克；3 秒倒计时期间禁止操作；命中后结束回合并计分；达到目标分数后进入游戏结束状态；P 和 ESC 行为与当前版本一致。

测试方法：Play Mode 完整打一局 PVP；自动测试验证击杀、自杀、三坦克计分规则；测试目标分数结束；测试出生点距离和连通性。

### 阶段 5：菜单与地图/难度选择

目标：迁移现有菜单流程，支持鼠标和键盘操作。

需要创建/修改的 Unity 脚本：`MenuController.cs`、`MapSelectController.cs`、`DifficultySelectController.cs`、`MapThumbnailRenderer.cs`、`WinScoreSelector.cs`。

完成标准：主菜单可选择三种模式；地图选择可选择 5 张预设地图或随机地图；胜利分数可在 1/3/5/7/10 之间切换；AI 模式进入普通/困难选择；鼠标点击和键盘快捷键均可使用。

测试方法：Play Mode 手动走完所有菜单路径；测试返回上一级；测试地图缩略图不会每帧重复生成造成卡顿。

### 阶段 6：AI 迁移

目标：迁移普通和困难 AI，先复刻当前行为，再考虑优化。

需要创建/修改的 Unity 脚本：`AIController.cs`、`AIPathfinding.cs`、`AIBallistics.cs`、`DangerMap.cs`、`AITankDriver.cs`。

完成标准：AI 使用与玩家相同的移动和转向速度；普通/困难参数与源码一致；AI 能移动到敌人附近、预测危险格、尝试躲避、计算直线和单次反弹射击角，并进行 2 发或 3 发连射。

测试方法：单元测试验证 BFS 路径；单元测试验证危险格预测不会无限循环；Play Mode 观察 AI 60 秒不卡死；测试两档难度的决策间隔和连射数量。

### 阶段 7：音效、表现与体验对齐

目标：迁移现有音效和视觉反馈，保持简洁几何风格。

需要创建/修改的 Unity 脚本：`AudioManager.cs`、`ProceduralSoundGenerator.cs` 或导入等效音效资源、`TankView.cs`、`BulletView.cs`、`OverlayView.cs`。

完成标准：射击音效约 0.1 秒，爆炸音效约 0.2 秒；音效失败不影响流程；白底、黑墙、蓝/红/橙坦克配色一致；顶部 UI 和各类遮罩一致。

测试方法：Play Mode 检查射击和命中音效；不同窗口尺寸下检查 UI 不遮挡地图；对比 Pygame 版本截图确认布局和颜色。

### 阶段 8：回归测试、参数校准与发布准备

目标：将迁移后的 Unity 版本变成稳定可交付版本，用测试锁定核心玩法。

需要创建/修改的 Unity 脚本：`GameplayTests.cs`、`MapTests.cs`、`CollisionTests.cs`、`AITests.cs`、`BuildValidation.cs`。

完成标准：核心规则有自动测试覆盖；PVP、PVAI、PVPAI 三种模式都可完成整局；所有地图可正常游玩；没有明显穿墙、卡墙、炮弹卡死、AI 卡死问题；可生成 Windows 构建。

测试方法：Unity Test Runner 全部通过；手动验收三种模式；长时间运行随机地图和 AI 对战；构建后在目标机器上运行 smoke test。

## 4. 迁移顺序原则

- 先迁移数据和规则，再迁移表现。
- 每个阶段都必须能在 Unity 中独立运行和验证。
- 不把菜单、AI、音效、特效压到一次大重写里。
- 对有歧义的规则先写测试，再写实现。
- Pygame 版本作为行为参考，Unity 版本以测试固定关键体验。

## 5. 迁移前需要确认的规则

- 炮弹到底是“第 7 次碰墙直接消失”，还是“完成 7 次反弹后下一次碰墙消失”。当前源码实际接近后者。
- Unity 版本是否继续使用当前源码参数：坦克 20 x 28、炮弹半径 2、炮弹速度 165、弹药回复 0.75 秒。
- 三坦克模式中，一名坦克死亡后是否立即结束回合。当前源码是立即结束。
- 三坦克模式中自杀时，所有其他存活坦克都得分。当前源码是这样处理。
- AI 弹道是否保持当前“直线 + 单次反弹”，还是后续再扩展多次反弹。迁移第一版建议保持当前行为。
