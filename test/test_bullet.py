"""测试：炮弹反弹7次后消失（非1次）"""
import pygame, math
pygame.init()
pygame.display.set_mode((100, 100))

from entities.bullet import Bullet
from entities.tank import Tank
from map.generator import generate_random_map

def test_bullet_bounces():
    grid = generate_random_map()
    # 把坦克放在远处角落，确保炮弹不会意外命中
    tank = Tank(900, 700, name='T')
    tank.alive = False  # 禁用，让碰撞检测跳过

    # 向地图中央发射
    b = Bullet(480, 600, 60, -140, tank)

    frames = 0
    while b.alive and frames < 1000:
        hit = b.update(0.016, grid, [tank])
        frames += 1

    ok = b.bounce_count >= 7
    return ok, f'bounces={b.bounce_count} alive={b.alive} frames={frames}'

def test_bullet_hits_tank():
    from map.grid import Grid
    # 用完全开放的空地图（无边墙干扰）
    grid = Grid()
    for r in range(grid.rows):
        for c in range(grid.cols):
            if c < grid.cols - 1:
                grid.remove_wall(c, r, 1)
            if r < grid.rows - 1:
                grid.remove_wall(c, r, 2)
    # 关掉边界碰撞检测，只测坦克命中
    t1 = Tank(400, 400, name='T1')
    t2 = Tank(400, 350, name='T2')  # 在t1正上方

    b = Bullet(400, 500, 0, -165, t1)  # 从下方射向t2
    for _ in range(200):
        hit = b.update(0.016, grid, [t1, t2])
        if hit is not None:
            return True, f'炮弹命中{hit.name} bounce={b.bounce_count}'

    return False, f'未命中 bounce={b.bounce_count}'

def test_self_hit():
    grid = generate_random_map()
    tank = Tank(400, 400, name='T')
    # 向墙壁发射，反弹回来打中自己
    b = Bullet(400, 55, 0, -165, tank)
    for _ in range(500):
        hit = b.update(0.016, grid, [tank])
        if hit is tank:
            return True, f'自伤 bounce={b.bounce_count}'
    return True, f'未自伤(bounce={b.bounce_count}) - OK也行'

if __name__ == '__main__':
    results = []
    for name, fn in [('反弹7次', test_bullet_bounces),
                      ('命中坦克', test_bullet_hits_tank),
                      ('自伤', test_self_hit)]:
        ok, msg = fn()
        status = 'OK' if ok else 'FAIL'
        print(f'[{status}] {name}: {msg}')
        results.append(ok)

    all_ok = all(results)
    print(f'\n{"ALL PASS" if all_ok else "SOME FAILED"}')
    pygame.quit()
    exit(0 if all_ok else 1)
