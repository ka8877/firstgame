"""Extract unchanged embedded image bytes and model metadata, never repaint them."""
from pathlib import Path
import hashlib
import json
import struct

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
SOURCE = HERE / 'Source/Characters'
OUT = HERE / 'Extracted'

def save(path, raw):
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists():
        assert path.read_bytes() == raw
    else:
        with path.open('xb') as stream:
            stream.write(raw)
    return {'file': str(path.relative_to(ROOT)), 'bytes': len(raw), 'sha256': hashlib.sha256(raw).hexdigest()}

records = []
for prefix, name in [('C', 'AvatarSample_C.vrm'), ('F', 'OfficialFemale.vrm')]:
    original = SOURCE / name
    raw = original.read_bytes()
    magic, version, total = struct.unpack_from('<4sII', raw)
    assert magic == b'glTF' and version == 2 and total == len(raw)
    length, chunk_type = struct.unpack_from('<II', raw, 12)
    assert chunk_type == 0x4E4F534A
    data = json.loads(raw[20:20+length])
    offset = 20 + length
    size, kind = struct.unpack_from('<II', raw, offset)
    assert kind == 0x004E4942
    binary = raw[offset+8:offset+8+size]
    images = []
    for index, image in enumerate(data.get('images', [])):
        view = data['bufferViews'][image['bufferView']]
        start, count = view.get('byteOffset', 0), view['byteLength']
        content = binary[start:start+count]
        assert len(content) == count
        extension = '.png' if image['mimeType'] == 'image/png' else '.jpg' if image['mimeType'] == 'image/jpeg' else None
        assert extension is not None
        images.append(dict(save(OUT / 'Textures' / (prefix + '_' + str(index).zfill(2) + extension), content), index=index, originalName=image.get('name', ''), mime=image['mimeType']))
    ext = data['extensions']
    if 'VRM' in ext:
        vrm = ext['VRM']
        humanoid = {item['bone']: data['nodes'][item['node']]['name'] for item in vrm['humanoid']['humanBones']}
        thumbnail_image = data['textures'][vrm['meta']['texture']]['source']
        metadata = vrm['meta']
        material_properties = vrm['materialProperties']
    else:
        vrm = ext['VRMC_vrm']
        humanoid = {key: data['nodes'][item['node']]['name'] for key, item in vrm['humanoid']['humanBones'].items()}
        thumbnail_image = vrm['meta']['thumbnailImage']
        metadata = vrm['meta']
        material_properties = []
    textures = data.get('textures', [])
    materials = []
    for index, material in enumerate(data['materials']):
        pbr = material.get('pbrMetallicRoughness', {})
        main = pbr.get('baseColorTexture', {}).get('index')
        if main is None and index < len(material_properties):
            main = material_properties[index].get('textureProperties', {}).get('_MainTex')
        image = images[textures[main]['source']] if main is not None else None
        materials.append({'index': index, 'originalName': material.get('name', 'Material'+str(index)),
                          'baseColorFactor': pbr.get('baseColorFactor', [1,1,1,1]), 'alphaMode': material.get('alphaMode', 'OPAQUE'),
                          'alphaCutoff': material.get('alphaCutoff', .5), 'doubleSided': material.get('doubleSided', False),
                          'mainTexture': image, 'sourceMaterial': material,
                          'vrm0Properties': material_properties[index] if index < len(material_properties) else None})
    model = {'prefix': prefix, 'source': str(original.relative_to(ROOT)), 'sourceSha256': hashlib.sha256(raw).hexdigest(),
             'licenseMetadata': metadata, 'humanBones': humanoid, 'materials': materials, 'images': images,
             'thumbnailImage': images[thumbnail_image], 'nodes': data['nodes'], 'meshes': data['meshes'], 'skins': data['skins'],
             'scope': 'Exact extracted source metadata and unchanged image bytes; no finished character/animation/rendering claim.'}
    save(OUT / (prefix + '-model.json'), (json.dumps(model, indent=2)+'\n').encode())
    records.append({'prefix': prefix, 'bones': len(humanoid), 'images': len(images), 'materials': len(materials), 'thumbnail': images[thumbnail_image]})
print(json.dumps({'status': 'exact-vrm-metadata-and-image-bytes-extracted', 'models': records}))
