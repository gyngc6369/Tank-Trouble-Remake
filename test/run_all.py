"""运行全部测试"""
import subprocess, sys, os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
env = os.environ.copy()
env['PYTHONPATH'] = ROOT

tests = [
    'test/test_bullet.py',
    'test/test_player.py',
    'test/test_ai.py',
]

failed = []
for t in tests:
    print(f'\n{"="*50}')
    print(f'Running: {t}')
    print("="*50)
    r = subprocess.run([sys.executable, t], cwd=ROOT, env=env)
    if r.returncode != 0:
        failed.append(t)

print(f'\n{"="*50}')
if failed:
    print(f'FAILED: {failed}')
    exit(1)
else:
    print('ALL TESTS PASSED')
    exit(0)
