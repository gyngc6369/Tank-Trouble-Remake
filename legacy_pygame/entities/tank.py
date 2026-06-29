"""
《坦克动荡》— 坦克实体
矩形车身 + 细炮管，平滑转向，弹药系统。
"""

import math
import pygame
from settings import *


class Tank:
    """坦克实体：矩形车身 + 炮管"""

    def __init__(self, x, y, angle=0.0, name="坦克",
                 color_body=COLOR_P1_BODY, color_barrel=COLOR_P1_BARREL,
                 is_ai=False):
        self.x = float(x)
        self.y = float(y)
        self.angle = float(angle)          # 弧度，0=↑，顺时针增加
        self.name = name
        self.color_body = color_body
        self.color_barrel = color_barrel
        self.is_ai = is_ai

        # 状态
        self.alive = True
        self.score = 0

        # 弹药
        self.ammo = MAX_AMMO
        self.ammo_timer = 0.0

        # 射击冷却（防连发）
        self.shoot_cooldown = 0.0

    @property
    def angle_deg(self):
        """角度（度），用于 pygame.transform.rotate"""
        return math.degrees(self.angle)

    def reset(self, x, y, angle=0.0):
        """重置状态（新回合）"""
        self.x = float(x)
        self.y = float(y)
        self.angle = float(angle)
        self.alive = True
        self.ammo = MAX_AMMO
        self.ammo_timer = 0.0
        self.shoot_cooldown = 0.0

    # ---- 几何 ----

    def get_body_corners(self):
        """返回旋转后车身矩形的四个角点 [(x,y), ...]，顺序：左上→右上→右下→左下"""
        hw = TANK_BODY_W / 2  # 半宽
        hh = TANK_BODY_H / 2  # 半高

        # 未旋转时的四个角（相对于中心，angle=0 时车身宽沿X，高沿Y向上）
        # angle=0 时坦克面朝上，所以车身高沿Y轴负方向
        local_corners = [
            (-hw, -hh),  # 左上（前左）
            (hw, -hh),  # 右上（前右）
            (hw, hh),   # 右下（后右）
            (-hw, hh),  # 左下（后左）
        ]

        cos_a = math.cos(self.angle)
        sin_a = math.sin(self.angle)

        corners = []
        for lx, ly in local_corners:
            # 旋转
            rx = lx * cos_a - ly * sin_a
            ry = lx * sin_a + ly * cos_a
            corners.append((self.x + rx, self.y + ry))

        return corners

    def get_body_edges(self):
        """返回车身矩形的四条边 [(x1,y1,x2,y2), ...]"""
        corners = self.get_body_corners()
        edges = []
        for i in range(4):
            x1, y1 = corners[i]
            x2, y2 = corners[(i + 1) % 4]
            edges.append((x1, y1, x2, y2))
        return edges

    def get_barrel_tip(self):
        """返回炮口尖端坐标"""
        # 炮管从车身中心沿 angle 方向伸出
        half_body = TANK_BODY_H / 2
        tip_dist = half_body + BARREL_LENGTH
        tip_x = self.x + math.sin(self.angle) * tip_dist
        tip_y = self.y - math.cos(self.angle) * tip_dist
        return (tip_x, tip_y)

    def get_barrel_base(self):
        """返回炮管根部坐标（车身前边中点）"""
        half_body = TANK_BODY_H / 2
        bx = self.x + math.sin(self.angle) * half_body
        by = self.y - math.cos(self.angle) * half_body
        return (bx, by)

    # ---- 移动 ----

    def move(self, dt, direction, grid):
        """
        前进/后退。
        direction: 1=前进, -1=后退, 0=不动
        返回是否成功移动。
        """
        if direction == 0:
            return False

        dist = TANK_SPEED * dt * direction
        new_x = self.x + math.sin(self.angle) * dist
        new_y = self.y - math.cos(self.angle) * dist

        # 碰撞检测
        if self._check_collision_at(new_x, new_y, grid):
            return False

        self.x = new_x
        self.y = new_y
        return True

    def rotate(self, dt, direction, grid):
        """
        旋转。
        direction: 1=右转, -1=左转, 0=不转
        返回是否成功旋转。
        """
        if direction == 0:
            return False

        delta = math.radians(TANK_ROTATION_SPEED) * dt * direction
        new_angle = self.angle + delta

        # 检测旋转后是否会撞墙
        old_angle = self.angle
        self.angle = new_angle
        if self._check_collision_at(self.x, self.y, grid):
            self.angle = old_angle
            return False

        return True

    def _check_collision_at(self, x, y, grid):
        """检测车身在(x,y)处是否与任何墙相交"""
        if grid is None:
            return False

        hw = TANK_BODY_W / 2
        hh = TANK_BODY_H / 2
        cos_a = math.cos(self.angle)
        sin_a = math.sin(self.angle)
        corners = []
        for lx, ly in [(-hw, -hh), (hw, -hh), (hw, hh), (-hw, hh)]:
            corners.append((x + lx * cos_a - ly * sin_a,
                            y + lx * sin_a + ly * cos_a))

        for i in range(4):
            ax1, ay1 = corners[i]
            ax2, ay2 = corners[(i + 1) % 4]
            for wx1, wy1, wx2, wy2 in grid.get_wall_segments():
                if _segments_intersect(ax1, ay1, ax2, ay2, wx1, wy1, wx2, wy2):
                    return True
        return False

    # ---- 射击 ----

    def shoot(self):
        """尝试射击，返回 Bullet 或 None"""
        if self.ammo <= 0:
            return None
        if self.shoot_cooldown > 0:
            return None

        self.ammo -= 1
        self.shoot_cooldown = 0.15  # 最小射击间隔 150ms

        # 射击音效
        from sound.sounds import play_shoot
        play_shoot()

        tip_x, tip_y = self.get_barrel_tip()
        vx = math.sin(self.angle) * BULLET_SPEED
        vy = -math.cos(self.angle) * BULLET_SPEED

        from entities.bullet import Bullet
        return Bullet(tip_x, tip_y, vx, vy, self)

    # ---- 弹药回复 ----

    def update_ammo(self, dt):
        """弹药回复计时"""
        if self.ammo < MAX_AMMO:
            self.ammo_timer += dt
            while self.ammo_timer >= AMMO_REGEN_TIME and self.ammo < MAX_AMMO:
                self.ammo_timer -= AMMO_REGEN_TIME
                self.ammo += 1

    # ---- 更新 ----

    def update(self, dt, move_dir, rotate_dir, grid):
        """每帧更新"""
        if not self.alive:
            return

        # 冷却递减
        if self.shoot_cooldown > 0:
            self.shoot_cooldown -= dt

        # 弹药回复
        self.update_ammo(dt)

        # 旋转先于移动（更自然）
        self.rotate(dt, rotate_dir, grid)
        self.move(dt, move_dir, grid)


# ================================================================
# 碰撞检测工具函数
# ================================================================

def _segments_intersect(x1, y1, x2, y2, x3, y3, x4, y4):
    """两条线段是否相交（包括端点接触）"""
    def ccw(ax, ay, bx, by, cx, cy):
        return (cy - ay) * (bx - ax) > (by - ay) * (cx - ax)

    def on_segment(ax, ay, bx, by, cx, cy):
        return (min(ax, bx) <= cx <= max(ax, bx) and
                min(ay, by) <= cy <= max(ay, by))

    # 快速排斥
    if (max(x1, x2) < min(x3, x4) or max(x3, x4) < min(x1, x2) or
            max(y1, y2) < min(y3, y4) or max(y3, y4) < min(y1, y2)):
        return False

    # 跨立测试
    d1 = ccw(x1, y1, x2, y2, x3, y3) != ccw(x1, y1, x2, y2, x4, y4)
    d2 = ccw(x3, y3, x4, y4, x1, y1) != ccw(x3, y3, x4, y4, x2, y2)

    if d1 and d2:
        return True

    # 共线端点情况
    if (on_segment(x1, y1, x2, y2, x3, y3) or
        on_segment(x1, y1, x2, y2, x4, y4) or
        on_segment(x3, y3, x4, y4, x1, y1) or
            on_segment(x3, y3, x4, y4, x2, y2)):
        return True

    return False


def circle_rect_collision(cx, cy, radius, rect_corners):
    """
    圆与旋转矩形的碰撞检测（最近点法）。
    rect_corners: 矩形的四个角点 [(x,y), ...]
    """
    # 找矩形上距圆心最近的点
    closest_dist_sq = float('inf')
    closest_point = None

    for i in range(4):
        ax1, ay1 = rect_corners[i]
        ax2, ay2 = rect_corners[(i + 1) % 4]

        # 圆心到线段的最短距离
        cpx, cpy = _closest_point_on_segment(cx, cy, ax1, ay1, ax2, ay2)
        dist_sq = (cx - cpx) ** 2 + (cy - cpy) ** 2
        if dist_sq < closest_dist_sq:
            closest_dist_sq = dist_sq
            closest_point = (cpx, cpy)

    return closest_dist_sq <= radius ** 2


def _closest_point_on_segment(px, py, x1, y1, x2, y2):
    """点 (px,py) 到线段 (x1,y1)-(x2,y2) 的最近点"""
    dx = x2 - x1
    dy = y2 - y1
    if dx == 0 and dy == 0:
        return (x1, y1)

    t = ((px - x1) * dx + (py - y1) * dy) / (dx * dx + dy * dy)
    t = max(0.0, min(1.0, t))
    return (x1 + t * dx, y1 + t * dy)
