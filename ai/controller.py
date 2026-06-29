"""
《坦克动荡》— AI 控制器
持续行为 + 定时决策 + 连射。
"""

import math
import random
from settings import *
from ai.pathfinding import bfs_path, get_danger_cells, get_nearest_safe_cell
from ai.ballistics import find_hit_angles


class AIController:
    """AI 坦克决策控制器"""

    def __init__(self, difficulty="hard"):
        self.difficulty = difficulty
        self._set_params()

        # 决策计时器
        self.ballistics_timer = 0.0
        self.pathfind_timer = 0.0
        self.shoot_timer = 0.0

        # 持续行为状态（帧间保持）
        self.current_move_dir = 0    # -1/0/1
        self.current_rotate_dir = 0  # -1/0/1
        self.current_path = []       # 当前路径
        self.target_angle = None     # 目标朝向（弧度）

        # 连射
        self.burst_shots_remaining = 0
        self.burst_angle = 0.0
        self._burst_timer = 0.0      # 超时保护

    def _set_params(self):
        if self.difficulty == "hard":
            self.ballistics_interval = 0.3
            self.shoot_cooldown = 0.5
            self.dodge_predict_time = 1.5
            self.dodge_reaction_delay = 0.15
            self.pathfind_interval = 0.2
            self.burst_count = 3
        else:
            self.ballistics_interval = 1.0
            self.shoot_cooldown = 1.5
            self.dodge_predict_time = 0.8
            self.dodge_reaction_delay = 0.5
            self.pathfind_interval = 0.6
            self.burst_count = 2

    def update(self, dt, tank, enemy_tanks, bullets, grid):
        """
        每帧更新。返回 (move_dir, rotate_dir, should_shoot)。
        move_dir: 1/-1/0, rotate_dir: 1/-1/0, should_shoot: bool
        """
        if not tank.alive:
            return 0, 0, False

        self.ballistics_timer += dt
        self.pathfind_timer += dt
        self.shoot_timer += dt

        alive_enemies = [t for t in enemy_tanks if t.alive]
        if not alive_enemies:
            return 0, 0, False

        # ---- 持续行为：连射（不阻塞移动和路径规划） ----
        burst_shot_this_frame = False
        if self.burst_shots_remaining > 0:
            self._burst_timer += dt
            if self._burst_timer > 1.5:  # 超时放弃
                self.burst_shots_remaining = 0
                self._burst_timer = 0.0
            else:
                angle_diff = _normalize_angle(self.burst_angle - tank.angle)
                if abs(angle_diff) < math.radians(5):
                    self.burst_shots_remaining -= 1
                    self._burst_timer = 0.0
                    burst_shot_this_frame = True
                else:
                    self.current_rotate_dir = 1 if angle_diff > 0 else -1

        # ---- 决策周期：重算策略 ----
        should_shoot = burst_shot_this_frame
        move_dir = self.current_move_dir
        rotate_dir = self.current_rotate_dir

        # 弹道计算
        if self.ballistics_timer >= self.ballistics_interval:
            self.ballistics_timer = 0.0
            if tank.ammo >= self.burst_count:
                tip_x, tip_y = tank.get_barrel_tip()
                solutions = find_hit_angles(tip_x, tip_y, tank.angle, alive_enemies, grid)
                if solutions:
                    best = solutions[0]
                    self.burst_angle = best.angle
                    self.burst_shots_remaining = self.burst_count

        # 路径规划
        if self.pathfind_timer >= self.pathfind_interval:
            self.pathfind_timer = 0.0
            current_cell = grid.pixel_to_cell(tank.x, tank.y)
            danger = get_danger_cells(grid, bullets, self.dodge_predict_time, current_cell)

            if tuple(current_cell) in danger:
                # 躲避
                safe = get_nearest_safe_cell(grid, current_cell, danger)
                if safe and safe != tuple(current_cell):
                    self.current_path = bfs_path(grid, current_cell, safe) or []
            else:
                # 战术移动：向最近敌人靠近
                nearest = min(alive_enemies,
                    key=lambda e: (grid.pixel_to_cell(e.x, e.y)[0]-current_cell[0])**2 +
                                  (grid.pixel_to_cell(e.x, e.y)[1]-current_cell[1])**2)
                target_cell = grid.pixel_to_cell(nearest.x, nearest.y)
                path = bfs_path(grid, current_cell, target_cell, danger)
                if path:
                    self.current_path = path

        # ---- 每帧：沿路径移动/转向 ----
        if self.current_path:
            current_cell = grid.pixel_to_cell(tank.x, tank.y)
            next_cell = self.current_path[0]

            if tuple(current_cell) == tuple(next_cell):
                self.current_path.pop(0)
                if not self.current_path:
                    self.current_move_dir = 0
                    self.current_rotate_dir = 0
                    return 0, 0, should_shoot
                next_cell = self.current_path[0]

            target_px, target_py = grid.cell_to_pixel(next_cell[0], next_cell[1])
            desired_angle = math.atan2(target_px - tank.x, -(target_py - tank.y))
            angle_diff = _normalize_angle(desired_angle - tank.angle)

            if abs(angle_diff) > math.radians(12):
                self.current_rotate_dir = 1 if angle_diff > 0 else -1
                self.current_move_dir = 0
            else:
                self.current_rotate_dir = 0
                self.current_move_dir = 1

        return self.current_move_dir, self.current_rotate_dir, should_shoot


def _normalize_angle(angle):
    while angle > math.pi:
        angle -= 2 * math.pi
    while angle < -math.pi:
        angle += 2 * math.pi
    return angle
