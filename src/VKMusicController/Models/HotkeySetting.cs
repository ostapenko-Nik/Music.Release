using System.Windows.Input;

namespace VKMusicController.Models
{
    public class HotkeySetting
    {
        public HotkeyAction Action { get; set; }
        public bool Enabled { get; set; } = true;
        public ModifierKeys Modifiers { get; set; } = ModifierKeys.Control | ModifierKeys.Alt;
        public Key Key { get; set; } = Key.None;

        public override string ToString()
        {
            if (!Enabled || Key == Key.None)
                return "Disabled";
            return $"{Modifiers}+{Key}";
        }
    }
}
