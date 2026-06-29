# Tank Trouble Unity Remake

> 基于 **Unity + C#** 重构经典双人坦克对战游戏《Tank Trouble》，从原有 **Python + Pygame** 版本迁移至 Unity，引入工程化架构、Unity Physics2D 和模块化 AI，实现更加稳定、易维护和可扩展的游戏系统。

---

# 项目背景

## 选题背景

《Tank Trouble》是一款经典的 2D 坦克对战游戏，其特色在于狭窄迷宫中的移动、炮弹反弹机制以及多人竞技玩法。

项目最初基于 **Python + Pygame** 实现，并借助 **Claude Code** 自动生成了第一版游戏。然而，在实际测试过程中发现，该版本存在以下问题：

* AI 行为逻辑复杂且耦合严重，容易陷入原地旋转、反复决策等死循环；
* Pygame 版本需要自行处理大量碰撞与渲染逻辑，维护成本较高；
* 游戏逻辑与界面耦合，扩展新功能较困难；
* 随着功能不断增加，代码结构逐渐混乱，不利于后续开发和优化。

因此，本项目放弃直接继续维护 Python 版本，而是在保留原有游戏玩法的基础上，重新使用 **Unity + C#** 对整个项目进行工程化重构。

重构过程中，以 Pygame 版本作为玩法参考，实现了更加清晰的软件架构、更加稳定的 AI 框架以及更加完善的游戏管理系统。

---

# 项目目标

本项目主要目标如下：

* 保留原版《Tank Trouble》的核心玩法；
* 将 Python + Pygame 项目完整迁移至 Unity；
* 使用 Unity Physics2D 提高碰撞检测可靠性；
* 重构 AI 系统，解决原版本 AI 容易卡死的问题；
* 建立模块化、可维护的软件架构；
* 运用课程中的数据结构、算法和软件工程思想完成完整游戏开发。

---

# 核心算法与课程知识点

本项目综合运用了《数据结构》《算法设计》《面向对象程序设计》《软件工程》等课程中的知识。

## 1. 图搜索（BFS）

AI 在迷宫地图中采用 **广度优先搜索（Breadth First Search）** 进行路径规划。

主要用于：

* AI 自动寻路
* 地图连通性检测
* 出生点合法性验证

通过 BFS 获取从当前位置到目标位置的最短可行路径，为 AI 导航提供基础。

---

## 2. 有限状态机（Finite State Machine）

AI 行为采用有限状态机管理。

主要状态包括：

* Idle（待机）
* Navigate（导航）
* Attack（攻击）
* Evade（躲避）
* Recover（恢复）

状态之间根据游戏环境进行切换，提高 AI 行为稳定性，并避免复杂逻辑之间互相干扰。

---

## 3. 网格寻路与路径跟随

地图被离散为规则网格。

AI 首先计算路径，再按照 Waypoint 逐格移动，实现：

* 路径规划
* 路径跟随
* 卡死检测
* 路径重规划

同时结合 Unity Physics2D 判断路径是否真正可执行。

---

## 4. Unity Physics2D 碰撞系统

利用 Unity Physics2D 实现：

* 坦克碰撞
* 墙体碰撞
* 炮弹反弹
* 子弹命中检测

相比 Pygame 版本，大幅减少了手工几何计算，提高了游戏稳定性。

---

## 5. 对象池（Object Pool）

炮弹采用对象池管理。

避免频繁 Instantiate / Destroy，减少垃圾回收，提高游戏运行效率。

---

## 6. 面向对象设计

游戏采用模块化设计。

主要模块包括：

* GameManager
* RoundManager
* ScoreManager
* MapManager
* TankController
* BulletController
* AIController
* HUDController

各模块职责单一，降低耦合，提高可维护性。

---

## 7. ScriptableObject 配置管理

将游戏参数统一保存为 ScriptableObject，包括：

* 坦克属性
* 炮弹参数
* AI 参数
* 地图配置

方便后续调整和平衡游戏。

---

## 8. 自动化测试

项目引入 Unity Test Framework，对以下内容进行测试：

* 地图连通性
* 炮弹反弹次数
* AI 参数配置
* 回合规则
* 坐标转换
* 三坦克模式
* HUD 布局

保证修改代码后不会破坏已有功能。

---

# 项目结构

```text
unity_tank_trouble
│
├── Assets
│   ├── Audio
│   ├── Editor
│   ├── Materials
│   ├── Prefabs
│   ├── Scenes
│   ├── Scripts
│   │   ├── AI
│   │   ├── Core
│   │   ├── Entities
│   │   ├── Map
│   │   ├── UI
│   │   └── Tests
│   └── ...
├── Packages
├── ProjectSettings
└── README.md
```

---

# 运行指南

## 开发环境

* Windows 10 / Windows 11
* Unity 2022.3 LTS
* Visual Studio 2022 或 VS Code
* Unity Hub

---

## 获取项目

进入 Unity 项目：

```bash
cd unity_tank_trouble
```

---

## 打开项目

1. 打开 Unity Hub。
2. 点击 **Open**。
3. 选择 `unity_tank_trouble` 文件夹。
4. 等待 Unity 自动导入资源并完成脚本编译。

---

## 运行游戏

1. 打开 `Assets/Scenes/MainMenu.unity`。
2. 点击 Unity 顶部 **Play**。
3. 在主菜单中选择：

   * 游戏模式（PVP / PVAI / 双人+AI）
   * 地图和胜利分数
   * AI 难度
4. 开始游戏。

---

## 项目校验

编译完成后，可通过 Unity 菜单：

```
Tank Trouble
    └── Validate Project
```

检查：

* 场景配置
* Build Settings
* Prefab
* 核心参数

确保项目可以正常运行。

---

# 项目特色

* 保留经典《Tank Trouble》玩法，同时采用现代游戏开发架构；
* 使用 Unity Physics2D 实现稳定碰撞与炮弹反弹；
* AI 支持自动寻路、攻击与危险规避；
* 模块化设计，便于后续功能扩展和维护；
* 引入自动化测试，提高代码质量和项目稳定性。

---

# 后续工作

后续计划继续完善以下内容：

* 重构 AI 导航系统，进一步解决复杂场景下的卡死问题；
* 增加更多地图与游戏模式；
* 优化 UI 和视觉表现；
* 丰富音效和粒子特效；
* 持续完善自动化测试，提升项目整体质量。

---

# 致谢

本项目的 **Python + Pygame** 初始版本由 **Claude Code** 协助完成，用于验证游戏玩法与整体框架。

当前 **Unity + C#** 重构版本由**Codex**在此基础上进行了重新设计与工程化开发，对游戏架构、AI 系统、碰撞处理和资源管理进行了全面重构，以提升项目的稳定性、可维护性和扩展能力。
本项目中的核心算法逻辑均由本人提供，AI完成代码实现，再由本人进行测试和调试修改。
（处理一个有体积的实体对象的，集成了移动，追击，躲避，调整，射击功能的AI决策体系确实好难，仅AI玩家的部分就做了整整三天，烧了巨量token，gpt5plus的一周额度也花干净了。目前只能够保证躲避数量不多的炮弹以及精准计算弹道并发射命中的功能，长距离复杂追击决策仍有瑕疵。）
