"""Mutation checks against real saved Unity/Unreal UI captures; no engine launches."""
from pathlib import Path
import copy
import hashlib
import json
import subprocess
import sys

ROOT = Path(__file__).resolve().parent.parent
OUTPUT = ROOT / 'artifacts/ui-capture-integrity'
OUTPUT.mkdir(exist_ok=True)
checks = []

def read(path):
    raw = path.read_bytes()
    return json.loads(raw.decode('utf-16' if raw[:2] in (b'\xff\xfe', b'\xfe\xff') else 'utf-8-sig'))

def checkpoint(frame, driver):
    for actor in frame['actors']:
        if actor.get('class', actor.get('className')) == driver:
            state = actor['state']
            state = {item['name'].casefold(): item['value'] for item in state} if isinstance(state, list) else {key.casefold(): value for key, value in state.items()}
            return int(state['frame'])
    return None

def run(script, expected, actual, report, optimized=False):
    args = [sys.executable] + (['-O'] if optimized else []) + [str(script), str(expected), str(actual), str(report)]
    return subprocess.run(args, capture_output=True, text=True)

for profile, driver in [('ui-api', 'RuntimeUIApiDriver'), ('ui-prefab', 'RuntimeUIDriver')]:
    script = ROOT / ('tools/compare-runtime-' + profile + '.py')
    expected = ROOT / ('artifacts/runtime-' + profile + '-tests/unity-original.json')
    if profile == 'ui-api':
        actual = ROOT / 'artifacts/runtime-ui-api-tests/unreal-v2.json'
    else:
        execution = read(ROOT / 'artifacts/offline-runtime-first-execution/fixed-path-execution.json')
        actual = Path(execution['capture'])
    original_hashes = [hashlib.sha256(path.read_bytes()).hexdigest() for path in [expected, actual]]
    data = read(actual)
    result = run(script, expected, actual, OUTPUT / (profile + '-positive.json'))
    if result.returncode:
        raise RuntimeError(result.stderr)
    checks.append({'profile': profile, 'case': 'existing Unity vs Unreal capture', 'status': 'PASS'})
    mutations = {}
    duplicate = copy.deepcopy(data)
    frame = next(frame for frame in duplicate['frames'] if frame['ui'])
    frame['ui'].append(copy.deepcopy(frame['ui'][0]))
    mutations['duplicate-ui-owner'] = duplicate
    duplicate = copy.deepcopy(data)
    frame = next(frame for frame in duplicate['frames'] if frame['actors'])
    frame['actors'].append(copy.deepcopy(frame['actors'][0]))
    mutations['duplicate-actor-id'] = duplicate
    duplicate = copy.deepcopy(data)
    frame = next(frame for frame in duplicate['frames'] if frame['objects'])
    frame['objects'].append(copy.deepcopy(frame['objects'][0]))
    mutations['duplicate-object-id'] = duplicate
    duplicate = copy.deepcopy(data)
    index = next(index for index, frame in enumerate(duplicate['frames']) if checkpoint(frame, driver) == 5)
    duplicate['frames'].insert(index, copy.deepcopy(duplicate['frames'][index]))
    mutations['duplicate-checkpoint'] = duplicate
    duplicate = copy.deepcopy(data)
    first = next(index for index, frame in enumerate(duplicate['frames']) if checkpoint(frame, driver) == 5)
    second = next(index for index, frame in enumerate(duplicate['frames']) if checkpoint(frame, driver) == 6)
    duplicate['frames'][first], duplicate['frames'][second] = duplicate['frames'][second], duplicate['frames'][first]
    mutations['out-of-order-checkpoint'] = duplicate
    duplicate = copy.deepcopy(data)
    duplicate['frames'] = [frame for frame in duplicate['frames'] if checkpoint(frame, driver) != 5]
    mutations['missing-checkpoint'] = duplicate
    duplicate = copy.deepcopy(data)
    actor = next(actor for frame in duplicate['frames'] for actor in frame['actors'] if actor.get('class', actor.get('className')) == driver)
    state = actor['state']
    if isinstance(state, list):
        entry = copy.deepcopy(state[0]); entry['name'] = entry['name'].swapcase(); state.append(entry)
    else:
        key = next(iter(state)); state[key.swapcase()] = state[key]
    mutations['duplicate-state-field'] = duplicate
    duplicate = copy.deepcopy(data)
    frame = next(frame for frame in duplicate['frames'] if frame['ui'])
    frame['ui'][0]['offHits'] = 1
    mutations['wrong-ui-behaviour'] = duplicate
    duplicate = copy.deepcopy(data)
    frame = next(frame for frame in duplicate['frames'] if frame['ui'])
    frame['ui'][0]['visible'] = int(frame['ui'][0]['visible'])
    mutations['numeric-boolean'] = duplicate
    duplicate = copy.deepcopy(data)
    frame = next(frame for frame in duplicate['frames'] if frame['ui'])
    frame['ui'][0]['hits'] = float(frame['ui'][0]['hits'])
    mutations['floating-integer-counter'] = duplicate
    duplicate = copy.deepcopy(data)
    frame = next(frame for frame in duplicate['frames'] if frame.get('uiViewportWidgets') is not None)
    frame['uiViewportWidgets'] = False
    mutations['boolean-viewport-counter'] = duplicate
    duplicate = copy.deepcopy(data)
    frame = next(frame for frame in duplicate['frames'] if checkpoint(frame, driver) == 5)
    actor = next(actor for actor in frame['actors'] if actor.get('class', actor.get('className')) == driver)
    actor = copy.deepcopy(actor); actor['id'] += '-duplicate-driver'; frame['actors'].append(actor)
    mutations['multiple-runtime-drivers'] = duplicate
    duplicate = copy.deepcopy(data); duplicate['invalidNumericData'] = float('nan')
    mutations['nonstandard-json-numeric'] = duplicate
    for name, mutation in mutations.items():
        path = OUTPUT / (profile + '-' + name + '.json')
        path.write_text(json.dumps(mutation), encoding='utf-8')
        report = OUTPUT / (profile + '-' + name + '-verification.json')
        report.write_text('{"status":"PASS","stale":true}', encoding='utf-8')
        result = run(script, expected, path, report)
        status = read(report)
        if result.returncode == 0 or status['status'] != 'FAIL' or status.get('stale'):
            raise RuntimeError('Invalid capture or stale PASS accepted: ' + profile + '/' + name)
        checks.append({'profile': profile, 'case': name, 'status': 'REJECTED', 'error': status['error']})
    duplicate_key = OUTPUT / (profile + '-duplicate-json-key.json')
    serialized = json.dumps(data)
    serialized = serialized.replace('"status": "CAPTURED"', '"status": "BROKEN", "status": "CAPTURED"', 1)
    duplicate_key.write_text(serialized, encoding='utf-8')
    result = run(script, expected, duplicate_key, OUTPUT / (profile + '-duplicate-json-key-verification.json'))
    if result.returncode == 0:
        raise RuntimeError('Duplicate JSON key accepted')
    checks.append({'profile': profile, 'case': 'duplicate-json-key', 'status': 'REJECTED'})
    result = run(script, expected, actual, OUTPUT / (profile + '-optimized-verification.json'), optimized=True)
    if result.returncode == 0 or read(OUTPUT / (profile + '-optimized-verification.json'))['status'] != 'FAIL':
        raise RuntimeError('Optimized assertion-free execution accepted')
    checks.append({'profile': profile, 'case': 'optimized-Python', 'status': 'REJECTED'})
    input_path = OUTPUT / (profile + '-protected-input.json')
    input_path.write_bytes(actual.read_bytes()); before = input_path.read_bytes()
    result = run(script, expected, input_path, input_path)
    if result.returncode == 0 or input_path.read_bytes() != before:
        raise RuntimeError('Input overwrite protection failed')
    checks.append({'profile': profile, 'case': 'input-output-alias', 'status': 'REJECTED_INPUT_PRESERVED'})
    if original_hashes != [hashlib.sha256(path.read_bytes()).hexdigest() for path in [expected, actual]]:
        raise RuntimeError('Original evidence changed')
(OUTPUT / 'verification.json').write_text(json.dumps({'status': 'PASS', 'checks': checks, 'count': len(checks), 'engineStarted': False, 'existingCaptureReplayOnly': True}, indent=2), encoding='utf-8')
print(f'{len(checks)} UI capture integrity checks PASS')
