const { test } = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const folder = path.join(__dirname, "../src/VKMusicController/BrowserExtension");
const playerSource = fs.readFileSync(path.join(folder, "player.js"), "utf8");

function page({ volume = 0.5, playing = true, hostname = "vk.com", pathname = "/audios1", audio, api = true } = {}) {
  const player = { _volume: volume, isPlaying: () => playing, setVolume(value) { this._volume = value; } };
  const context = vm.createContext({
    window: api ? { getAudioPlayer: () => player } : {},
    location: { hostname, pathname }, document: { querySelectorAll: () => audio || [] },
    setTimeout: callback => callback()
  });
  vm.runInContext(playerSource, context);
  return { player, context, run: action => context.vkMusicVolume(action) };
}

test("changes only VK's music player and clamps 0–100%", async () => {
  const fixture = page({ volume: 0.98 });
  assert.equal((await fixture.run("up")).volume, 1);
  assert.equal((await fixture.run("up")).volume, 1);
  for (let i = 0; i < 22; i++) await fixture.run("down");
  assert.equal(fixture.player._volume, 0);
  assert.equal((await fixture.run("up")).volume, 0.05);
});

test("mute restores previous volume across injected calls", async () => {
  const fixture = page({ volume: 0.73 });
  assert.equal((await fixture.run("mute")).volume, 0);
  assert.equal((await fixture.run("probe")).volume, 0);
  assert.equal((await fixture.run("mute")).volume, 0.73);
});

test("reads live slider changes instead of keeping stale volume", async () => {
  const fixture = page();
  await fixture.run("up");
  fixture.player._volume = 0.2;
  assert.equal((await fixture.run("up")).volume, 0.25);
});

test("supports getVolume and reports a setter that did not apply", async () => {
  const fixture = page();
  fixture.player.getVolume = () => fixture.player._volume;
  fixture.player.setVolume = () => {};
  assert.equal((await fixture.run("down")).ok, false);
});

test("rejects non-VK pages and players with unknown volume", async () => {
  const external = page({ hostname: "youtube.com" });
  assert.equal((await external.run("up")).found, false);
  assert.equal(external.player._volume, 0.5);
  const unsupported = page();
  unsupported.player._volume = undefined;
  assert.equal((await unsupported.run("up")).found, false);
});

test("HTML audio fallback leaves videos/voice audio outside music pages alone", async () => {
  const audio = { volume: 0.4, muted: false, src: "track.mp3", paused: false, ended: false };
  const videoPage = page({ api: false, pathname: "/video1_2", audio: [audio] });
  assert.equal((await videoPage.run("up")).found, false);
  assert.equal(audio.volume, 0.4);
  const musicPage = page({ api: false, pathname: "/audios123", audio: [audio] });
  assert.equal((await musicPage.run("down")).volume, 0.35);
  assert.equal((await musicPage.run("mute")).volume, 0);
  assert.equal((await musicPage.run("mute")).volume, 0.35);
});

test("multiple playing HTML audio elements are ambiguous", async () => {
  const audio = () => ({ volume: 0.4, muted: false, src: "track.mp3", paused: false, ended: false });
  const fixture = page({ api: false, pathname: "/music", audio: [audio(), audio()] });
  assert.equal((await fixture.run("up")).ambiguous, true);
});

function background(tabs, states) {
  const calls = [];
  const context = vm.createContext({
    importScripts() {}, vkMusicVolume() {},
    WebSocket: class { static OPEN = 1; static CONNECTING = 0; readyState = 0; },
    setTimeout() {}, clearTimeout() {}, setInterval() {}, clearInterval() {},
    chrome: {
      tabs: { query: async () => tabs },
      scripting: { executeScript: async request => { calls.push(request); return [{ result: states[request.target.tabId] }]; } },
      action: { setBadgeText() {}, setBadgeBackgroundColor() {} },
      alarms: { create() {}, onAlarm: { addListener() {} } },
      runtime: { onStartup: { addListener() {} }, onInstalled: { addListener() {} }, onMessage: { addListener() {} } }
    }
  });
  vm.runInContext(fs.readFileSync(path.join(folder, "background.js"), "utf8"), context);
  return { run: message => context.handleCommand(message), calls };
}

test("playing background VK tab wins over active paused tab", async () => {
  const fixture = background([{ id: 1, active: true }, { id: 2, active: false }], {
    1: { found: true, playing: false }, 2: { found: true, playing: true }
  });
  assert.equal((await fixture.run({ id: "p", command: "probe" })).tabId, 2);
  await fixture.run({ id: "v", command: "apply", tabId: 2, action: "up" });
  assert.equal(fixture.calls.at(-1).target.tabId, 2);
  assert.equal(fixture.calls.at(-1).world, "MAIN");
});

test("probe signals multiple playing tabs; missing/navigated targets are untouched", async () => {
  const fixture = background([{ id: 1 }, { id: 2 }], {
    1: { found: true, playing: true }, 2: { found: true, playing: true }
  });
  assert.equal((await fixture.run({ id: "p", command: "probe" })).ambiguous, true);
  const count = fixture.calls.length;
  assert.equal((await fixture.run({ id: "v", command: "apply", tabId: 99, action: "up" })).ok, false);
  assert.equal(fixture.calls.length, count);
  await fixture.run({ id: "x", command: "apply", tabId: 1, action: "systemVolume" });
  assert.equal(fixture.calls.length, count);
});

test("manifest requests VK access only and ships popup/scripts", () => {
  const manifest = JSON.parse(fs.readFileSync(path.join(folder, "manifest.json"), "utf8"));
  assert.equal(manifest.manifest_version, 3);
  assert.deepEqual(manifest.permissions, ["scripting", "alarms"]);
  assert.ok(manifest.host_permissions.every(url => /^https:\/\/(www\.|music\.)?vk\.(com|ru)\/\*$/.test(url)));
  for (const file of [manifest.background.service_worker, manifest.action.default_popup, "popup.js", "player.js"]) {
    assert.ok(fs.existsSync(path.join(folder, file)));
  }
});
