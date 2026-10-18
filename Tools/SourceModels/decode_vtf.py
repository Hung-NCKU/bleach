"""Decode one source VTF texture to PNG using srctools and Pillow."""
from pathlib import Path
import argparse
from srctools.vtf import VTF

parser=argparse.ArgumentParser()
parser.add_argument('source',type=Path)
parser.add_argument('output',type=Path)
args=parser.parse_args()
with args.source.open('rb') as file:
    texture=VTF.read(file)
    args.output.parent.mkdir(parents=True,exist_ok=True)
    texture.get().to_PIL().save(args.output)
    print(texture.width,texture.height,texture.format)
