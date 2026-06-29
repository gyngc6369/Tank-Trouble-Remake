# 执行计划文档 — 《坦克动荡》

> 版本：v1.0 | 日期：2026-06-26

---

## 执行总览

| 阶段 | 名称 | 文件数 | 预计复杂度 |
|------|------|--------|-----------|
| 0 | 项目搭建 | 7 | 低 |
| 1 | 基础框架 | 3 | 低 |
| 2 | 地图系统 | 4 | 中 |
| 3 | 实体系统 | 3 | 中 |
| 4 | 游戏流程 | 2 | 中 |
| 5 | AI系统 | 3 | 高 |
| 6 | 打磨 | 3 | 低 |

---

## 阶段0：项目搭建 ✅

**目标：** 建立项目骨架和文档体系。

| 步骤 | 文件 | 描述 |
|------|------|------|
| 0.1 | 目录结构 | 创建 map/, entities/, ai/, render/, sound/, docs/ |
| 0.2 | `__init__.py` ×5 | Python 包初始化文件 |
| 0.3 | `DEVLOG.md` | 开发日志 |
| 0.4 | `docs/requirements.md` | 需求规格文档 |
| 0.5 | `docs/technical-spec.md` | 技术规格文档 |
| 0.6 | `docs/design-spec.md` | 设计规范文档 |
| 0.7 | `docs/execution-plan.md` | 本文件 |
| 0.8 | `CLAUDE.md` | Claude 项目指引 |

**验证：** 目录结构完整，所有文档内容与计划一致。

---

## 阶段1：基础框架

**目标：** 实现最小可运行骨架（显示窗口、可退出）。

### 1.1 settings.py
- 定义所有常量（尺寸、速度、颜色、键位）
- 无外部依赖
- 所有后续模块从此导入常量

### 1.2 main.py
- `pygame.init()` + 设置屏幕模式
- 创建 `Game` 和 `Renderer` 实例
- 主循环：`while running: handle_events(); game.update(dt); game.draw(renderer); pygame.display.flip()`
- 处理退出事件（QUIT, ESC）
- FPS 时钟 60fps
- delta time 计算（`clock.tick(FPS) / 1000.0`，上限 0.05s 防跳帧）

### 1.3 game.py (骨架)
- `GameState` 枚举
- `Game.__init__()` 初始化状态为 MAIN_MENU
- `Game.update(dt, keys)` 空壳
- `Game.draw(renderer)` 委托渲染

**验证：** 运行 `python main.py`，显示白色窗口 960×744，可按 ESC 退出，无报错。

---

## 阶段2：地图系统

**目标：** 实现地图数据结构、生成、渲染。

### 2.1 map/grid.py
- `WallSegment` dataclass
- `Grid` 类完整实现
- `get_wall_segments()` 遍历所有墙标记生成线段列表
- `get_neighbors()` 返回可通行的相邻格子
- `bfs_reachable()` + `check_all_reachable()`
- `cell_to_pixel()` / `pixel_to_cell()`
- 边界墙始终存在

### 2.2 map/presets.py
- 5 张 15×11×4 的预设墙数据
- `get_preset(index) → Grid`
- 每张预设手动设计 + 验证连通性

### 2.3 map/generator.py
- `generate_random_map(cols, rows, extra_ratio) → Grid`
- DFS Recursive Backtracker 实现
- 额外墙打通逻辑
- 连通性验证
- `get_spawn_points(grid, min_dist=5) → [(c1,r1), (c2,r2)]`

### 2.4 render/renderer.py (地图部分)
- `draw_map(grid)`：白色填充 + 黑色墙线
- 可选的开发模式：格子坐标标注

**验证：**
- 运行并渲染预设/随机地图
- BFS 断言所有格子可达
- 验证通路宽度 ≥ 60px
- 出生点距离 ≥ 5 格

---

## 阶段3：实体系统

**目标：** 实现坦克和炮弹的完整逻辑。

### 3.1 entities/tank.py
- `Tank` 类完整实现
- `get_body_corners()` 旋转矩形四角
- `get_barrel_tip()` 炮口坐标
- `move(dt, direction, grid)` + 墙碰撞
- `rotate(dt, direction, grid)` + 墙碰撞
- `shoot()` 生成 Bullet
- `update_ammo(dt)` 弹药回复

### 3.2 entities/bullet.py
- `Bullet` 类完整实现
- `update(dt, grid, tanks)` 完整碰撞检测
- 反弹逻辑（先查 bounce_count==7 → 消失）
- 命中坦克逻辑

### 3.3 render/renderer.py (实体部分)
- `draw_tank(tank)`：旋转车身 + 炮管
- `draw_bullet(bullet)`：圆形
- 仅渲染 alive=True 的实体

**验证：**
- 创建测试坦克，验证移动、旋转、碰撞
- 发射炮弹，观察反弹（1-7次）
- 第 7 次碰墙直接消失
- 炮弹命中坦克导致死亡
- 自伤检测

---

## 阶段4：游戏流程

**目标：** 完整的游戏循环、回合、分数系统。

### 4.1 完善 game.py
- 状态机完整实现
- 回合管理：`start_round()`, `end_round()`
- 分数追踪
- 输入处理分发
- 坦克-坦克碰撞
- 死亡检测与回合结束触发
- ROUND_END 倒计时

### 4.2 menu.py
- 主菜单：三个模式按钮
- 地图选择：6 选项 + 分数选择器
- 难度选择：普通/困难
- 暂停菜单
- 键盘导航（↑↓选择，Enter确认，B返回）

**验证：**
- 完整流程：主菜单→地图选择→难度→游戏→回合结束→游戏结束→重玩/返回
- 双人对战可操作，双方可射击，死亡后正确计分
- 分数达到目标后正确触发 GAME_OVER

---

## 阶段5：AI系统

**目标：** 实现带难度分级的电脑对手。

### 5.1 ai/pathfinding.py
- BFS 在 Grid 上的最短路径
- `get_danger_cells()` 预测炮弹危险区域
- `get_nearest_safe_cell()` 安全位置搜索

### 5.2 ai/ballistics.py
- `find_hit_angles()` 360° 射线扫描
- 反射模拟 + 坦克碰撞检测
- 结果排序（反弹少 + 路径短优先）

### 5.3 ai/controller.py
- `AIController` 类
- 难度参数配置
- 决策优先级：躲避 > 射击 > 移动
- 平滑转向逻辑
- 射击频率控制

**验证：**
- AI 能正确计算弹道路径并命中目标
- AI 能检测威胁并移动到安全位置
- 困难 AI 和普通 AI 行为差异明显
- 开发模式可视化 AI 射线

---

## 阶段6：打磨

**目标：** 音效、UI 完善、整体测试。

### 6.1 sound/sounds.py
- `init_sounds()` 合成音效
- `play_shoot_sound()` 射击音
- `play_explosion_sound()` 击毁音
- 使用 `pygame.sndarray` + 波形合成

### 6.2 UI 完善
- 弹药圆点显示
- 回合倒计时动画
- 菜单过渡动画（可选）
- 字体美化（可选，使用系统中文字体）

### 6.3 测试与平衡
- 双人对战完整流程测试
- AI 难度平衡测试
- 边界情况覆盖（墙角、满弹药、同时死亡等）
- 性能测试（60fps 稳定）

**最终验证：** 完整游戏可玩，所有功能正常，无明显 bug。

---

## 附录：执行规则

1. **每完成一个阶段，更新 DEVLOG.md**，标记完成项和下一阶段待办
2. **每完成一个模块（单个 .py 文件），运行验证**，确保不引入回归
3. **遇到设计偏离时，更新对应的 docs/ 文档**，保持文档与代码一致
4. **AI 系统开发时启用 debug 模式**，可视化射线和路径，确认正确后关闭
