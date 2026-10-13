using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using VKMusicController.Helpers;
using VKMusicController.Models;

namespace VKMusicController.ViewModels
{
    public class HotkeySettingViewModel : ObservableObject
    {
        private bool _enabled;
        private bool _ctrl;
        private bool _alt;
        private bool _shift;
        private bool _win;
        private Key _key;

        public HotkeySettingViewModel(HotkeySetting model)
        {
            Action = model.Action;
            Enabled = model.Enabled;
            Ctrl = model.Modifiers.HasFlag(ModifierKeys.Control);
            Alt = model.Modifiers.HasFlag(ModifierKeys.Alt);
            Shift = model.Modifiers.HasFlag(ModifierKeys.Shift);
            Win = model.Modifiers.HasFlag(ModifierKeys.Windows);
            Key = model.Key;
            AvailableKeys = Enum.GetValues(typeof(Key)).Cast<Key>().Where(k => k != Key.None).ToList();
        }

        public HotkeyAction Action { get; }

        public string ActionName => Action switch
        {
            HotkeyAction.NextTrack => "Следующий трек",
            HotkeyAction.PreviousTrack => "Предыдущий трек",
            HotkeyAction.PlayPause => "Play/Pause",
            HotkeyAction.VolumeUp => "Громкость VK +",
            HotkeyAction.VolumeDown => "Громкость VK −",
            HotkeyAction.ToggleMute => "Звук VK вкл./выкл.",
            HotkeyAction.ShowHideOverlay => "Показать/скрыть оверлей",
            _ => Action.ToString(),
        };

        public bool Enabled
        {
            get => _enabled;
            set => SetProperty(ref _enabled, value);
        }

        public bool Ctrl
        {
            get => _ctrl;
            set => SetProperty(ref _ctrl, value);
        }

        public bool Alt
        {
            get => _alt;
            set => SetProperty(ref _alt, value);
        }

        public bool Shift
        {
            get => _shift;
            set => SetProperty(ref _shift, value);
        }

        public bool Win
        {
            get => _win;
            set => SetProperty(ref _win, value);
        }

        public Key Key
        {
            get => _key;
            set => SetProperty(ref _key, value);
        }

        public List<Key> AvailableKeys { get; }

        public string HotkeyDisplay => Enabled ? $"{(Ctrl ? "Ctrl+" : string.Empty)}{(Alt ? "Alt+" : string.Empty)}{(Shift ? "Shift+" : string.Empty)}{(Win ? "Win+" : string.Empty)}{Key}" : "Disabled";

        public HotkeySetting ToModel() => new()
        {
            Action = Action,
            Enabled = Enabled,
            Modifiers = (Ctrl ? ModifierKeys.Control : ModifierKeys.None) |
                        (Alt ? ModifierKeys.Alt : ModifierKeys.None) |
                        (Shift ? ModifierKeys.Shift : ModifierKeys.None) |
                        (Win ? ModifierKeys.Windows : ModifierKeys.None),
            Key = Key
        };
    }
}
