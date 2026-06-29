# 技术规格文档 — 《坦克动荡》

> 版本：v1.0 | 日期：2026-06-26

---

## 1. 技术栈

| 层级 | 选型 |
|------|------|
| 语言 | Python 3.13 |
| 游戏框架 | Pygame 2.6 |
| 音效 | `pygame.mixer` + 纯 Python 波形合成 |
| 打包 | 无需（直接运行 main.py） |

---

## 2. 架构概览

```
main.py ──→ Game (game.py) ──→ Renderer (render/renderer.py)
                │                        │
                ├── Grid (map/grid.py)   │
                ├── Tank (entities/)     │
                ├── Bullet (entities/)   │
                ├── AIController (ai/)   │
                └── Menu (menu.py)       │
                     └── Sounds (sound/) │
```

**数据流向：**
```
输入 → Game.update() → Tank/Bullet 状态变更 → Renderer.draw() → 屏幕
                         ↑
                    AIController (读取状态，写入AI坦克指令)
```

---

## 3. 模块规格

### 3.1 settings.py — 全局常量

所有可调参数集中于此文件：

```python
# 屏幕
SCREEN_WIDTH = 960
SCREEN_HEIGHT = 744
UI_BAR_HEIGHT = 40
GRID_OFFSET_Y = UI_BAR_HEIGHT

# 网格
GRID_COLS = 15
GRID_ROWS = 11
CELL_SIZE = 64
WALL_THICKNESS = 4

# 坦克
TANK_BODY_W = 28
TANK_BODY_H = 22
TANK_SPEED = 110          # px/s
TANK_ROTATION_SPEED = 150 # 度/s
BARREL_LENGTH = 16
BARREL_WIDTH = 4

# 炮弹
BULLET_SPEED = 280        # px/s
BULLET_RADIUS = 6
MAX_BOUNCES = 7
MAX_AMMO = 7
AMMO_REGEN_TIME = 1.0     # s

# 游戏
FPS = 60
DEFAULT_WIN_SCORE = 5
WIN_SCORE_OPTIONS = [1, 3, 5, 7, 10]

# 键位
P1_UP = K_w, P1_DOWN = K_s, P1_LEFT = K_a, P1_RIGHT = K_d, P1_FIRE = K_f
P2_UP = K_UP, P2_DOWN = K_DOWN, P2_LEFT = K_LEFT, P2_RIGHT = K_RIGHT, P2_FIRE = K_SLASH

# 颜色
COLOR_BG = (255, 255, 255)
COLOR_WALL = (0, 0, 0)
COLOR_P1_BODY = (60, 100, 220)
COLOR_P1_BARREL = (30, 60, 160)
COLOR_P2_BODY = (220, 60, 60)
COLOR_P2_BARREL = (160, 30, 30)
COLOR_AI_BODY = (220, 150, 50)
COLOR_AI_BARREL = (160, 100, 20)
COLOR_UI_TEXT = (40, 40, 40)
```

### 3.2 map/grid.py — 地图数据结构

**核心数据结构：**

```python
class Grid:
    def __init__(self, cols=GRID_COLS, rows=GRID_ROWS):
        # walls[r][c] = [top, right, bottom, left]  四个布尔值
        # top wall 在格子上边界，right wall 在格子右边界...
        self.walls: list[list[list[bool]]]
        self.cols: int
        self.rows: int

    def get_wall_segments(self) -> list[WallSegment]:
        """返回所有墙体线段列表，用于渲染和碰撞检测"""
        # 每条墙线 = 两个端点 + 法向量方向

    def get_neighbors(self, col, row) -> list[tuple[int, int]]:
        """返回相邻且无墙阻隔的邻居格子"""

    def bfs_reachable(self, start_col, start_row) -> set:
        """BFS 返回从起点可达的所有格子集合"""

    def check_all_reachable(self) -> bool:
        """验证所有格子互通"""

    def cell_to_pixel(self, col, row) -> tuple[float, float]:
        """格子坐标→像素坐标（格子中心）"""

    def pixel_to_cell(self, px, py) -> tuple[int, int]:
        """像素坐标→格子坐标"""
```

**WallSegment：**
```python
@dataclass
class WallSegment:
    x1, y1: float   # 起点
    x2, y2: float   # 终点
    nx, ny: float   # 法向量（指向墙外/空格侧）
```

### 3.3 map/generator.py — 随机地图生成

```python
def generate_random_map(cols=15, rows=11, extra_ratio=0.20) -> Grid:
    """
    1. 初始化全封闭 Grid（所有墙存在）
    2. Recursive Backtracker DFS 挖墙 → 生成树
    3. 以 extra_ratio 概率随机打通额外墙 → 增加回路
    4. BFS 验证连通性
    5. 返回 Grid
    """
```

### 3.4 map/presets.py — 预设地图

```python
PRESET_MAPS: list[list[list[list[bool]]]]  # 5 × 15 × 11 × 4

def get_preset(index: int) -> Grid:
    """加载第 index 张预设地图"""
```

### 3.5 entities/tank.py — 坦克类

```python
class Tank:
    # 属性
    x, y: float            # 车身中心像素坐标
    angle: float           # 面朝方向（弧度），0=↑
    ammo: int              # 0~7
    ammo_timer: float      # 累计秒数
    alive: bool
    score: int             # 总得分
    color_body: tuple
    color_barrel: tuple
    shoot_cooldown: float  # 射击冷却（防连发）

    # 方法
    def get_body_rect(self) -> pygame.Rect:
        """返回未旋转的车身矩形（用于碰撞检测的四角）"""

    def get_body_corners(self) -> list[tuple[float, float]]:
        """返回旋转后的四个角点坐标"""

    def get_barrel_tip(self) -> tuple[float, float]:
        """返回炮口尖端坐标"""

    def move(self, dt, direction, grid) -> bool:
        """前进/后退，collision with walls，返回是否移动成功"""

    def rotate(self, dt, direction, grid) -> bool:
        """旋转，collision check，返回是否旋转成功"""

    def shoot(self) -> Bullet | None:
        """若弹药>0且冷却完毕，返回新Bullet，否则None"""

    def update_ammo(self, dt):
        """弹药回复计时"""
```

**碰撞体积：** 仅车身矩形（28×22），炮管不计。

### 3.6 entities/bullet.py — 炮弹类

```python
class Bullet:
    # 属性
    x, y: float
    vx, vy: float
    bounce_count: int  # 0~7
    owner: Tank        # 发射者引用
    alive: bool

    # 方法
    def update(self, dt, grid, tanks) -> Tank | None:
        """
        1. 计算新位置
        2. 检测墙线碰撞：
           - bounce_count == 7 → alive=False, return None（不反弹！）
           - 否则 → 反射 v，bounce_count++
        3. 检测坦克碰撞（圆 vs AABB）：
           - 命中 → 该坦克死亡，alive=False
           - 返回被命中的坦克（或None）
        4. 若碰到多个墙 → 依次处理所有碰撞
        """

    def get_pos(self) -> tuple[float, float]:
        """返回圆心"""
```

### 3.7 碰撞检测函数

```python
# 圆 vs 线段（炮弹 vs 墙）
def circle_segment_collision(cx, cy, radius, x1, y1, x2, y2) -> bool:
    """检测圆是否与线段相交"""

# AABB vs 线段（坦克 vs 墙）
def rect_segment_collision(corners, x1, y1, x2, y2) -> bool:
    """检测旋转矩形的四条边是否与线段相交"""

# 圆 vs AABB（炮弹 vs 坦克）
def circle_rect_collision(cx, cy, radius, corners) -> bool:
    """圆与旋转矩形的碰撞检测（最近点法）"""

# 反射计算
def reflect(vx, vy, nx, ny) -> tuple[float, float]:
    """速度向量关于法向量的反射"""
    # dot = vx*nx + vy*ny
    # vx' = vx - 2*dot*nx
    # vy' = vy - 2*dot*ny
```

---

## 4. AI 系统

### 4.1 ai/ballistics.py — 弹道计算

```python
@dataclass
class HitSolution:
    angle: float         # 射击角度（弧度）
    bounces: int         # 反弹次数
    path_length: float   # 路径总长

def find_hit_angles(shooter_pos, shooter_angle, targets, grid,
                    max_bounces=7, step_size=2.0) -> list[HitSolution]:
    """
    360°扫描（1°步长），模拟射线+反射，找到所有命中目标的角度。
    按 (bounces↑, path_length↑) 排序，返回最优解在前。
    """
```

### 4.2 ai/pathfinding.py — 路径规划

```python
def bfs_path(grid, start_cell, goal_cell, avoid_cells=set()) -> list[tuple[int,int]]:
    """BFS最短路径，可避让指定格子"""

def get_danger_cells(grid, bullets, predict_time) -> set:
    """预测炮弹未来轨迹，标记危险格子"""

def get_nearest_safe_cell(grid, current_cell, danger_cells) -> tuple[int,int]:
    """找到最近的安全格子"""
```

### 4.3 ai/controller.py — AI 决策

```python
class AIController:
    def __init__(self, difficulty: str):
        self.difficulty = difficulty  # "hard" | "normal"
        # 根据难度设置各参数

    def update(self, dt, tank, enemy_tanks, bullets, grid):
        """
        优先级：
        1. 躲避 → move指令
        2. 射击 → rotate + shoot指令
        3. 巡逻 → move指令
        """
```

---

## 5. 渲染系统 (render/renderer.py)

```python
class Renderer:
    def __init__(self, screen):
        self.screen = screen

    def draw_map(self, grid):
        """白色背景 + 黑色墙线"""

    def draw_tank(self, tank):
        """旋转后的矩形车身 + 炮管"""

    def draw_bullet(self, bullet):
        """填充圆形"""

    def draw_ui(self, game_state):
        """顶部状态栏：分数、弹药、回合信息"""

    def draw_overlay(self, text, subtext=""):
        """半透明遮罩 + 居中文字（用于回合结束/游戏结束/暂停）"""

    def draw_menu(self, menu_state):
        """菜单界面"""

    def draw_debug_rays(self, ai_controller):
        """开发模式：绘制AI弹道射线"""
```

---

## 6. 游戏状态管理 (game.py)

```python
class GameState(Enum):
    MAIN_MENU = 0
    MAP_SELECT = 1
    DIFFICULTY_SELECT = 2
    GAME_PLAYING = 3
    ROUND_END = 4
    GAME_OVER = 5
    PAUSED = 6

class Game:
    def __init__(self):
        self.state = GameState.MAIN_MENU
        self.mode = None          # "pvp" | "pvai" | "pvpai"
        self.difficulty = None    # "hard" | "normal"
        self.grid = None
        self.tanks = []
        self.bullets = []
        self.win_score = DEFAULT_WIN_SCORE
        self.round_timer = 0

    def update(self, dt, keys):
        """根据当前状态分发更新"""

    def start_round(self):
        """重置坦克位置，清空炮弹"""

    def end_round(self, loser, winner):
        """更新分数，检查是否有人获胜"""

    def draw(self, renderer):
        """根据状态调用渲染"""
```

---

## 7. 文件依赖图

```
settings.py          ← 被所有模块导入
map/grid.py          ← 无依赖（仅 settings）
map/presets.py       ← map/grid.py
map/generator.py     ← map/grid.py
entities/tank.py     ← settings, map/grid
entities/bullet.py   ← settings, map/grid, entities/tank
ai/ballistics.py     ← settings, map/grid, entities/tank
ai/pathfinding.py    ← settings, map/grid
ai/controller.py     ← settings, entities/tank, entities/bullet, ai/*
render/renderer.py   ← settings, 所有实体类
sound/sounds.py      ← 无依赖
menu.py              ← settings, render/renderer
game.py              ← 所有模块
main.py              ← game, render, settings
```
