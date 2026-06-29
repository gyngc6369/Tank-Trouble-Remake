"""
《坦克动荡》— 音效模块
使用纯 Python 合成短促音效，无需外部音频文件。
"""

import math
import struct
import pygame


# 缓存已生成的 Sound 对象
_shoot_sound = None
_explosion_sound = None
_initialized = False


def init_sounds():
    """初始化音效（合成并缓存）"""
    global _shoot_sound, _explosion_sound, _initialized
    if _initialized:
        return

    try:
        _shoot_sound = _generate_shoot_sound()
        _explosion_sound = _generate_explosion_sound()
        _initialized = True
    except Exception:
        # 音效是非关键的，静默失败
        pass


def play_shoot():
    """播放射击音效"""
    if _shoot_sound:
        _shoot_sound.play()


def play_explosion():
    """播放击毁音效"""
    if _explosion_sound:
        _explosion_sound.play()


def _generate_shoot_sound():
    """
    生成短促低音射击音 "嘭" (~0.1s)
    使用快速衰减的方波。
    """
    sample_rate = 22050
    duration = 0.10  # 秒
    frequency = 120  # Hz，低音
    num_samples = int(sample_rate * duration)

    # 生成方波 + 指数衰减
    samples = []
    for i in range(num_samples):
        t = i / sample_rate
        # 方波
        if int(t * frequency * 2) % 2 == 0:
            val = 0.3
        else:
            val = -0.3
        # 衰减
        decay = math.exp(-t * 40)
        val *= decay
        samples.append(int(val * 32767))

    return _make_sound(samples, sample_rate)


def _generate_explosion_sound():
    """
    生成短促爆破音 "砰" (~0.2s)
    使用噪音 + 快速衰减。
    """
    sample_rate = 22050
    duration = 0.20  # 秒
    num_samples = int(sample_rate * duration)

    # 使用确定性伪随机生成噪音
    seed = 12345
    samples = []
    for i in range(num_samples):
        t = i / sample_rate
        # 伪随机噪音
        seed = (seed * 1103515245 + 12345) & 0x7FFFFFFF
        noise = (seed / 0x7FFFFFFF) * 2.0 - 1.0

        # 混合低频成分
        low_freq = math.sin(2 * math.pi * 80 * t) * 0.5

        val = noise * 0.5 + low_freq * 0.5
        # 快速衰减
        decay = math.exp(-t * 20)
        val *= decay * 0.4

        samples.append(int(val * 32767))

    return _make_sound(samples, sample_rate)


def _make_sound(samples, sample_rate):
    """
    将样本列表转换为 pygame Sound 对象。
    使用 struct 生成 16-bit 有符号整数 PCM 数据。
    """
    # 打包为 16-bit signed int PCM
    raw_data = struct.pack(f'<{len(samples)}h', *samples)
    return pygame.mixer.Sound(buffer=raw_data)
