"""Classify only independently proved cache revalidation in classic card runs.

This module is importable without browser dependencies. It never changes the
report or discards a request failure: a transport abort is expected only when
the same navigation has HTTP 304, the exact UnityCache success message, a ready
Unity instance, and successful restoration of the actual visible classic state.
"""

RESTORE_CHECK_ID = "classic.save_reload.visible_state"
RESTORE_CHECK_NAMES = frozenset({
    "Full reload and Continue restore visible deck, hand, LP, terrain and every hand card",
    "Visible deck, hand, LP, terrain and every hand card survived full browser reload and Continue",
})
READY_CHECK_NAME = "Unity createUnityInstance resolved after reload"


def _visible_state_equal(evidence):
    if not isinstance(evidence, dict):
        return False
    before, after = evidence.get("before"), evidence.get("after")
    if not isinstance(before, dict) or not isinstance(after, dict):
        return False
    fields = ("deck", "hand", "lp", "terrain")
    if not all(type(before.get(key)) is int and type(after.get(key)) is int
               and before[key] == after[key] for key in fields):
        return False
    before_hand, after_hand = evidence.get("beforeHand"), evidence.get("afterHand")
    return (isinstance(before_hand, list) and isinstance(after_hand, list)
            and bool(before_hand) and before_hand == after_hand
            and all(isinstance(name, str) and bool(name.strip()) for name in before_hand)
            and len(before_hand) == before["hand"])


def classify_network_failures(report):
    """Return the shared diagnostic keys, retaining every raw failed request.

Non-classic reports and incomplete, mismatched, or ambiguous proof fail closed.
Display labels are accepted only as exact known classic restore labels; future
harnesses can use RESTORE_CHECK_ID without depending on English wording.
"""
    expected, unexpected = [], []
    is_classic = (report.get("game") == "FantasyCards"
                  and report.get("suite") == "classic-cards-smoke-v1"
                  and isinstance(report.get("revision"), str)
                  and report["revision"].startswith("fm-classic-"))
    classic_evidence = report.get("classicEvidence")
    save_reload = classic_evidence.get("save_reload") if isinstance(classic_evidence, dict) else None
    restored_state = is_classic and _visible_state_equal(save_reload)
    for failure in report.get("failedRequests", []):
        navigation, url = failure.get("navigation"), failure.get("url")
        if not (restored_state and type(navigation) is int and navigation > 0
                and isinstance(url, str) and bool(url)
                and failure.get("error") == "net::ERR_ABORTED"
                and failure.get("method") == "GET"):
            unexpected.append(failure)
            continue
        configurations = [c for c in report.get("unityConfigurations", [])
                          if type(c.get("navigation")) is int
                          and c.get("navigation") == navigation and c.get("dataUrl") == url]
        responses = [r for r in report.get("networkResponses", [])
                     if type(r.get("navigation")) is int
                     and r.get("navigation") == navigation and r.get("url") == url
                     and r.get("method") == "GET" and r.get("status") == 304]
        marker = "[UnityCache] '" + url + "' successfully revalidated and served from the browser cache"
        cache_logs = [m for m in report.get("console", [])
                      if type(m.get("navigation")) is int
                      and m.get("navigation") == navigation and m.get("type") == "log"
                      and m.get("text") == marker]
        ready = [c for c in report.get("checks", [])
                 if c.get("passed") is True and type(c.get("navigation")) is int
                 and c.get("navigation") == navigation
                 and c.get("name") == READY_CHECK_NAME]
        restored = [c for c in report.get("checks", [])
                    if c.get("passed") is True and type(c.get("navigation")) is int
                    and c.get("navigation") == navigation
                    and (c.get("id") == RESTORE_CHECK_ID
                         or c.get("name") in RESTORE_CHECK_NAMES)
                    and _visible_state_equal(c.get("evidence"))
                    and all(c["evidence"][key] == save_reload[key]
                            for key in ("before", "after", "beforeHand", "afterHand"))]
        if configurations and responses and cache_logs and ready and restored:
            expected.append({
                "request": failure,
                "cacheEvidence": [m["text"] for m in cache_logs],
                "http304Evidence": responses,
                "unityConfigurationEvidence": configurations,
                "unityReadyEvidence": ready,
                "visibleRestoreCheckEvidence": restored,
                "visibleRestoreEvidence": {
                    key: save_reload[key]
                    for key in ("before", "after", "beforeHand", "afterHand")
                },
                "reason": "The exact loader data URL had a same-navigation GET HTTP 304 and exact UnityCache success log, Unity became ready, and a passed classic restore check is corroborated by equal actual visible resources and every hand card. The original ERR_ABORTED event remains recorded.",
            })
        else:
            unexpected.append(failure)
    return {"successfulCacheRevalidationsWithTransportAbort": expected,
            "unexpectedFailures": unexpected}
