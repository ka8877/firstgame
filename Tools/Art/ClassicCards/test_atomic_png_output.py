"""Focused atomic-output checks; no downloads or repository assets are used."""
import importlib.util
import io
import os
from pathlib import Path
import stat
import tempfile
import unittest
from unittest import mock

from PIL import Image


SPEC = importlib.util.spec_from_file_location("classic_card_media_atomic", Path(__file__).with_name("prepare-media.py"))
MEDIA = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MEDIA)


def encoded(image):
    stream = io.BytesIO()
    image.save(stream, format="PNG")
    return stream.getvalue()


class AtomicPngOutputTests(unittest.TestCase):
    def test_normal_png_preserves_encoder_bytes_pixels_and_syncs_payload_then_parent(self):
        for mode in ("RGB", "RGBA"):
            with self.subTest(mode=mode), tempfile.TemporaryDirectory() as folder:
                image = Image.new(mode, (3, 2))
                image.putdata([(255, 0, 127), (0, 255, 3), (1, 2, 3), (8, 19, 31), (255, 255, 255), (0, 0, 0)]
                              if mode == "RGB" else [(255, 0, 127, 0), (0, 255, 3, 255), (1, 2, 3, 64), (8, 19, 31, 128), (255, 255, 255, 200), (0, 0, 0, 255)])
                output = Path(folder) / "card.png"
                sync_kinds = []
                original_sync = os.fsync

                def sync(descriptor):
                    sync_kinds.append("directory" if stat.S_ISDIR(os.fstat(descriptor).st_mode) else "payload")
                    return original_sync(descriptor)

                with mock.patch.object(MEDIA.os, "fsync", side_effect=sync):
                    MEDIA.save_png_atomic(image, output)
                self.assertEqual(output.read_bytes(), encoded(image))
                with Image.open(output) as result:
                    result.load()
                    self.assertEqual((result.format, result.size, result.mode), ("PNG", image.size, image.mode))
                    self.assertEqual(result.tobytes(), image.tobytes())
                self.assertEqual(sync_kinds, ["payload", "directory"])
                self.assertEqual(list(Path(folder).iterdir()), [output])

    def test_regeneration_detaches_hardlink_and_preserves_cache_bytes_and_output_mode(self):
        with tempfile.TemporaryDirectory() as folder:
            cache, output = Path(folder) / "cache.png", Path(folder) / "card.png"
            old = encoded(Image.new("RGBA", (2, 2), (1, 2, 3, 4)))
            cache.write_bytes(old); cache.chmod(0o440); os.link(cache, output)
            before = cache.stat()
            image = Image.new("RGBA", (2, 2), (50, 60, 70, 80))
            MEDIA.save_png_atomic(image, output)
            self.assertEqual(cache.read_bytes(), old)
            self.assertEqual((cache.stat().st_ino, cache.stat().st_mtime_ns), (before.st_ino, before.st_mtime_ns))
            self.assertNotEqual(output.stat().st_ino, before.st_ino)
            self.assertEqual(stat.S_IMODE(output.stat().st_mode), 0o440)
            self.assertEqual(output.read_bytes(), encoded(image))

    def test_partial_save_failure_leaves_shared_original_and_no_new_temporary(self):
        with tempfile.TemporaryDirectory() as folder:
            cache, output = Path(folder) / "cache.png", Path(folder) / "card.png"
            old = encoded(Image.new("RGB", (2, 2), (7, 8, 9)))
            cache.write_bytes(old); os.link(cache, output)
            before = output.stat()
            image = Image.new("RGB", (2, 2), (90, 80, 70))

            def fail_after_writing(payload, **options):
                payload.write(b"incomplete PNG")
                raise OSError("injected PNG encoding failure")

            with mock.patch.object(image, "save", side_effect=fail_after_writing):
                with self.assertRaisesRegex(OSError, "injected PNG encoding failure"):
                    MEDIA.save_png_atomic(image, output)
            self.assertEqual(output.read_bytes(), old); self.assertEqual(cache.read_bytes(), old)
            self.assertEqual((output.stat().st_ino, output.stat().st_mtime_ns), (before.st_ino, before.st_mtime_ns))
            self.assertEqual(set(Path(folder).iterdir()), {cache, output})

    def test_identical_content_reruns_never_write_into_the_cache_inode(self):
        with tempfile.TemporaryDirectory() as folder:
            cache, output = Path(folder) / "cache.png", Path(folder) / "card.png"
            image = Image.new("RGBA", (2, 3), (11, 22, 33, 44))
            original = encoded(image)
            cache.write_bytes(original); os.link(cache, output)
            before = cache.stat()
            for _ in range(2):
                MEDIA.save_png_atomic(image, output)
                self.assertEqual(cache.read_bytes(), original); self.assertEqual(output.read_bytes(), original)
                self.assertEqual((cache.stat().st_ino, cache.stat().st_mtime_ns), (before.st_ino, before.st_mtime_ns))
                self.assertNotEqual(output.stat().st_ino, before.st_ino)
                self.assertEqual(set(Path(folder).iterdir()), {cache, output})


if __name__ == "__main__":
    unittest.main()
