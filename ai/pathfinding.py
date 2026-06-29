"""
《坦克动荡》— AI 路径规划
BFS 最短路径 + 炮弹危险区域评估。
"""

import math
from collections import deque
from settings import *


def bfs_path(grid, start_cell, goal_cell, avoid_cells=None):
    """
    BFS 最短路径。
    返回从 start 到 goal 的格子路径 [(col,row), ...]，不含起点。
    若 avoid_cells 提供，优先避开但不完全禁止。
    """
    if avoid_cells is None:
        avoid_cells = set()

    start = tuple(start_cell)
    goal = tuple(goal_cell)

    if start == goal:
        return []

    visited = {start: None}  # cell → parent
    queue = deque([start])

    while queue:
        current = queue.popleft()

        for neighbor in grid.get_neighbors(current[0], current[1]):
            n = tuple(neighbor)
            if n not in visited:
                visited[n] = current
                if n == goal:
                    # 重建路径
                    path = []
                    node = n
                    while node != start:
                        parent = visited[node]
                        path.append(node)
                        node = parent
                    path.reverse()
                    return path
                queue.append(n)

    return None  # 不可达


def bfs_nearest(grid, start_cell, targets, avoid_cells=None):
    """
    BFS 找到最近的目标格子（从targets集合中）。
    返回 (path, target) 或 (None, None)。
    """
    if avoid_cells is None:
        avoid_cells = set()

    if not targets:
        return None, None

    start = tuple(start_cell)
    target_set = {tuple(t) for t in targets}

    if start in target_set:
        return [], start

    visited = {start: None}
    queue = deque([start])

    while queue:
        current = queue.popleft()
        for neighbor in grid.get_neighbors(current[0], current[1]):
            n = tuple(neighbor)
            if n not in visited:
                visited[n] = current
                if n in target_set:
                    # 重建路径
                    path = []
                    node = n
                    while node != start:
                        parent = visited[node]
                        path.append(node)
                        node = parent
                    path.reverse()
                    return path, n
                queue.append(n)

    return None, None


def get_danger_cells(grid, bullets, predict_time, current_cell):
    """预测炮弹轨迹，标记危险格子。步长100ms减少计算量。"""
    danger = set()
    if not bullets:
        return danger

    for bullet in bullets:
        if not bullet.alive:
            continue
        sim_x, sim_y = bullet.x, bullet.y
        sim_vx, sim_vy = bullet.vx, bullet.vy
        sim_bounces = bullet.bounce_count
        remaining = predict_time
        step = 0.1  # 100ms步长

        while remaining > 0 and sim_bounces <= MAX_BOUNCES:
            dt = min(step, remaining)
            sim_x += sim_vx * dt
            sim_y += sim_vy * dt
            remaining -= dt

            # 检查墙碰撞
            for wx1, wy1, wx2, wy2 in grid.get_wall_segments():
                if _pt_near_seg(sim_x, sim_y, wx1, wy1, wx2, wy2):
                    if sim_bounces >= MAX_BOUNCES:
                        remaining = 0
                        break
                    sim_vx, sim_vy = _reflect(sim_vx, sim_vy, wx1, wy1, wx2, wy2)
                    sim_bounces += 1
                    break
            col, row = grid.pixel_to_cell(sim_x, sim_y)
            danger.add((col, row))
    return danger


def _pt_near_seg(px, py, x1, y1, x2, y2):
    """点到线段距离 <= WALL_THICKNESS"""
    dx, dy = x2-x1, y2-y1
    if dx == 0 and dy == 0:
        return (px-x1)**2 + (py-y1)**2 <= (WALL_THICKNESS)**2
    t = max(0, min(1, ((px-x1)*dx+(py-y1)*dy)/(dx*dx+dy*dy)))
    cx, cy = x1+t*dx, y1+t*dy
    return (px-cx)**2 + (py-cy)**2 <= (WALL_THICKNESS)**2


def _reflect(vx, vy, wx1, wy1, wx2, wy2):
    """反射速度向量"""
    dx, dy = wx2-wx1, wy2-wy1
    length = math.sqrt(dx*dx+dy*dy)
    if length == 0:
        return vx, vy
    nx, ny = -dy/length, dx/length
    dot = vx*nx + vy*ny
    if dot > 0:
        nx, ny = -nx, -ny
    dot = vx*nx + vy*ny
    return vx - 2*dot*nx, vy - 2*dot*ny


def get_nearest_safe_cell(grid, current_cell, danger_cells):
    """
    找到最近的安全格子（不在 danger_cells 中）。
    返回 (col, row) 或 None。
    """
    current = tuple(current_cell)

    if current not in danger_cells:
        return current

    # BFS 从当前位置找最近的安全格
    visited = {current}
    queue = deque([current])

    while queue:
        c = queue.popleft()
        for neighbor in grid.get_neighbors(c[0], c[1]):
            n = tuple(neighbor)
            if n not in visited:
                if n not in danger_cells:
                    return n
                visited.add(n)
                queue.append(n)

    return current  # 无处可逃


def get_all_cells(grid):
    """返回所有格子坐标"""
    return [(c, r) for r in range(grid.rows) for c in range(grid.cols)]


def get_safe_cells(grid, danger_cells):
    """返回所有安全格子"""
    all_cells = get_all_cells(grid)
    return [c for c in all_cells if tuple(c) not in danger_cells]


