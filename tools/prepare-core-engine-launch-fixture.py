from pathlib import Path
import re,json
root=Path('artifacts/core-engine-launch-tests');root.mkdir(exist_ok=True)
parts=[]
for name in ['loop','switch','conditional','constant','division']:
 parts.append((Path('artifacts')/(name+'-statement-check')/'original.cs').read_text(encoding='utf-8-sig'))
parts.append(Path('artifacts/reference-conditional-check/original.cs').read_text(encoding='utf-8-sig'))
source='using UnityEngine;\n'+'\n'.join(text.replace('using UnityEngine;','') for text in parts)
for name in ['LoopActor','SwitchActor','ConditionalActor','ConstantActor','DivisionActor','ReferenceConditional']:
 source=re.sub(r'\b'+name+r'\b','Audit'+name,source)
(root/'source.cs').write_text(source,encoding='utf-8')
