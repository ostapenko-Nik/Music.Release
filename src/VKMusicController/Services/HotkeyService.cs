using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Input;
using VKMusicController.Models;

namespace VKMusicController.Services
{
    public class HotkeyService : IDisposable
    {
        private readonly LoggerService _logger;
        private readonly Dictionary<int, HotkeyAction> _registered = new();
        private readonly Dictionary<HotkeyAction, int> _actionIds = new();
        private readonly InvisibleMessageWindow _messageWindow;
        private int _currentId = 0;
        private string? _registeredSettings;

        public event EventHandler<HotkeyAction>? HotkeyPressed;

        public HotkeyService(LoggerService logger)
        {
            _logger = logger;
            _messageWindow = new InvisibleMessageWindow();
            _messageWindow.HotkeyPressed += OnHotkeyPressed;
        }

        public void RegisterHotkeys(AppSettings settings)
        {
            var signature = string.Join(";", settings.Hotkeys.Select(h => $"{h.Action}:{h.Enabled}:{h.Modifiers}:{h.Key}"));
            if (_registeredSettings == signature)
                return;
            UnregisterHotkeys();
            foreach (var hotkey in settings.Hotkeys)
            {
                if (!hotkey.Enabled || hotkey.Key == Key.None)
                    continue;

                var modifiers = (uint)hotkey.Modifiers;
                var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(hotkey.Key);
                var id = ++_currentId;
                if (RegisterHotKey(_messageWindow.Handle, id, modifiers, virtualKey))
                {
                    _registered[id] = hotkey.Action;
                    _actionIds[hotkey.Action] = id;
                    _logger.LogInfo($"Registered hotkey {hotkey.Action}: {hotkey.Modifiers}+{hotkey.Key}");
                }
                else
                {
                    _logger.LogError($"Failed to register hotkey: {hotkey.Modifiers}+{hotkey.Key}");
                }
            }
            if (_registered.Count == settings.Hotkeys.Count(h => h.Enabled && h.Key != Key.None))
                _registeredSettings = signature;
        }

        public void UnregisterHotkeys()
        {
            foreach (var id in _registered.Keys)
            {
                UnregisterHotKey(_messageWindow.Handle, id);
            }
            _registered.Clear();
            _actionIds.Clear();
            _registeredSettings = null;
            _currentId = 0;
        }

        private void OnHotkeyPressed(int id)
        {
            if (_registered.TryGetValue(id, out var action))
            {
                HotkeyPressed?.Invoke(this, action);
            }
        }

        public void Dispose()
        {
            UnregisterHotkeys();
            _messageWindow.Dispose();
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}
