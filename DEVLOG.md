# 开发日志 — 《坦克动荡》

> 项目路径：`c:\Users\lenovo\Desktop\tank trouble`
> 开始日期：2026-06-26

---

## 2026-06-26 — 阶段0：项目搭建 ✅

- [x] 创建目录结构（map/, entities/, ai/, render/, sound/, docs/）
- [x] 创建各包 `__init__.py`
- [x] 编写需求文档 [docs/requirements.md](docs/requirements.md)
- [x] 编写技术规范 [docs/technical-spec.md](docs/technical-spec.md)
- [x] 编写设计规范 [docs/design-spec.md](docs/design-spec.md)
- [x] 编写执行计划 [docs/execution-plan.md](docs/execution-plan.md)
- [x] 创建 [CLAUDE.md](CLAUDE.md) 项目指引

---

## 2026-06-26 — 阶段1：基础框架 ✅

- [x] `settings.py` — 所有常量定义（尺寸、速度、颜色、键位）
- [x] `main.py` — Pygame 初始化，主循环骨架，事件处理
- [x] `game.py` — Game 类，状态机框架（含初步回合逻辑）
- [x] `render/renderer.py` — 渲染器骨架（为阶段2准备接口）

**验证：** `python main.py` 启动正常，无导入错误，3 秒内无崩溃。

---

## 阶段1：基础框架 ✅ (已完成)

---

## 2026-06-26 — 阶段2：地图系统 ✅

- [x] `map/grid.py` — Grid 薄墙图结构（BFS连通性、出生点选取、墙线段生成）
- [x] `map/presets.py` — 5张预设地图（开放、走廊、对称、螺旋、格栅）
- [x] `map/generator.py` — 随机生成器（DFS挖墙 + 额外20%连通边）
- [x] `render/renderer.py` — 地图薄墙线条渲染（已在上阶段预留接口）

**验证：**
- 随机地图：连通性 True，164 墙段，出生距 14 ≥ 5 ✓
- 5 张预设全部连通，出生距 ≥ 5 ✓

---

## 阶段2：地图系统 ✅ (已完成)

---

## 2026-06-26 — 阶段3：实体系统 ✅

- [x] `entities/tank.py` — Tank 类（矩形车身+炮管、移动碰撞、平滑转向、弹药回复）
- [x] `entities/bullet.py` — Bullet 类（反弹先查次数、圆-线段碰撞、反射计算）
- [x] 线段相交、圆-矩形碰撞等工具函数

**验证：**
- 坦克位置、角点、炮口坐标正确
- 移动碰撞检测正常
- 射击生成炮弹，速度和方向正确
- 线段相交检测正确（交叉=True, 平行=False）

---

## 阶段3：实体系统 ✅ (已完成)

---

## 2026-06-26 — 阶段4：游戏流程 ✅

- [x] 完善 `game.py` — 完整游戏循环（双人输入、子弹更新、回合/分数、坦克碰撞）
- [x] `menu.py` — 菜单预留接口
- [x] `render/renderer.py` — 完整菜单渲染（主菜单、地图选择含缩略图、难度选择）
- [x] `main.py` — 更新以匹配新 Game 接口

**验证：** `python main.py` 启动正常，3s 无崩溃。菜单渲染、地图缩略图正常。

---

## 阶段4：游戏流程 ✅ (已完成)

---

## 2026-06-26 — 阶段5：AI系统 ✅

- [x] `ai/pathfinding.py` — BFS寻路 + 炮弹危险区域评估 + 安全格搜索
- [x] `ai/ballistics.py` — 360°射线扫描 + 反射模拟 + 命中解排序
- [x] `ai/controller.py` — AI决策（躲避>射击>移动）+ 难度分级
- [x] 集成到 `game.py` — AI坦克由AIController驱动

**验证：**
- 弹道计算正常（无视线时返回0解，符合预期）
- BFS路径25步可达 ✓
- AI决策产出正确（move/rotate/shoot指令）
- 难度参数正确区分（hard/normal各参数符合设计）

---

## 阶段5：AI系统 ✅ (已完成)

## 2026-06-26 — 性能优化 ✅

- [x] 墙段缓存 (grid.py): get_wall_segments() 计算一次，后续直接返回
- [x] 空间索引 (grid.py): get_walls_near() 按格子哈希，查询从300→32条
- [x] 坦克碰撞用空间索引 (tank.py)
- [x] 炮弹碰撞用空间索引 (bullet.py)
- [x] AI弹道优化: 360射线→72射线, max_steps 3000→800, 空间索引
- [x] 缩略图缓存 (renderer.py): 菜单不再每帧生成迷宫
- [x] 弹药回复 1.0→0.75s

**性能对比：**
- 弹道计算: ~500ms+ → 70ms (7x加速)
- 碰撞检测: 300墙段/次 → 32墙段/次 (10x加速)
- 模拟FPS: 13769 (充裕)

---

## 2026-06-26 — 坦克朝向 + 炮弹 + AI 修复 ✅

- [x] 坦克朝向修正：W=20(窄边=侧面), H=28(长边=前进方向/炮管方向)
- [x] 炮管平行于长边(H=28)，从前进方向前端伸出16px
- [x] 炮弹速度降至165 (=TANK_SPEED*1.5)
- [x] 炮弹半径缩小至2px (<WALL_THICKNESS=4)
- [x] 炮弹步长1px保证边缘反弹精度
- [x] AI持续行为：帧间保持移动方向，决策周期更新路径
- [x] AI连射：普通2发/困难3发，瞄准同一弹道方向

**验证：** 炮口距中心30px(=14+16)、炮弹速度165、AI连射2/3发、边缘反弹vy=165 → 全部通过

---

## 2026-06-26 — Bug修复 + 增强 ✅

- [x] Fix 1: 中文字体兜底链（SimHei→MSYH→SimSun→默认+警告）
- [x] Fix 2: 坦克炮管可见性（从车身前边缘伸出BARREL_LENGTH，深色+边框）
- [x] Fix 3A: AI continue bug修复（移除 `or tank.is_ai`，AI可以移动和射击）
- [x] Fix 3B: 分数箭头修复（整合进 `_draw_button()` 返回值处理）
- [x] Fix 3C: dt硬编码修复（`handle_menu_input(dt)` 接受实参）
- [x] New: 开局3秒倒计时（灰色遮罩+中央数字 3→2→1→开始！）

**验证：** 字体80px中文、AI正常移动、箭头切换5→7、倒计时3秒 → 全部通过。

---

## 2026-06-26 — UI 大修 ✅

- [x] 中文字体支持（使用系统字体 simhei.ttf / msyh.ttc）
- [x] 鼠标交互（点击选择菜单项、地图、难度、调整分数）
- [x] UI 清晰度提升（更大按钮、合理间距、高亮反馈、颜色对比）
- [x] 坦克尺寸调整（TANK_BODY_H: 22→20，长边=侧面更明显）
- [x] 方法重构（私有→公开，消除渲染器访问的命名问题）

- [x] `sound/sounds.py` — 合成音效（射击音+击毁音，纯Python无外部文件）
- [x] 音效集成到坦克射击和回合结束
- [x] 全面系统验证通过

### 最终验证结果
| 测试项 | 结果 |
|--------|------|
| 随机地图连通性 | ✅ |
| 5张预设地图连通性+出生距 | ✅ |
| 坦克射击+炮弹生成 | ✅ |
| 碰撞检测函数 | ✅ |
| BFS路径规划 (23步) | ✅ |
| AI难度参数 (hard/normal) | ✅ |
| 音效合成与播放 | ✅ |
| 渲染器 | ✅ |

---

## 阶段6：打磨 ✅ (已完成)

---

## 项目完成状态

**所有 6 个阶段已完成！** 🎉

```
tank trouble/
├── main.py              ✅ 入口
├── settings.py          ✅ 配置
├── game.py              ✅ 游戏逻辑
├── menu.py              ✅ 菜单接口
├── CLAUDE.md            ✅ 项目指引
├── DEVLOG.md            ✅ 开发日志
├── map/
│   ├── grid.py          ✅ 薄墙图
│   ├── presets.py       ✅ 5张预设
│   └── generator.py     ✅ 随机生成
├── entities/
│   ├── tank.py          ✅ 坦克
│   └── bullet.py        ✅ 炮弹
├── ai/
│   ├── pathfinding.py   ✅ BFS寻路
│   ├── ballistics.py    ✅ 弹道计算
│   └── controller.py    ✅ AI决策
├── render/
│   └── renderer.py      ✅ 渲染
├── sound/
│   └── sounds.py        ✅ 音效
└── docs/
    ├── requirements.md  ✅ 需求
    ├── technical-spec.md✅ 技术
    ├── design-spec.md   ✅ 设计
    └── execution-plan.md✅ 执行计划
```

---

## 阶段6：打磨 [待开始]

- [ ] `sound/sounds.py` — 合成音效
- [ ] UI 完善（弹药显示、倒计时动画）
- [ ] 测试与平衡调整
