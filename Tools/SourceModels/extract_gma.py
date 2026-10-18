"""Extract a GMA v3 archive as data, without executing addon code."""
from pathlib import Path
import argparse
import struct

parser=argparse.ArgumentParser()
parser.add_argument('archive',type=Path)
parser.add_argument('output',type=Path)
args=parser.parse_args()
data=args.archive.read_bytes()
if data[:5]!=b'GMAD\x03': raise ValueError('Expected GMA version 3')
cursor=21
def string():
    global cursor
    end=data.index(b'\0',cursor)
    value=data[cursor:end].decode('utf-8')
    cursor=end+1
    return value
while string(): pass
print('Archive:',string())
string(); string(); cursor+=4
entries=[]
while True:
    number=struct.unpack_from('<I',data,cursor)[0]; cursor+=4
    if not number: break
    name=string()
    size,crc=struct.unpack_from('<qI',data,cursor); cursor+=12
    if size<0: raise ValueError('Invalid entry size')
    entries.append((name,size))
output=args.output.resolve(); output.mkdir(parents=True,exist_ok=True)
for name,size in entries:
    target=(output/name).resolve()
    if not target.is_relative_to(output): raise ValueError('Unsafe archive path')
    if cursor+size>len(data): raise ValueError('Truncated archive')
    target.parent.mkdir(parents=True,exist_ok=True)
    target.write_bytes(data[cursor:cursor+size]); cursor+=size
    print(name,size)
