using System.Collections.Generic;
using System.Windows.Input;

namespace VKMusicController.Models
{
    public class AppSettings
    {
        public List<HotkeySetting> Hotkeys { get; set; } = new()
        {
            new HotkeySetting { Action = HotkeyAction.NextTrack, Enabled = true, Modifiers = ModifierKeys.Control | ModifierKeys.Alt, Key = Key.Right },
            new HotkeySetting { Action = HotkeyAction.PreviousTrack, Enabled = true, Modifiers = ModifierKeys.Control | ModifierKeys.Alt, Key = Key.Left },
            new HotkeySetting { Action = HotkeyAction.PlayPause, Enabled = true, Modifiers = ModifierKeys.Control | ModifierKeys.Alt, Key = Key.Space },
            new HotkeySetting { Action = HotkeyAction.VolumeUp, Enabled = true, Modifiers = ModifierKeys.Control | ModifierKeys.Alt, Key = Key.Up },
            new HotkeySetting { Action = HotkeyAction.VolumeDown, Enabled = true, Modifiers = ModifierKeys.Control | ModifierKeys.Alt, Key = Key.Down },
            new HotkeySetting { Action = HotkeyAction.ToggleMute, Enabled = true, Modifiers = ModifierKeys.Control | ModifierKeys.Alt, Key = Key.M },
            new HotkeySetting { Action = HotkeyAction.ShowHideOverlay, Enabled = true, Modifiers = ModifierKeys.Control | ModifierKeys.Alt, Key = Key.O }
        };

        public bool StartWithWindows { get; set; } = false;
        public bool ShowOverlayOnLaunch { get; set; } = true;
        public bool ShowSettingsOnLaunch { get; set; } = false;
        public bool ShowTrackChangeNotification { get; set; } = true;
        public int NotificationDurationSeconds { get; set; } = 3;

        public double OverlayLeft { get; set; } = 32;
        public double OverlayTop { get; set; } = 32;
        public double OverlayWidth { get; set; } = 520;
        public double OverlayHeight { get; set; } = 190;
        public double OverlayOpacity { get; set; } = 0.96;
        public double CornerRadius { get; set; } = 22;
        public bool ShowArtwork { get; set; } = true;
        public bool ShowArtist { get; set; } = true;
        public bool ShowAlbum { get; set; } = true;
        public bool ShowProgress { get; set; } = true;
        public bool ShowControls { get; set; } = true;
        public bool ShowOverlayOnTrackChange { get; set; } = false;
        public string AppTheme { get; set; } = "Nebula";
    }
}
