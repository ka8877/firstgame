/* Passive diagnostics for the shipped local music elements. No source, playback,
 * volume, clock, network method or controller operation is changed. */
(() => {
  'use strict';
  const observations = [];
  function observe(reason) {
    const player = window.FirstGameLocalMusic;
    if (!player || !player.getState || !player.getDiagnostics) return;
    observations.push({epochMs: Date.now(), atMs: performance.now(), reason,
      state: player.getState(), players: player.getDiagnostics()});
  }
  ['play', 'playing', 'pause', 'abort', 'emptied', 'loadeddata'].forEach(type => {
    document.addEventListener(type, event => {
      if (event.target instanceof HTMLMediaElement && /^fm-local-audio-(title|lobby|deck|battle)$/.test(event.target.id))
        observe('media-' + type + ':' + event.target.id);
    }, true);
  });
  setInterval(() => observe('sample'), 250);
  Object.defineProperty(window, '__firstGameLocalMediaEvidence', {value: Object.freeze({
    snapshot: () => ({version: 1, observations: observations.slice()})
  }), writable: false});
})();
