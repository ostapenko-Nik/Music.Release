importScripts("player.js");

const vkUrls = ["https://vk.com/*", "https://www.vk.com/*", "https://vk.ru/*", "https://www.vk.ru/*", "https://music.vk.com/*", "https://music.vk.ru/*"];
let socket;
let keepAlive;
let reconnect;

async function inspectTab(tab, action) {
  try {
    const results = await chrome.scripting.executeScript({
      target: { tabId: tab.id }, world: "MAIN", func: vkMusicVolume, args: [action]
    });
    return { ...(results[0]?.result || { found: false }), tabId: tab.id, active: !!tab.active };
  } catch { return { found: false }; }
}

async function handleCommand(message) {
  if (!message.id) return;
  let reply = { found: false, ok: false };
  const tabs = await chrome.tabs.query({ url: vkUrls });
  if (message.command === "probe") {
    const states = await Promise.all(tabs.map(tab => inspectTab(tab, "probe")));
    const found = states.filter(state => state.found);
    found.sort((a, b) => Number(b.playing) - Number(a.playing) || Number(b.active) - Number(a.active));
    reply = found[0] || reply;
    reply.ambiguous = states.some(state => state.ambiguous) || found.filter(state => state.playing).length > 1;
  } else if (message.command === "apply" && ["up", "down", "mute"].includes(message.action)) {
    // Recheck the tab URL: it may have navigated since the probe.
    const tab = tabs.find(candidate => candidate.id === message.tabId);
    if (tab) reply = await inspectTab(tab, message.action);
  }
  return { ...reply, id: message.id };
}

function setBadge(connected) {
  chrome.action.setBadgeText({ text: connected ? "VK" : "OFF" });
  chrome.action.setBadgeBackgroundColor({ color: connected ? "#168452" : "#7a4753" });
}

function connect() {
  if (socket && (socket.readyState === WebSocket.OPEN || socket.readyState === WebSocket.CONNECTING)) return;
  clearTimeout(reconnect);
  const current = socket = new WebSocket("ws://127.0.0.1:43821/vk-volume");
  current.onopen = () => {
    setBadge(true);
    clearInterval(keepAlive);
    keepAlive = setInterval(() => {
      if (current.readyState === WebSocket.OPEN) current.send(JSON.stringify({ type: "ping" }));
    }, 20000);
  };
  current.onmessage = async event => {
    try {
      const message = JSON.parse(event.data);
      if (message.type === "pong") return;
      const reply = await handleCommand(message);
      if (reply && current.readyState === WebSocket.OPEN) current.send(JSON.stringify(reply));
    } catch { /* A closed/navigated tab must not terminate the connection. */ }
  };
  current.onerror = () => current.close();
  current.onclose = () => {
    clearInterval(keepAlive);
    setBadge(false);
    if (socket === current) socket = null;
    reconnect = setTimeout(connect, 3000);
  };
}

chrome.alarms.create("vk-volume-reconnect", { periodInMinutes: 0.5 });
chrome.alarms.onAlarm.addListener(alarm => { if (alarm.name === "vk-volume-reconnect") connect(); });
chrome.runtime.onStartup.addListener(connect);
chrome.runtime.onInstalled.addListener(connect);
chrome.runtime.onMessage.addListener((message, sender, respond) => {
  if (message.type === "status") {
    connect();
    respond({ connected: socket?.readyState === WebSocket.OPEN });
  }
});
connect();
