"""Fail-closed classification of the official widget's specific cancellation.

Raw errors stay in the report. This does not permit arbitrary provider errors:
the exact abort, every stack frame's source, and every actual playback window
must all match before a cancellation can be separated from runtime failures.
"""
from __future__ import annotations

import re
from collections.abc import Mapping

ABORT_MESSAGE = "signal is aborted without reason"
ABORT_HEADER = "AbortError: " + ABORT_MESSAGE
_SOURCE = r"https://widget\.sndcdn\.com/widget-[A-Za-z0-9_-]+\.js:[1-9][0-9]*:[1-9][0-9]*"
_NAMED_FRAME = re.compile(r"at [^()\r\n]+ \(" + _SOURCE + r"\)")
_BARE_FRAME = re.compile(r"at " + _SOURCE)


def _specific_provider_abort(error):
    if not isinstance(error, Mapping) or error.get("message") != ABORT_MESSAGE:
        return False
    stack = error.get("stack")
    if not isinstance(stack, str):
        return False
    lines = stack.splitlines()
    if not lines or lines[0] != ABORT_HEADER:
        return False
    frames = [line.strip() for line in lines[1:] if line.strip()]
    return bool(frames) and all(_NAMED_FRAME.fullmatch(frame) or _BARE_FRAME.fullmatch(frame) for frame in frames)


def classify_page_errors(game, page_errors, provider_windows):
    """Return raw errors partitioned into external cancellations/runtime errors.

    Only FantasyCards can have an accepted cancellation, and only after a
    nonempty set of exclusively verified actual-provider playback windows.
    Unknown formats and every other error remain gameRuntimeErrors.
    """
    verified = (game == "FantasyCards" and isinstance(provider_windows, (list, tuple))
                and bool(provider_windows) and all(isinstance(window, Mapping)
                    and window.get("status") == "verified-provider-playback" for window in provider_windows))
    diagnostic = {"externalProviderCancellations": [], "gameRuntimeErrors": []}
    for error in page_errors:
        group = "externalProviderCancellations" if verified and _specific_provider_abort(error) else "gameRuntimeErrors"
        diagnostic[group].append(error)
    return diagnostic
