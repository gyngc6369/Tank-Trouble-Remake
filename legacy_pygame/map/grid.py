"""
《坦克动荡》— 地图数据结构（薄墙图 + 缓存 + 空间索引）
"""

import random
from collections import deque
from settings import *


class Grid:
    """
    薄墙图结构：15x11 格子，每格存储 [上,右,下,左] 四面墙。
    - walls[row][col] = [top, right, bottom, left]
    - 包含墙段缓存和空间索引以优化碰撞检测。
    """

    def __init__(self, cols=GRID_COLS, rows=GRID_ROWS):
        self.cols = cols
        self.rows = rows
        self.walls = [
            [[True, True, True, True] for _ in range(cols)]
            for _ in range(rows)
        ]
        # 缓存
        self._cached_segments = None
        self._cached_segments_normals = None

    # ---- 墙操作 ----

    def has_wall(self, col, row, direction):
        if 0 <= row < self.rows and 0 <= col < self.cols:
            return self.walls[row][col][direction]
        return True

    def remove_wall(self, col, row, direction):
        """移除墙并失效缓存"""
        if not (0 <= row < self.rows and 0 <= col < self.cols):
            return
        self.walls[row][col][direction] = False
        dr = [-1, 0, 1, 0]
        dc = [0, 1, 0, -1]
        opposite = [2, 3, 0, 1]
        nr, nc = row + dr[direction], col + dc[direction]
        if 0 <= nr < self.rows and 0 <= nc < self.cols:
            self.walls[nr][nc][opposite[direction]] = False
        self._invalidate_cache()

    def _invalidate_cache(self):
        self._cached_segments = None
        self._cached_segments_normals = None

    # ---- 邻居 ----

    def get_neighbors(self, col, row):
        neighbors = []
        dirs = [(0, -1, 0), (1, 0, 1), (0, 1, 2), (-1, 0, 3)]
        for dc, dr, d in dirs:
            nc, nr = col + dc, row + dr
            if 0 <= nc < self.cols and 0 <= nr < self.rows:
                if not self.has_wall(col, row, d):
                    neighbors.append((nc, nr))
        return neighbors

    # ---- BFS ----

    def bfs_reachable(self, start_col, start_row):
        visited = set()
        queue = deque()
        queue.append((start_col, start_row))
        visited.add((start_col, start_row))
        while queue:
            c, r = queue.popleft()
            for nc, nr in self.get_neighbors(c, r):
                if (nc, nr) not in visited:
                    visited.add((nc, nr))
                    queue.append((nc, nr))
        return visited

    def check_all_reachable(self) -> bool:
        """验证所有格子是否互通"""
        if self.rows == 0 or self.cols == 0:
            return True
        reached = self.bfs_reachable(0, 0)
        return len(reached) == self.cols * self.rows
        return True

    # ---- 出生点 ----

    def pick_spawn_points(self, count=2, min_dist=SPAWN_MIN_DIST):
        all_cells = [(c, r) for r in range(self.rows) for c in range(self.cols)]
        for _ in range(100):
            candidates = random.sample(all_cells, count)
            ok = True
            for i in range(count):
                for j in range(i + 1, count):
                    dist = abs(candidates[i][0] - candidates[j][0]) + \
                           abs(candidates[i][1] - candidates[j][1])
                    if dist < min_dist:
                        ok = False; break
                if not ok: break
            if ok:
                reachable = self.bfs_reachable(candidates[0][0], candidates[0][1])
                if all((c, r) in reachable for c, r in candidates):
                    return list(candidates)
        return [(1, 1), (self.cols - 2, self.rows - 2)]

    # ---- 坐标 ----

    def cell_to_pixel(self, col, row):
        x = col * CELL_SIZE + CELL_SIZE // 2
        y = row * CELL_SIZE + CELL_SIZE // 2 + GRID_OFFSET_Y
        return (float(x), float(y))

    def pixel_to_cell(self, px, py):
        col = int(px // CELL_SIZE)
        row = int((py - GRID_OFFSET_Y) // CELL_SIZE)
        return (max(0, min(self.cols - 1, col)),
                max(0, min(self.rows - 1, row)))

    # ================================================================
    # 墙段（带缓存）
    # ================================================================

    def get_wall_segments(self):
        """返回所有墙段 [(x1,y1,x2,y2), ...]，结果被缓存"""
        if self._cached_segments is None:
            self._cached_segments = self._compute_wall_segments()
        return self._cached_segments

    def get_wall_segments_with_normals(self):
        """返回带法向量的墙段，结果被缓存"""
        if self._cached_segments_normals is None:
            self._cached_segments_normals = self._compute_wall_segments_with_normals()
        return self._cached_segments_normals

    def _compute_wall_segments(self):
        segments = []
        for r in range(self.rows):
            for c in range(self.cols):
                x0 = c * CELL_SIZE
                y0 = r * CELL_SIZE + GRID_OFFSET_Y
                x1 = x0 + CELL_SIZE
                y1 = y0 + CELL_SIZE
                if self.walls[r][c][0]:
                    segments.append((x0, y0, x1, y0))
                if self.walls[r][c][2]:
                    segments.append((x0, y1, x1, y1))
                if self.walls[r][c][3]:
                    segments.append((x0, y0, x0, y1))
                if self.walls[r][c][1]:
                    segments.append((x1, y0, x1, y1))
        return self._dedup_segments(segments)

    def _compute_wall_segments_with_normals(self):
        segments = []
        for r in range(self.rows):
            for c in range(self.cols):
                x0 = c * CELL_SIZE
                y0 = r * CELL_SIZE + GRID_OFFSET_Y
                x1 = x0 + CELL_SIZE
                y1 = y0 + CELL_SIZE
                if self.walls[r][c][0]:
                    segments.append((x0, y0, x1, y0, 0, -1))
                if self.walls[r][c][2]:
                    segments.append((x0, y1, x1, y1, 0, 1))
                if self.walls[r][c][3]:
                    segments.append((x0, y0, x0, y1, -1, 0))
                if self.walls[r][c][1]:
                    segments.append((x1, y0, x1, y1, 1, 0))
        return self._dedup_normals(segments)

    def _dedup_segments(self, segments):
        seen = set()
        unique = []
        for x1, y1, x2, y2 in segments:
            key = (min(x1, x2), min(y1, y2), max(x1, x2), max(y1, y2))
            if key not in seen:
                seen.add(key)
                unique.append((x1, y1, x2, y2))
        return unique

    def _dedup_normals(self, segments):
        seen = {}
        for sx1, sy1, sx2, sy2, nx, ny in segments:
            key = (min(sx1, sx2), min(sy1, sy2), max(sx1, sx2), max(sy1, sy2))
            if key not in seen:
                seen[key] = (sx1, sy1, sx2, sy2, nx, ny)
        return list(seen.values())

