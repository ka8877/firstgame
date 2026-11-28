"""Fit the image-space joints before transferring the textile surfaces to the live rig."""

from pathlib import Path
import json
import cv2
import numpy as np
import onnxruntime as ort
from PIL import Image
from observations import VIEWS, ROLES

NAMES = {
    0: "nose",
    2: "eyeL",
    5: "eyeR",
    7: "earL",
    8: "earR",
    11: "shoulderL",
    12: "shoulderR",
    13: "elbowL",
    14: "elbowR",
    15: "wristL",
    16: "wristR",
    17: "pinkyL",
    18: "pinkyR",
    19: "indexL",
    20: "indexR",
    21: "thumbL",
    22: "thumbR",
    23: "hipL",
    24: "hipR",
    25: "kneeL",
    26: "kneeR",
    27: "ankleL",
    28: "ankleR",
    29: "heelL",
    30: "heelR",
    31: "toeL",
    32: "toeR",
}


def measure(session, image):
    rgba = np.asarray(image)
    h, w = rgba.shape[:2]
    side = max(w, h)
    offx = (side - w) // 2
    offy = (side - h) // 2
    canvas = np.full((side, side, 3), 168, np.uint8)
    fg = np.where((rgba[:, :, 3] > 128)[:, :, None], rgba[:, :, :3], 168)
    canvas[offy : offy + h, offx : offx + w] = fg
    tensor = (
        cv2.resize(canvas, (256, 256), interpolation=cv2.INTER_AREA).astype("f4")[None]
        / 255
    )
    positions, score, _, _, _ = session.run(None, {"input_1": tensor})
    raw = positions[0].reshape(-1, 5)
    return {
        NAMES[i]: [
            float(raw[i, 0] / 256 * side - offx),
            float(raw[i, 1] / 256 * side - offy),
        ]
        for i in NAMES
    }, float(score.squeeze())


if __name__ == "__main__":
    import sys

    root = Path(sys.argv[1])
    opts = ort.SessionOptions()
    opts.intra_op_num_threads = 2
    sess = ort.InferenceSession(sys.argv[2], opts, providers=["CPUExecutionProvider"])
    for role in ROLES:
        output = {}
        for view in VIEWS:
            points, score = measure(sess, Image.open(root / role / (view + ".png")))
            output[view] = {"points": points, "score": score}
        (root / role / "joints.json").write_text(json.dumps(output, indent=2) + "\n")
        print(
            role,
            round(output["front"]["score"], 2),
            {
                k: np.round(output["front"]["points"][k], 0).tolist()
                for k in ["eyeL", "shoulderL", "elbowL", "wristL", "kneeL", "ankleL"]
            },
        )
