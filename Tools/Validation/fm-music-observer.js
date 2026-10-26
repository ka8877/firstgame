// Passive observation of the shipped official-player footer. No game or player
// state is set, and no requested track ID is treated as actual source identity.
(function () {
  "use strict";
  var evidence = window.__fmOriginalMusicEvidence = { version: 1, requests: [], handovers: [], gestures: [] };
  var seenRequests = new Set(), seenHandovers = new Set();
  function players() {
    return window.FirstGameOriginalMusic ? window.FirstGameOriginalMusic.getDiagnostics() : [];
  }
  function observe() {
    var status = document.getElementById("fm-music-status");
    if (!status) return;
    var data = status.dataset;
    var requestAt = Number(data.requestedAtMs);
    if (!Number.isFinite(requestAt)) return;
    if (!seenRequests.has(requestAt)) {
      seenRequests.add(requestAt);
      evidence.requests.push({ requestedAtMs: requestAt, requestedScene: data.requestedScene,
        requestedTrackId: data.requestedTrackId, observedAtMs: performance.now(), players: players() });
    }
    var acceptedAt = Number(data.playAtMs), providerPlayAt = Number(data.playEventAtMs);
    var key = requestAt + ":" + data.trackId;
    if (data.playback !== "playing" || !data.trackId || !Number.isFinite(acceptedAt) || acceptedAt < requestAt || seenHandovers.has(key)) return;
    seenHandovers.add(key);
    var firstGestureAt = evidence.gestures.length ? evidence.gestures[0].atMs : null;
    evidence.handovers.push({ requestedAtMs: requestAt, requestedScene: data.requestedScene,
      requestedTrackId: data.requestedTrackId, actualTrackId: data.trackId,
      actualTitle: data.trackTitle, actualPublisher: data.publisher,
      actualProviderPlayAtMs: Number.isFinite(providerPlayAt) ? providerPlayAt : null,
      confirmedHandoverAtMs: acceptedAt, requestToConfirmedHandoverMs: acceptedAt - requestAt,
      requestToProviderPlayMs: Number.isFinite(providerPlayAt) && providerPlayAt >= requestAt ? providerPlayAt - requestAt : null,
      providerWasAlreadyPlaying: Number.isFinite(providerPlayAt) && providerPlayAt < requestAt,
      firstTrustedGestureAtMs: firstGestureAt,
      firstGestureToProviderPlayMs: firstGestureAt !== null && providerPlayAt >= firstGestureAt ? providerPlayAt-firstGestureAt : null,
      firstGestureToConfirmedHandoverMs: firstGestureAt !== null && acceptedAt >= firstGestureAt ? acceptedAt-firstGestureAt : null,
      observedAtMs: performance.now(), players: players() });
  }
  new MutationObserver(observe).observe(document, { subtree: true, childList: true, attributes: true,
    attributeFilter: ["data-requested-at-ms", "data-play-at-ms", "data-playback", "data-track-id"] });
  ["pointerdown", "pointerup", "touchend", "keydown", "click"].forEach(function (type) {
    document.addEventListener(type, function (event) {
      if (event.isTrusted) evidence.gestures.push({ type: type, targetId: event.target && event.target.id || "",
        atMs: performance.now(), userActivation: !!(navigator.userActivation && navigator.userActivation.isActive) });
    }, true);
  });
}());
