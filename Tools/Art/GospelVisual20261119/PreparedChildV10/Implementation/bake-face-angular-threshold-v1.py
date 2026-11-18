#!/usr/bin/env python3
"""Blender 4.3, source-only draft: geometry angular threshold, NOT a painted SDF.

Root invokes in a fresh background Blender process, never an open artist scene:
  blender --background --factory-startup --python-exit-code 2 --python THIS.py -- \
    --contract /absolute/contract.json --contract-sha256 SHA --out /new/directory \
    --inspect-only

No model, texture, .blend, rig or morph is saved or changed. A later independent
bake requires a byte-pinned accepted inspection. See FACE-ANGULAR-CONTRACT.md.
"""

import argparse
import array
import hashlib
import json
import math
from pathlib import Path
import struct
import sys
import time

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree


REVISION = "gospel-geometry-angular-threshold-v1"
EXPECTED_SOURCE_SHA = "7526838f4a45086a1d0abb23e9f38ad3f0103889f1efa70d2605ef25e7e6ef99"
SKIN_MATERIAL = "N00_000_00_Face_00_SKIN"
LIMIT_KEYS = {
    "mirrorCoverageMin", "mirrorPositionMaxMeters", "mirrorNormalCosMin",
    "uvOverlapFractionMax", "nonMonotonicFractionMax", "frontDarkFractionMax",
    "backLitFractionMax", "uvDegenerateTriangleFractionMax",
}


def fail(message):
    raise RuntimeError(message)


def sha_bytes(payload):
    return hashlib.sha256(payload).hexdigest()


def sha_file(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            fail("Duplicate JSON key: " + key)
        result[key] = value
    return result


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8"), object_pairs_hook=unique_object,
                      parse_constant=lambda value: fail("Non-finite JSON: " + value))


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(",", ":"),
                      ensure_ascii=False, allow_nan=False).encode("utf-8")


def absolute_file(value, name):
    path = Path(value)
    if not path.is_absolute() or path.is_symlink() or not path.is_file():
        fail(name + " must be an existing absolute regular non-symlink file")
    return path


def number(value, name, minimum, maximum):
    if type(value) not in (int, float) or not math.isfinite(value):
        fail(name + " must be finite numeric data")
    if not minimum <= value <= maximum:
        fail(name + " is outside the explicit safety bound")
    return float(value)


def integer(value, name, minimum, maximum):
    if type(value) is not int or not minimum <= value <= maximum:
        fail(name + " must be a bounded integer")
    return value


def vector(value, name):
    if not isinstance(value, list) or len(value) != 3:
        fail(name + " must contain three numeric components")
    result = Vector(tuple(number(v, name, -1, 1) for v in value))
    if result.length < 0.9:
        fail(name + " must be an explicit nonzero direction")
    return result.normalized()


class Budget:
    def __init__(self, seconds, ray_limit):
        self.start = time.monotonic()
        self.seconds = seconds
        self.ray_limit = ray_limit
        self.rays = 0

    def check(self):
        if time.monotonic() - self.start > self.seconds:
            fail("Explicit analysis time budget exhausted; no shader map is approved")

    def ray(self):
        self.rays += 1
        if self.rays > self.ray_limit:
            fail("Explicit ray budget exhausted; no shader map is approved")
        if not self.rays % 4096:
            self.check()


def validate_contract(data, inspect_only):
    if data.get("schema") != 1 or data.get("revision") != REVISION:
        fail("Unknown contract schema/revision")
    source = absolute_file(data["sourceFile"], "sourceFile")
    if data.get("sourceSha256") != EXPECTED_SOURCE_SHA:
        fail("Source SHA differs from the specifically selected hinzka source")
    if sha_file(source) != EXPECTED_SOURCE_SHA:
        fail("Selected source bytes do not match the pinned SHA")
    if data.get("faceSkinMaterial") != SKIN_MATERIAL:
        fail("Exact FaceSkin material slot must be selected, never mouth/eye slots")
    for key in ("faceObject", "armatureObject", "headBone"):
        if not isinstance(data.get(key), str) or not data[key]:
            fail(key + " must explicitly name the imported source object/bone")
    if data.get("axisSpace") != "BLENDER_POSE_HEAD_LOCAL":
        fail("Axes must explicitly be Blender pose-head-local; Unity axes are not inferred")
    forward = vector(data["headForwardLocal"], "headForwardLocal")
    up = vector(data["headUpLocal"], "headUpLocal")
    if abs(forward.dot(up)) > 0.01:
        fail("Verified head forward/up must be orthogonal within .01")
    units = number(data["unitsToMeters"], "unitsToMeters", 0.001, 1000)
    resolution = integer(data.get("resolution", 256), "resolution", 32, 1024)
    samples = integer(data.get("yawSamples", 33), "yawSamples", 9, 65)
    if not samples % 2:
        fail("yawSamples must be odd so front, side and rear are sampled")
    ray_limit = integer(data.get("maxRays", 20000000), "maxRays", 1000, 20000000)
    seconds = number(data.get("maxSeconds", 600), "maxSeconds", 10, 1800)
    epsilon = number(data.get("rayEpsilonMeters", 0.00002), "rayEpsilonMeters", 0.000001, 0.0001)
    limits = data.get("limits", {})
    if set(limits) != LIMIT_KEYS:
        fail("All explicit conservative limits, and no unknown limit, are required")
    number(limits["mirrorCoverageMin"], "mirrorCoverageMin", .98, 1)
    number(limits["mirrorPositionMaxMeters"], "mirrorPositionMaxMeters", .000001, .003)
    number(limits["mirrorNormalCosMin"], "mirrorNormalCosMin", .8, 1)
    for key in ("uvOverlapFractionMax", "uvDegenerateTriangleFractionMax"):
        number(limits[key], key, 0, .001)
    for key in ("nonMonotonicFractionMax", "frontDarkFractionMax", "backLitFractionMax"):
        number(limits[key], key, 0, .005)
    if not inspect_only and (data.get("axesVerified") is not True
                             or data.get("unitsVerified") is not True
                             or data.get("allowGeometryBake") is not True):
        fail("Bake requires Root verified axes/units and allowGeometryBake=true")
    analysis_keys = ("revision", "sourceSha256", "faceObject", "armatureObject", "headBone",
                     "faceSkinMaterial", "axisSpace", "headForwardLocal", "headUpLocal",
                     "unitsToMeters", "resolution", "yawSamples", "rayEpsilonMeters", "limits")
    analysis = {key: data.get(key) for key in analysis_keys}
    analysis.update(resolution=resolution, yawSamples=samples, rayEpsilonMeters=epsilon)
    return source, forward, up, units, resolution, samples, epsilon, limits, analysis, Budget(seconds, ray_limit)


def native_fingerprint(face, armature):
    """Hash native data, including every shape position and untruncated influence.

    No evaluated mesh is substituted for these original payload invariants.
    Derived triangle/normal caches and datablock users are deliberately excluded.
    """
    digest = hashlib.sha256()

    def item(value):
        payload = canonical(value)
        digest.update(struct.pack("<Q", len(payload)))
        digest.update(payload)

    def floats(values):
        values = tuple(float(v) for v in values)
        digest.update(struct.pack("<Q", len(values)))
        digest.update(struct.pack("<" + "d" * len(values), *values))

    mesh = face.data
    item([face.name, mesh.name, [m.name if m else None for m in mesh.materials]])
    floats(value for row in face.matrix_world for value in row)
    item([[g.name, g.index, g.lock_weight] for g in face.vertex_groups])
    for vertex in mesh.vertices:
        floats(vertex.co)
        item([[g.group, g.weight] for g in vertex.groups])
    for polygon in mesh.polygons:
        item([list(polygon.vertices), polygon.material_index, polygon.use_smooth])
    for layer in mesh.uv_layers:
        item(layer.name)
        floats(value for datum in layer.data for value in datum.uv)
    item([mesh.has_custom_normals, len(mesh.loops)])
    floats(value for normal in mesh.corner_normals for value in normal.vector)
    if mesh.shape_keys:
        item(mesh.shape_keys.use_relative)
        for key in mesh.shape_keys.key_blocks:
            item([key.name, key.value, key.mute, key.relative_key.name,
                  key.interpolation, key.slider_min, key.slider_max, key.vertex_group])
            floats(value for datum in key.data for value in datum.co)
    item([armature.name, armature.data.name, armature.data.pose_position])
    floats(value for row in armature.matrix_world for value in row)
    for bone in armature.data.bones:
        item([bone.name, bone.parent.name if bone.parent else None, bone.use_deform])
        floats(value for row in bone.matrix_local for value in row)
    for bone in armature.pose.bones:
        item(bone.name)
        floats(value for row in bone.matrix for value in row)
    for modifier in face.modifiers:
        item([modifier.name, modifier.type, modifier.show_viewport, modifier.show_render])
        if modifier.type == "ARMATURE":
            item([modifier.object.name if modifier.object else None,
                  modifier.use_vertex_groups, modifier.use_bone_envelopes,
                  modifier.use_deform_preserve_volume])
    return digest.hexdigest()


def get_geometry(face, depsgraph, units, budget):
    """World-meter neutral skin triangles; preserve actual UV0 and corner normals."""
    original = face.data
    if original.shape_keys:
        nonzero = [key.name for key in original.shape_keys.key_blocks[1:] if abs(key.value) > 1e-7]
        if nonzero:
            fail("Source is not a neutral face; do not reset morphs: " + ", ".join(nonzero))
    evaluated = face.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh(preserve_all_data_layers=True, depsgraph=depsgraph)
    try:
        if len(mesh.vertices) != len(original.vertices) or len(mesh.polygons) != len(original.polygons):
            fail("Evaluated mesh topology differs; skin/UV correspondence cannot be assumed")
        for a, b in zip(mesh.polygons, original.polygons):
            if tuple(a.vertices) != tuple(b.vertices) or a.material_index != b.material_index:
                fail("Evaluated material or polygon correspondence changed")
        if not mesh.uv_layers:
            fail("Actual UV0 is absent")
        uv = mesh.uv_layers[0].data
        original_uv = original.uv_layers[0].data
        if any((a.uv - b.uv).length > 1e-7 for a, b in zip(uv, original_uv)):
            fail("Evaluated UV0 differs from native UV0")
        mesh.calc_loop_triangles()
        world = evaluated.matrix_world
        normal_matrix = world.to_3x3().inverted().transposed()
        result = []
        skin_polygons = set()
        for triangle in mesh.loop_triangles:
            budget.check()
            material = mesh.materials[triangle.material_index]
            if material is None or material.name != SKIN_MATERIAL:
                continue
            skin_polygons.add(triangle.polygon_index)
            positions = tuple((world @ mesh.vertices[i].co) * units for i in triangle.vertices)
            texcoords = tuple(uv[i].uv.copy() for i in triangle.loops)
            normals = tuple((normal_matrix @ mesh.corner_normals[i].vector).normalized()
                            for i in triangle.loops)
            geometric = (positions[1] - positions[0]).cross(positions[2] - positions[0])
            if geometric.length < 1e-16:
                fail("Degenerate actual FaceSkin geometry; no fabricated normal is permitted")
            geometric.normalize()
            if geometric.dot(sum(normals, Vector())) < 0:
                geometric.negate()
            result.append((positions, texcoords, normals, geometric))
        if not result:
            fail("No triangle uses the exact FaceSkin material")
        if len(result) > 50000:
            fail("FaceSkin triangle count exceeds this bounded draft")
        stats = {"nativeVertexCount": len(original.vertices), "nativePolygonCount": len(original.polygons),
                 "shapeKeyBlocksIncludingBasis": len(original.shape_keys.key_blocks) if original.shape_keys else 0,
                 "faceSkinPolygonCount": len(skin_polygons), "faceSkinTriangleCount": len(result),
                 "hasCustomNormals": bool(mesh.has_custom_normals), "uv0Name": mesh.uv_layers[0].name,
                 "evaluatedNeutralTopologyCorrespondence": True}
        return result, stats
    finally:
        evaluated.to_mesh_clear()


def barycentric(uv, triangle_uv, tolerance=1e-8):
    a, b, c = triangle_uv
    cross = (b.x-a.x)*(c.y-a.y) - (b.y-a.y)*(c.x-a.x)
    if abs(cross) < 1e-14:
        return None
    v = ((uv.x-a.x)*(c.y-a.y) - (uv.y-a.y)*(c.x-a.x)) / cross
    w = ((b.x-a.x)*(uv.y-a.y) - (b.y-a.y)*(uv.x-a.x)) / cross
    u = 1-v-w
    return (u, v, w) if min(u, v, w) >= -tolerance else None


def interpolate(triangle, bary):
    position = sum((point*weight for point, weight in zip(triangle[0], bary)), Vector())
    normal = sum((point*weight for point, weight in zip(triangle[2], bary)), Vector())
    if normal.length < 1e-8:
        fail("Interpolated custom normal is undefined")
    return position, normal.normalized()


def uv_index(triangles, budget):
    bins = {}
    count = 64
    outside = degenerate = 0
    for index, triangle in enumerate(triangles):
        budget.check()
        uv = triangle[1]
        if any(p.x < -1e-7 or p.x > 1+1e-7 or p.y < -1e-7 or p.y > 1+1e-7 for p in uv):
            outside += 1
        uv_area_twice = ((uv[1].x-uv[0].x)*(uv[2].y-uv[0].y)
                         - (uv[1].y-uv[0].y)*(uv[2].x-uv[0].x))
        if abs(uv_area_twice) < 1e-14:
            degenerate += 1
            continue
        x0 = max(0, min(count-1, int(math.floor(min(p.x for p in uv)*count))))
        x1 = max(0, min(count-1, int(math.floor(max(p.x for p in uv)*count))))
        y0 = max(0, min(count-1, int(math.floor(min(p.y for p in uv)*count))))
        y1 = max(0, min(count-1, int(math.floor(max(p.y for p in uv)*count))))
        for y in range(y0, y1+1):
            for x in range(x0, x1+1):
                bins.setdefault((x, y), []).append(index)
    return bins, count, outside, degenerate


def query_uv(uv, triangles, bins, count):
    if uv.x < 0 or uv.x > 1 or uv.y < 0 or uv.y > 1:
        return []
    key = (min(count-1, int(uv.x*count)), min(count-1, int(uv.y*count)))
    hits = []
    for index in bins.get(key, ()):
        bary = barycentric(uv, triangles[index][1])
        if bary is not None:
            hits.append((index, bary))
    return hits


def mirror_analysis(triangles, bins, count, origin, right, epsilon, budget):
    """Continuous barycentric surface test, not a coincidental texel pairing."""
    paired = total = ambiguous = 0
    errors = []
    normal_min = 1.0
    worst = []
    probes = ((1/3, 1/3, 1/3), (.6, .2, .2), (.2, .6, .2), (.2, .2, .6))
    for index, triangle in enumerate(triangles):
        budget.check()
        for bary in probes:
            total += 1
            uv = sum((p*w for p, w in zip(triangle[1], bary)), Vector((0, 0)))
            position, normal = interpolate(triangle, bary)
            expected_position = position - 2*right*(position-origin).dot(right)
            expected_normal = normal - 2*right*normal.dot(right)
            hits = query_uv(Vector((1-uv.x, uv.y)), triangles, bins, count)
            if not hits:
                continue
            surfaces = [interpolate(triangles[i], b) for i, b in hits]
            if any((p-surfaces[0][0]).length > epsilon*4 for p, _ in surfaces[1:]):
                ambiguous += 1
                continue
            mirrored_position, mirrored_normal = surfaces[0]
            error = (mirrored_position-expected_position).length
            normal_cos = max(-1.0, min(1.0, expected_normal.dot(mirrored_normal)))
            normal_min = min(normal_min, normal_cos)
            errors.append(error)
            paired += 1
            worst.append({"triangle": index, "barycentric": list(bary), "uv": list(uv),
                          "mirroredPositionErrorMeters": error, "mirroredNormalCos": normal_cos})
            worst.sort(key=lambda value: value["mirroredPositionErrorMeters"], reverse=True)
            del worst[16:]
    return {"probeCount": total, "pairedCount": paired, "ambiguousCount": ambiguous,
            "coverage": paired/max(1, total), "maxPositionErrorMeters": max(errors, default=0),
            "meanPositionErrorMeters": sum(errors)/max(1, len(errors)),
            "minimumNormalCos": normal_min if paired else -1, "worstPositionPairs": worst}


def rasterize(triangles, resolution, epsilon, budget):
    """UV0 texel centers; duplicate shared-edge hits are not 3D UV overlaps."""
    owners = [None] * (resolution*resolution)
    conflicts = set()
    normal_conflicts = set()
    for index, triangle in enumerate(triangles):
        budget.check()
        uv = triangle[1]
        x0 = max(0, int(math.ceil(min(p.x for p in uv)*resolution-.5)))
        x1 = min(resolution-1, int(math.floor(max(p.x for p in uv)*resolution-.5)))
        y0 = max(0, int(math.ceil(min(p.y for p in uv)*resolution-.5)))
        y1 = min(resolution-1, int(math.floor(max(p.y for p in uv)*resolution-.5)))
        for y in range(y0, y1+1):
            for x in range(x0, x1+1):
                bary = barycentric(Vector(((x+.5)/resolution, (y+.5)/resolution)), uv)
                if bary is None:
                    continue
                address = y*resolution+x
                if owners[address] is not None:
                    previous_index, previous_bary = owners[address]
                    p0, n0 = interpolate(triangles[previous_index], previous_bary)
                    p1, n1 = interpolate(triangle, bary)
                    if (p1-p0).length > epsilon*4:
                        conflicts.add(address)
                    if n0.dot(n1) < .99:
                        normal_conflicts.add(address)
                else:
                    owners[address] = (index, bary)
    covered = sum(value is not None for value in owners)
    if not covered:
        fail("UV0 FaceSkin has no covered texel centers at this resolution")
    return owners, {"resolution": resolution, "coveredTexels": covered,
                    "coverageOfFullTexture": covered/(resolution*resolution),
                    "overlapTexels": len(conflicts), "overlapFraction": len(conflicts)/covered,
                    "conflictingNormalTexels": len(normal_conflicts),
                    "conflictingNormalFraction": len(normal_conflicts)/covered}


def make_bvh(triangles):
    vertices = []
    polygons = []
    for triangle in triangles:
        offset = len(vertices)
        vertices.extend(triangle[0])
        polygons.append((offset, offset+1, offset+2))
    minimum = Vector(tuple(min(point[axis] for point in vertices) for axis in range(3)))
    maximum = Vector(tuple(max(point[axis] for point in vertices) for axis in range(3)))
    return BVHTree.FromPolygons(vertices, polygons, all_triangles=True, epsilon=0.0), (maximum-minimum).length*2


def angular_analysis(triangles, owners, forward, right, samples, epsilon, budget):
    bvh, ray_distance = make_bvh(triangles)
    if ray_distance < .01:
        fail("FaceSkin bounds are implausibly small in verified meter units")
    directions = [(forward*math.cos(i*math.pi/(samples-1))
                   + right*math.sin(i*math.pi/(samples-1))).normalized() for i in range(samples)]
    thresholds = [.5*(1-math.cos(i*math.pi/(samples-1))) for i in range(samples)]
    values = array.array("f", [0.0]) * len(owners)
    front_dark = back_lit = non_monotonic = tested = 0
    examples = []
    for address, owner in enumerate(owners):
        if owner is None:
            continue
        budget.check()
        index, bary = owner
        triangle = triangles[index]
        position, normal = interpolate(triangle, bary)
        origin = position + triangle[3]*epsilon
        states = []
        for direction in directions:
            if normal.dot(direction) <= 0:
                states.append(False)
            else:
                budget.ray()
                hit, _, _, _ = bvh.ray_cast(origin, direction, ray_distance)
                states.append(hit is None)
        tested += 1
        front_dark += int(not states[0])
        back_lit += int(states[-1])
        transitions_back_to_light = any(states[i] and not states[i-1] for i in range(1, samples))
        non_monotonic += int(transitions_back_to_light)
        first_dark = next((i for i, lit in enumerate(states) if not lit), samples)
        # Values for rejected texels remain diagnostics and never become an
        # approved image unless their fractions meet the explicit strict limits.
        value = 0.0 if first_dark == 0 else 1.0 if first_dark == samples else (
            thresholds[first_dark-1]+thresholds[first_dark])*.5
        values[address] = value
        if (transitions_back_to_light or not states[0] or states[-1]) and len(examples) < 24:
            examples.append({"texelIndex": address, "triangle": index,
                             "yawVisibility": "".join("1" if state else "0" for state in states)})
    return values, {"testedTexels": tested, "yawSamples": samples, "yawDegrees": [i*180/(samples-1) for i in range(samples)],
                    "frontDarkTexels": front_dark, "frontDarkFraction": front_dark/max(1, tested),
                    "backLitTexels": back_lit, "backLitFraction": back_lit/max(1, tested),
                    "nonMonotonicTexels": non_monotonic,
                    "nonMonotonicFraction": non_monotonic/max(1, tested),
                    "maximumSampleThresholdStep": max(b-a for a, b in zip(thresholds, thresholds[1:])),
                    "rayCount": budget.rays, "invalidExamples": examples}


def rejection_reasons(data, limits, uv, mirror, angular):
    reasons = []
    if data.get("axesVerified") is not True:
        reasons.append("head-axes-not-verified-by-Root")
    if data.get("unitsVerified") is not True:
        reasons.append("world-meter-units-not-verified-by-Root")
    checks = [
        (uv["outsideUvTriangles"] == 0, "uv0-outside-unit-square"),
        (uv["degenerateTriangleFraction"] <= limits["uvDegenerateTriangleFractionMax"], "degenerate-uv0-triangles"),
        (uv["overlapFraction"] <= limits["uvOverlapFractionMax"], "uv0-overlapping-distinct-surface"),
        (uv["conflictingNormalFraction"] <= limits["uvOverlapFractionMax"], "uv0-overlapping-distinct-custom-normal"),
        (mirror["coverage"] >= limits["mirrorCoverageMin"], "mirror-uv0-surface-coverage"),
        (mirror["maxPositionErrorMeters"] <= limits["mirrorPositionMaxMeters"], "mirror-uv0-geometric-error"),
        (mirror["minimumNormalCos"] >= limits["mirrorNormalCosMin"], "mirror-uv0-custom-normal-error"),
        (angular["nonMonotonicFraction"] <= limits["nonMonotonicFractionMax"], "angular-non-monotonic-visibility"),
        (angular["frontDarkFraction"] <= limits["frontDarkFractionMax"], "front-key-not-initially-lit"),
        (angular["backLitFraction"] <= limits["backLitFractionMax"], "rear-key-still-lit"),
    ]
    reasons.extend(reason for passes, reason in checks if not passes)
    return reasons


def save_png(path, resolution, values, owners):
    # New data image only. No source image or .blend is edited/saved. Uncovered
    # texels are dark diagnostics, NOT a gradient or dilation into unknown UVs.
    image = bpy.data.images.new("NEW_geometry_angular_threshold", width=resolution,
                                height=resolution, alpha=True, float_buffer=False, is_data=True)
    try:
        image.colorspace_settings.name = "Non-Color"
        pixels = array.array("f")
        for value, owner in zip(values, owners):
            scalar = max(0.0, min(1.0, value)) if owner is not None else 0.0
            pixels.extend((scalar, scalar, scalar, 1.0))
        image.pixels.foreach_set(pixels)
        image.file_format = "PNG"
        image.filepath_raw = str(path)
        image.save()
    finally:
        bpy.data.images.remove(image)


def write_json_new(path, value):
    with path.open("x", encoding="utf-8") as stream:
        json.dump(value, stream, indent=2, sort_keys=True, ensure_ascii=False, allow_nan=False)
        stream.write("\n")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--contract", required=True)
    parser.add_argument("--contract-sha256", required=True)
    parser.add_argument("--out", required=True)
    parser.add_argument("--inspect-only", action="store_true")
    args = parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
    if not bpy.app.background or tuple(bpy.app.version[:2]) != (4, 3) or bpy.data.filepath:
        fail("Run only fresh, unsaved Blender 4.3 in background mode")
    contract_path = absolute_file(args.contract, "contract")
    contract_sha = sha_file(contract_path)
    if contract_sha != args.contract_sha256:
        fail("Contract bytes differ from the Root-pinned SHA")
    data = read_json(contract_path)
    source, local_f, local_u, units, resolution, samples, epsilon, limits, analysis, budget = validate_contract(data, args.inspect_only)
    output = Path(args.out)
    if not output.is_absolute() or output.exists() or output.is_symlink():
        fail("Output must be a new absolute directory; overwriting is forbidden")
    output.mkdir(parents=False, exist_ok=False)
    metadata = {"schema": 1, "revision": REVISION, "status": "started-not-approved",
                "sourceSha256": EXPECTED_SOURCE_SHA, "contractSha256": contract_sha,
                "generatorSourceSha256": sha_file(Path(__file__).resolve()),
                "analysisContract": analysis, "analysisContractSha256": sha_bytes(canonical(analysis)),
                "geometryDerivedAnalysis": True, "geometryBaked": False,
                "artistPainted": False, "isSignedDistanceField": False,
                "fieldSemantics": "first front-to-back angular geometry visibility threshold",
                "shaderContract": "positive-head-right key uses raw UV0; left key uses (1-u,v); t=.5*(1-cos(yaw))",
                "shaderCompatible": False, "approvedForShader": False, "executedByThisSourceDraft": True,
                "notVerified": "No Unity compilation, in-game rendering, art acceptance, expression compatibility, or release approval is implied."}
    face = armature = None
    initial_native = None
    exit_code = 2
    try:
        # Fresh process only; imported native source payload is never modified.
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.gltf(filepath=str(source))
        face = bpy.data.objects.get(data["faceObject"])
        armature = bpy.data.objects.get(data["armatureObject"])
        if face is None or face.type != "MESH" or armature is None or armature.type != "ARMATURE":
            fail("Exact source face/armature names were not found")
        if not any(modifier.type == "ARMATURE" and modifier.object == armature for modifier in face.modifiers):
            fail("Selected face is not bound to the selected actual armature")
        bone = armature.pose.bones.get(data["headBone"])
        if bone is None:
            fail("Explicit actual head bone was not found")
        bpy.context.view_layer.update()
        initial_native = native_fingerprint(face, armature)
        metadata["nativePayloadSha256Before"] = initial_native
        frame = armature.matrix_world @ bone.matrix
        forward = (frame.to_3x3() @ local_f).normalized()
        up = frame.to_3x3() @ local_u
        up = (up-forward*up.dot(forward)).normalized()
        right = up.cross(forward).normalized()
        origin = frame.translation*units
        metadata["verifiedFrameWorldMeters"] = {"origin": list(origin), "forward": list(forward),
                                                 "up": list(up), "right": list(right)}
        triangles, geometry = get_geometry(face, bpy.context.evaluated_depsgraph_get(), units, budget)
        metadata["geometry"] = geometry
        bins, count, outside, degenerate = uv_index(triangles, budget)
        mirror = mirror_analysis(triangles, bins, count, origin, right, epsilon, budget)
        owners, uv = rasterize(triangles, resolution, epsilon, budget)
        uv.update(outsideUvTriangles=outside, degenerateUvTriangles=degenerate,
                  degenerateTriangleFraction=degenerate/len(triangles))
        # Upper bound: every covered texel could need every yaw ray.
        if uv["coveredTexels"]*samples > budget.ray_limit:
            fail("Worst-case angular rays exceed maxRays; reduce requested resolution")
        values, angular = angular_analysis(triangles, owners, forward, right, samples, epsilon, budget)
        reasons = rejection_reasons(data, limits, uv, mirror, angular)
        metadata.update(uv0=uv, mirrorSurface=mirror, angularVisibility=angular,
                        rejectionReasons=reasons, shaderCompatible=not reasons)
        if args.inspect_only:
            metadata["status"] = "inspection-completed-no-shader-map-created"
            exit_code = 0
        elif reasons:
            metadata["status"] = "geometry-not-compatible-no-shader-map-created"
        else:
            accepted = data.get("acceptedInspect", {})
            accepted_path = absolute_file(accepted.get("file", ""), "acceptedInspect.file")
            if sha_file(accepted_path) != accepted.get("sha256"):
                fail("Accepted inspection bytes do not match the explicit SHA")
            previous = read_json(accepted_path)
            if (previous.get("status") != "inspection-completed-no-shader-map-created"
                    or previous.get("shaderCompatible") is not True
                    or previous.get("sourceSha256") != EXPECTED_SOURCE_SHA
                    or previous.get("generatorSourceSha256") != metadata["generatorSourceSha256"]
                    or previous.get("analysisContractSha256") != metadata["analysisContractSha256"]
                    or previous.get("nativePayloadSha256Before") != initial_native
                    or previous.get("nativePayloadUnchanged") is not True
                    or previous.get("sourceFileUnchanged") is not True):
                fail("Accepted inspection is not compatible with these exact source/frame/settings")
            metadata["acceptedInspectSha256"] = accepted["sha256"]
            # Verify immutable payload BEFORE writing the only generated image.
            if native_fingerprint(face, armature) != initial_native or sha_file(source) != EXPECTED_SOURCE_SHA:
                fail("Native source invariants changed; refuse all image output")
            path = output/"face-angular-threshold-data.png"
            save_png(path, resolution, values, owners)
            metadata["dataPngSha256"] = sha_file(path)
            metadata["image"] = {"file": path.name, "sha256": metadata["dataPngSha256"], "encoding": "RGBA8 PNG, linear data R=G=B, A=1",
                                 "uvOrigin": "bottom-left, direct Blender UV0", "importSrgb": False,
                                 "importAlphaIsTransparency": False, "wrap": "Clamp", "mipmaps": False,
                                 "filter": "Bilinear", "uncoveredTexelValue": 0,
                                 "maximum8BitQuantizationError": 1/255}
            metadata["approvedForShader"] = True
            metadata["geometryBaked"] = True
            metadata["status"] = "geometry-threshold-baked-root-accepted-inspection"
            exit_code = 0
    except Exception as error:
        metadata["status"] = "failed-no-shader-map-approved"
        metadata["error"] = str(error)
        metadata["shaderCompatible"] = False
        metadata["approvedForShader"] = False
    finally:
        metadata["elapsedSeconds"] = time.monotonic()-budget.start
        metadata["sourceFileUnchanged"] = sha_file(source) == EXPECTED_SOURCE_SHA
        metadata["contractFileUnchanged"] = sha_file(contract_path) == contract_sha
        if face is not None and armature is not None and initial_native is not None:
            metadata["nativePayloadSha256After"] = native_fingerprint(face, armature)
            metadata["nativePayloadUnchanged"] = metadata["nativePayloadSha256After"] == initial_native
        else:
            metadata["nativePayloadUnchanged"] = False
        if not (metadata["sourceFileUnchanged"] and metadata["contractFileUnchanged"]
                and metadata["nativePayloadUnchanged"]):
            metadata.update(status="invariant-failure-no-shader-map-approved", shaderCompatible=False,
                            approvedForShader=False)
            exit_code = 2
        write_json_new(output/("inspection.json" if args.inspect_only else "bake.json"), metadata)
    if exit_code:
        raise SystemExit(exit_code)


if __name__ == "__main__":
    main()
