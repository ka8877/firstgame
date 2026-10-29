#!/usr/bin/env node
// Lifecycle regression tests for the bundled player. Browser PCM is checked
// separately; these mocks do not claim physical playback or browser permission.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const test = require('node:test');
const html = fs.readFileSync(path.join(__dirname, '../../Games/FantasyCards/Assets/WebGLTemplates/FirstGame/index.html'), 'utf8');
const source = html.match(/<script id="fm-local-music-controller">([\s\S]*?)<\/script>/)[1];

class Element {
  constructor() { this.dataset = {}; this.events = {}; this.attributes = {}; this.textContent = ''; }
  addEventListener(name, fn) { (this.events[name] ||= []).push(fn); }
  setAttribute(name, value) { this.attributes[name] = value; }
  fire(name, event = {}) { for (const fn of this.events[name] || []) fn.call(this, event); }
}
class Media extends Element {
  constructor(vorbis) { super(); this.vorbis = vorbis; this.paused = true; this.currentTime = 0; this.volume = 1; this.readyState = 4; this.requests = []; this.pauseCalls = 0; }
  canPlayType() { return this.vorbis ? 'probably' : ''; }
  get currentSrc() { return 'https://example.test/games/fantasy-cards/' + this.src; }
  play() {
    let resolve, reject;
    const result = new Promise((yes, no) => { resolve = yes; reject = no; });
    this.requests.push({ resolve: () => { this.paused = false; resolve(); }, reject });
    return result;
  }
  pause() { this.paused = true; this.pauseCalls++; }
}
function param(clock) {
  let stored = 0, automation = null;
  function actual(at = clock()) {
    if (!automation || at < automation.start) return stored;
    if (automation.kind === 'target') {
      return automation.to + (automation.from - automation.to) *
        Math.exp(-(at - automation.start) / automation.timeConstant);
    }
    const progress = Math.max(0, Math.min(1, (at - automation.start) / (automation.end - automation.start)));
    if (automation.kind === 'curve') {
      // WebAudio linearly interpolates neighbouring supplied curve samples;
      // it does not apply an additional easing function between the points.
      const position = progress * (automation.values.length - 1);
      const left = Math.floor(position), right = Math.min(left + 1, automation.values.length - 1);
      return automation.values[left] + (automation.values[right] - automation.values[left]) * (position - left);
    }
    return automation.from + (automation.to - automation.from) * progress;
  }
  function hold(at = clock()) { stored = actual(at); automation = null; }
  return {
    get value() { return actual(); }, set value(v) { stored = v; automation = null; },
    cancelScheduledValues(at = clock()) {
      if (!automation) return;
      // The controller's legacy fallback explicitly reads/sets the held value.
      // A future event is removed; an already-started curve remains scheduled.
      if (automation.start >= at) automation = null;
    },
    cancelAndHoldAtTime(at = clock()) { hold(at); },
    setValueAtTime(v) { stored = v; automation = null; },
    setTargetAtTime(v, start = clock(), timeConstant = .035) {
      assert(timeConstant > 0);
      automation = { kind: 'target', from: actual(), to: v, start, timeConstant };
    },
    linearRampToValueAtTime(v, until) {
      automation = { kind: 'linear', from: actual(), to: v, start: clock(), end: until };
    },
    setValueCurveAtTime(values, start, duration) {
      assert(values.length >= 2 && duration > 0 && start >= 0);
      assert(Array.from(values).every(Number.isFinite));
      stored = actual();
      automation = { kind: 'curve', values: Array.from(values), start, end: start + duration };
    }
  };
}
function setup({ stored = {}, vorbis = true, webAudio = true } = {}) {
  const elements = Object.fromEntries(['fm-music-toggle', 'fm-music-status', 'fm-loading-start'].map(id => [id, new Element()]));
  const media = [], contexts = [], storage = new Map(Object.entries(stored));
  const document = new Element();
  document.getElementById = id => elements[id];
  document.createElement = name => { assert.equal(name, 'audio'); const audio = new Media(vorbis); media.push(audio); return audio; };
  document.body = { appendChild() {} };
  let now = 100, timerId = 0;
  const timers = new Map();
  class AudioContext {
    constructor() { this.state = 'suspended'; this.currentTime = now / 1000; this.destination = {}; this.sources = []; this.gains = []; contexts.push(this); }
    createGain() { const gain = { gain: param(() => this.currentTime), connect() {} }; this.gains.push(gain); return gain; }
    createMediaElementSource(audio) { assert(!this.sources.includes(audio), 'No element can be routed twice'); this.sources.push(audio); return { connect() {} }; }
    addEventListener() {}
    resume() { this.state = 'running'; return Promise.resolve(); }
  }
  const window = {
    AudioContext: webAudio ? AudioContext : undefined,
    setTimeout(fn, delay) { const id = ++timerId; timers.set(id, { fn, at: now + delay }); return id; },
    clearTimeout(id) { timers.delete(id); },
    localStorage: { getItem: key => storage.has(key) ? storage.get(key) : null, setItem: (key, value) => storage.set(key, value) }
  };
  function stepTo(time) {
    const elapsed = (time - now) / 1000;
    for (const audio of media) if (!audio.paused) audio.currentTime += elapsed;
    now = time;
    for (const context of contexts) context.currentTime = now / 1000;
  }
  function advance(ms) {
    const until = now + ms;
    let iterations = 0;
    while (timers.size) {
      const next = [...timers.entries()].sort((a, b) => a[1].at - b[1].at)[0];
      if (next[1].at > until) break;
      assert(++iterations < 1000, 'Fade timers must finish');
      stepTo(next[1].at); timers.delete(next[0]); next[1].fn();
    }
    stepTo(until);
  }
  vm.runInNewContext(source, { window, document, performance: { now: () => now }, Number, String, Object, Math });
  const api = window.FirstGameLocalMusic;
  const gesture = (target = null, trusted = true) => document.fire('pointerdown', { isTrusted: trusted, target: { closest: () => target } });
  const receive = message => api.receive(message);
  const scene = name => receive({ change: 'scene', scene: name });
  const mute = value => receive({ change: 'muted', muted: value });
  return { api, elements, media, contexts, gesture, receive, scene, mute, storage, document, advance };
}
const CROSSFADE_MS = 550; // Documented equal-power role transition, not a generous timeout.
const FALLBACK_SETTLE_MS = 560; // First 16ms fallback tick after the 550ms envelope.
const flush = () => new Promise(resolve => setImmediate(resolve));
async function resolve(audio, index = audio.requests.length - 1) { audio.requests[index].resolve(); await flush(); }

test('AudioParam fixture interpolates curve samples and holds an interrupted curve at the current value', () => {
  let now = 0;
  const gain = param(() => now);
  const values = new Float32Array([0, .5, 1]);
  gain.setValueCurveAtTime(values, 1, 2);
  values[1] = 0; // WebAudio owns a copy after scheduling.
  assert.equal(gain.value, 0);
  now = 1.5; assert.equal(gain.value, .25);
  now = 2; assert.equal(gain.value, .5);
  gain.cancelAndHoldAtTime(now);
  now = 3; assert.equal(gain.value, .5);
  gain.setValueCurveAtTime(new Float32Array([.5, .25, 0]), now, 2);
  now = 3.5; assert.equal(gain.value, .375);
  now = 5.5; assert.equal(gain.value, 0);
});

test('normal play contains no hosted player/SDK and creates no audio graph before a real gesture', () => {
  assert(!/<iframe\b|w\.soundcloud\.com|api-v2\.soundcloud\.com|SC\.Widget/.test(html));
  const p = setup();
  assert.equal(p.media.length, 4);
  assert(p.media.every(audio => audio.src.startsWith('StreamingAssets/Audio/ModernCard/')));
  assert.equal(p.contexts.length, 0);
  assert(p.media.every(audio => audio.requests.length === 0));
});

test('loading gesture starts one local theme through one actual media graph, with no silent multi-track prewarming', async () => {
  const p = setup();
  p.gesture();
  assert.equal(p.contexts.length, 1);
  assert.equal(p.contexts[0].sources.length, 4);
  assert.deepEqual(p.media.map(audio => audio.requests.length), [1, 0, 0, 0]);
  await resolve(p.media[0]);
  assert.equal(p.api.getState().actualPaused, false);
  assert(p.api.getState().actualSource.endsWith('/title.ogg'));
  assert.equal(p.elements['fm-music-status'].dataset.playback, 'playing');
});

test('synthetic page/toggle gestures do not authorize music', () => {
  const p = setup();
  p.gesture(null, false);
  p.elements['fm-music-toggle'].fire('click', { isTrusted: false });
  assert.equal(p.contexts.length, 0);
  assert(p.media.every(audio => audio.requests.length === 0));
});

test('a first title Continue gesture after full reload also unlocks the selected scene', async () => {
  const p = setup();
  p.scene('lobby');
  p.gesture();
  await resolve(p.media[1]);
  assert(p.api.getState().actualSource.endsWith('/lobby.ogg'));
  assert.deepEqual(p.media.map(audio => audio.requests.length), [0, 1, 0, 0]);
});

test('ready target crossfades the outgoing track, while same-scene redraw keeps its playhead', async () => {
  const p = setup();
  p.gesture(); await resolve(p.media[0]);
  p.advance(CROSSFADE_MS);
  assert.equal(p.api.getState().fadeOutSeconds, CROSSFADE_MS / 1000);
  assert.equal(p.api.getState().fadeInSeconds, CROSSFADE_MS / 1000);
  p.media[0].currentTime = 14;
  p.scene('login');
  assert.equal(p.media[0].requests.length, 1);
  assert.equal(p.media[0].currentTime, 14);
  p.scene('battle');
  assert.equal(p.media[0].paused, false);
  assert(p.api.getState().actualSource.endsWith('/title.ogg'));
  await resolve(p.media[3]);
  assert.equal(p.media[0].paused, false, 'Outgoing tail must not be cut abruptly');
  p.advance(90);
  assert(p.contexts[0].gains[1].gain.value > 0 && p.contexts[0].gains[1].gain.value < 1);
  assert(p.contexts[0].gains[4].gain.value > 0 && p.contexts[0].gains[4].gain.value < 1);
  const outgoingGain = p.contexts[0].gains[1].gain.value;
  const incomingGain = p.contexts[0].gains[4].gain.value;
  assert(Math.abs(outgoingGain ** 2 + incomingGain ** 2 - 1) < .001, 'Overlap must avoid the old energy trough');
  p.advance(CROSSFADE_MS - 90 - 1);
  assert.equal(p.media[0].paused, false, 'Outgoing tail remains until the documented envelope ends');
  p.advance(1);
  assert.equal(p.media[0].paused, true);
  assert(p.api.getState().actualSource.endsWith('/battle.ogg'));
  assert.equal(p.media.filter(audio => !audio.paused).length, 1);
});

test('late scene promises cannot become audible after rapid scene changes', async () => {
  const p = setup();
  p.gesture(); await resolve(p.media[0]);
  p.scene('deck'); p.scene('battle');
  await resolve(p.media[2]);
  assert.equal(p.media[2].paused, true);
  assert(p.api.getState().actualSource.endsWith('/title.ogg'));
  await resolve(p.media[3]);
  p.advance(CROSSFADE_MS);
  assert.equal(p.media.filter(audio => !audio.paused).length, 1);
  assert(p.api.getState().actualSource.endsWith('/battle.ogg'));
});

test('muting a pending play remains silent even if the old play promise resolves later', async () => {
  const p = setup();
  p.gesture(); p.mute(true);
  await resolve(p.media[0]);
  assert(p.media.every(audio => audio.paused));
  assert.equal(p.contexts[0].gains[0].gain.value, 0);
  assert.equal(p.api.getState().actualPaused, true);
});

test('an old promise cannot pause a later unmuted request on the same element', async () => {
  const p = setup();
  p.gesture(); p.mute(true); p.mute(false);
  await resolve(p.media[0], 1);
  const pauses = p.media[0].pauseCalls;
  await resolve(p.media[0], 0);
  assert.equal(p.media[0].pauseCalls, pauses);
  assert.equal(p.media[0].paused, false);
  assert(p.api.getState().actualSource.endsWith('/title.ogg'));
});

test('restored mute/volume survive scene messages and the footer shares the same preference', async () => {
  const p = setup({ stored: { 'FC.originalMusic.muted': '1', 'FC.originalMusic.volume': '0.28' } });
  p.gesture(); p.scene('battle');
  assert(p.media.every(audio => audio.requests.length === 0));
  p.elements['fm-music-toggle'].fire('click', { isTrusted: true });
  await resolve(p.media[3]);
  assert.equal(p.api.getState().muted, false);
  assert.equal(p.api.getState().volume, .28);
  assert.equal(p.contexts[0].gains[0].gain.value, .28);
  assert.equal(p.storage.get('FC.originalMusic.muted'), '0');
});

test('browsers without Vorbis media support use bundled MP3, never a remote fallback', () => {
  const p = setup({ vorbis: false });
  assert(p.media.every(audio => audio.src.endsWith('.mp3') && !audio.src.includes('://')));
});


test('returning lobby -> deck -> lobby restarts the lobby recording at zero exactly once', async () => {
  const p = setup();
  p.scene('lobby'); p.gesture(); await resolve(p.media[1]); p.advance(12000);
  assert(p.media[1].currentTime >= 12);
  p.scene('deck');
  assert.equal(p.media[2].currentTime, 0);
  await resolve(p.media[2]); p.advance(900);
  assert(p.media[1].paused);
  p.scene('lobby');
  assert.equal(p.media[1].currentTime, 0, 'A real scene return rewinds before play');
  await resolve(p.media[1]); p.advance(CROSSFADE_MS);
  const diagnostics = p.api.getDiagnostics().find(track => track.scene === 'lobby');
  assert.equal(diagnostics.restartCount, 2);
  assert.equal(diagnostics.lastStartPosition, 0);
  assert(p.media[1].currentTime > 0 && p.media[1].currentTime < 1);
  assert.equal(p.media.filter(audio => !audio.paused).length, 1);
});

test('settings, volume, mute and same-role redraw do not rewind a playing recording', async () => {
  const p = setup(); p.gesture(); await resolve(p.media[0]); p.advance(7000);
  const restarts = p.api.getDiagnostics()[0].restartCount;
  p.scene('login'); p.receive({ change: 'volume', volume: .35 });
  assert.equal(p.media[0].currentTime, 7);
  p.mute(true); p.advance(1000);
  assert.equal(p.media[0].currentTime, 7, 'Mute pauses instead of silently advancing');
  p.scene('login'); p.mute(false); await resolve(p.media[0]); p.advance(300);
  assert(Math.abs(p.media[0].currentTime - 7.3) < .001);
  assert.equal(p.api.getDiagnostics()[0].restartCount, restarts);
  assert.equal(p.api.getState().volume, .35);
});

test('a scene selected while muted is rewound when the user unmutes', async () => {
  const p = setup(); p.gesture(); await resolve(p.media[0]); p.advance(4000);
  p.scene('lobby'); await resolve(p.media[1]); p.advance(9000);
  p.mute(true); p.scene('login');
  assert(p.media[0].currentTime >= 4, 'No hidden playback is started while muted');
  p.mute(false); assert.equal(p.media[0].currentTime, 0);
  await resolve(p.media[0]); p.advance(500);
  assert(p.api.getState().actualSource.endsWith('/title.ogg'));
  assert(p.media[0].currentTime < 1);
  assert.equal(p.media.filter(audio => !audio.paused).length, 1);
});

test('rapid A -> B -> A waits for the audible old A tail before rewinding, with no stale pause', async () => {
  const p = setup(); p.gesture(); await resolve(p.media[0]); p.advance(5000);
  p.scene('deck'); p.scene('login');
  assert.equal(p.media[0].currentTime, 5, 'The old audible tail is not abruptly rewound');
  p.advance(CROSSFADE_MS - 1);
  assert(p.media[0].currentTime > 5, 'The previous recording continues until the last fade millisecond');
  p.advance(1);
  assert.equal(p.media[0].currentTime, 0, 'The replacement request starts at zero after the fade');
  await resolve(p.media[0]);
  await resolve(p.media[2]); // Previously pending deck is obsolete.
  p.advance(500);
  assert.equal(p.media[0].paused, false);
  assert.equal(p.media[2].paused, true);
  assert.equal(p.media.filter(audio => !audio.paused).length, 1);
  assert.equal(p.contexts.length, 1);
  assert.equal(p.contexts[0].sources.length, 4);
});

test('outgoing cleanup timers cannot stop a later return to that same track', async () => {
  const p = setup(); p.gesture(); await resolve(p.media[0]); p.advance(2000);
  p.scene('deck'); await resolve(p.media[2]); p.advance(50);
  p.scene('login'); p.advance(CROSSFADE_MS); await resolve(p.media[0]); p.advance(CROSSFADE_MS);
  assert.equal(p.media[0].paused, false);
  assert.equal(p.media.filter(audio => !audio.paused).length, 1);
  assert(p.media[0].currentTime < 1);
});

test('without WebAudio, the same local media sources fade and scene returns still restart', async () => {
  const p = setup({ webAudio: false }); p.gesture(); await resolve(p.media[0]); p.advance(9000);
  p.scene('battle'); await resolve(p.media[3]); p.advance(90);
  assert(p.media[0].volume > 0 && p.media[0].volume < .6);
  assert(p.media[3].volume > 0 && p.media[3].volume < .6);
  p.advance(500); assert(p.media[0].paused);
  p.scene('login'); assert.equal(p.media[0].currentTime, 0);
  await resolve(p.media[0]); p.advance(FALLBACK_SETTLE_MS);
  assert.equal(p.media.filter(audio => !audio.paused).length, 1);
  assert.equal(p.contexts.length, 0);
});


test('fallback volume edits during a transition use the new master level instead of restoring an old one', async () => {
  const p = setup({ webAudio: false }); p.gesture(); await resolve(p.media[0]); p.advance(1000);
  p.scene('deck'); await resolve(p.media[2]); p.advance(80);
  p.receive({ change: 'volume', volume: .12 }); p.advance(500);
  assert.equal(p.media[0].paused, true);
  assert(Math.abs(p.media[2].volume - .12) < .001);
  assert.equal(p.api.getDiagnostics()[2].restartCount, 1);
});


test('bounded transition diagnostics prove restart-at-zero and ignore same-role redraw requests', async () => {
  const p = setup(); p.scene('lobby'); p.gesture(); await resolve(p.media[1]); p.advance(7000);
  p.scene('deck'); await resolve(p.media[2]); p.advance(1000);
  p.scene('lobby'); await resolve(p.media[1]); p.advance(CROSSFADE_MS);
  const before = p.api.getState().sceneTransitionCount;
  p.scene('lobby'); p.scene('lobby');
  assert.equal(p.api.getState().sceneTransitionCount, before);
  assert(p.api.getState().sameRoleMessages >= 2);
  const changes = p.api.getTransitions().filter(event => event.event === 'role-change');
  assert.equal(changes.length, 3);
  assert(changes[2].targetPositionBefore > 7);
  const starts = p.api.getTransitions().filter(event => event.event === 'play-request' && event.scene === 'lobby');
  assert.equal(starts.length, 2);
  assert(starts.every(event => event.position === 0));
  assert(starts[1].serial > starts[0].serial);
  const detached = p.api.getTransitions(); detached[0].event = 'tampered';
  assert.notEqual(p.api.getTransitions()[0].event, 'tampered');
  for (let i = 0; i < 20; i++) {
    const scene = i % 2 ? 'lobby' : 'deck', audio = i % 2 ? p.media[1] : p.media[2];
    p.scene(scene); await resolve(audio); p.advance(CROSSFADE_MS);
  }
  assert.equal(p.api.getTransitions().length, 24);
  assert.equal(p.contexts.length, 1);
  assert.equal(p.media.length, 4);
});
