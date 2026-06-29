"""测试：AI不卡死，burst fire超时保护"""
import pygame, time
pygame.init()
pygame.display.set_mode((100, 100))

from game import Game, GameState, GameMode

def test_ai_runs_without_freeze():
    """AI运行30帧不卡死"""
    game = Game()
    game.mode = GameMode.PVAI
    game.difficulty = 'hard'
    game.start_game()
    game.countdown_timer = 0

    ai_ctrl = game.ai_controllers[1]
    ai_tank = game.tanks[1]
    enemy = [game.tanks[0]]

    t0 = time.time()
    for _ in range(60):
        m, r, s = ai_ctrl.update(0.016, ai_tank, enemy, [], game.grid)
    elapsed = (time.time() - t0) * 1000

    # 60帧应在1.5秒内完成
    fast_enough = elapsed < 1500
    return fast_enough, f'60 frames in {elapsed:.0f}ms (limit 1500ms)'

def test_ai_burst_timeout():
    """burst fire超时保护"""
    from ai.controller import AIController
    from map.generator import generate_random_map

    grid = generate_random_map()
    from entities.tank import Tank
    ai = Tank(400, 400, name='AI', is_ai=True)
    enemy = Tank(500, 400, name='Enemy')

    ctrl = AIController('hard')
    ctrl.burst_shots_remaining = 3
    ctrl.burst_angle = 3.0  # 朝向完全不同的角度
    ctrl._burst_timer = 0

    # 模拟大量帧，burst应超时清零
    for _ in range(200):
        ctrl.update(0.016, ai, [enemy], [], grid)
        if ctrl.burst_shots_remaining == 0:
            break

    timed_out = ctrl.burst_shots_remaining == 0
    return timed_out, f'burst_remaining={ctrl.burst_shots_remaining} (should be 0 after timeout)'

def test_ai_produces_output():
    """AI始终产生有效输出"""
    game = Game()
    game.mode = GameMode.PVAI
    game.start_game()
    game.countdown_timer = 0

    ctrl = game.ai_controllers[1]
    tank = game.tanks[1]
    enemy = [game.tanks[0]]

    outputs = set()
    for _ in range(30):
        m, r, s = ctrl.update(0.016, tank, enemy, [], game.grid)
        outputs.add((m, r, s))

    # 至少应该有一个非零输出
    has_action = any(m != 0 or r != 0 for m, r, s in outputs)
    return has_action, f'unique outputs={len(outputs)} has_action={has_action}'

if __name__ == '__main__':
    results = []
    for name, fn in [('60帧不卡死', test_ai_runs_without_freeze),
                      ('Burst超时', test_ai_burst_timeout),
                      ('有有效输出', test_ai_produces_output)]:
        ok, msg = fn()
        status = 'OK' if ok else 'FAIL'
        print(f'[{status}] {name}: {msg}')
        results.append(ok)

    all_ok = all(results)
    print(f'\n{"ALL PASS" if all_ok else "SOME FAILED"}')
    pygame.quit()
    exit(0 if all_ok else 1)
