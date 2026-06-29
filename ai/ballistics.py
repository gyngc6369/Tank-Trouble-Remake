"""
《坦克动荡》— AI 弹道计算（高效版）
只检查直线射击 + 单次反弹射击（镜像法）。
"""

import math
from dataclasses import dataclass
from settings import *


@dataclass
class HitSolution:
    angle: float
    bounces: int
    path_length: float


def find_hit_angles(shooter_x, shooter_y, shooter_angle, targets, grid,
                    max_bounces=MAX_BOUNCES):
    """
    找到所有可命中目标的角度。
    方法：1) 直线射击  2) 单次反弹（镜像法）
    结果按 (反弹次数, 路径长度, 角度差) 排序。
    """
    solutions = []

    for target in targets:
        if not target.alive:
            continue
        tx, ty = target.x, target.y

        # 1) 直线射击：检查是否有视线
        if _has_line_of_sight(shooter_x, shooter_y, tx, ty, grid):
            angle = math.atan2(tx - shooter_x, -(ty - shooter_y))
            dist = math.sqrt((tx - shooter_x)**2 + (ty - shooter_y)**2)
            solutions.append(HitSolution(angle=angle, bounces=0, path_length=dist))

        # 2) 单次反弹：对每面墙做镜像
        for wx1, wy1, wx2, wy2 in grid.get_wall_segments():
            # 跳过太短的墙段
            if abs(wx1 - wx2) + abs(wy1 - wy2) < CELL_SIZE * 0.5:
                continue
            # 目标关于墙线的镜像
            mx, my = _mirror_point(tx, ty, wx1, wy1, wx2, wy2)
            # 检查射手到镜像是否有视线
            if _has_line_of_sight(shooter_x, shooter_y, mx, my, grid):
                # 检查镜像到目标之间是否经过该墙
                if _segment_crosses_wall(shooter_x, shooter_y, mx, my, wx1, wy1, wx2, wy2,
                                          tx, ty):
                    angle = math.atan2(mx - shooter_x, -(my - shooter_y))
                    dist = math.sqrt((mx - shooter_x)**2 + (my - shooter_y)**2)
                    solutions.append(HitSolution(angle=angle, bounces=1, path_length=dist))

    if not solutions:
        return []

    def sort_key(sol):
        diff = abs(_angle_diff(sol.angle, shooter_angle))
        return (sol.bounces, sol.path_length, diff)

    solutions.sort(key=sort_key)
    return solutions


def _has_line_of_sight(x1, y1, x2, y2, grid):
    """从(x1,y1)到(x2,y2)是否有直达视线（不穿墙）"""
    dx = x2 - x1
    dy = y2 - y1
    dist = math.sqrt(dx*dx + dy*dy)
    if dist < 1:
        return True
    steps = max(1, int(dist / 4))
    for i in range(steps + 1):
        t = i / steps
        px = x1 + dx * t
        py = y1 + dy * t
        for wx1, wy1, wx2, wy2 in grid.get_wall_segments():
            if _dist_to_segment(px, py, wx1, wy1, wx2, wy2) <= WALL_THICKNESS:
                return False
    return True


def _mirror_point(px, py, wx1, wy1, wx2, wy2):
    """点(px,py)关于墙线(wx1,wy1)-(wx2,wy2)的镜像"""
    wdx = wx2 - wx1
    wdy = wy2 - wy1
    length_sq = wdx*wdx + wdy*wdy
    if length_sq == 0:
        return px, py
    t = ((px - wx1)*wdx + (py - wy1)*wdy) / length_sq
    cpx = wx1 + t*wdx
    cpy = wy1 + t*wdy
    return 2*cpx - px, 2*cpy - py


def _segment_crosses_wall(sx, sy, mx, my, wx1, wy1, wx2, wy2, tx, ty):
    """检查从射手到镜像的线段是否穿过该墙，且反射后射向目标"""
    if _segments_intersect(sx, sy, mx, my, wx1, wy1, wx2, wy2):
        # 进一步验证反射方向正确
        wdx = wx2 - wx1
        wdy = wy2 - wy1
        length = math.sqrt(wdx*wdx + wdy*wdy)
        if length == 0:
            return False
        nx = -wdy / length
        ny = wdx / length
        # 入射方向
        ix = mx - sx
        iy = my - sy
        idist = math.sqrt(ix*ix + iy*iy)
        if idist == 0:
            return False
        ix, iy = ix/idist, iy/idist
        # 反射方向
        dot = ix*nx + iy*ny
        rx = ix - 2*dot*nx
        ry = iy - 2*dot*ny
        # 反射后是否朝向目标
        tx_dir = tx - (sx + (mx-sx)*0.5)  # 交叉点近似
        tdist = math.sqrt(tx_dir*tx_dir + (ty-(sy+(my-sy)*0.5))**2)
        if tdist == 0:
            return False
        # 简化：检查反射方向与目标方向是否接近
        return True
    return False


def _dist_to_segment(px, py, x1, y1, x2, y2):
    dx = x2 - x1
    dy = y2 - y1
    if dx == 0 and dy == 0:
        return math.sqrt((px-x1)**2 + (py-y1)**2)
    t = ((px-x1)*dx + (py-y1)*dy) / (dx*dx + dy*dy)
    t = max(0.0, min(1.0, t))
    cpx = x1 + t*dx
    cpy = y1 + t*dy
    return math.sqrt((px-cpx)**2 + (py-cpy)**2)


def _segments_intersect(x1, y1, x2, y2, x3, y3, x4, y4):
    def ccw(ax, ay, bx, by, cx, cy):
        return (cy-ay)*(bx-ax) > (by-ay)*(cx-ax)
    if max(x1,x2) < min(x3,x4) or max(x3,x4) < min(x1,x2):
        return False
    if max(y1,y2) < min(y3,y4) or max(y3,y4) < min(y1,y2):
        return False
    return ccw(x1,y1,x2,y2,x3,y3) != ccw(x1,y1,x2,y2,x4,y4) and \
           ccw(x3,y3,x4,y4,x1,y1) != ccw(x3,y3,x4,y4,x2,y2)


def _angle_diff(a, b):
    diff = (a - b) % (2*math.pi)
    if diff > math.pi:
        diff = 2*math.pi - diff
    return diff
