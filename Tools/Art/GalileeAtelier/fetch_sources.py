"""Read only pinned members of the CC0 art packs into the external art workspace."""

import hashlib
import io
import json
from pathlib import Path
from urllib.request import Request, urlopen
import zipfile
from layout import input_root

manifest = json.loads(Path(__file__).with_name("source-manifest.json").read_bytes())
work = input_root()


def request(url, headers=None):
    return urlopen(
        Request(
            url, headers={"User-Agent": "Firstgame-Galilee-art/1", **(headers or {})}
        ),
        timeout=60,
    )


class RemoteZip(io.RawIOBase):
    def __init__(self, url, size):
        self.url = url
        self.size = size
        self.offset = 0

    def tell(self):
        return self.offset

    def seek(self, delta, whence=0):
        self.offset = (0, self.offset, self.size)[whence] + delta
        if self.offset < 0:
            raise OSError("Negative ZIP offset")
        return self.offset

    def seekable(self):
        return True

    def read(self, n=-1):
        end = self.size if n < 0 else min(self.offset + n, self.size)
        if end <= self.offset:
            return b""
        span = f"{self.offset}-{end - 1}"
        url = self.url + "?member-range=" + span
        with request(
            url, {"Range": "bytes=" + span, "Accept-Encoding": "identity"}
        ) as response:
            if response.status != 206 or not response.headers.get(
                "Content-Range", ""
            ).startswith("bytes " + span + "/"):
                raise OSError(
                    "The CC0 distribution did not return the requested byte range"
                )
            data = response.read()
        if len(data) != end - self.offset:
            raise OSError("Truncated source archive range")
        self.offset = end
        return data


def store(root, row, load):
    target = (root / row["file"]).resolve()
    if not target.is_relative_to(root):
        raise ValueError(row["file"])

    def valid(data):
        return (
            len(data) == row["bytes"]
            and hashlib.sha256(data).hexdigest() == row["sha256"]
        )

    if target.is_file() and valid(target.read_bytes()):
        return
    data = load()
    if not valid(data):
        raise ValueError("Source does not match its pinned content: " + row["file"])
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(data)


for row in manifest["direct"]:

    def load():
        with request(row["source"]) as response:
            return response.read()

    store(work / "source", row, load)
for name, archive in manifest["archives"].items():
    with zipfile.ZipFile(RemoteZip(archive["source"], archive["size"])) as members:
        for row in archive["files"]:
            store(work / "packs" / name, row, lambda: members.read(row["file"]))
