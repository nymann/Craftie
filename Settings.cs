using System.Windows.Forms;
using PoeHUD.Hud.Settings;
using PoeHUD.Plugins;

namespace Craftie
{
    public class Settings : SettingsBase
    {
        public Settings()
        {
            Enable = true;
            CurrencyTab = new RangeNode<int>(0, 0, 50);

            LinkItem = false;
            LinksWanted = new RangeNode<int>(6, 1, 6);

            SocketItem = false;
            SocketsWanted = new RangeNode<int>(6, 1, 6);

            ChromaticItem = false;
            RedSocketsWanted = new RangeNode<int>(0, 0, 6);
            GreenSocketsWanted = new RangeNode<int>(0, 0, 6);
            BlueSocketsWanted = new RangeNode<int>(0, 0, 6);

            HotkeyEnabled = true;
            Hotkey = new HotkeyNode(Keys.F2);

            ExtraDelay = new RangeNode<int>(0, 0, 2000);
        }

        [Menu("Curreny Tab", 6000)]
        public RangeNode<int> CurrencyTab { get; set; }

        #region Jeweller's

        [Menu("Socket Item", 7000)]
        public ToggleNode SocketItem { get; set; }

        [Menu("Sockets wanted:", 700, 7000)]
        public RangeNode<int> SocketsWanted { get; set; }

        #endregion

        #region Fusing

        [Menu("Fuse Item", 8000)]
        public ToggleNode LinkItem { get; set; }

        [Menu("Links wanted:", 800, 8000)]
        public RangeNode<int> LinksWanted { get; set; }

        #endregion

        #region Chromatic

        [Menu("Chromatic Item", 9000)]
        public ToggleNode ChromaticItem { get; set; }

        [Menu("Reds wanted:", 900, 9000)]
        public RangeNode<int> RedSocketsWanted { get; set; }

        [Menu("Blues wanted:", 901, 9000)]
        public RangeNode<int> BlueSocketsWanted { get; set; }

        [Menu("Greens wanted:", 902, 9000)]
        public RangeNode<int> GreenSocketsWanted { get; set; }

        #endregion

        [Menu("Hotkey", 10000)]
        public ToggleNode HotkeyEnabled { get; set; }

        [Menu("Change Hotkey", 1000, 10000)]
        public HotkeyNode Hotkey { get; set; }

        [Menu("Extra Delay", 11000)]
        public RangeNode<int> ExtraDelay { get; set; }
    }
}
