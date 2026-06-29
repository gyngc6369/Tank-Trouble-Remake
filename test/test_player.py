"""测试：玩家坦克可操控，不卡墙"""
import pygame, math
pygame.init()
pygame.display.set_mode((100, 100))

from game import Game, GameState, GameMode

def test_player_can_move():
    game = Game()
    game.mode = GameMode.PVP
    game.start_game()
    game.countdown_timer = 0  # 跳过倒计时

    p1 = game.tanks[0]
    old_x, old_y = p1.x, p1.y

    # 模拟前进 (W键)
    result = p1.move(0.1, 1, game.grid)

    moved = (p1.x != old_x or p1.y != old_y)
    ok = result and moved
    return ok, f'move={result} moved={moved} from({old_x:.0f},{old_y:.0f}) to({p1.x:.0f},{p1.y:.0f})'

def test_player_not_stuck():
    game = Game()
    game.mode = GameMode.PVP
    game.start_game()
    game.countdown_timer = 0

    p1 = game.tanks[0]

    # 模拟连续30帧的W键移动
    cnt = 0
    for _ in range(30):
        if p1.move(0.016, 1, game.grid):
            cnt += 1

    return cnt > 0, f'moved {cnt}/30 frames'

def test_player_after_clamp():
    """测试：嵌入墙壁后clamp修复 + 恢复可移动"""
    game = Game()
    game.mode = GameMode.PVP
    game.start_game()
    game.countdown_timer = 0
    p1 = game.tanks[0]

    p1.x = 5; p1.y = 400  # 推入左墙
    game._clamp_tank_to_map(p1)

    can_move = p1.move(0.1, 1, game.grid)
    stuck = p1._check_collision_at(p1.x, p1.y, game.grid)

    return not stuck and can_move, f'stuck={stuck} move={can_move} x={p1.x:.0f}'

if __name__ == '__main__':
    results = []
    for name, fn in [('移动测试', test_player_can_move),
                      ('连续移动', test_player_not_stuck),
                      ('推墙修复', test_player_after_clamp)]:
        ok, msg = fn()
        status = 'OK' if ok else 'FAIL'
        print(f'[{status}] {name}: {msg}')
        results.append(ok)

    all_ok = all(results)
    print(f'\n{"ALL PASS" if all_ok else "SOME FAILED"}')
    pygame.quit()
    exit(0 if all_ok else 1)
