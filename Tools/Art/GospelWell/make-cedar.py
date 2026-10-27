"""Original small painted-style cedar grain maps, not an edited source image."""
import hashlib
import json
from pathlib import Path
import numpy as np
from PIL import Image

ROOT=Path(__file__).resolve().parents[3]
OUT=ROOT/'Games/PathOfDawn/Assets/Resources/Art/GospelWell'
OUT.mkdir(parents=True,exist_ok=True)
N=512
y,x=np.mgrid[0:N,0:N]/N
rng=np.random.default_rng(27741)
warp=x+.014*np.sin(y*12)+.008*np.sin(y*33+x*7)
# Knots bend continuous grain without photographic/texture atlas seams.
for cx,cy in ((.27,.34),(.72,.76)):
    dx=x-cx;dy=(y-cy)*.38
    warp+=.018*np.sin(np.arctan2(dy,dx)*2)*np.exp(-(dx*dx+dy*dy)/.013)
grain=np.sin(warp*2*np.pi*37)
fine=np.sin(warp*2*np.pi*126)*.18
v=.68+.13*grain+.045*fine+.022*rng.standard_normal((N,N))
v=np.clip(v,.25,.95)
rgb=np.stack((v*.48+.11,v*.30+.067,v*.16+.035),axis=-1)
Image.fromarray(np.uint8(np.clip(rgb,0,1)*255),'RGB').save(OUT/'cedar-albedo.png')
h=.004*np.sin(warp*2*np.pi*37)+.0012*np.sin(warp*2*np.pi*126)
gy,gx=np.gradient(h)
normal=np.stack((-gx*34,-gy*34,np.ones_like(x)),axis=-1)
normal/=np.linalg.norm(normal,axis=-1,keepdims=True)
Image.fromarray(np.uint8((normal*.5+.5)*255),'RGB').save(OUT/'cedar-normal.png')
files=[]
for name in ('cedar-albedo.png','cedar-normal.png'):
    p=OUT/name
    files.append({'file':str(p.relative_to(ROOT)),'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'bytes':p.stat().st_size,'size':[N,N]})
(ROOT/'Tools/Art/GospelWell/cedar-manifest.json').write_text(json.dumps({'authorship':'Original deterministic grain textures authored for this project','seed':27741,'normal':'OpenGL +Y tangent normal','files':files},indent=2)+'\n')
