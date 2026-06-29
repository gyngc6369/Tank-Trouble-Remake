"""
《坦克动荡》— 全局常量与配置
"""

import pygame
from pygame.locals import *

# ============================================================
# 屏幕
# ============================================================
SCREEN_WIDTH = 960
SCREEN_HEIGHT = 744
UI_BAR_HEIGHT = 40
GRID_OFFSET_Y = UI_BAR_HEIGHT          # 地图在 Y 方向偏移
FPS = 60

# ============================================================
# 网格
# ============================================================
GRID_COLS = 15
GRID_ROWS = 11
CELL_SIZE = 64
WALL_THICKNESS = 4                     # 薄墙线条粗细

# ============================================================
# 坦克
# ============================================================
TANK_BODY_W = 20                       # 车身宽度（侧向，窄边=侧面）
TANK_BODY_H = 28                       # 车身高度（前后向，长边=前进方向/炮管方向）
TANK_SPEED = 110                       # px/s
TANK_ROTATION_SPEED = 150              # 度/s
BARREL_LENGTH = 16                     # 炮管长度（从车身前边伸出）
BARREL_WIDTH = 4                       # 炮管粗细

# ============================================================
# 炮弹
# ============================================================
BULLET_SPEED = 165                     # px/s (= TANK_SPEED * 1.5)
BULLET_RADIUS = 2                      # 圆形半径 (< WALL_THICKNESS=4)
MAX_BOUNCES = 7                        # 最大反弹次数（第7次碰墙消失）
MAX_AMMO = 7                           # 最大弹药数
AMMO_REGEN_TIME = 0.75                 # s/发

# ============================================================
# 游戏规则
# ============================================================
DEFAULT_WIN_SCORE = 5
WIN_SCORE_OPTIONS = [1, 3, 5, 7, 10]
SPAWN_MIN_DIST = 5                     # 出生点最小曼哈顿距离（格）

# ============================================================
# 键位绑定
# ============================================================
# 玩家1: WASD + F
P1_UP = K_w
P1_DOWN = K_s
P1_LEFT = K_a
P1_RIGHT = K_d
P1_FIRE = K_f

# 玩家2: 方向键 + /
P2_UP = K_UP
P2_DOWN = K_DOWN
P2_LEFT = K_LEFT
P2_RIGHT = K_RIGHT
P2_FIRE = K_SLASH

# ============================================================
# 颜色方案（白底黑墙）
# ============================================================
COLOR_BG = (255, 255, 255)
COLOR_WALL = (0, 0, 0)

COLOR_P1_BODY = (60, 100, 220)
COLOR_P1_BARREL = (30, 60, 160)

COLOR_P2_BODY = (220, 60, 60)
COLOR_P2_BARREL = (160, 30, 30)

COLOR_AI_BODY = (220, 150, 50)
COLOR_AI_BARREL = (160, 100, 20)

COLOR_UI_TEXT = (40, 40, 40)
COLOR_UI_BG = (240, 240, 240)
COLOR_OVERLAY = (0, 0, 0)              # 半透明遮罩底色
COLOR_BUTTON = (220, 220, 220)
COLOR_BUTTON_HOVER = (180, 180, 180)

# ============================================================
# 字体
# ============================================================
# 中文字体兜底链（按优先级排列）
FONT_PATHS = [
    "C:/Windows/Fonts/simhei.ttf",    # 黑体
    "C:/Windows/Fonts/msyh.ttc",      # 微软雅黑
    "C:/Windows/Fonts/simsun.ttc",    # 宋体
]
FONT_PATH_DEFAULT = FONT_PATHS[0]

# ============================================================
# 导出列表（方便 from settings import *）
# ============================================================
__all__ = [
    'FONT_PATHS', 'FONT_PATH_DEFAULT',
    'SCREEN_WIDTH', 'SCREEN_HEIGHT', 'UI_BAR_HEIGHT', 'GRID_OFFSET_Y', 'FPS',
    'GRID_COLS', 'GRID_ROWS', 'CELL_SIZE', 'WALL_THICKNESS',
    'TANK_BODY_W', 'TANK_BODY_H', 'TANK_SPEED', 'TANK_ROTATION_SPEED',
    'BARREL_LENGTH', 'BARREL_WIDTH',
    'BULLET_SPEED', 'BULLET_RADIUS', 'MAX_BOUNCES', 'MAX_AMMO', 'AMMO_REGEN_TIME',
    'DEFAULT_WIN_SCORE', 'WIN_SCORE_OPTIONS', 'SPAWN_MIN_DIST',
    'P1_UP', 'P1_DOWN', 'P1_LEFT', 'P1_RIGHT', 'P1_FIRE',
    'P2_UP', 'P2_DOWN', 'P2_LEFT', 'P2_RIGHT', 'P2_FIRE',
    'COLOR_BG', 'COLOR_WALL',
    'COLOR_P1_BODY', 'COLOR_P1_BARREL',
    'COLOR_P2_BODY', 'COLOR_P2_BARREL',
    'COLOR_AI_BODY', 'COLOR_AI_BARREL',
    'COLOR_UI_TEXT', 'COLOR_UI_BG', 'COLOR_OVERLAY',
    'COLOR_BUTTON', 'COLOR_BUTTON_HOVER',
]
