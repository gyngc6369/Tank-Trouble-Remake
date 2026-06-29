"""
《坦克动荡》— 游戏主模块（完整版）
状态管理、回合逻辑、输入处理、实体协调。
"""

import random
import math
from enum import Enum, auto
import pygame
from pygame.locals import *

from settings import *
from entities.tank import Tank
from entities.bullet import Bullet


class GameState(Enum):
    MAIN_MENU = auto()
    MAP_SELECT = auto()
    DIFFICULTY_SELECT = auto()
    GAME_PLAYING = auto()
    ROUND_END = auto()
    GAME_OVER = auto()
    PAUSED = auto()


class GameMode(Enum):
    PVP = auto()       # 双人对战
    PVAI = auto()      # 单人 vs AI
    PVPAI = auto()     # 双人 + AI


class Game:
    """游戏主控制器"""

    def __init__(self):
        self.state = GameState.MAIN_MENU
        self.mode = None
        self.difficulty = "hard"
        self.grid = None
        self.tanks = []
        self.ai_controllers = []   # 与 tanks 并行，非AI为None
        self.bullets = []
        self.win_score = DEFAULT_WIN_SCORE
        self.selected_map = 5       # 默认随机
        self.round_number = 0
        self.round_timer = 0.0
        self.round_result_text = ""
        self.countdown_timer = 0.0    # 开局3秒倒计时

        # 输入
        self._keys_prev = set()
        self._just_pressed = set()

        # 菜单
        self.menu_selection = 0
        self._menu_cooldown = 0.0   # 防按键重复

    # ================================================================
    # 输入
    # ================================================================

    def update_input(self):
        """每帧开始调用"""
        current = set()
        pressed = pygame.key.get_pressed()
        for key in range(len(pressed)):
            if pressed[key]:
                current.add(key)
        self._just_pressed = current - self._keys_prev
        self._keys_prev = current

    def just_pressed(self, key):
        return key in self._just_pressed

    # ================================================================
    # 菜单操作
    # ================================================================

    def handle_menu_input(self, dt):
        """处理菜单通用输入"""
        if self._menu_cooldown > 0:
            self._menu_cooldown -= dt
            return

        if self.state == GameState.MAIN_MENU:
            self._handle_main_menu()

        elif self.state == GameState.MAP_SELECT:
            self._handle_map_select()

        elif self.state == GameState.DIFFICULTY_SELECT:
            self._handle_difficulty_select()

        elif self.state == GameState.GAME_OVER:
            if self.just_pressed(K_r):
                self.start_game()
            elif self.just_pressed(K_m):
                self._go_main_menu()

    def _handle_main_menu(self):
        if self.just_pressed(K_1) or self.just_pressed(K_KP1):
            self.mode = GameMode.PVP
            self.state = GameState.MAP_SELECT
            self.menu_selection = 0
            self._menu_cooldown = 0.2
        elif self.just_pressed(K_2) or self.just_pressed(K_KP2):
            self.mode = GameMode.PVAI
            self.state = GameState.MAP_SELECT
            self.menu_selection = 0
            self._menu_cooldown = 0.2
        elif self.just_pressed(K_3) or self.just_pressed(K_KP3):
            self.mode = GameMode.PVPAI
            self.state = GameState.MAP_SELECT
            self.menu_selection = 0
            self._menu_cooldown = 0.2

    def _handle_map_select(self):
        if self.just_pressed(K_1):
            self.selected_map = 0; self._menu_cooldown = 0.15
        elif self.just_pressed(K_2):
            self.selected_map = 1; self._menu_cooldown = 0.15
        elif self.just_pressed(K_3):
            self.selected_map = 2; self._menu_cooldown = 0.15
        elif self.just_pressed(K_4):
            self.selected_map = 3; self._menu_cooldown = 0.15
        elif self.just_pressed(K_5):
            self.selected_map = 4; self._menu_cooldown = 0.15
        elif self.just_pressed(K_6):
            self.selected_map = 5; self._menu_cooldown = 0.15
        elif self.just_pressed(K_LEFT):
            self.win_score = self.prev_win_score()
            self._menu_cooldown = 0.15
        elif self.just_pressed(K_RIGHT):
            self.win_score = self.next_win_score()
            self._menu_cooldown = 0.15
        elif self.just_pressed(K_RETURN) or self.just_pressed(K_SPACE):
            self.confirm_map_selection()
            self._menu_cooldown = 0.3
        elif self.just_pressed(K_b):
            self.state = GameState.MAIN_MENU
            self._menu_cooldown = 0.3

    def confirm_map_selection(self):
        if self.mode in (GameMode.PVAI, GameMode.PVPAI):
            self.state = GameState.DIFFICULTY_SELECT
        else:
            self.start_game()

    def _handle_difficulty_select(self):
        if self.just_pressed(K_1):
            self.difficulty = "normal"; self._menu_cooldown = 0.3
            self.start_game()
        elif self.just_pressed(K_2):
            self.difficulty = "hard"; self._menu_cooldown = 0.3
            self.start_game()
        elif self.just_pressed(K_b):
            self.state = GameState.MAP_SELECT
            self._menu_cooldown = 0.3

    def next_win_score(self):
        opts = WIN_SCORE_OPTIONS
        idx = opts.index(self.win_score) if self.win_score in opts else 0
        return opts[(idx + 1) % len(opts)]

    def prev_win_score(self):
        opts = WIN_SCORE_OPTIONS
        idx = opts.index(self.win_score) if self.win_score in opts else 0
        return opts[(idx - 1) % len(opts)]

    def _go_main_menu(self):
        self.state = GameState.MAIN_MENU
        self.tanks.clear()
        self.ai_controllers.clear()
        self.bullets.clear()
        self.grid = None
        self.round_number = 0

    # ================================================================
    # 游戏初始化
    # ================================================================

    def start_game(self):
        """初始化游戏"""
        # 加载地图
        if self.selected_map == 5:
            from map.generator import generate_random_map
            self.grid = generate_random_map()
        else:
            from map.presets import get_preset
            self.grid = get_preset(self.selected_map)

        # 创建坦克
        self.tanks.clear()
        self.ai_controllers.clear()
        self.bullets.clear()
        self.round_number = 0

        if self.mode == GameMode.PVP:
            self.tanks.append(Tank(0, 0, name="玩家1",
                                   color_body=COLOR_P1_BODY, color_barrel=COLOR_P1_BARREL))
            self.tanks.append(Tank(0, 0, name="玩家2",
                                   color_body=COLOR_P2_BODY, color_barrel=COLOR_P2_BARREL))
            self.ai_controllers = [None, None]
        elif self.mode == GameMode.PVAI:
            self.tanks.append(Tank(0, 0, name="玩家",
                                   color_body=COLOR_P1_BODY, color_barrel=COLOR_P1_BARREL))
            self.tanks.append(Tank(0, 0, name="AI",
                                   color_body=COLOR_AI_BODY, color_barrel=COLOR_AI_BARREL,
                                   is_ai=True))
            from ai.controller import AIController
            self.ai_controllers = [None, AIController(self.difficulty)]
        elif self.mode == GameMode.PVPAI:
            self.tanks.append(Tank(0, 0, name="玩家1",
                                   color_body=COLOR_P1_BODY, color_barrel=COLOR_P1_BARREL))
            self.tanks.append(Tank(0, 0, name="玩家2",
                                   color_body=COLOR_P2_BODY, color_barrel=COLOR_P2_BARREL))
            self.tanks.append(Tank(0, 0, name="AI",
                                   color_body=COLOR_AI_BODY, color_barrel=COLOR_AI_BARREL,
                                   is_ai=True))
            from ai.controller import AIController
            self.ai_controllers = [None, None, AIController(self.difficulty)]

        self.state = GameState.GAME_PLAYING
        self.start_round()

    # ================================================================
    # 回合管理
    # ================================================================

    COUNTDOWN_DURATION = 3.0

    def start_round(self):
        """开始新回合"""
        self.round_number += 1
        self.bullets.clear()
        self.countdown_timer = self.COUNTDOWN_DURATION  # 开局倒计时

        if self.grid is not None:
            spawns = self.grid.pick_spawn_points(len(self.tanks))
            for i, tank in enumerate(self.tanks):
                if i < len(spawns):
                    col, row = spawns[i]
                    px, py = self.grid.cell_to_pixel(col, row)
                    # 面朝对方
                    tank.reset(px, py)

    def end_round(self, dead_tank, killer_tank):
        """处理回合结束"""
        dead_tank.alive = False

        # 击毁音效
        from sound.sounds import play_explosion
        play_explosion()

        if killer_tank is not None and killer_tank is not dead_tank:
            killer_tank.score += 1
            self.round_result_text = f"{killer_tank.name} 击毁了 {dead_tank.name}！"
        else:
            # 自毁：其他存活坦克得分
            for t in self.tanks:
                if t.alive and t is not dead_tank:
                    t.score += 1
            self.round_result_text = f"{dead_tank.name} 自毁了！"

        # 检查获胜
        for t in self.tanks:
            if t.score >= self.win_score:
                self.state = GameState.GAME_OVER
                return

        self.state = GameState.ROUND_END
        self.round_timer = 3.0

    # ================================================================
    # 主更新
    # ================================================================

    def update(self, dt):
        """每帧更新"""
        self.update_input()
        self.handle_menu_input(dt)

        if self.state == GameState.GAME_PLAYING:
            self._update_playing(dt)
        elif self.state == GameState.ROUND_END:
            self.round_timer -= dt
            if self.round_timer <= 0:
                self.start_round()
                self.state = GameState.GAME_PLAYING

    def _update_playing(self, dt):
        """游戏进行中的更新"""
        # ---- 倒计时：禁止所有操作 ----
        if self.countdown_timer > 0:
            self.countdown_timer -= dt
            return

        # ---- 玩家输入 ----
        keys = pygame.key.get_pressed()

        for i, tank in enumerate(self.tanks):
            if not tank.alive:
                continue

            if i == 0:  # 玩家1: WASD+F
                move_dir = 0
                if keys[P1_UP]: move_dir = 1
                elif keys[P1_DOWN]: move_dir = -1

                rotate_dir = 0
                if keys[P1_LEFT]: rotate_dir = -1
                elif keys[P1_RIGHT]: rotate_dir = 1

                tank.update(dt, move_dir, rotate_dir, self.grid)

                if keys[P1_FIRE]:
                    bullet = tank.shoot()
                    if bullet:
                        self.bullets.append(bullet)

            elif i == 1 and not tank.is_ai:  # 玩家2: 方向键+/
                move_dir = 0
                if keys[P2_UP]: move_dir = 1
                elif keys[P2_DOWN]: move_dir = -1

                rotate_dir = 0
                if keys[P2_LEFT]: rotate_dir = -1
                elif keys[P2_RIGHT]: rotate_dir = 1

                tank.update(dt, move_dir, rotate_dir, self.grid)

                if keys[P2_FIRE]:
                    bullet = tank.shoot()
                    if bullet:
                        self.bullets.append(bullet)

            # AI 坦克
            elif tank.is_ai and i < len(self.ai_controllers):
                ai = self.ai_controllers[i]
                if ai is not None:
                    enemy_tanks = [t for t in self.tanks if t is not tank]
                    move_dir, rot_dir, should_shoot = ai.update(
                        dt, tank, enemy_tanks, self.bullets, self.grid
                    )
                    tank.update(dt, move_dir, rot_dir, self.grid)
                    if should_shoot:
                        bullet = tank.shoot()
                        if bullet:
                            self.bullets.append(bullet)

        # ---- 更新炮弹 ----
        dead_tanks = []
        for bullet in self.bullets[:]:
            hit_tank = bullet.update(dt, self.grid, self.tanks)
            if not bullet.alive:
                self.bullets.remove(bullet)
            if hit_tank is not None and hit_tank.alive:
                # 确定击杀者
                killer = bullet.owner if bullet.owner.alive else None
                self.end_round(hit_tank, killer)
                break  # 一帧只处理一个死亡

        # ---- 坦克间碰撞 ----
        self._resolve_tank_collisions()

        # ---- 安全检查：确保所有坦克不嵌入墙壁 ----
        for tank in self.tanks:
            if tank.alive:
                self._clamp_tank_to_map(tank)

    def _resolve_tank_collisions(self):
        """处理坦克之间的碰撞（互相推开），并确保推后不嵌入墙壁"""
        alive_tanks = [t for t in self.tanks if t.alive]
        for i in range(len(alive_tanks)):
            for j in range(i + 1, len(alive_tanks)):
                a, b = alive_tanks[i], alive_tanks[j]
                a_corners = a.get_body_corners()
                b_corners = b.get_body_corners()
                if _rects_overlap(a_corners, b_corners):
                    dx = b.x - a.x
                    dy = b.y - a.y
                    dist = math.sqrt(dx**2 + dy**2) or 1.0
                    push = TANK_BODY_W * 0.7  # 安全推距
                    nx, ny = dx / dist, dy / dist
                    # 只推动，然后由 _clamp_to_valid 确保不嵌入墙壁
                    a.x -= nx * push * 0.5
                    a.y -= ny * push * 0.5
                    b.x += nx * push * 0.5
                    b.y += ny * push * 0.5
                    # 验证并修正：确保推后不嵌入墙壁
                    self._clamp_tank_to_map(a)
                    self._clamp_tank_to_map(b)

    def _clamp_tank_to_map(self, tank):
        """
        将坦克限制在地图可通行区域内。
        如果坦克嵌入墙壁，逐步向外移动直到脱离碰撞。
        """
        if self.grid is None:
            return

        margin = TANK_BODY_W / 2 + 4
        min_x = margin
        max_x = GRID_COLS * CELL_SIZE - margin
        min_y = GRID_OFFSET_Y + margin
        max_y = GRID_OFFSET_Y + GRID_ROWS * CELL_SIZE - margin

        # 硬夹到边界内
        tank.x = max(min_x, min(max_x, tank.x))
        tank.y = max(min_y, min(max_y, tank.y))

        # 逐步推出：尝试越来越大的步长直到脱离碰撞
        for step in [1, 2, 3, 5, 8, 12, 18, 25]:
            if not tank._check_collision_at(tank.x, tank.y, self.grid):
                return
            for dx, dy in [(step, 0), (-step, 0), (0, step), (0, -step),
                           (step, step), (-step, step), (step, -step), (-step, -step)]:
                tx = tank.x + dx
                ty = tank.y + dy
                if (min_x <= tx <= max_x and min_y <= ty <= max_y and
                        not tank._check_collision_at(tx, ty, self.grid)):
                    tank.x = tx
                    tank.y = ty
                    return

    # ================================================================
    # 渲染分发
    # ================================================================

    def draw(self, renderer):
        if self.state == GameState.MAIN_MENU:
            renderer.draw_menu_main(self)
        elif self.state == GameState.MAP_SELECT:
            renderer.draw_menu_map_select(self)
        elif self.state == GameState.DIFFICULTY_SELECT:
            renderer.draw_menu_difficulty(self)
        elif self.state in (GameState.GAME_PLAYING, GameState.ROUND_END, GameState.PAUSED):
            renderer.draw_game(self)
            if self.state == GameState.ROUND_END:
                renderer.draw_round_end(self)
            elif self.state == GameState.PAUSED:
                renderer.draw_pause()
        elif self.state == GameState.GAME_OVER:
            renderer.draw_game(self)
            renderer.draw_game_over(self)


# ================================================================
# 矩形重叠检测（用于坦克-坦克碰撞）
# ================================================================

def _rects_overlap(corners_a, corners_b):
    """两个凸四边形是否重叠（SAT简化版）"""
    # 用 AABB 快速测试
    def get_aabb(corners):
        xs = [p[0] for p in corners]
        ys = [p[1] for p in corners]
        return (min(xs), min(ys), max(xs), max(ys))

    ax1, ay1, ax2, ay2 = get_aabb(corners_a)
    bx1, by1, bx2, by2 = get_aabb(corners_b)

    return not (ax2 < bx1 or bx2 < ax1 or ay2 < by1 or by2 < ay1)
