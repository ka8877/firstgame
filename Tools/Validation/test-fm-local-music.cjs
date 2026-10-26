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
function param() { return { value: 0, cancelScheduledValues() {}, setValueAtTime(v) { this.value = v; }, setTargetAtTime(v) { this.value = v; } }; }
function setup({ stored = {}, vorbis = true } = {}) {
  const elements = Object.fromEntries(['fm-music-toggle', 'fm-music-status', 'fm-loading-start'].map(id => [id, new Element()]));
  const media = [], contexts = [], storage = new Map(Object.entries(stored));
  const document = new Element();
  document.getElementById = id => elements[id];
  document.createElement = name => { assert.equal(name, 'audio'); const audio = new Media(vorbis); media.push(audio); return audio; };
  document.body = { appendChild() {} };
  class AudioContext {
    constructor() { this.state = 'suspended'; this.currentTime = 0; this.destination = {}; this.sources = []; this.gains = []; contexts.push(this); }
    createGain() { const gain = { gain: param(), connect() {} }; this.gains.push(gain); return gain; }
    createMediaElementSource(audio) { assert(!this.sources.includes(audio), 'No element can be routed twice'); this.sources.push(audio); return { connect() {} }; }
    addEventListener() {}
    resume() { this.state = 'running'; return Promise.resolve(); }
  }
  const window = { AudioContext, localStorage: { getItem: key => storage.has(key) ? storage.get(key) : null, setItem: (key, value) => storage.set(key, value) } };
  let now = 100;
  vm.runInNewContext(source, { window, document, performance: { now: () => now++ }, Number, String, Object, Math });
  const api = window.FirstGameLocalMusic;
  const gesture = (target = null, trusted = true) => document.fire('pointerdown', { isTrusted: trusted, target: { closest: () => target } });
  const receive = message => api.receive(message);
  const scene = name => receive({ change: 'scene', scene: name });
  const mute = value => receive({ change: 'muted', muted: value });
  return { api, elements, media, contexts, gesture, receive, scene, mute, storage, document };
}
const flush = () => new Promise(resolve => setImmediate(resolve));
async function resolve(audio, index = audio.requests.length - 1) { audio.requests[index].resolve(); await flush(); }

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

test('outgoing track continues until local target playback is confirmed; same-scene redraw never restarts it', async () => {
  const p = setup();
  p.gesture(); await resolve(p.media[0]);
  p.media[0].currentTime = 14;
  p.scene('login');
  assert.equal(p.media[0].requests.length, 1);
  assert.equal(p.media[0].currentTime, 14);
  p.scene('battle');
  assert.equal(p.media[0].paused, false);
  assert(p.api.getState().actualSource.endsWith('/title.ogg'));
  await resolve(p.media[3]);
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
