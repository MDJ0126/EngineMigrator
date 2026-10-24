"""Required actual-engine gate; models never substitute for execution evidence."""
from pathlib import Path
import json
from object_equality_evidence import verify

root=Path(__file__).resolve().parent.parent
print(json.dumps(verify(root,root/'artifacts/object-equality-check')))
