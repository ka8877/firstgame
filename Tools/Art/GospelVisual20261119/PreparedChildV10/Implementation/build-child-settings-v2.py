#!/usr/bin/env python3
"""Pure report/GLB metadata -> GospelHeroChildPreparationV2 Settings JSON.

No bpy, Unity, image edits or game-state access. Root supplies exact real bake
and inspection pins; this program only emits new config/audit JSON files.
Optional, explicitly pinned CC0 rough_linen color/normal maps affect only
generated linen/facing materials. V1 defaults remain when these are absent.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import re
import struct
import sys

SOURCE_SHA = '7526838f4a45086a1d0abb23e9f38ad3f0103889f1efa70d2605ef25e7e6ef99'
PREFIX = 'N00_000_00_'
ROLES = {
    PREFIX + 'Face_00_SKIN': 'Face', PREFIX + 'FaceMouth_00_FACE': 'Face',
    PREFIX + 'EyeIris_00_EYE': 'Eyes', PREFIX + 'EyeWhite_00_EYE': 'Eyes',
    PREFIX + 'EyeHighlight_00_EYE': 'Eyes', PREFIX + 'FaceBrow_00_FACE': 'BrowsLashes',
    PREFIX + 'FaceEyelash_00_FACE': 'BrowsLashes', PREFIX + 'FaceEyeline_00_FACE': 'BrowsLashes',
    PREFIX + 'Body_00_SKIN': 'Skin', PREFIX + 'HairBack_00_HAIR': 'Hair',
    'N00_000_Hair_00_HAIR': 'Hair', 'N00_001_01_Bottoms_01_CLOTH': 'Linen',
    'N00_004_01_Shoes_01_CLOTH': 'Linen', 'N00_004_01_Tops_01_CLOTH': 'Linen',
}
FLAT_NORMALS = {'f8f4e1ddb775ff90723cf3da6f2b8e8537482059a894437564dfaf2b8db85e53': [128, 128, 255],
                'd76fc537166ea42dad9f97cde34574ae348397e1279710fb69b8f2e743b2cc95': [127, 127, 255]}
LINEN_SOURCE = {
    'page': 'https://polyhaven.com/a/rough_linen', 'license': 'CC0',
    'authors': {'colormass': 'Photography', 'Rico Cilliers': 'Processing'},
    'manifest': 'Tools/Art/GospelAuthored/linen-source-manifest.json',
    'diffuse': {'sha256': '04897d7611d7390ddf5b2c6f7273f448dfc48341fdbd1f4f207fa30b331d7f57', 'bytes': 835801},
    'normal': {'sha256': 'e2402166d1a489f7414814e69e1836ab672aa24e30bec2d41fea356f81687e53', 'bytes': 916843},
}


def need(value, message):
    if not value: raise RuntimeError(message)


def digest(payload): return hashlib.sha256(payload).hexdigest()


def regular(value):
    path = Path(value)
    need(path.is_absolute() and path.is_file() and not any(p.is_symlink() for p in (path, *path.parents)),
         'Exact absolute nonsymlink input file required: ' + str(path))
    return path


def pin(value, expected_sha=None, expected_bytes=None):
    path = regular(value); payload = path.read_bytes(); actual = digest(payload)
    if expected_sha is not None: need(actual == expected_sha, 'Input SHA mismatch: ' + str(path))
    if expected_bytes is not None: need(len(payload) == expected_bytes, 'Input size mismatch: ' + str(path))
    return dict(file=str(path), sha256=actual, bytes=len(payload)), payload


def unique(pairs):
    data = {}
    for key, value in pairs:
        need(key not in data, 'Duplicate JSON key: ' + key); data[key] = value
    return data


def json_bytes(payload):
    return json.loads(payload, object_pairs_hook=unique,
                      parse_constant=lambda value: (_ for _ in ()).throw(ValueError('Nonfinite JSON: ' + value)))


def glb_document(raw):
    need(raw[:4] == b'glTF' and len(raw) >= 20, 'Exact self-contained GLB/VRM required')
    _, version, size = struct.unpack_from('<III', raw)
    length, kind = struct.unpack_from('<II', raw, 12)
    need(version == 2 and size == len(raw) and kind == 0x4e4f534a and 20 + length <= size, 'Exact GLB2 JSON chunk required')
    return json_bytes(raw[20:20 + length].rstrip(b' \x00\r\n'))


def finite_vector(value, count, name):
    need(isinstance(value, list) and len(value) == count
         and all(type(v) in (int, float) and math.isfinite(v) for v in value), name + ' must be finite numeric data')
    return [float(v) for v in value]


def texture_info(document, info):
    if info is None: return None
    index = info['index']; need(type(index) is int and 0 <= index < len(document['textures']), 'Actual texture index out of bounds')
    image = document['textures'][index].get('source')
    need(type(image) is int and 0 <= image < len(document['images']), 'Actual texture->image mapping absent')
    transform = info.get('extensions', {}).get('KHR_texture_transform', {})
    result = dict(image=image, uvChannel=transform.get('texCoord', info.get('texCoord', 0)),
        uvScale=finite_vector(transform.get('scale', [1, 1]), 2, 'UV scale'),
        uvOffset=finite_vector(transform.get('offset', [0, 0]), 2, 'UV offset'),
        uvRotation=transform.get('rotation', 0))
    need(result['uvChannel'] == 0 and type(result['uvRotation']) in (int, float)
         and math.isfinite(result['uvRotation']) and abs(result['uvRotation']) < 1e-8,
         'Non-UV0/rotated source texture requires an actual mesh UV bake; do not ignore it')
    return result


def write_new(path, value):
    need(path.is_absolute() and path.parent.is_dir() and not path.exists()
         and not any(p.is_symlink() for p in (path, *path.parents)), 'New absolute output with existing parent required')
    payload = (json.dumps(value, indent=2, ensure_ascii=False, allow_nan=False) + '\n').encode()
    with path.open('xb') as stream: need(stream.write(payload) == len(payload), 'Short config output write')
    return dict(file=str(path), sha256=digest(payload), bytes=len(payload))


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--bake-report', required=True); p.add_argument('--bake-report-sha256', required=True)
    p.add_argument('--baker-source-sha256', required=True)
    p.add_argument('--controller-asset', required=True); p.add_argument('--output-version', required=True)
    p.add_argument('--output', required=True)
    p.add_argument('--flat-normal-pixels'); p.add_argument('--flat-normal-pixels-sha256')
    p.add_argument('--verified-eye-material', action='append', default=[])
    p.add_argument('--hair-flow-mode', type=int, choices=(0, 1, 2), required=True)
    p.add_argument('--hair-uv-flow', type=float, nargs=2, default=[1, 0])
    p.add_argument('--brow-linear-tint', type=float, nargs=3)
    for kind in ('diffuse', 'normal'):
        p.add_argument('--linen-' + kind)
        p.add_argument('--linen-' + kind + '-sha256')
        p.add_argument('--linen-' + kind + '-bytes', type=int)
    p.add_argument('--linen-tiling', type=float, nargs=2)
    p.add_argument('--linen-normal-strength', type=float)
    args = p.parse_args()
    need(re.fullmatch(r'[A-Za-z0-9_-]+', args.output_version), 'Fresh safe output version required')
    need(args.controller_asset.startswith('Assets/') and '..' not in args.controller_asset
         and args.controller_asset.endswith('.controller'), 'Explicit project-relative admitted controller asset required')
    report_pin, payload = pin(args.bake_report, args.bake_report_sha256); report = json_bytes(payload)
    need(report.get('schema') == 2 and report.get('identity') == 'JesusChild' and report.get('sourceSha256') == SOURCE_SHA
         and report.get('bakerSourceSha256') == args.baker_source_sha256 and re.fullmatch('[0-9a-f]{64}', args.baker_source_sha256),
         'Exact successful new child bake/source-code provenance required')
    need(report.get('originalBoneCount') == 109 and abs(report.get('desiredHeight', 0) - 1.42) < 1e-8
         and report.get('faceSourceHash') == report.get('faceFinalHash')
         and report.get('hairSourceHash') == report.get('hairFinalHash')
         and report.get('bodyPositionsNativeWeightsMorphsBefore') == report.get('bodyPositionsNativeWeightsMorphsAfter')
         and report.get('bodyHairBackRegionBefore') == report.get('bodyHairBackRegionAfter')
         and report.get('faceHairAndRetainedBodyHairBackCornerNormalsPreserved') is True,
         'Bake must retain actual admitted face/hair/body-native payloads')
    for field in ('faceSourceHash', 'hairSourceHash', 'rigRestHash', 'bodyHairBackRegionBefore'):
        need(re.fullmatch('[0-9a-f]{64}', report.get(field, '')), 'Missing actual preserved hash: ' + field)
    original_pin, raw = pin(report['sourceFile'], SOURCE_SHA, report['sourceBytes']); document = glb_document(raw)
    model_pin, _ = pin(report['fbx'], report['fbxSha256'], report['fbxBytes'])
    old = report['rootInspection']; inspection_pin, old_payload = pin(old['path'], old['sha256']); inspection = json_bytes(old_payload)
    need(inspection['sourceSha256'] == SOURCE_SHA, 'Actual original inspection source differs')

    flat_rows, flat_pin = {}, None
    need(bool(args.flat_normal_pixels) == bool(args.flat_normal_pixels_sha256), 'Flat normal inspection requires path+SHA together')
    if args.flat_normal_pixels:
        flat_pin, flat_payload = pin(args.flat_normal_pixels, args.flat_normal_pixels_sha256)
        flat = json_bytes(flat_payload)
        need(flat.get('status') == 'original-normal-pixels-inspected-not-edited', 'Actual original-pixel report required')
        for row in flat['images']:
            expected = FLAT_NORMALS.get(row['sha256'])
            if expected is None: continue
            need(row['distinctColors'] == 1 and row['dominantColor'] == expected and row['stddev'] == [0, 0, 0]
                 and row['dominantPixels'] == row['size'][0] * row['size'][1]
                 and row['extrema'] == [[v, v] for v in expected], 'Uniform normal omission requires exact real pixel evidence')
            flat_rows[row['sha256']] = row

    native_names = [b['name'] for b in report['bones']]
    need(len(native_names) == len(set(native_names)) == 109, 'Actual exported/native bone names must be unique109')
    face_shapes = report['faceShapes']; need(len(face_shapes) == 123, 'Actual Basis+122 face shapes required')
    need(all(abs(s['value']) < 1e-7 for s in face_shapes[1:]), 'Nonzero source defaults require an explicit importer-default contract; do not silently zero them')
    morphs = [s['name'] for s in face_shapes[1:]]; need(len(set(morphs)) == 122, 'Unique actual face morph names required')
    humanoid = [dict(humanName=v['unitySemantic'], boneName=v['name']) for _, v in sorted(report['humanoid'].items())]
    need(all(b['boneName'] in native_names for b in humanoid), 'Use actual imported aliases, not guessed raw long bone names')
    expressions = []
    for preset in report['expressions']:
        for bind in preset['binds']:
            weight = bind['weight'] / 100.0
            need(0 < weight <= 1 and bind['shape'] in morphs, 'Actual source preset shape/normalized weight required')
            expressions.append(dict(preset=preset['preset'], renderer=report['faceRenderer'], shapeKey=bind['shape'], weight=weight))
    objects = set(report['materialRoleContract']['objects'].values())
    original_max = max(m['maximumInfluences'] for m in inspection['meshes'] if m['object'] in objects)
    native_max = max([original_max] + [v['maximumInfluences'] for v in report['garmentNativeBindings'].values()])
    need(0 < native_max <= 32 and all(v.get('truncated') is False for v in report['garmentNativeBindings'].values()), 'Actual complete exported influence range required')

    image_rows = {r['index']: r for r in report['images']}; image_pins = {}
    for index, image in image_rows.items(): image_pins[index], _ = pin(image['file'], image['sha256'], image['bytes'])
    raw_materials = {r['name']: r for r in report['sourceMaterials']}
    mappings = {r['exportName']: r for r in report['exportMaterialMappings']}
    generated = {r['name']: r for r in report['generatedMaterials']}
    exported_set = {name for names in report['exportedMaterialNames'].values() for name in names}
    need(None not in exported_set, 'No missing exported material is allowed')
    exported_names = sorted(exported_set)
    verified_eyes = set(args.verified_eye_material)
    need(verified_eyes <= {PREFIX + 'EyeIris_00_EYE', PREFIX + 'EyeWhite_00_EYE'}, 'Only actual verified iris/sclera surfaces may opt into new eyes; highlight planes remain legacy')
    flow = finite_vector(args.hair_uv_flow, 2, 'Hair UV direction')
    need(args.hair_flow_mode != 2 or sum(v * v for v in flow) > 1e-8, 'Authored constant UV flow must be nonzero')
    brow = finite_vector(args.brow_linear_tint, 3, 'Reviewed brow linear tint') if args.brow_linear_tint else None
    need(brow is None or all(0 <= v <= 1 for v in brow), 'Brow tint is a linear multiplier in0..1')
    textures, texture_keys, materials, decisions = [], {}, [], []
    linen_pins = {}
    for kind in ('diffuse', 'normal'):
        values = [getattr(args, 'linen_' + kind + suffix) for suffix in ('', '_sha256', '_bytes')]
        present = [value is not None for value in values]
        need(not any(present) or all(present), 'Optional linen ' + kind + ' requires explicit path/SHA/bytes together')
        if all(present):
            admitted = LINEN_SOURCE[kind]
            need(values[1] == admitted['sha256'] and values[2] == admitted['bytes'],
                 'Only the text-manifest-admitted CC0 rough_linen original is supported: ' + kind)
            linen_pins[kind], _ = pin(values[0], values[1], values[2])
            textures.append(dict(key='generated-linen-' + kind, source=linen_pins[kind],
                normal=kind == 'normal', data=False, alpha=False, maximumSize=1024))
    need(bool(linen_pins) or args.linen_tiling is None, 'Linen tiling requires an explicitly pinned linen map')
    need('normal' in linen_pins or args.linen_normal_strength is None, 'Linen normal strength requires its explicitly pinned normal map')
    linen_tiling = finite_vector(args.linen_tiling if args.linen_tiling is not None else [4, 4], 2, 'Linen tiling')
    need(all(1 <= value <= 24 for value in linen_tiling), 'Reviewed linen tiling must stay in1..24')
    linen_strength = args.linen_normal_strength if args.linen_normal_strength is not None else .16
    need(math.isfinite(linen_strength) and 0 <= linen_strength <= .5, 'Modest linen normal strength must stay in0..0.5')

    def texture_key(index, normal=False, alpha=False):
        # A MASK/BLEND importer may dilate RGB into transparent texels. Keep
        # that derived import separate from an OPAQUE use of the same PNG.
        key = (index, normal, bool(alpha and not normal))
        if key not in texture_keys:
            name = 'source-image-%03d%s' % (index, '-normal' if normal else '-alpha' if alpha else '')
            texture_keys[key] = name
            textures.append(dict(key=name, source=image_pins[index], normal=normal, data=False,
                                 alpha=bool(alpha and not normal), maximumSize=2048))
        return texture_keys[key]

    for name in exported_names:
        if name in generated:
            row = generated[name]; role = {'linen': 'Linen', 'facing': 'Linen', 'leather': 'Leather'}[row['role']]
            cloth_maps = bool(linen_pins) and row['role'] in ('linen', 'facing')
            color_key = 'generated-linen-diffuse' if cloth_maps and 'diffuse' in linen_pins else ''
            normal_key = 'generated-linen-normal' if cloth_maps and 'normal' in linen_pins else ''
            materials.append(dict(sourceName=name, role=role, textureKey=color_key, normalTextureKey=normal_key,
                normalScale=linen_strength if normal_key else 1,
                color=row['baseColorFactorLinear'], hsvg=[0, 1, 1, 1], artisticTintSrgb=[1, 1, 1, 1],
                alphaMode=row['alphaMode'], cutoff=row['alphaCutoff'], doubleSided=row['doubleSided'], heroSurface='Legacy',
                uvScale=linen_tiling if cloth_maps else [1, 1], uvOffset=[0, 0], uvChannel=0, uvRotation=0))
            decisions.append(dict(exportName=name, sourceName=None, surface='Legacy', generatedRole=row['role'],
                linenMapsApplied=cloth_maps, textureKey=color_key, normalTextureKey=normal_key,
                originalLinearFactorRetained=True))
            continue
        mapping = mappings.get(name); original_name = mapping['sourceMaterial'] if mapping else name
        need(original_name in raw_materials and original_name in ROLES, 'Unreviewed exact exported source material: ' + name)
        source = raw_materials[original_name]; raw_material = source['rawMaterial']; role = ROLES[original_name]
        color = list(mapping['baseColorFactorLinear'] if mapping else source['baseColorFactor'])
        if original_name == PREFIX + 'FaceBrow_00_FACE' and brow is not None:
            color = [color[i] * brow[i] for i in range(3)] + [color[3]]
        mode = mapping['alphaMode'] if mapping else raw_material.get('alphaMode', 'OPAQUE')
        cutoff = mapping['alphaCutoff'] if mapping else raw_material.get('alphaCutoff', .5)
        double_sided = mapping['doubleSided'] if mapping else raw_material.get('doubleSided', False)
        need(mode in ('OPAQUE', 'MASK', 'BLEND'), 'Exact source alpha mode required')
        base = texture_info(document, raw_material.get('pbrMetallicRoughness', {}).get('baseColorTexture'))
        if mapping: need(base and base['image'] == mapping['baseColorImageIndex'], 'FBX shim base-image mapping differs from raw selected source')
        transform = base or dict(uvChannel=0, uvScale=[1, 1], uvOffset=[0, 0], uvRotation=0)
        surface = 'Face' if original_name == PREFIX + 'Face_00_SKIN' and mode != 'BLEND' else 'Hair' if role == 'Hair' and mode != 'BLEND' else 'Eyes' if original_name in verified_eyes and mode != 'BLEND' else 'Legacy'
        normal = texture_info(document, raw_material.get('normalTexture')); normal_key = ''; omission = None
        if normal:
            need(all(normal[k] == transform[k] for k in ('uvChannel', 'uvScale', 'uvOffset', 'uvRotation')), 'Normal UV transform differs from base; one shared transform cannot represent it')
            image = image_rows[normal['image']]
            if surface in ('Face', 'Eyes'):
                need(image['sha256'] in flat_rows, 'New Face/Eyes may omit only a pinned proven-uniform original normal; retain genuine detail on legacy path')
                omission = dict(reason='uniform-original-normal-has-no-spatial-detail', sourceImageSha256=image['sha256'],
                                sourceImageIndex=normal['image'], evidenceReport=flat_pin)
            else: normal_key = texture_key(normal['image'], True)
        material = dict(sourceName=name, role=role, textureKey=texture_key(base['image'], False, mode != 'OPAQUE') if base else '',
            normalTextureKey=normal_key, normalScale=raw_material.get('normalTexture', {}).get('scale', 1),
            color=finite_vector(color, 4, 'Material linear factor'), hsvg=[0, 1, 1, 1], artisticTintSrgb=[1, 1, 1, 1],
            alphaMode=mode, cutoff=cutoff, doubleSided=double_sided, heroSurface=surface,
            uvScale=transform['uvScale'], uvOffset=transform['uvOffset'], uvChannel=0, uvRotation=0,
            fittedEyeVerified=original_name in verified_eyes, eyeSurface=2 if original_name == PREFIX + 'EyeWhite_00_EYE' else 1,
            hairFlow=args.hair_flow_mode, hairUvFlow=dict(x=flow[0], y=flow[1]))
        materials.append(material)
        decisions.append(dict(exportName=name, sourceName=original_name, surface=surface, alphaMode=mode,
            normalImageIndex=normal['image'] if normal else None, normalOmission=omission,
            linearFactor=color, brownAppliedExactlyOnce=bool(mapping), browTintApplied=original_name == PREFIX + 'FaceBrow_00_FACE' and brow is not None))

    need(any(m['heroSurface'] == 'Face' for m in materials), 'The current RendererPatch requires an actual compatible Face surface; do not substitute a dummy mask')
    settings = dict(schema=1, actorId='JesusChild', sourceSha256=SOURCE_SHA, outputVersion=args.output_version,
        controllerAsset=args.controller_asset, originalVrm=original_pin, model=model_pin,
        humanBones=humanoid, nativeBoneNames=native_names, faceMorphNames=morphs, faceRenderer=report['faceRenderer'],
        maximumExportedInfluences=native_max, textures=textures, materials=materials, expressions=expressions,
        axesVerified=False, enableGeometryFaceSdf=False, geometryFaceSdfCompatibilityVerified=False)
    output = Path(args.output); audit_output = output.with_name(output.name + '.bridge.json')
    need(not output.exists() and not audit_output.exists(), 'Config and bridge record must both be new')
    settings_pin = write_new(output, settings)
    audit = dict(schema=2, settings=settings_pin, bakeReport=report_pin, sourceInspection=inspection_pin,
        builderSourceSha256=digest(Path(__file__).read_bytes()), bakerSourceSha256=args.baker_source_sha256,
        normalPixelsReport=flat_pin, choices=dict(hairFlowMode=args.hair_flow_mode, hairUvFlow=flow,
            fittedEyeMaterials=sorted(verified_eyes), browLinearTint=brow, faceSdfEnabled=False,
            linen=dict(source=LINEN_SOURCE if linen_pins else None, inputs=linen_pins,
                tiling=linen_tiling if linen_pins else None,
                normalStrength=linen_strength if 'normal' in linen_pins else None,
                affectedGeneratedRoles=['linen', 'facing'] if linen_pins else [])),
        materialDecisions=decisions, geometryEdited=False, imagePixelsEdited=False,
        actualUnityPreparationExecuted=False, actualVisualReviewCompleted=False)
    audit_pin = write_new(audit_output, audit)
    print(json.dumps(dict(settings=settings_pin, bridge=audit_pin), sort_keys=True))


if __name__ == '__main__': main()
