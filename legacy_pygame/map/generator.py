"""
《坦克动荡》— 随机地图生成器
使用 Recursive Backtracker (DFS) 生成迷宫，再打通额外墙增加连通度。
"""

import random
from map.grid import Grid
from settings import *


def generate_random_map(cols=GRID_COLS, rows=GRID_ROWS, extra_ratio=0.20) -> Grid:
    """
    生成随机地图：
    1. 全封闭初始化
    2. DFS 挖墙 → 生成树
    3. 额外打通约 extra_ratio 比例的非树墙 → 增加回路
    4. BFS 验证连通性
    """
    grid = Grid(cols, rows)

    # ---- Step 1: DFS 挖墙 (Recursive Backtracker) ----
    visited = [[False] * cols for _ in range(rows)]
    stack = []
    start_c = random.randrange(cols)
    start_r = random.randrange(rows)
    visited[start_r][start_c] = True
    stack.append((start_c, start_r))

    # 方向：(dc, dr, wall_dir)
    dirs = [(0, -1, 0), (1, 0, 1), (0, 1, 2), (-1, 0, 3)]

    while stack:
        c, r = stack[-1]

        # 找未访问的邻居
        neighbors = []
        for dc, dr, wall_dir in dirs:
            nc, nr = c + dc, r + dr
            if 0 <= nc < cols and 0 <= nr < rows and not visited[nr][nc]:
                neighbors.append((nc, nr, wall_dir))

        if neighbors:
            nc, nr, wall_dir = random.choice(neighbors)
            grid.remove_wall(c, r, wall_dir)       # 打通当前格子该方向墙
            visited[nr][nc] = True
            stack.append((nc, nr))
        else:
            stack.pop()  # 回溯

    # ---- Step 2: 额外打通墙 ----
    # 收集所有内部墙（非边界、当前存在的）
    internal_walls = []
    for r in range(rows):
        for c in range(cols):
            # 右墙（非最右列）
            if c < cols - 1 and grid.walls[r][c][1]:
                internal_walls.append((c, r, 1))
            # 下墙（非最下行）
            if r < rows - 1 and grid.walls[r][c][2]:
                internal_walls.append((c, r, 2))

    # 随机打乱，按比例打通
    random.shuffle(internal_walls)
    extra_count = int(len(internal_walls) * extra_ratio)
    for i in range(extra_count):
        c, r, d = internal_walls[i]
        grid.remove_wall(c, r, d)

    # ---- Step 3: 验证连通性 ----
    if not grid.check_all_reachable():
        # 极少情况：额外打通过程中出了问题，手动修复
        _fix_connectivity(grid)

    return grid


def _fix_connectivity(grid: Grid):
    """如果存在不连通区域，打通它们之间的墙"""
    all_cells = [(c, r) for r in range(grid.rows) for c in range(grid.cols)]
    reachable = grid.bfs_reachable(all_cells[0][0], all_cells[0][1])

    while len(reachable) < len(all_cells):
        unreachable = [cell for cell in all_cells if cell not in reachable]
        # 找 reachable 和 unreachable 之间的最近墙
        fixed = False
        for uc, ur in unreachable:
            dirs = [(0, -1, 0), (1, 0, 1), (0, 1, 2), (-1, 0, 3)]
            for dc, dr, wall_dir in dirs:
                nc, nr = uc + dc, ur + dr
                if (nc, nr) in reachable:
                    grid.remove_wall(uc, ur, wall_dir)
                    fixed = True
                    break
            if fixed:
                break

        reachable = grid.bfs_reachable(all_cells[0][0], all_cells[0][1])
