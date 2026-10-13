// This function is injected into the page's MAIN world without extension privileges.
async function vkMusicVolume(action) {
  if (!["vk.com", "www.vk.com", "vk.ru", "www.vk.ru", "music.vk.com", "music.vk.ru"].includes(location.hostname)) {
    return { found: false };
  }
  if (!["probe", "up", "down", "mute"].includes(action)) return { found: false };

  let player;
  try { player = typeof window.getAudioPlayer === "function" ? window.getAudioPlayer() : null; }
  catch { player = null; }
  let readVolume;
  let writeVolume;
  let playing = false;
  let identity;

  if (player && typeof player.setVolume === "function") {
    readVolume = () => typeof player.getVolume === "function" ? player.getVolume() : player._volume;
    writeVolume = value => player.setVolume(value);
    playing = typeof player.isPlaying === "function" && !!player.isPlaying();
    identity = player;
  } else {
    // Only HTML audio on a music page is eligible; never touch VK videos or clips.
    const musicPage = location.hostname.startsWith("music.") || /^\/(audios?|music)(?:[/?_\d]|$)/.test(location.pathname);
    if (!musicPage) return { found: false };
    const elements = Array.from(document.querySelectorAll("audio")).filter(audio => audio.currentSrc || audio.src);
    const playingElements = elements.filter(audio => !audio.paused && !audio.ended);
    if (playingElements.length > 1) return { found: false, ambiguous: true };
    const audio = playingElements[0] || (elements.length === 1 ? elements[0] : null);
    if (!audio) return { found: false };
    readVolume = () => audio.muted ? 0 : audio.volume;
    writeVolume = value => { audio.volume = value; audio.muted = false; };
    playing = !audio.paused && !audio.ended;
    identity = audio;
  }

  const volume = readVolume();
  if (typeof volume !== "number" || !Number.isFinite(volume) || volume < 0 || volume > 1) return { found: false };
  if (action === "probe") return { found: true, playing, volume };

  // Keep mute memory per player and per page, across repeated injected calls.
  const stateKey = "__playerForGameVkVolume_v1";
  let state = window[stateKey];
  if (!state || state.player !== identity) state = window[stateKey] = { player: identity, previous: 0.5 };
  let next;
  if (action === "mute") {
    if (volume > 0) { state.previous = volume; next = 0; }
    else next = typeof state.previous === "number" && state.previous > 0 && state.previous <= 1 ? state.previous : 0.5;
  } else {
    next = Math.max(0, Math.min(1, Math.round((volume + (action === "up" ? 0.05 : -0.05)) * 100) / 100));
  }
  writeVolume(next);
  // Give VK's player time to update its own slider/state.
  await new Promise(resolve => setTimeout(resolve, 50));
  const applied = readVolume();
  return { found: true, playing, ok: Number.isFinite(applied) && Math.abs(applied - next) < 0.011, volume: applied };
}
