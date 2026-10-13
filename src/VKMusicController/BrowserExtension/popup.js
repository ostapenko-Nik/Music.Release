async function refresh() {
  const status = await chrome.runtime.sendMessage({ type: "status" });
  document.getElementById("status").textContent = status?.connected
    ? "Подключено к PlayerForGame"
    : "Нет подключения. Запустите PlayerForGame.";
}
refresh();
setInterval(refresh, 1000);
