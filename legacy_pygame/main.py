"""
《坦克动荡》— 入口模块
"""

import sys
import pygame
from pygame.locals import *

from settings import *
from game import Game, GameState


def main():
    pygame.init()
    pygame.mixer.init(frequency=22050, size=-16, channels=2, buffer=512)
    pygame.display.set_caption("坦克动荡")
    screen = pygame.display.set_mode((SCREEN_WIDTH, SCREEN_HEIGHT))
    clock = pygame.time.Clock()

    from sound.sounds import init_sounds
    init_sounds()

    game = Game()
    from render.renderer import Renderer
    renderer = Renderer(screen)

    running = True
    while running:
        dt = min(clock.tick(FPS) / 1000.0, 0.05)

        mouse_clicked = False
        for event in pygame.event.get():
            if event.type == QUIT:
                running = False
            elif event.type == MOUSEBUTTONDOWN and event.button == 1:
                mouse_clicked = True
            elif event.type == KEYDOWN:
                if event.key == K_ESCAPE:
                    if game.state in (GameState.GAME_PLAYING, GameState.ROUND_END):
                        game.state = GameState.PAUSED
                    elif game.state == GameState.PAUSED:
                        game._go_main_menu()
                    elif game.state == GameState.GAME_OVER:
                        game._go_main_menu()
                    else:
                        running = False
                elif event.key == K_p:
                    if game.state == GameState.GAME_PLAYING:
                        game.state = GameState.PAUSED
                    elif game.state == GameState.PAUSED:
                        game.state = GameState.GAME_PLAYING

        renderer.set_mouse(pygame.mouse.get_pos(), mouse_clicked)
        game.update(dt)
        screen.fill(COLOR_BG)
        game.draw(renderer)
        pygame.display.flip()

    pygame.quit()
    sys.exit()


if __name__ == "__main__":
    main()
