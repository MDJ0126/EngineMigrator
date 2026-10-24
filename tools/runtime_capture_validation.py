"""Strict capture integrity and fail-closed comparison reports; never starts engines."""
import hashlib
import json
import os
import sys
from pathlib import Path
import tempfile


def _unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f'Duplicate JSON key: {key}')
        result[key] = value
    return result


def _invalid_constant(value):
    raise ValueError(f'Nonstandard JSON numeric constant: {value}')


def load_capture(path, case_insensitive_state=True):
    if not __debug__:
        raise RuntimeError('Optimized Python disables semantic assertions; capture comparison is refused')
    raw = Path(path).read_bytes()
    encoding = 'utf-16' if raw.startswith((b'\xff\xfe', b'\xfe\xff')) else 'utf-8-sig'
    data = json.loads(raw.decode(encoding), object_pairs_hook=_unique_object, parse_constant=_invalid_constant)
    if not isinstance(data, dict) or data.get('status') != 'CAPTURED':
        raise ValueError('A CAPTURED runtime object is required')
    frames = data.get('frames')
    if not isinstance(frames, list) or not frames:
        raise ValueError('A nonempty frame list is required')
    for index, frame in enumerate(frames):
        if not isinstance(frame, dict):
            raise ValueError(f'Invalid frame {index}')
        for collection, identity in [('actors', 'id'), ('objects', 'id'), ('ui', 'owner'), ('templates', 'id')]:
            if collection in ('ui', 'templates') and collection not in frame:
                continue
            entries = frame.get(collection)
            if not isinstance(entries, list):
                raise ValueError(f'Frame {index}: missing {collection} list')
            identities = set()
            for entry in entries:
                if not isinstance(entry, dict) or not isinstance(entry.get(identity), str) or not entry[identity]:
                    raise ValueError(f'Frame {index}: invalid {collection} identity')
                value = entry[identity]
                if value in identities:
                    raise ValueError(f'Frame {index}: duplicate {collection} {identity}: {value}')
                identities.add(value)
        for item in frame.get('ui', []):
            for key in ('visible', 'hasWidget', 'interactable', 'buttonActive', 'canvasEnabled', 'buttonEnabled'):
                if key in item and type(item[key]) is not bool:
                    raise ValueError(f'Frame {index}: UI {key} must be boolean')
            for key in ('awakes', 'enables', 'starts', 'disables', 'hits', 'order', 'offHits', 'childHits'):
                if key in item and type(item[key]) is not int:
                    raise ValueError(f'Frame {index}: UI {key} must be integer')
            if 'text' in item and not isinstance(item['text'], str):
                raise ValueError(f'Frame {index}: UI text must be string')
        if 'uiViewportWidgets' in frame and type(frame['uiViewportWidgets']) is not int:
            raise ValueError(f'Frame {index}: viewport count must be integer')
        if 'uiViewportWorldIndependent' in frame and type(frame['uiViewportWorldIndependent']) is not bool:
            raise ValueError(f'Frame {index}: viewport independence must be boolean')
        for actor in frame['actors']:
            state = actor.get('state')
            if isinstance(state, dict):
                names = list(state)
            elif isinstance(state, list) and all(isinstance(item, dict) and isinstance(item.get('name'), str) and 'value' in item for item in state):
                names = [item['name'] for item in state]
            else:
                raise ValueError(f'Frame {index}: invalid actor state')
            folded = [name.casefold() if case_insensitive_state else name for name in names]
            if len(folded) != len(set(folded)):
                raise ValueError(f'Frame {index}: duplicate case-insensitive state field')
    return data


def single_runtime_driver(actors):
    actors = list(actors)
    if len(actors) > 1:
        raise ValueError('Multiple runtime drivers in one captured frame')
    return actors[0] if actors else None


def register_checkpoint(rows, number, sequence=None):
    if str(number) in rows:
        raise ValueError(f'Duplicate checkpoint: {number}')
    expected = len(rows) + 1 if sequence is None else sequence[len(rows)] if len(rows) < len(sequence) else None
    if number != expected:
        raise ValueError(f'Out-of-order checkpoint: {number}, expected {expected}')


def checkpoint_values_equal(expected, actual):
    # Python considers True == 1. Runtime boolean state must retain its type.
    if isinstance(expected, bool) or isinstance(actual, bool):
        return type(expected) is bool and type(actual) is bool and expected == actual
    if isinstance(expected, dict):
        return isinstance(actual, dict) and expected.keys() == actual.keys() and all(checkpoint_values_equal(value, actual[key]) for key, value in expected.items())
    if isinstance(expected, list):
        return isinstance(actual, list) and len(expected) == len(actual) and all(checkpoint_values_equal(left, right) for left, right in zip(expected, actual))
    return expected == actual


def require_boolean_types(expected, actual):
    if isinstance(expected, bool) or isinstance(actual, bool):
        if type(expected) is not bool or type(actual) is not bool:
            raise ValueError('Boolean runtime state must not be replaced by a number')
    elif isinstance(expected, dict) and isinstance(actual, dict):
        for key in expected.keys() & actual.keys():
            require_boolean_types(expected[key], actual[key])
    elif isinstance(expected, list) and isinstance(actual, list):
        for left, right in zip(expected, actual):
            require_boolean_types(left, right)


def _write_report(path, report):
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = None
    try:
        with tempfile.NamedTemporaryFile(mode='w', encoding='utf-8', dir=path.parent, prefix=path.name + '.', suffix='.tmp', delete=False) as stream:
            temporary = Path(stream.name)
            json.dump(report, stream, ensure_ascii=False, indent=2)
        os.replace(temporary, path)
    finally:
        if temporary is not None and temporary.exists():
            temporary.unlink()


def run_comparison(arguments, summarize, semantics, metadata, success_message, dependencies=(), comparison=None):
    if len(arguments) != 3:
        raise ValueError('Expected original capture, actual capture, and report paths')
    expected_path, actual_path, output = [Path(arg).resolve() for arg in arguments]
    if output in (expected_path, actual_path):
        raise ValueError('Report output must not overwrite either input capture')
    provenance = {}
    scripts = {name: hashlib.sha256(path.read_bytes()).hexdigest() for name, path in [('comparerSha256', Path(sys.argv[0]).resolve()), ('captureValidationSha256', Path(__file__).resolve())]}
    for dependency in dependencies:
        dependency = Path(dependency).resolve()
        scripts[dependency.name + 'Sha256'] = hashlib.sha256(dependency.read_bytes()).hexdigest()
    try:
        for name, path in [('expected', expected_path), ('actual', actual_path)]:
            provenance[name] = {'path': str(path), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()}
        expected, actual = summarize(expected_path), summarize(actual_path)
        semantics(expected)
        if comparison is None:
            if not checkpoint_values_equal(expected, actual):
                raise ValueError('Runtime checkpoint states differ')
        else:
            require_boolean_types(expected, actual)
            comparison(expected, actual)
        semantics(actual)
        flags = metadata(expected, actual, actual_path)
        _write_report(output, {'status': 'PASS', 'checkpoints': actual, **flags, 'inputs': provenance, 'captureIntegrityVerified': True, 'verificationCode': scripts, 'projectPortComplete': False, 'renderingEquivalent': False})
    except Exception as error:
        _write_report(output, {'status': 'FAIL', 'error': f'{type(error).__name__}: {error}', 'verificationCode': scripts, 'inputs': provenance, 'projectPortComplete': False, 'renderingEquivalent': False})
        raise
    print(success_message)
