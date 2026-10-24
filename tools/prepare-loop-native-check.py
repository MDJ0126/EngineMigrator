"""Extract actual generated methods for engine-free C++ control-flow verification."""
import hashlib
import json
from pathlib import Path
import re
import argparse

parser = argparse.ArgumentParser()
parser.add_argument('--profile', choices=['loop', 'switch', 'conditional', 'constant', 'division'], default='loop')
profile = parser.parse_args().profile
class_name = {'loop': 'LoopActor', 'switch': 'SwitchActor', 'conditional': 'ConditionalActor', 'constant': 'ConstantActor', 'division': 'DivisionActor'}[profile]
root = Path(__file__).resolve().parent.parent / 'artifacts' / (profile + '-statement-check')
header = (root / (class_name + '.h')).read_text(encoding='utf-8')
blocks = re.findall(r'// EM_METHOD ([^\n]+)\n(.*?)// EM_END_METHOD', header, re.S)
methods = {}
expected_methods = {
    'loop': {'Bump', 'Run', 'Early', 'Overflow', 'Ordered', 'Inferred'},
    'switch': {'Next', 'Pick', 'NoDefault', 'Empty', 'DefaultOnly', 'Shared', 'Run'},
    'conditional': {'Ready', 'Next', 'Choose', 'Scalar', 'BooleanChoice', 'Nested', 'Wide'},
    'constant': {'Next', 'Compute', 'Bounds', 'Ratio', 'Wide', 'Cross', 'Guard', 'Zero', 'Precision', 'Negative'},
    'division': {'Q2', 'QNeg2', 'Mod3', 'ModNeg3', 'QMax', 'QMin', 'ModMin', 'Compound', 'Loop'},
}[profile]
for metadata, block in blocks:
    name = json.loads(metadata)['Name']
    if name in expected_methods:
        block = re.sub(r'^public:\s*\n', '', block)
        block = re.sub(r'^UFUNCTION\([^\n]+\)\s*\n', '', block, flags=re.M)
        methods[name] = block
if set(methods) != expected_methods:
    raise RuntimeError('Missing generated methods')
helpers = re.findall(r'^static int32 EM_Int(?:Add|Sub|Mul|Neg)\([^\n]+$', header, re.M)
if len(helpers) != 4:
    raise RuntimeError('Missing exact generated integer helpers')
field_pattern = {'loop': 'Sum|Index', 'switch': 'Calls|Score', 'conditional': 'Calls', 'constant': 'Calls', 'division': 'Field'}[profile]
fields = re.findall(r'^int32 (?:' + field_pattern + r') = 0;', header, re.M)
if len(fields) != (1 if profile in {'conditional', 'constant', 'division'} else 2):
    raise RuntimeError('Missing generated fields')
source = '''#include <cstdint>
#include <cstring>
#include <iostream>
#include <cmath>
using int32=std::int32_t;using uint32=std::uint32_t;
struct FMemory {static void Memcpy(void* d,const void* s,std::size_t n){std::memcpy(d,s,n);}};
struct ''' + class_name + ''' {
''' + '\n'.join(helpers + fields + list(methods.values())) + '''
};
'''
if profile == 'loop':
    source += 'int main(){LoopActor a;a.Run();std::cout<<a.Sum<<"|"<<a.Index<<"|"<<a.Early()<<"|"<<a.Overflow()<<"|"<<a.Ordered()<<"|"<<a.Inferred()<<"\\n";}\n'
    expected = '123|3|3|2|7|6'
elif profile == 'switch':
    source += 'int main(){SwitchActor a;for(int v:{INT32_MIN,-1,2,3,9}){std::cout<<a.Pick(v)<<"|";}a.Run();a.Empty(9);for(int v:{0,1,2,3,9}){std::cout<<a.Shared(v)<<"|";}std::cout<<a.Calls<<"|"<<a.Score<<"|"<<a.NoDefault(9)<<"|"<<a.NoDefault(1)<<"|"<<a.DefaultOnly(4)<<"\\n";}\n'
    expected = '1|2|12|13|29|9|18|11|12|0|17|68|0|7|5'
elif profile == 'conditional':
    source += 'int main(){ConditionalActor a;for(int v:{1,0,-1}){std::cout<<a.Choose(v)<<"|";}std::cout<<a.Calls<<"|"<<a.Scalar(true)<<"|"<<a.Scalar(false)<<"|"<<a.BooleanChoice(true)<<"|"<<a.BooleanChoice(false)<<"|"<<a.Nested(-1)<<"|"<<a.Nested(0)<<"|"<<a.Nested(1)<<"|"<<a.Wide(true)<<"|"<<a.Wide(false)<<"\\n";}\n'
    expected = '10|-10|-10|6|1.25|2|1|0|-1|0|1|1|2.5'
elif profile == 'constant':
    source += 'int main(){ConstantActor a;float p=a.Precision();uint32 bits;std::memcpy(&bits,&p,sizeof(bits));std::cout<<a.Compute(9)<<"|"<<a.Compute(0)<<"|"<<a.Bounds()<<"|"<<a.Ratio()<<"|"<<a.Wide()<<"|"<<a.Cross(0)<<"|"<<a.Cross(1)<<"|"<<a.Cross(9)<<"|"<<a.Guard(true)<<"|"<<a.Guard(false)<<"|"<<a.Calls<<"|"<<(std::signbit(a.Zero())?1:0)<<"|"<<bits<<"|"<<a.Negative()<<"\\n";}\n'
    expected = '48|49|-1|2.5|3.25|7|8|0|10|-10|2|1|1066192077|-0.125'
else:
    source += 'int main(){DivisionActor a;bool first=true;for(int v:{INT32_MIN,INT32_MIN+1,-17,-1,0,1,17,INT32_MAX}){if(!first)std::cout<<"/";first=false;std::cout<<v<<":"<<a.Q2(v)<<","<<a.QNeg2(v)<<","<<a.Mod3(v)<<","<<a.ModNeg3(v)<<","<<a.QMax(v)<<","<<a.QMin(v)<<","<<a.ModMin(v)<<","<<a.Compound(v);}std::cout<<"/loop:"<<a.Loop()<<"\\n";}\n'
    def q(a, b):
        return (abs(a) // abs(b)) * (-1 if (a < 0) != (b < 0) else 1)
    def r(a, b):
        return a - q(a, b) * b
    rows = []
    for v in [-2147483648, -2147483647, -17, -1, 0, 1, 17, 2147483647]:
        values = [q(v, 2), q(v, -2), r(v, 3), r(v, -3), q(v, 2147483647), q(v, -2147483648), r(v, -2147483648), r(q(v, 2), 3) + r(q(v, -2), 5)]
        rows.append(str(v) + ':' + ','.join(map(str, values)))
    expected = '/'.join(rows) + '/loop:7'
(root / 'native-main.cpp').write_text(source, encoding='utf-8')
states = (root / 'managed-states.txt').read_text(encoding='utf-8-sig').splitlines()
if states != [expected, expected]:
    raise RuntimeError('Original/restored managed results differ')
(root / 'expected.txt').write_text(states[0] + '\n', encoding='ascii')
(root / 'native-extraction.json').write_text(json.dumps({
    'generatedHeaderSha256': hashlib.sha256((root / (class_name + '.h')).read_bytes()).hexdigest(),
    'nativeSourceSha256': hashlib.sha256((root / 'native-main.cpp').read_bytes()).hexdigest(),
    'methodsExtractedUnchanged': sorted(methods),
    'engineDependencies': False,
}, indent=2), encoding='utf-8')
print('GENERATED_' + profile.upper() + '_METHODS_EXTRACTED')
