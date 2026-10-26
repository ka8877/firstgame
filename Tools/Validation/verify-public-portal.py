#!/usr/bin/env python3
"""Verify the existing public portal against an already staged release.

Downloads every manifest file plus build-manifest.json over verified HTTPS,
then captures the real landing page at desktop and mobile viewports. Never
publishes or enters a game. Run exclusively with the other browser suites.
Automation can pass while visualReview remains pending; direct review of the
saved original screenshots is required before release summary approval.
"""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import ssl
import sys
import urllib.error
import urllib.parse
import urllib.request

ROOT = Path(__file__).resolve().parents[2]
DEFAULT_URL = "https://ka8877.github.io/firstgame/"
GAME_SLUGS = ("fantasy-cards", "path-of-dawn")
MOBILE_UA = ("Mozilla/5.0 (Linux; Android 14; Mobile Browser Smoke) "
             "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Mobile Safari/537.36")


def require(condition, message):
    if not condition:
        raise ValueError(message)


def digest(path):
    result = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            result.update(block)
    return result.hexdigest()


def timestamp():
    return datetime.now(timezone.utc).isoformat()


def safe_error(error):
    # Proxy authentication must never leak through third-party exception text.
    return re.sub(r"(https?://)[^\s/@]+:[^\s/@]+@", r"\1[redacted]@", str(error))


def root_url(value):
    parsed = urllib.parse.urlsplit(value)
    require(parsed.scheme == "https" and parsed.netloc and not parsed.username
            and not parsed.password and not parsed.query and not parsed.fragment,
            "--base-url must be an HTTPS site root without credentials, query or fragment")
    return value.rstrip("/") + "/"


def relative_file(value):
    path = PurePosixPath(value)
    require(isinstance(value, str) and value and not path.is_absolute() and ".." not in path.parts
            and "\\" not in value and str(path) == value, f"Invalid staged relative path: {value}")
    return path


def archive_previous(output):
    if not output.exists() or not any(output.iterdir()):
        output.mkdir(parents=True, exist_ok=True)
        return None
    archived = output.with_name(output.name + ".previous-" + datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ"))
    output.rename(archived)
    output.mkdir(parents=True)
    return str(archived.relative_to(ROOT))


def save_report(output, report):
    destination = output / "report.json"
    temp = output / "report.json.tmp"
    temp.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n")
    temp.replace(destination)


def staged_manifest(stage):
    path = stage / "build-manifest.json"
    require(path.is_file(), "Stage the final release before verifying the public portal")
    manifest = json.loads(path.read_text())
    require(manifest.get("publishReady") is True and manifest.get("sourceDirty") is False,
            "The staged release must be browser-verified and identify a clean source commit")
    require(re.fullmatch(r"[0-9a-f]{40}", manifest.get("sourceCommit", "")), "Invalid staged sourceCommit")
    files = manifest.get("files", {})
    require(isinstance(files, dict) and files and "index.html" in files and "site.css" in files,
            "Stage manifest lacks public landing files")
    require("build-manifest.json" not in files, "Manifest must not hash itself")
    for name, expected in files.items():
        relative_file(name)
        local = (stage / name).resolve()
        require(local.is_relative_to(stage) and local.is_file(), f"Missing staged file: {name}")
        require(digest(local) == expected, f"Staged bytes changed after manifest: {name}")
    expected = {**files, "build-manifest.json": digest(path)}
    return manifest, expected


def https_context():
    # Match standard SDK CA environment variables while retaining verification.
    ca_file = next((os.environ.get(key) for key in ("SSL_CERT_FILE", "REQUESTS_CA_BUNDLE", "CURL_CA_BUNDLE")
                    if os.environ.get(key)), None)
    return ssl.create_default_context(cafile=ca_file)


def fetch_file(base_url, stage, name, expected, timeout, context):
    url = urllib.parse.urljoin(base_url, urllib.parse.quote(name, safe="/"))
    record = {"file": name, "url": url, "expectedBytes": (stage / name).stat().st_size,
              "expectedSha256": expected, "status": None, "passed": False}
    request = urllib.request.Request(url, headers={"Accept-Encoding": "identity", "Cache-Control": "no-cache",
                                                  "User-Agent": "FirstGame-public-portal-verification"})
    try:
        # urlopen honors inherited HTTP(S)_PROXY and NO_PROXY. Do not bypass them.
        with urllib.request.urlopen(request, timeout=timeout, context=context) as response:
            record.update(status=response.status, resolvedUrl=response.geturl(),
                          contentType=response.headers.get("Content-Type", ""),
                          contentEncoding=response.headers.get("Content-Encoding", "identity"))
            require(record["contentEncoding"].lower() in ("", "identity"),
                    "Server ignored Accept-Encoding: identity; exact byte comparison unavailable")
            hasher, size = hashlib.sha256(), 0
            for block in iter(lambda: response.read(1024 * 1024), b""):
                hasher.update(block)
                size += len(block)
            record.update(bytes=size, sha256=hasher.hexdigest())
            record["passed"] = (record["status"] == 200 and record["resolvedUrl"] == url
                                and size == record["expectedBytes"] and record["sha256"] == expected)
    except urllib.error.HTTPError as error:
        record.update(status=error.code, error=safe_error(error))
    except (OSError, ValueError, urllib.error.URLError) as error:
        record["error"] = safe_error(error)
    return record


def browser_proxy():
    proxy = os.environ.get("HTTPS_PROXY") or os.environ.get("https_proxy")
    if not proxy:
        return None
    parsed = urllib.parse.urlsplit(proxy)
    require(parsed.scheme in ("http", "https", "socks5") and parsed.hostname, "Unsupported inherited HTTPS proxy")
    host = f"[{parsed.hostname}]" if ":" in parsed.hostname else parsed.hostname
    server = f"{parsed.scheme}://{host}" + (f":{parsed.port}" if parsed.port else "")
    settings = {"server": server}
    if parsed.username is not None:
        settings["username"] = urllib.parse.unquote(parsed.username)
        settings["password"] = urllib.parse.unquote(parsed.password or "")
    bypass = os.environ.get("NO_PROXY") or os.environ.get("no_proxy")
    if bypass:
        settings["bypass"] = bypass
    return settings


def inspect_landing(browser, base_url, output, width, height, timeout):
    mobile = width == 390
    options = {"viewport": {"width": width, "height": height}, "device_scale_factor": 1,
               "has_touch": mobile, "is_mobile": mobile, "ignore_https_errors": False}
    if mobile:
        options["user_agent"] = MOBILE_UA
    context = browser.new_context(**options)
    result = {"viewport": options["viewport"], "profile": "mobile-portrait" if mobile else "desktop",
              "status": None, "pageErrors": [], "failedRequests": [], "httpErrors": [], "passed": False}
    try:
        page = context.new_page()
        page.on("pageerror", lambda error: result["pageErrors"].append(safe_error(error)))
        page.on("requestfailed", lambda request: result["failedRequests"].append({"url": request.url, "error": request.failure}))
        page.on("response", lambda response: result["httpErrors"].append({"url": response.url, "status": response.status})
                if response.status >= 400 else None)
        response = page.goto(base_url, wait_until="networkidle", timeout=timeout * 1000)
        result["status"] = response.status if response else None
        require(result["status"] == 200, "Landing HTTP status is not 200")
        page.evaluate("document.fonts.ready")
        require(page.locator(".worlds .world").count() == 2, "Landing must render both game cards")
        require(page.locator(".worlds img").count() == 2, "Landing must contain both title illustrations")
        # Scroll actual lazy images into view without changing CSS or DOM.
        for picture in page.locator(".worlds img").all():
            picture.scroll_into_view_if_needed(timeout=timeout * 1000)
            picture.evaluate("img => img.decode()")
        page.evaluate("window.scrollTo(0, 0)")
        metrics = page.evaluate("""() => ({
          width: window.innerWidth,
          documentWidth: Math.max(document.documentElement.scrollWidth, document.body.scrollWidth),
          documentHeight: Math.max(document.documentElement.scrollHeight, document.body.scrollHeight),
          images: Array.from(document.querySelectorAll('.worlds img')).map(img => ({
            src: img.currentSrc, width: img.naturalWidth, height: img.naturalHeight,
            complete: img.complete, renderedWidth: img.getBoundingClientRect().width,
            renderedHeight: img.getBoundingClientRect().height})),
          links: Array.from(document.querySelectorAll('.worlds a.play')).map(a => a.href),
          headings: Array.from(document.querySelectorAll('.worlds h2')).map(h => h.textContent.trim()),
          edition: document.querySelector('.edition')?.textContent.trim() || ''
        })""")
        result.update(metrics)
        expected_links = [urllib.parse.urljoin(base_url, f"games/{slug}/") for slug in GAME_SLUGS]
        result["assertions"] = {
            "requestedViewport": metrics["width"] == width,
            "noHorizontalOverflow": metrics["documentWidth"] <= width,
            "bothGameLinks": len(metrics["links"]) == 2 and set(metrics["links"]) == set(expected_links),
            "bothIllustrationsDecoded": len(metrics["images"]) == 2 and all(
                image["complete"] and image["width"] > 0 and image["height"] > 0
                and image["renderedWidth"] > 0 and image["renderedHeight"] > 0 for image in metrics["images"]),
            "classicEditionVisible": "REVIVAL & GOSPEL" in metrics["edition"],
            "bothHeadings": metrics["headings"] == ["판타지마스터즈", "예수님의 길"],
            "noPageErrors": not result["pageErrors"],
            "noFailedRequests": not result["failedRequests"],
            "noHttpErrors": not result["httpErrors"],
        }
        shot = output / f"landing-{width}.png"
        page.screenshot(path=str(shot), full_page=True, animations="disabled")
        result["screenshot"] = {"file": str(shot.relative_to(ROOT)), "sha256": digest(shot),
                                "captureKind": "original-full-page", "viewport": options["viewport"]}
        result["passed"] = all(result["assertions"].values())
    except Exception as error:
        result["error"] = safe_error(error)
    finally:
        context.close()
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base-url", default=DEFAULT_URL)
    parser.add_argument("--stage", type=Path, default=ROOT / "Artifacts/WebPlaytest")
    parser.add_argument("--output", type=Path, default=ROOT / "Artifacts/Validation/PublicWeb/Portal")
    parser.add_argument("--chromium", default=os.environ.get("CHROMIUM_BINARY", "/usr/bin/chromium"))
    parser.add_argument("--renderer", choices=("x11-mesa", "swiftshader"), default="x11-mesa")
    parser.add_argument("--timeout", type=int, default=180)
    args = parser.parse_args()
    base_url, stage, output = root_url(args.base_url), args.stage.resolve(), args.output.resolve()
    artifact_root = (ROOT / "Artifacts").resolve()
    require(stage.is_relative_to(artifact_root) and stage != artifact_root, "Stage must be inside Artifacts")
    require(output.is_relative_to(artifact_root) and output != artifact_root and not stage.is_relative_to(output)
            and not output.is_relative_to(stage), "Report output must be a separate Artifacts subdirectory")
    require(args.timeout > 0, "Timeout must be positive")
    manifest, expected = staged_manifest(stage)
    previous = archive_previous(output)
    report = {"status": "running", "startedUtc": timestamp(), "baseUrl": base_url,
              "scope": "Actual HTTPS bytes for every staged file, desktop/mobile landing checks and original captures; game interaction is separate.",
              "sourceCommit": manifest["sourceCommit"], "gameRevisions": manifest.get("gameRevisions"),
              "manifest": {"file": str((stage / "build-manifest.json").relative_to(ROOT)), "sha256": expected["build-manifest.json"]},
              "files": [], "checks": [], "visualReview": {"status": "pending",
                  "scope": "Direct inspection of original landing-1440.png and landing-390.png is required separately."},
              "limits": ["Mobile is Chromium portrait emulation, not a physical phone or Safari test.",
                         "No game interaction, performance claim or user aesthetic approval is implied."],
              "harness": {"file": str(Path(__file__).relative_to(ROOT)), "sha256": digest(Path(__file__))}}
    if previous:
        report["previousRunArchive"] = previous
    save_report(output, report)
    try:
        context = https_context()
        for name, expected_hash in sorted(expected.items()):
            report["files"].append(fetch_file(base_url, stage, name, expected_hash, args.timeout, context))
            save_report(output, report)
        from playwright.sync_api import sync_playwright
        renderer = (["--ozone-platform=x11", "--use-gl=angle", "--use-angle=gl", "--ignore-gpu-blocklist"]
                    if args.renderer == "x11-mesa" else ["--use-gl=angle", "--use-angle=swiftshader", "--enable-unsafe-swiftshader"])
        with sync_playwright() as playwright:
            browser = playwright.chromium.launch(executable_path=args.chromium, headless=args.renderer != "x11-mesa",
                      proxy=browser_proxy(), args=["--no-sandbox", "--disable-dev-shm-usage", *renderer])
            try:
                for width, height in ((1440, 1050), (390, 844)):
                    report["checks"].append(inspect_landing(browser, base_url, output, width, height, args.timeout))
                    save_report(output, report)
            finally:
                browser.close()
        report["screenshots"] = [check["screenshot"] for check in report["checks"] if check.get("screenshot")]
        report["status"] = ("passed" if len(report["files"]) == len(expected)
                            and all(item["passed"] for item in report["files"])
                            and len(report["checks"]) == 2 and all(check["passed"] for check in report["checks"]) else "failed")
    except Exception as error:
        report.update(status="failed", error=safe_error(error))
    finally:
        report["checkedUtc"] = timestamp()
        save_report(output, report)
    print(json.dumps({"status": report["status"], "files": len(report["files"]), "viewports": len(report["checks"]),
                      "visualReview": report["visualReview"]["status"], "report": str((output / "report.json").relative_to(ROOT))}))
    return 0 if report["status"] == "passed" else 1


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (ValueError, OSError, json.JSONDecodeError) as error:
        print("Public portal verification refused: " + safe_error(error), file=sys.stderr)
        sys.exit(1)
