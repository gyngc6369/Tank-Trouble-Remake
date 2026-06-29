"""
《坦克动荡》— 炮弹（极简版）
一步移动 + 墙壁反射 + 坦克命中。
"""

import math
from settings import *


class Bullet:
    """炮弹：圆形，墙壁反射，命中坦克消失"""

    def __init__(self, x, y, vx, vy, owner):
        self.x = float(x)
        self.y = float(y)
        self.vx = float(vx)
        self.vy = float(vy)
        self.owner = owner
        self.bounce_count = 0
        self.alive = True
        self.color = owner.color_body

    def update(self, dt, grid, tanks):
        """每帧更新。返回被命中的Tank或None。"""
        if not self.alive:
            return None

        # 一步移动
        self.x += self.vx * dt
        self.y += self.vy * dt

        # 墙壁碰撞：先查次数，再反射
        for wx1, wy1, wx2, wy2 in grid.get_wall_segments():
            if _dist_to_segment(self.x, self.y, wx1, wy1, wx2, wy2) <= BULLET_RADIUS + WALL_THICKNESS / 2:
                if self.bounce_count >= MAX_BOUNCES:
                    self.alive = False
                    return None
                self._reflect(wx1, wy1, wx2, wy2)
                self.bounce_count += 1
                break

        # 坦克碰撞
        from entities.tank import circle_rect_collision
        for tank in tanks:
            if tank.alive and circle_rect_collision(self.x, self.y, BULLET_RADIUS, tank.get_body_corners()):
                self.alive = False
                return tank

        return None

    def _reflect(self, wx1, wy1, wx2, wy2):
        """反射：v' = v - 2(v·n)n"""
        wdx = wx2 - wx1
        wdy = wy2 - wy1
        length = math.sqrt(wdx * wdx + wdy * wdy)
        if length == 0:
            return
        # 法向量（指向炮弹来向）
        nx = -wdy / length
        ny = wdx / length
        dot = self.vx * nx + self.vy * ny
        if dot > 0:
            nx, ny = -nx, -ny
        dot = self.vx * nx + self.vy * ny
        self.vx -= 2 * dot * nx
        self.vy -= 2 * dot * ny


def _dist_to_segment(px, py, x1, y1, x2, y2):
    """点到线段的最短距离"""
    dx = x2 - x1
    dy = y2 - y1
    if dx == 0 and dy == 0:
        return math.sqrt((px - x1) ** 2 + (py - y1) ** 2)
    t = ((px - x1) * dx + (py - y1) * dy) / (dx * dx + dy * dy)
    t = max(0.0, min(1.0, t))
    cpx = x1 + t * dx
    cpy = y1 + t * dy
    return math.sqrt((px - cpx) ** 2 + (py - cpy) ** 2)
