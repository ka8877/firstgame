#!/usr/bin/env python3
"""Copy selected original Lost Crypt art, preserving pixels and documenting import changes.

This does not edit images. Runtime sprite UV regions are separately specified by
the world renderer. Unity import settings control compression and normal usage.
"""
from pathlib import Path
from PIL import Image
import hashlib
import json
import re
import shutil

ROOT = Path(__file__).resolve().parents[4]
SOURCE = Path('/tmp/firstgame-luminous-reference/official-lostcrypt/LostCrypt/Assets/Environment')
DEST = ROOT / 'Games/PathOfDawn/Assets/Resources/Art/GospelLuminous/World'
GEN = Path('/workspace/generated_images/exec-d6a1538a-aac3-479c-8752-0339840ed670.png')
FILES = {
    **{f'Tree{i}': f'Sprites/Vegetation/Sprite_Tree_{i}.png' for i in (1,2,3)},
    **{f'Trees{i}': f'Sprites/Vegetation/Sprite_Trees_{i}.png' for i in (1,2)},
    **{f'Trunk{i}': f'Sprites/Vegetation/Sprite_TrunkAndBranches_{i}_Color.png' for i in (1,2)},
    **{f'Foliage{i}': f'Sprites/Vegetation/Sprite_Foliage_{i}_Color.png' for i in (1,2,3)},
    **{f'Foliage{i}Normal': f'Sprites/Vegetation/Sprite_Foliage_{i}_Normal.png' for i in (1,2,3)},
    **{f'Rock{i}': f'Sprites/Rocks/Sprite_Rock_{i}_Color.png' for i in (1,2,3,4)},
    'Sky': 'Sprites/Sky/Sprite_Sky_Gradient.png',
    'CloudHorizon': 'Sprites/Clouds/Sprite_Cloud_Horizon.png',
    'Cloud1': 'Sprites/Clouds/Sprite_Cloud_1.png',
    'Cloud2': 'Sprites/Clouds/Sprite_Cloud_2.png',
    'LightShafts': 'Sprites/Lights/Sprite_Shafts_1.png',
    'SoftGlow': 'Sprites/Lights/Sprite_Soft_1.png',
    'SunSpots': 'Sprites/Lights/Sprite_SunSpots_1.png',
    'GrassFill': 'Sprite Shapes/Sprites/Sprite_Fill_Grass_1.png',
    'GrassFill2': 'Sprite Shapes/Sprites/Sprite_Fill_Grass_2.png',
    'GrassEdge': 'Sprite Shapes/Sprites/Sprite_Strip_Grass_1.png',
    'GrassWild': 'Sprite Shapes/Sprites/Sprite_Strip_Grass_3.png',
    'Flowers': 'Sprite Shapes/Sprites/Sprite_Strip_Flowers_1.png',
}


def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def guid(path): return hashlib.md5(('firstgame-luminous-world/' + path).encode()).hexdigest()


def folder(path):
    path.mkdir(parents=True, exist_ok=True)
    if not path.with_name(path.name + '.meta').exists():
        path.with_name(path.name + '.meta').write_text(
            f'fileFormatVersion: 2\nguid: {guid(str(path.relative_to(ROOT)))}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')


def main():
    for p in [DEST.parent, DEST]: folder(p)
    rows=[]
    for name, relative in FILES.items():
        source = SOURCE / relative
        with Image.open(source) as image:
            image.verify()
        target = DEST / (name + '.png')
        shutil.copyfile(source, target)
        text = source.with_name(source.name + '.meta').read_text()
        original_guid = re.search(r'^guid: ([0-9a-f]+)', text, re.M).group(1)
        text = re.sub(r'^guid: [0-9a-f]+', 'guid: ' + guid(name), text, flags=re.M)
        text = re.sub(r'maxTextureSize: \d+', 'maxTextureSize: 2048', text)
        text = re.sub(r'enableMipMap: \d+', 'enableMipMap: 0', text)
        if name.endswith('Normal'):
            text = re.sub(r'textureType: \d+', 'textureType: 1', text)
            text = re.sub(r'sRGBTexture: \d+', 'sRGBTexture: 0', text)
        if name.startswith('GrassFill'):
            text = re.sub(r'wrap([UVW]): \d+', r'wrap\1: 0', text)
        if name == 'SoftGlow':
            with Image.open(source) as cookie:
                text = re.sub(r'spritePixelsToUnits: [\d.]+', 'spritePixelsToUnits: ' + str(cookie.height / 2), text)
        target.with_name(target.name + '.meta').write_text(text)
        im=Image.open(target)
        rows.append({'resource': name, 'source': relative, 'sourceGuid': original_guid,
            'sourceSha256': sha(source), 'copiedSha256': sha(target), 'pixelBytesUnchanged': sha(source)==sha(target),
            'size': list(im.size), 'mode': im.mode, 'resourceGuid': guid(name),
            'sourceRepository': 'https://github.com/Unity-Technologies/mobileQA-LostCrypt-AutoPlay-Deterministic',
            'commit': '96015220e0f35b18b3ad390d38f97ddcc38bb514'})
    generated = DEST/'TempleGateway.png'
    shutil.copyfile(GEN, generated)
    template=(DEST/'Tree1.png.meta').read_text()
    template=re.sub(r'^guid: [0-9a-f]+','guid: '+guid('TempleGateway'),template,flags=re.M)
    generated.with_name(generated.name+'.meta').write_text(template)
    license_text=Path('/tmp/firstgame-luminous-reference/UnityCompanionLicense.txt').read_text()
    (DEST/'SOURCE-NOTICE.txt').write_text(
        'LOST CRYPT ORIGINAL ENVIRONMENT ART\nCopyright Unity Technologies.\n'
        'Original files are copied byte-for-byte from the official Unity sample repository, commit 96015220e0f35b18b3ad390d38f97ddcc38bb514.\n'
        'https://github.com/Unity-Technologies/mobileQA-LostCrypt-AutoPlay-Deterministic\n'
        'The official sample listing permits repurposing its assets in Unity projects: https://assetstore.unity.com/packages/essentials/tutorial-projects/lost-crypt-2d-sample-project-158673\n'
        'Unity import GUIDs and compression settings are adapted; all image bytes remain unchanged.\n'
        'TempleGateway.png is a separately generated original development asset, not part of Lost Crypt or an archaeological reconstruction.\n\n' + license_text)
    manifest={'sourceFiles': rows, 'generated': {'resource':'TempleGateway','source':str(GEN),'sha256':sha(GEN),'copiedSha256':sha(generated),
        'method':'image_gen transparent original painterly Jerusalem garden gateway; no post-generation pixel editing'},
        'importChanges':['New deterministic resource GUIDs, original source GUIDs retained in ledger.',
            '2048 maximum import dimension, no mipmaps; source PNG bytes preserved.',
            'Foliage normal textures imported as linear normal maps for URP2D lights.',
            'Ground fill textures repeat in generated ground-ribbon UVs.',
            'SoftGlow sprite pixels per unit is source height/2, making the original cookie two world units high.'],
        'license':'Unity Companion License 1.4; complete notice included with assets.',
        'spriteRegions':'Grass and flower source padding is addressed with runtime UV rectangles; the source images are not cropped or edited.'}
    (Path(__file__).parent/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
    print(json.dumps({'copiedOriginals':len(rows),'bytes':sum(p.stat().st_size for p in DEST.glob('*.png')),'destination':str(DEST)}))


if __name__ == '__main__': main()
