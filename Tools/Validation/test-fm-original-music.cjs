#!/usr/bin/env node
// Exercise the actual template controller with deferred official-widget replies.
// This checks lifecycle/races, not network latency, browser policy, or audibility.
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const { test } = require("node:test");

const root = path.resolve(__dirname, "../..");
const template = fs.readFileSync(path.join(root, "Games/FantasyCards/Assets/WebGLTemplates/FirstGame/index.html"), "utf8");
const controller = template.match(/<script id="fm-original-music-controller">([\s\S]*?)<\/script>/)[1];
const ids = { login: 234910360, lobby: 234910359, deck: 234910362, battle: 234910353 };

function harness(preferences = {}) {
  const elements = new Map(), listeners = new Map(), widgets = {}, calls = [];
  const storage = new Map(Object.entries(preferences));
  let clock = 100;
  function element(id) {
    if (!elements.has(id)) elements.set(id, {
      id, dataset: {}, attributes: {}, handlers: {}, textContent: "", open: false,
      classList: { toggle() {} },
      setAttribute(key, value) { this.attributes[key] = value; },
      addEventListener(type, callback) { this.handlers[type] = callback; },
      closest(selector) { return selector === "#" + this.id ? this : null; }
    });
    return elements.get(id);
  }
  const document = {
    getElementById: element,
    addEventListener(type, callback) { listeners.set(type, callback); },
    createElement() { return {}; },
    body: { appendChild(api) { document.api = api; } }
  };
  const Events = Object.fromEntries(["READY", "PLAY", "PLAY_PROGRESS", "PAUSE", "FINISH", "ERROR"].map(e => [e, e]));
  const SC = { Widget(frame) {
    const scene = frame.id === "fm-original-player" ? "login" : frame.id.replace("fm-original-player-", "");
    const widget = widgets[scene] = {
      callbacks: {}, sounds: [], positions: [], volume: null,
      bind(type, callback) { this.callbacks[type] = callback; },
      emit(type, data = {}) { this.callbacks[type](data); },
      setVolume(value) { this.volume = value; calls.push([scene, "volume", value]); },
      play() { calls.push([scene, "play"]); },
      pause() { calls.push([scene, "pause"]); },
      seekTo(value) { calls.push([scene, "seek", value]); },
      getCurrentSound(callback) { this.sounds.push(callback); },
      getPosition(callback) { this.positions.push(callback); },
      load() { throw Error("Scene changes must not reload an official player"); },
      replySound(sound = { id: ids[scene], title: "Actual " + scene, user: { username: "SOUND-I" } }) {
        assert.ok(this.sounds.length, "Expected a pending source query for " + scene);
        this.sounds.shift()(sound);
      },
      replyPosition(position) { this.positions.shift()(position); }
    };
    return widget;
  } };
  SC.Widget.Events = Events;
  const window = { localStorage: {
    getItem(key) { return storage.has(key) ? storage.get(key) : null; },
    setItem(key, value) { storage.set(key, value); }
  } };
  vm.runInNewContext(controller, { window, document, SC, performance: { now: () => ++clock }, Number, String, Math, Object });
  document.api.onload();
  return {
    widgets, calls, storage, element, status: element("fm-music-status"), bridge: window.FirstGameOriginalMusic,
    ready() { Object.values(widgets).forEach(w => w.emit("READY")); },
    gesture(target = "landscape-start", type = "pointerdown", trusted = true) {
      listeners.get(type)({ target: element(target), isTrusted: trusted });
    },
    scene(scene) { window.FirstGameOriginalMusic.receive({ change: "scene", scene, muted: false, volume: .6 }); },
    accept(scene) { widgets[scene].emit("PLAY"); widgets[scene].replySound(); },
    count(scene, command) { return calls.filter(c => c[0] === scene && c[1] === command).length; }
  };
}

test("four official embeds are eager, independent players and try playback during loading", () => {
  const frames = [...template.matchAll(/<iframe [^>]+>/g)].map(m => m[0]);
  assert.equal(frames.length, 4);
  for (const frame of frames) {
    assert.match(frame, /https:\/\/w\.soundcloud\.com\/player\//);
    assert.match(frame, /single_active=false/);
    assert.match(frame, /loading="eager"/);
    assert.match(frame, /allow="autoplay"/);
  }
  const h = harness(); h.ready();
  for (const scene of Object.keys(ids)) assert.equal(h.count(scene, "play"), 1);
  assert.equal(h.widgets.login.volume, 60);
  for (const scene of ["lobby", "deck", "battle"]) assert.equal(h.widgets[scene].volume, 0);
  assert.equal(h.status.dataset.trackId, undefined);
});

test("a trusted orientation/loading/page gesture retries autoplay without a canvas-only gate", () => {
  const h = harness(); h.ready();
  h.gesture("landscape-start", "pointerdown", false);
  assert.equal(h.count("login", "play"), 1);
  h.gesture("landscape-start");
  assert.equal(h.count("login", "play"), 2);
  h.gesture("portrait-start", "pointerup");
  h.gesture("fm-loading-start", "keydown");
  assert.equal(h.count("login", "play"), 4);
  assert.equal(h.count("battle", "play"), 4);
});

test("handover keeps outgoing playback until the incoming actual sound is confirmed", () => {
  const h = harness(); h.ready(); h.accept("login");
  h.widgets.login.replyPosition(320);
  h.scene("lobby");
  assert.equal(h.count("login", "pause"), 0);
  assert.equal(h.status.dataset.trackId, String(ids.login));
  h.widgets.lobby.emit("PLAY");
  assert.equal(h.count("login", "pause"), 0);
  h.widgets.lobby.replySound();
  assert.equal(h.count("login", "pause"), 1);
  assert.equal(h.widgets.login.volume, 0);
  assert.equal(h.widgets.lobby.volume, 60);
  assert.equal(h.status.dataset.trackId, String(ids.lobby));
  assert.equal(h.status.dataset.trackTitle, "Actual lobby");
  assert.equal(h.status.dataset.publisher, "SOUND-I");
  h.widgets.lobby.replyPosition(25);
  h.widgets.login.emit("PLAY_PROGRESS", { currentPosition: 9999 });
  assert.equal(h.status.dataset.positionMs, "25");
});

test("late PLAY/source/position replies cannot win a rapid scene change", () => {
  const h = harness(); h.ready(); h.accept("login");
  h.scene("lobby"); h.widgets.lobby.emit("PLAY");
  h.scene("deck"); h.widgets.lobby.replySound();
  assert.equal(h.status.dataset.trackId, String(ids.login));
  assert.equal(h.widgets.lobby.volume, 0);
  assert.equal(h.count("login", "pause"), 0);
  h.accept("deck"); h.widgets.deck.replyPosition(17);
  h.widgets.login.replyPosition(6000);
  assert.equal(h.status.dataset.positionMs, "17");
  assert.equal(h.status.dataset.trackId, String(ids.deck));
  h.widgets.login.emit("PAUSE");
  assert.equal(h.status.dataset.playback, "playing");
});

test("actual source mismatch never publishes the requested id or interrupts outgoing music", () => {
  const h = harness(); h.ready(); h.accept("login");
  h.scene("battle"); h.widgets.battle.emit("PLAY");
  h.widgets.battle.replySound({ id: 999, title: "Unexpected sound" });
  assert.equal(h.status.dataset.trackId, String(ids.login));
  assert.equal(h.count("login", "pause"), 0);
  assert.equal(h.widgets.battle.volume, 0);
  assert.equal(h.count("battle", "pause"), 1);
  assert.equal(h.element("fm-player-panel").open, true);
});

test("warm-up waits for real progress, then pauses and rewinds the prepared player", () => {
  const h = harness(); h.ready(); h.widgets.deck.emit("PLAY");
  assert.equal(h.count("deck", "pause"), 0);
  h.widgets.deck.emit("PLAY_PROGRESS", { currentPosition: 0 });
  assert.equal(h.count("deck", "pause"), 0);
  h.widgets.deck.emit("PLAY_PROGRESS", { currentPosition: 123 });
  assert.equal(h.count("deck", "pause"), 1);
  assert.equal(h.count("deck", "seek"), 1);
  assert.equal(h.bridge.getDiagnostics().find(p => p.scene === "deck").warmed, true);
  h.widgets.deck.emit("PAUSE");
  h.scene("deck");
  assert.equal(h.count("deck", "play"), 2);
});

test("footer mute persists and scene messages cannot overwrite mute or volume", () => {
  const h = harness(); h.ready(); h.accept("login");
  h.element("fm-music-toggle").handlers.click();
  assert.equal(h.bridge.getState().muted, true);
  h.bridge.receive({ change: "volume", volume: .25 });
  h.scene("lobby"); h.gesture();
  assert.equal(h.bridge.getState().muted, true);
  assert.equal(h.bridge.getState().volume, .25);
  assert.equal(h.storage.get("FC.originalMusic.muted"), "1");
  assert.equal(h.storage.get("FC.originalMusic.volume"), "0.25");
  assert.equal(h.widgets.lobby.volume, 0);
  h.bridge.receive({ change: "muted", muted: false });
  h.accept("lobby");
  assert.equal(h.widgets.lobby.volume, 25);
});

test("muting invalidates an in-flight source callback and later gestures keep explicit mute", () => {
  const h = harness(); h.ready(); h.widgets.login.emit("PLAY");
  h.bridge.receive({ change: "muted", muted: true });
  h.widgets.login.replySound();
  assert.equal(h.status.dataset.trackId, undefined);
  assert.equal(h.status.dataset.playback, "paused");
  h.gesture();
  assert.equal(h.widgets.login.volume, 0);
  assert.equal(h.bridge.getState().muted, true);
});

test("a cancelled pending PLAY is silenced and stopped rather than resurrecting a stale scene", () => {
  const h = harness(); h.ready();
  h.scene("lobby");
  h.widgets.login.emit("PLAY");
  assert.equal(h.widgets.login.volume, 0);
  assert.equal(h.count("login", "pause"), 1);
  assert.equal(h.status.dataset.trackId, undefined);
  h.accept("lobby");
  assert.equal(h.status.dataset.trackId, String(ids.lobby));
});

test("the footer start gesture starts once and a later click mutes the actual playing track", () => {
  const h = harness(); h.ready();
  h.gesture("fm-music-toggle");
  assert.equal(h.count("login", "play"), 1);
  h.element("fm-music-toggle").handlers.click();
  assert.equal(h.bridge.getState().muted, false);
  h.accept("login");
  h.element("fm-music-toggle").handlers.click();
  assert.equal(h.bridge.getState().muted, true);
  assert.equal(h.widgets.login.volume, 0);
});

test("volume updates also reach an initial pending audible playback attempt", () => {
  const h = harness(); h.ready();
  h.bridge.receive({ change: "volume", volume: .2 });
  assert.equal(h.widgets.login.volume, 20);
  assert.equal(h.widgets.deck.volume, 0);
});

test("selecting a warm player before its PAUSE reply still issues a fresh play command", () => {
  const h = harness(); h.ready(); h.accept("login");
  h.widgets.deck.emit("PLAY");
  h.widgets.deck.emit("PLAY_PROGRESS", { currentPosition: 120 });
  // The host issued pause, but the iframe has not replied with PAUSE yet.
  h.scene("deck");
  assert.equal(h.count("deck", "play"), 2);
  assert.equal(h.count("login", "pause"), 0);
  h.widgets.deck.emit("PAUSE");
  h.accept("deck");
  assert.equal(h.status.dataset.trackId, String(ids.deck));
  assert.equal(h.count("login", "pause"), 1);
});

test("rapid reentry into an outgoing player before PAUSE never mistakes it for continuing playback", () => {
  const h = harness(); h.ready(); h.accept("login");
  h.scene("lobby"); h.accept("lobby");
  assert.equal(h.count("login", "pause"), 1);
  h.scene("login");
  assert.equal(h.count("login", "play"), 2);
  assert.equal(h.count("lobby", "pause"), 0);
  h.widgets.login.emit("PAUSE");
  h.accept("login");
  assert.equal(h.count("lobby", "pause"), 1);
  assert.equal(h.status.dataset.trackId, String(ids.login));
});

test("stored browser settings survive reload while absent settings allow legacy Unity import", () => {
  const fresh = harness();
  assert.equal(fresh.bridge.getState().hasMutedPreference, false);
  assert.equal(fresh.bridge.getState().hasVolumePreference, false);
  const saved = harness({ "FC.originalMusic.muted": "1", "FC.originalMusic.volume": "0.4" }); saved.ready();
  assert.equal(saved.bridge.getState().muted, true);
  assert.equal(saved.bridge.getState().volume, .4);
  assert.equal(saved.bridge.getState().hasMutedPreference, true);
  assert.equal(saved.widgets.login.volume, 0);
});
