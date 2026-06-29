"""
《坦克动荡》— 5张预设地图
每张地图以 (col, row, direction) 列表的形式定义需要移除的墙。
Direction: 0=上 1=右 2=下 3=左
"""

from map.grid import Grid


# ---- 预设地图定义 ----
# 每张预设是一个函数，接收全封闭 Grid，移除特定墙形成迷宫

def _preset_open(grid: Grid):
    """开放式：少量墙，大面积开阔区域"""
    # 先移除所有内部墙（造出一个完全开放的空间）
    for r in range(grid.rows):
        for c in range(grid.cols):
            if c < grid.cols - 1:
                grid.remove_wall(c, r, 1)  # 右墙
            if r < grid.rows - 1:
                grid.remove_wall(c, r, 2)  # 下墙

    # 然后放回一些障碍墙，制造简单掩体
    obstacles = [
        (3, 2, 0), (5, 3, 1), (7, 4, 0), (10, 5, 1),
        (4, 7, 0), (8, 6, 1), (11, 3, 0), (2, 5, 1),
        (6, 8, 0), (9, 2, 1), (12, 7, 0), (3, 8, 1),
        # 加一些竖直障碍
        (3, 2, 3), (8, 3, 3), (6, 5, 3), (11, 6, 3),
        (7, 7, 3), (4, 4, 3),
    ]
    for c, r, d in obstacles:
        if 0 <= c < grid.cols and 0 <= r < grid.rows:
            grid.remove_wall(c, r, d)  # 这里 re-add wall (by toggling)...

    # 实际上：在全开放基础上无法简单"放回"墙。
    # 换思路：手动指定哪些墙保留。
    return grid


def _preset_corridors(grid: Grid):
    """走廊式：长走廊连接多个房间"""
    # 走廊式布局：不采用全开+放回，直接回到初始全墙再打通
    # 先重置
    for r in range(grid.rows):
        for c in range(grid.cols):
            grid.walls[r][c] = [True, True, True, True]

    # 主水平走廊（第2行、第5行、第8行）
    for c in range(grid.cols - 1):
        for row in [2, 5, 8]:
            grid.remove_wall(c, row, 1)  # 打通左右

    # 主垂直走廊（第3列、第7列、第11列）
    for r in range(grid.rows - 1):
        for col in [3, 7, 11]:
            grid.remove_wall(col, r, 2)  # 打通上下

    # 额外的房间开口
    extra_opens = [
        (1, 3, 1), (4, 3, 1), (9, 3, 1), (13, 3, 1),
        (1, 7, 1), (4, 7, 1), (9, 7, 1), (13, 7, 1),
    ]
    for c, r, d in extra_opens:
        grid.remove_wall(c, r, d)

    return grid


def _preset_symmetric(grid: Grid):
    """对称式：左右对称布局，适合公平对战"""
    for r in range(grid.rows):
        for c in range(grid.cols):
            grid.walls[r][c] = [True, True, True, True]

    mid = grid.cols // 2  # 7

    # 左右对称的水平通道
    for c in range(grid.cols - 1):
        for row in [1, 3, 5, 7, 9]:
            grid.remove_wall(c, row, 1)

    # 垂直通道（保持左右对称）
    for r in range(grid.rows - 1):
        for col in [2, 5, mid, 9, 12]:
            grid.remove_wall(col, r, 2)

    # 中心区域加一些障碍
    for r in [2, 4, 6, 8]:
        grid.remove_wall(mid - 1, r, 1)  # 中心线打通

    return grid


def _preset_spiral(grid: Grid):
    """螺旋式：绕圈路径"""
    for r in range(grid.rows):
        for c in range(grid.cols):
            grid.walls[r][c] = [True, True, True, True]

    # 外圈
    for c in range(1, grid.cols - 2):
        grid.remove_wall(c, 1, 1)
    for r in range(1, grid.rows - 2):
        grid.remove_wall(grid.cols - 2, r, 2)
    for c in range(2, grid.cols - 1):
        grid.remove_wall(c, grid.rows - 2, 1)
    for r in range(2, grid.rows - 1):
        grid.remove_wall(1, r, 2)

    # 内圈
    for c in range(3, grid.cols - 4):
        grid.remove_wall(c, 3, 1)
    for r in range(3, grid.rows - 4):
        grid.remove_wall(grid.cols - 4, r, 2)
    for c in range(4, grid.cols - 3):
        grid.remove_wall(c, grid.rows - 4, 1)
    for r in range(4, grid.rows - 3):
        grid.remove_wall(3, r, 2)

    # 开口连接内外圈
    grid.remove_wall(5, 2, 2)   # 入口
    grid.remove_wall(5, 4, 0)   # 出口
    grid.remove_wall(9, 6, 2)

    return grid


def _preset_grid(grid: Grid):
    """格栅式：规则网格状，十字交错"""
    for r in range(grid.rows):
        for c in range(grid.cols):
            grid.walls[r][c] = [True, True, True, True]

    # 每隔一格打通水平
    for r in [1, 2, 4, 5, 7, 8, 10]:
        for c in range(grid.cols - 1):
            grid.remove_wall(c, r, 1)

    # 每隔一格打通垂直
    for c in [1, 2, 4, 5, 7, 8, 10, 12, 13]:
        for r in range(grid.rows - 1):
            grid.remove_wall(c, r, 2)

    # 四角开口
    grid.remove_wall(0, 0, 1)
    grid.remove_wall(0, 0, 2)

    return grid


# ---- 预设注册表 ----

PRESET_NAMES = [
    "开放空间",
    "走廊迷宫",
    "对称战场",
    "螺旋路径",
    "格栅街区",
]

PRESET_BUILDERS = [
    _preset_open,
    _preset_corridors,
    _preset_symmetric,
    _preset_spiral,
    _preset_grid,
]


def get_preset(index: int) -> Grid:
    """
    加载第 index 张预设地图（0-4）。
    返回已通过连通性验证的 Grid。
    """
    if not (0 <= index < len(PRESET_BUILDERS)):
        raise IndexError(f"预设地图 index={index} 超出范围 (0-{len(PRESET_BUILDERS)-1})")

    grid = Grid()
    builder = PRESET_BUILDERS[index]

    # 对于开放地图，特殊处理
    if index == 0:
        _build_open_map(grid)
    else:
        builder(grid)

    # 验证连通性
    if not grid.check_all_reachable():
        # 如果存在孤立区域，强制打通直到连通
        _ensure_connected(grid)

    return grid


def _build_open_map(grid: Grid):
    """开放地图：大面积开阔 + 散布掩体"""
    # 先打通所有内部墙
    for r in range(grid.rows):
        for c in range(grid.cols):
            if c < grid.cols - 1:
                grid.remove_wall(c, r, 1)
            if r < grid.rows - 1:
                grid.remove_wall(c, r, 2)

    # 无法在已删除的墙上放回，所以采用另一种策略：
    # 重新初始化，手动定义保留的墙
    for r in range(grid.rows):
        for c in range(grid.cols):
            grid.walls[r][c] = [True, True, True, True]

    # 打通大量通道，只保留少量障碍
    _keep_only_walls(grid, [
        # 障碍墙：(col, row, dir)
        (2, 2, 0), (5, 3, 1), (8, 2, 0), (11, 4, 1),
        (3, 6, 0), (7, 5, 1), (10, 7, 0), (13, 3, 1),
        (4, 8, 0), (9, 6, 1), (1, 4, 3), (6, 9, 3),
        (12, 6, 3), (8, 8, 3),
    ])


def _keep_only_walls(grid: Grid, walls_to_keep: list):
    """先打开所有内部墙，再根据列表放回特定墙"""
    # Step 1: 打开所有内部通道
    for r in range(grid.rows):
        for c in range(grid.cols):
            if c < grid.cols - 1:
                grid.remove_wall(c, r, 1)
            if r < grid.rows - 1:
                grid.remove_wall(c, r, 2)

    # Step 2: 放回（实际上无法放回已删除的墙）
    # 这里改用：重新初始化
    for r in range(grid.rows):
        for c in range(grid.cols):
            grid.walls[r][c] = [True, True, True, True]

    # Step 3: 打开除了walls_to_keep之外的所有内部墙
    keep_set = set()
    for c, r, d in walls_to_keep:
        keep_set.add((c, r, d))
        # 也保留邻居对应的墙
        if d == 0 and r > 0:
            keep_set.add((c, r - 1, 2))
        elif d == 2 and r < grid.rows - 1:
            keep_set.add((c, r + 1, 0))
        elif d == 1 and c < grid.cols - 1:
            keep_set.add((c + 1, r, 3))
        elif d == 3 and c > 0:
            keep_set.add((c - 1, r, 1))

    for r in range(grid.rows):
        for c in range(grid.cols):
            for d in [0, 1, 2, 3]:
                if (c, r, d) not in keep_set:
                    grid.remove_wall(c, r, d)


def _ensure_connected(grid: Grid):
    """确保所有格子连通：找到孤立区域，打通它们之间的墙"""
    all_cells = [(c, r) for r in range(grid.rows) for c in range(grid.cols)]
    start = all_cells[0]
    reachable = grid.bfs_reachable(start[0], start[1])
    unreachable = [c for c in all_cells if c not in reachable]

    while unreachable:
        # 找到 reachable 和 unreachable 之间的一对邻居
        found = False
        for uc, ur in unreachable:
            for dc, dr in [(0, -1), (1, 0), (0, 1), (-1, 0)]:
                nc, nr = uc + dc, ur + dr
                if (nc, nr) in reachable:
                    # 打通两者之间的墙
                    d = [(0, -1), (1, 0), (0, 1), (-1, 0)].index((dc, dr))
                    # 方向映射：(0,-1)=上=0, (1,0)=右=1, (0,1)=下=2, (-1,0)=左=3
                    wall_dir = [(0, -1, 0), (1, 0, 1), (0, 1, 2), (-1, 0, 3)]
                    for wdc, wdr, wd in wall_dir:
                        if (wdc, wdr) == (dc, dr):
                            grid.remove_wall(uc, ur, wd)
                            found = True
                            break
                if found:
                    break
            if found:
                break

        # 重新计算
        reachable = grid.bfs_reachable(start[0], start[1])
        unreachable = [c for c in all_cells if c not in reachable]
