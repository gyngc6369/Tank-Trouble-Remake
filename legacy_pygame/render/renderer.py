"""
《坦克动荡》— 渲染模块
地图、坦克、炮弹、UI、菜单（中文 + 鼠标交互）。
"""

import sys
import math
import pygame
from settings import *
from game import GameMode, GameState


class Renderer:
    """集中渲染器"""

    def __init__(self, screen: pygame.Surface):
        self.screen = screen
        self._load_fonts()
        self.mouse_pos = (0, 0)
        self.mouse_clicked = False
        self._click_handled = False
        self._thumbnail_cache = {}     # 缩略图缓存

    # ================================================================
    # 字体
    # ================================================================

    def _load_fonts(self):
        """加载中文字体，带兜底链"""
        sizes = {
            'font_tiny': 16,
            'font_small': 20,
            'font_medium': 26,
            'font_large': 36,
            'font_title': 52,
            'font_huge': 64,
        }
        for attr, size in sizes.items():
            setattr(self, attr, self._try_load_font(size))

    def _try_load_font(self, size):
        """依次尝试系统字体路径，全部失败时回退默认字体"""
        for path in FONT_PATHS:
            try:
                return pygame.font.Font(path, size)
            except Exception:
                continue
        print("[WARNING] 无法加载中文字体，中文将显示为方框", file=sys.stderr)
        return pygame.font.Font(None, size)

    # ================================================================
    # 鼠标
    # ================================================================

    def set_mouse(self, pos, clicked):
        self.mouse_pos = pos
        self.mouse_clicked = clicked

    def _mouse_rect(self, rect):
        mx, my = self.mouse_pos
        return rect.collidepoint(mx, my)

    def _draw_button(self, rect, text, subtext=None, color=COLOR_UI_TEXT,
                     bg_color=None, hover=False, font=None):
        """
        绘制按钮，返回是否被点击。
        注意：每次调用只响应一次点击（通过 _click_handled 防重复）。
        """
        if font is None:
            font = self.font_large

        if hover and self.mouse_clicked and not self._click_handled:
            self._click_handled = True
            clicked = True
        else:
            clicked = False

        # 外观
        if clicked:
            bg = (60, 60, 60)
            border = (20, 20, 20)
            text_color = (255, 255, 255)
        elif hover:
            bg = bg_color or (100, 100, 100)
            border = COLOR_UI_TEXT
            text_color = (255, 255, 255) if bg_color else COLOR_UI_TEXT
        else:
            bg = bg_color or COLOR_BUTTON
            border = (160, 160, 160)
            text_color = color

        pygame.draw.rect(self.screen, bg, rect, border_radius=8)
        pygame.draw.rect(self.screen, border, rect, 2, border_radius=8)

        if subtext:
            text_surf = font.render(text, True, text_color)
            text_rect = text_surf.get_rect(center=(rect.centerx, rect.centery - 10))
            self.screen.blit(text_surf, text_rect)
            sub_surf = self.font_tiny.render(
                subtext, True,
                text_color if (hover or clicked) else (130, 130, 130)
            )
            sub_rect = sub_surf.get_rect(center=(rect.centerx, rect.centery + 16))
            self.screen.blit(sub_surf, sub_rect)
        else:
            text_surf = font.render(text, True, text_color)
            text_rect = text_surf.get_rect(center=rect.center)
            self.screen.blit(text_surf, text_rect)

        return clicked

    # ================================================================
    # 地图
    # ================================================================

    def draw_map(self, grid):
        if grid is None:
            return
        map_rect = pygame.Rect(0, GRID_OFFSET_Y, SCREEN_WIDTH, SCREEN_HEIGHT - GRID_OFFSET_Y)
        pygame.draw.rect(self.screen, COLOR_BG, map_rect)
        for x1, y1, x2, y2 in grid.get_wall_segments():
            pygame.draw.line(self.screen, COLOR_WALL, (x1, y1), (x2, y2), WALL_THICKNESS)

    # ================================================================
    # 坦克
    # ================================================================

    def draw_tank(self, tank):
        if not tank.alive:
            return

        # ---- 车身 (W=20侧=窄边, H=28前=长边=前进/炮管方向) ----
        body_surf = pygame.Surface((TANK_BODY_W + 6, TANK_BODY_H + 6), pygame.SRCALPHA)
        cx_b = body_surf.get_width() // 2
        cy_b = body_surf.get_height() // 2
        body_rect_local = pygame.Rect(
            cx_b - TANK_BODY_W // 2, cy_b - TANK_BODY_H // 2,
            TANK_BODY_W, TANK_BODY_H
        )
        pygame.draw.rect(body_surf, tank.color_body, body_rect_local)
        pygame.draw.rect(body_surf, (0, 0, 0, 100), body_rect_local, 2)

        rotated_body = pygame.transform.rotate(body_surf, -tank.angle_deg)
        body_rect = rotated_body.get_rect(center=(tank.x, tank.y))
        self.screen.blit(rotated_body, body_rect)

        # ---- 炮管：从车身前边缘向外伸出，不穿入车身 ----
        # 在独立 surface 上绘制 BARREL_LENGTH 长的炮管
        barrel_margin = 4
        barrel_surf_w = BARREL_WIDTH + barrel_margin * 2
        barrel_surf_h = BARREL_LENGTH + barrel_margin
        barrel_surf = pygame.Surface((barrel_surf_w, barrel_surf_h), pygame.SRCALPHA)

        bx_c = barrel_surf.get_width() // 2
        # 炮管：从顶部开始，向下 BARREL_LENGTH 长（根部在底部边缘）
        barrel_rect = pygame.Rect(
            bx_c - BARREL_WIDTH // 2, barrel_margin // 2,
            BARREL_WIDTH, BARREL_LENGTH
        )
        pygame.draw.rect(barrel_surf, tank.color_barrel, barrel_rect)
        # 清晰边框
        pygame.draw.rect(barrel_surf, (0, 0, 0, 150), barrel_rect, 1)

        # 炮管根部锚点：车身前边缘中点
        front_offset = TANK_BODY_H / 2 + barrel_margin // 2
        anchor_x = tank.x + math.sin(tank.angle) * front_offset
        anchor_y = tank.y - math.cos(tank.angle) * front_offset

        # 旋转：炮管根部在 barrel_surf 的底部中心 (bx_c, barrel_surf_h)
        # 需要把旋转中心设到根部
        pivot_x = bx_c
        pivot_y = barrel_surf_h - barrel_margin // 2
        # 先把炮管 surf 放到锚点
        temp_surf = pygame.Surface((barrel_surf_w * 2, barrel_surf_h * 2), pygame.SRCALPHA)
        temp_surf.blit(barrel_surf, (barrel_surf_w // 2, barrel_surf_h // 2))
        rotated_barrel = pygame.transform.rotate(temp_surf, -tank.angle_deg)
        barrel_rect_out = rotated_barrel.get_rect(center=(anchor_x, anchor_y))
        self.screen.blit(rotated_barrel, barrel_rect_out)

    # ================================================================
    # 炮弹
    # ================================================================

    def draw_bullet(self, bullet):
        if not bullet.alive:
            return
        cx, cy = int(bullet.x), int(bullet.y)
        pygame.draw.circle(self.screen, (0, 0, 0), (cx, cy), BULLET_RADIUS + 1)
        pygame.draw.circle(self.screen, bullet.color, (cx, cy), BULLET_RADIUS)

    # ================================================================
    # 游戏主画面
    # ================================================================

    def draw_game(self, game):
        self.draw_map(game.grid)
        for tank in game.tanks:
            self.draw_tank(tank)
        for bullet in game.bullets:
            self.draw_bullet(bullet)
        self._draw_ui_bar(game)

        # 倒计时覆盖层
        if game.countdown_timer > 0:
            self._draw_countdown(game)

    def _draw_ui_bar(self, game):
        bar_rect = pygame.Rect(0, 0, SCREEN_WIDTH, UI_BAR_HEIGHT)
        pygame.draw.rect(self.screen, COLOR_UI_BG, bar_rect)
        pygame.draw.line(self.screen, COLOR_WALL, (0, UI_BAR_HEIGHT),
                         (SCREEN_WIDTH, UI_BAR_HEIGHT), 2)

        if not game.tanks:
            return

        x_offset = 10
        for tank in game.tanks:
            label = f"{tank.name}  {tank.score}分"
            surf = self.font_tiny.render(label, True, tank.color_body)
            self.screen.blit(surf, (x_offset, 3))

            dot_y = 22
            for a in range(MAX_AMMO):
                dot_x = x_offset + a * 11
                color = tank.color_body if a < tank.ammo else (200, 200, 200)
                r = 3
                pygame.draw.circle(self.screen, color, (dot_x + 5, dot_y), r)
                if a >= tank.ammo:
                    pygame.draw.circle(self.screen, (170, 170, 170), (dot_x + 5, dot_y), r, 1)

            x_offset += 200

        round_text = f"第 {game.round_number} 回合"
        surf = self.font_small.render(round_text, True, COLOR_UI_TEXT)
        rect = surf.get_rect(center=(SCREEN_WIDTH // 2, 12))
        self.screen.blit(surf, rect)

        goal_text = f"目标 {game.win_score} 分"
        surf = self.font_tiny.render(goal_text, True, (120, 120, 120))
        rect = surf.get_rect(center=(SCREEN_WIDTH // 2, 30))
        self.screen.blit(surf, rect)

    # ================================================================
    # 开局倒计时
    # ================================================================

    def _draw_countdown(self, game):
        """灰色遮罩 + 中央倒计时数字"""
        self._draw_overlay()
        cx, cy = SCREEN_WIDTH // 2, SCREEN_HEIGHT // 2
        timer = game.countdown_timer

        if timer > 0.6:
            text = str(int(timer) + 1)  # 3, 2, 1
        else:
            text = "开始！"

        surf = self.font_huge.render(text, True, (255, 255, 255))
        self.screen.blit(surf, surf.get_rect(center=(cx, cy)))

    # ================================================================
    # 覆盖层
    # ================================================================

    def _draw_overlay(self):
        overlay = pygame.Surface((SCREEN_WIDTH, SCREEN_HEIGHT), pygame.SRCALPHA)
        overlay.fill((0, 0, 0, 160))
        self.screen.blit(overlay, (0, 0))

    def draw_round_end(self, game):
        self._draw_overlay()
        cx, cy = SCREEN_WIDTH // 2, SCREEN_HEIGHT // 2

        t1 = self.font_large.render(game.round_result_text, True, (255, 255, 255))
        self.screen.blit(t1, t1.get_rect(center=(cx, cy - 70)))

        scores = "   |   ".join(f"{t.name}  {t.score} 分" for t in game.tanks)
        t2 = self.font_medium.render(scores, True, (220, 220, 220))
        self.screen.blit(t2, t2.get_rect(center=(cx, cy - 15)))

        cd = int(game.round_timer) + 1
        t3 = self.font_medium.render(f"下一回合  {cd} 秒", True, (170, 170, 170))
        self.screen.blit(t3, t3.get_rect(center=(cx, cy + 45)))

    def draw_game_over(self, game):
        self._draw_overlay()
        cx, cy = SCREEN_WIDTH // 2, SCREEN_HEIGHT // 2

        winner = max(game.tanks, key=lambda t: t.score)
        t1 = self.font_large.render(f"{winner.name}  获得最终胜利！", True, (255, 220, 60))
        self.screen.blit(t1, t1.get_rect(center=(cx, cy - 70)))

        scores = "   |   ".join(f"{t.name}  {t.score} 分" for t in game.tanks)
        t2 = self.font_medium.render(f"最终比分    {scores}", True, (255, 255, 255))
        self.screen.blit(t2, t2.get_rect(center=(cx, cy - 10)))

        t3 = self.font_medium.render("[R] 再来一局    [M] 返回主菜单", True, (200, 200, 200))
        self.screen.blit(t3, t3.get_rect(center=(cx, cy + 50)))

    def draw_pause(self):
        self._draw_overlay()
        cx, cy = SCREEN_WIDTH // 2, SCREEN_HEIGHT // 2

        t1 = self.font_large.render("=  暂停  =", True, (255, 255, 255))
        self.screen.blit(t1, t1.get_rect(center=(cx, cy - 30)))
        t2 = self.font_medium.render("[P] 继续    [ESC] 退出到菜单", True, (200, 200, 200))
        self.screen.blit(t2, t2.get_rect(center=(cx, cy + 25)))

    # ================================================================
    # 主菜单
    # ================================================================

    def draw_menu_main(self, game):
        self.screen.fill(COLOR_BG)
        cx = SCREEN_WIDTH // 2

        title = self.font_huge.render("坦克动荡", True, COLOR_UI_TEXT)
        self.screen.blit(title, title.get_rect(center=(cx, 90)))

        subtitle = self.font_small.render("—  Tank Trouble  —", True, (140, 140, 140))
        self.screen.blit(subtitle, subtitle.get_rect(center=(cx, 155)))

        options = [
            ("双人对战", "两名玩家同键盘对战"),
            ("单人 vs AI", "与电脑玩家对战"),
            ("双人 + AI", "两名玩家加电脑混战"),
        ]

        self._click_handled = False
        for i, (label, desc) in enumerate(options):
            y = 230 + i * 85
            rect = pygame.Rect(cx - 200, y - 28, 400, 58)
            hover = self._mouse_rect(rect)

            if self._draw_button(rect, label, desc, hover=hover):
                if i == 0:
                    game.mode = GameMode.PVP
                elif i == 1:
                    game.mode = GameMode.PVAI
                else:
                    game.mode = GameMode.PVPAI
                game.state = GameState.MAP_SELECT
                game.menu_selection = 0

        hint = self.font_small.render("点击按钮选择模式    P1: WASD+F    P2: ↑↓←→+/", True, (150, 150, 150))
        self.screen.blit(hint, hint.get_rect(center=(cx, 520)))

        esc_hint = self.font_tiny.render("ESC 退出游戏", True, (180, 180, 180))
        self.screen.blit(esc_hint, esc_hint.get_rect(center=(cx, 550)))

    # ================================================================
    # 地图选择
    # ================================================================

    def draw_menu_map_select(self, game):
        self.screen.fill(COLOR_BG)
        cx = SCREEN_WIDTH // 2

        title = self.font_large.render("选择地图", True, COLOR_UI_TEXT)
        self.screen.blit(title, title.get_rect(center=(cx, 35)))

        # ---- 胜利分数 ----
        score_y = 72
        score_label = self.font_small.render("胜利分数：", True, COLOR_UI_TEXT)
        self.screen.blit(score_label, score_label.get_rect(center=(cx - 120, score_y)))

        self._click_handled = False

        arrow_left = pygame.Rect(cx - 50, score_y - 14, 30, 28)
        arrow_right = pygame.Rect(cx + 80, score_y - 14, 30, 28)

        if self._draw_button(arrow_left, "<", font=self.font_medium,
                              hover=self._mouse_rect(arrow_left)):
            game.win_score = game.prev_win_score()

        score_val = self.font_medium.render(str(game.win_score), True, COLOR_UI_TEXT)
        self.screen.blit(score_val, score_val.get_rect(center=(cx + 15, score_y)))

        if self._draw_button(arrow_right, ">", font=self.font_medium,
                              hover=self._mouse_rect(arrow_right)):
            game.win_score = game.next_win_score()

        # ---- 地图选项 (2行 × 3列) ----
        from map.presets import PRESET_NAMES
        map_names = list(PRESET_NAMES) + ["随机生成"]

        for i, name in enumerate(map_names):
            col = i % 3
            row = i // 3
            bx = 110 + col * 260
            by = 120 + row * 205

            is_selected = (i == game.selected_map)
            rect = pygame.Rect(bx, by, 220, 160)
            hover = self._mouse_rect(rect)

            if is_selected:
                bg = (200, 220, 255)
                border = COLOR_UI_TEXT
            elif hover:
                bg = (230, 230, 240)
                border = (100, 100, 120)
            else:
                bg = (245, 245, 245)
                border = (200, 200, 200)

            pygame.draw.rect(self.screen, bg, rect, border_radius=10)
            pygame.draw.rect(self.screen, border, rect, 3 if is_selected else 2, border_radius=10)

            self._draw_map_thumbnail(game, i, bx + 15, by + 10, 190, 100)

            label = f"[{i + 1}]  {name}"
            label_color = COLOR_UI_TEXT if is_selected else (80, 80, 80)
            l_surf = self.font_small.render(label, True, label_color)
            self.screen.blit(l_surf, l_surf.get_rect(center=(bx + 110, by + 135)))

            if hover and self.mouse_clicked and not self._click_handled:
                self._click_handled = True
                game.selected_map = i

        # ---- 确认 / 返回按钮 ----
        btn_y = 560
        confirm_rect = pygame.Rect(cx - 140, btn_y - 20, 120, 40)
        back_rect = pygame.Rect(cx + 20, btn_y - 20, 120, 40)

        if self._draw_button(confirm_rect, "开始游戏", font=self.font_small,
                              hover=self._mouse_rect(confirm_rect)):
            game.confirm_map_selection()

        if self._draw_button(back_rect, "返回", font=self.font_small,
                              hover=self._mouse_rect(back_rect)):
            game.state = GameState.MAIN_MENU

        hint = self.font_tiny.render(
            "点击缩略图选地图   点击箭头调分数   也可用键盘 1-6/←→/Enter/B",
            True, (150, 150, 150)
        )
        self.screen.blit(hint, hint.get_rect(center=(cx, 600)))

        sel_name = map_names[game.selected_map]
        sel_hint = self.font_small.render(f"当前选择：{sel_name}", True, (100, 100, 180))
        self.screen.blit(sel_hint, sel_hint.get_rect(center=(cx, 630)))

    def _draw_map_thumbnail(self, game, map_index, x, y, w, h):
        """绘制地图缩略图（使用缓存，避免每帧重新生成迷宫）"""
        if map_index not in self._thumbnail_cache:
            self._thumbnail_cache[map_index] = self._render_thumbnail(map_index, w, h)
        self.screen.blit(self._thumbnail_cache[map_index], (x, y))

    def _render_thumbnail(self, map_index, w, h):
        """预渲染一张缩略图到Surface（仅调用一次）"""
        thumb = pygame.Surface((w, h))
        thumb.fill((250, 250, 250))
        pygame.draw.rect(thumb, (210, 210, 210), (0, 0, w, h), 1)

        try:
            if map_index == 5:
                from map.generator import generate_random_map
                grid = generate_random_map()
            else:
                from map.presets import get_preset
                grid = get_preset(map_index)

            scale_x = w / (GRID_COLS * CELL_SIZE)
            scale_y = h / (GRID_ROWS * CELL_SIZE)
            scale = min(scale_x, scale_y)

            for rx1, ry1, rx2, ry2 in grid.get_wall_segments():
                sx1 = rx1 * scale
                sy1 = (ry1 - GRID_OFFSET_Y) * scale
                sx2 = rx2 * scale
                sy2 = (ry2 - GRID_OFFSET_Y) * scale
                pygame.draw.line(thumb, (180, 180, 180),
                                 (sx1, sy1), (sx2, sy2),
                                 max(1, int(WALL_THICKNESS * scale)))
        except Exception:
            pass

        return thumb

    # ================================================================
    # 难度选择
    # ================================================================

    def draw_menu_difficulty(self, game):
        self.screen.fill(COLOR_BG)
        cx = SCREEN_WIDTH // 2

        title = self.font_large.render("选择 AI 难度", True, COLOR_UI_TEXT)
        self.screen.blit(title, title.get_rect(center=(cx, 160)))

        options = [
            ("普通", "较低射击频率  ·  较慢反应速度  ·  弹道计算稍有延迟"),
            ("困难", "高频率快速射击  ·  快速躲避反应  ·  精确反弹弹道"),
        ]

        self._click_handled = False
        for i, (label, desc) in enumerate(options):
            y = 300 + i * 100
            rect = pygame.Rect(cx - 260, y - 30, 520, 65)

            is_sel = (i == 0 and game.difficulty == "normal") or \
                     (i == 1 and game.difficulty == "hard")
            hover = self._mouse_rect(rect)

            if is_sel:
                bg = (60, 60, 60)
                border = (20, 20, 20)
                t_color = (255, 255, 255)
                d_color = (200, 200, 200)
            elif hover:
                bg = (120, 120, 130)
                border = COLOR_UI_TEXT
                t_color = (255, 255, 255)
                d_color = (220, 220, 220)
            else:
                bg = COLOR_BUTTON
                border = (170, 170, 170)
                t_color = COLOR_UI_TEXT
                d_color = (120, 120, 120)

            pygame.draw.rect(self.screen, bg, rect, border_radius=10)
            pygame.draw.rect(self.screen, border, rect, 3, border_radius=10)

            t_surf = self.font_large.render(label, True, t_color)
            self.screen.blit(t_surf, t_surf.get_rect(center=(cx, y - 5)))

            d_surf = self.font_tiny.render(desc, True, d_color)
            self.screen.blit(d_surf, d_surf.get_rect(center=(cx, y + 22)))

            if hover and self.mouse_clicked and not self._click_handled:
                self._click_handled = True
                game.difficulty = "normal" if i == 0 else "hard"
                game.start_game()

        back_rect = pygame.Rect(cx - 50, 480, 100, 36)
        if self._draw_button(back_rect, "返回", font=self.font_small,
                              hover=self._mouse_rect(back_rect)):
            game.state = GameState.MAP_SELECT

        hint = self.font_tiny.render("点击选择难度    键盘 1=普通  2=困难  B=返回", True, (150, 150, 150))
        self.screen.blit(hint, hint.get_rect(center=(cx, 530)))
