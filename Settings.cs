using System.Windows.Forms;
using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using SharpDX;


namespace HighlightedItems
{
    public class Settings : ISettings
    {
        public int[,] IgnoredCells { get; set; } = new int[5, 12] {
            { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1},
            { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1},
            { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1},
            { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1},
            { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1}
        };

        public ToggleNode Enable { get; set; } = new(true);

        [Menu("Enable Inventory Dump Button")]
        public ToggleNode DumpButtonEnable { get; set; } = new(true);

        [Menu("Show Stack Sizes")]
        public ToggleNode ShowStackSizes { get; set; } = new(true);

        [Menu("Show Stack Count Next to Stack Size")]
        public ToggleNode ShowStackCountWithSize { get; set; } = new(true);

        [Menu("Hotkey")]
        public HotkeyNode HotKey { get; set; } = new(Keys.F1);


        [Menu("Use Thread.Sleep", "Is a little faster, but HUD will hang while clicking")]
        public ToggleNode UseThreadSleep { get; set; } = new ToggleNode(false);


        public ToggleNode CancelWithRightMouseButton { get; set; } = new ToggleNode(true);
        public DelaySettings DelaySettings { get; set; } = new DelaySettings();

    }

    [Submenu]
    public class DelaySettings
    {
        [Menu("Idle mouse delay", "Wait this long after the user lets go of the button and stops moving the mouse")]
        public RangeNode<int> IdleMouseDelay { get; set; } = new RangeNode<int>(200, 0, 1000);
        [Menu("Mouse Up/Down Extra Delay, 5 + X")]
        public RangeNode<int> ExtraDelay { get; set; } = new(20, 0, 100);
        public RangeNode<int> KeyDelay { get; set; } = new(20, 0, 100);
        public RangeNode<int> MouseMoveDelay { get; set; } = new(20, 0, 100);
        public RangeNode<int> MouseDownDelay { get; set; } = new(20, 0, 100);
        

    }
}