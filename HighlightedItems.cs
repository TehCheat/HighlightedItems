using System.Windows.Forms;
using HighlightedItems.Utils;
using ExileCore;
using ExileCore.PoEMemory.Elements.InventoryElements;
using ExileCore.PoEMemory.MemoryObjects;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExileCore.Shared.Enums;
using ImGuiNET;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Threading;
using ExileCore.PoEMemory.Components;
using static ExileCore.PoEMemory.MemoryObjects.ServerInventory;

namespace HighlightedItems
{
    public class HighlightedItems : BaseSettingsPlugin<Settings>
    {
        private IngameState ingameState;
        private SharpDX.Vector2 windowOffset;
        private IEnumerator<bool> _currentOperation;

        private bool MoveCancellationRequested => Settings.CancelWithRightMouseButton && (Control.MouseButtons & MouseButtons.Right) != 0;

        public override bool Initialise()
        {
            base.Initialise();
            Name = "HighlightedItems";
            ingameState = GameController.Game.IngameState;
            windowOffset = GameController.Window.GetWindowRectangle().TopLeft;

            var pickBtn = Path.Combine(DirectoryFullName, "images\\pick.png").Replace('\\', '/');
            var pickLBtn = Path.Combine(DirectoryFullName, "images\\pickL.png").Replace('\\', '/');
            Graphics.InitImage(pickBtn, false);
            Graphics.InitImage(pickLBtn, false);

            return true;
        }

        public override void DrawSettings()
        {
            base.DrawSettings();
            this.DrawIgnoredCellsSettings();
        }

        public override void Render()
        {
            if (_currentOperation != null && _currentOperation.MoveNext())
            {
                DebugWindow.LogMsg("Running the inventory dump procedure...");
                return;
            }
            if (!Settings.Enable)
                return;

            var stashPanel = ingameState.IngameUi.StashElement;
            if (stashPanel.IsVisible)
            {
                var visibleStash = stashPanel.VisibleStash;
                if (visibleStash == null || visibleStash.InventoryUIElement == null)
                    return;

                //Determine Stash Pickup Button position and draw
                var stashRect = visibleStash.InventoryUIElement.GetClientRect();
                var pickButtonRect = new SharpDX.RectangleF(stashRect.BottomRight.X - 43, stashRect.BottomRight.Y + 10, 37, 37);

                Graphics.DrawImage("pick.png", pickButtonRect);

                var highlightedItems = GetHighlightedItems();

                int? stackSizes = 0;
                foreach (var item in highlightedItems)
                {
                    stackSizes += item.Item?.GetComponent<Stack>()?.Size;
                }

                string countText;
                if (Settings.ShowStackSizes && highlightedItems.Count != stackSizes && stackSizes != null)
                    if (Settings.ShowStackCountWithSize) countText = $"{stackSizes} / {highlightedItems.Count}";
                    else countText = $"{stackSizes}";
                else
                    countText = $"{highlightedItems.Count}";

                var countPos = new Vector2(pickButtonRect.Left - 2, pickButtonRect.Center.Y);
                countPos.Y -= 11;
                Graphics.DrawText($"{countText}", new Vector2(countPos.X, countPos.Y + 2), SharpDX.Color.Black, 10, "FrizQuadrataITC:22", FontAlign.Right);
                Graphics.DrawText($"{countText}", new Vector2(countPos.X - 2, countPos.Y), SharpDX.Color.White, 10, "FrizQuadrataITC:22", FontAlign.Right);

                if (isButtonPressed(pickButtonRect) || Keyboard.IsKeyPressed(Settings.HotKey.Value))
                {
                    var orderedItems = highlightedItems
                       .OrderBy(stashItem => stashItem.InventPosX)
                       .ThenBy(stashItem => stashItem.InventPosY)
                       .ThenBy(stashItem => stashItem.GetClientRectCache.X)
                       .ThenBy(stashItem => stashItem.GetClientRectCache.Y)
                       .ToList();
                    _currentOperation = MoveItemsToInventory(orderedItems).GetEnumerator();
                }
            }

            var inventoryPanel = ingameState.IngameUi.InventoryPanel;
            if (inventoryPanel.IsVisible && Settings.DumpButtonEnable)
            {
                //Determine Inventory Pickup Button position and draw
                var inventoryRect = inventoryPanel.Children[2].GetClientRect();
                var pickButtonRect =
                    new SharpDX.RectangleF(inventoryRect.TopLeft.X + 18, inventoryRect.TopLeft.Y - 37, 37, 37);

                Graphics.DrawImage("pickL.png", pickButtonRect);
                if (isButtonPressed(pickButtonRect))
                {
                    var inventoryItemsServer = GameController.IngameState.Data.ServerData.PlayerInventories[0].Inventory.
                        InventorySlotItems.OrderBy(x => x.PosX).ThenBy(x => x.PosY).ToList();

                    /*var inventoryItems = inventoryPanel[InventoryIndex.PlayerInventory].VisibleInventoryItems
                       .OrderBy(x => x.InventPosX)
                       .ThenBy(x => x.InventPosY)
                       .ToList();
                    */
                    _currentOperation = MoveItemsToStash(inventoryItemsServer).GetEnumerator();
                }
            }
        }

        private IEnumerable<bool> MoveItemsCommonPreamble(CancellationTokenSource cts)
        {
            while (Control.MouseButtons == MouseButtons.Left)
            {
                if (MoveCancellationRequested)
                {
                    cts.Cancel();
                    yield break;
                }

                yield return false;
            }

            if (Settings.DelaySettings.IdleMouseDelay.Value == 0)
            {
                yield break;
            }

            var mousePos = Mouse.GetCursorPosition();
            var sw = Stopwatch.StartNew();
            yield return false;
            while (true)
            {
                if (MoveCancellationRequested)
                {
                    cts.Cancel();
                    yield break;
                }

                var newPos = Mouse.GetCursorPosition();
                if (mousePos != newPos)
                {
                    mousePos = newPos;
                    sw.Restart();
                }
                else if (sw.ElapsedMilliseconds >= Settings.DelaySettings.IdleMouseDelay.Value)
                {
                    yield break;
                }
                else
                {
                    yield return false;
                }
            }
        }

        private IEnumerable<bool> MoveItemsToStash(IList<InventSlotItem> items)
        {
            var cts = new CancellationTokenSource();
            foreach (var _ in MoveItemsCommonPreamble(cts)) { yield return false; }

            if (cts.Token.IsCancellationRequested)
            {
                yield break;
            }
            var prevMousePos = Mouse.GetCursorPosition();
            foreach (var item in items)
            {
                if (MoveCancellationRequested)
                {
                    yield break;
                }

                if (!CheckIgnoreCells(item))
                {
                    if (!ingameState.IngameUi.InventoryPanel.IsVisible)
                    {
                        DebugWindow.LogMsg("HighlightedItems: Inventory Panel closed, aborting loop");
                        break;
                    }

                    /*
                            if (!ingameState.IngameUi.StashElement.IsVisible
                                && !ingameState.IngameUi.SellWindow.IsVisible
                                && !ingameState.IngameUi.TradeWindow.IsVisible)
                            {
                                DebugWindow.LogMsg("HighlightedItems: Stash Panel closed, aborting loop");
                                break;
                            }*/
                    foreach (var _ in MoveItem(item.GetClientRect().Center)) { yield return false; }
                }
            }
            Mouse.moveMouse(prevMousePos);
            foreach (var _ in Wait(MouseMoveDelay, true)) { yield return false; }
        }

        private IEnumerable<bool> MoveItemsToInventory(IList<NormalInventoryItem> items)
        {
            var cts = new CancellationTokenSource();
            foreach (var _ in MoveItemsCommonPreamble(cts)) { yield return false; }

            if (cts.Token.IsCancellationRequested)
            {
                yield break;
            }
            var prevMousePos = Mouse.GetCursorPosition();
            foreach (var item in items)
            {
                if (MoveCancellationRequested)
                {
                    yield break;
                }

                if (!ingameState.IngameUi.StashElement.IsVisible)
                {
                    DebugWindow.LogMsg("HighlightedItems: Stash Panel closed, aborting loop");
                    break;
                }

                if (!ingameState.IngameUi.InventoryPanel.IsVisible)
                {
                    DebugWindow.LogMsg("HighlightedItems: Inventory Panel closed, aborting loop");
                    break;
                }

                if (IsInventoryFull())
                {
                    DebugWindow.LogMsg("HighlightedItems: Inventory full, aborting loop");
                    break;
                }

                foreach (var _ in MoveItem(item.GetClientRect().Center)) { yield return false; }
            }
            Mouse.moveMouse(prevMousePos);
            foreach (var _ in Wait(MouseMoveDelay, true)) { yield return false; }
        }


        public IList<NormalInventoryItem> GetHighlightedItems()
        {
            try
            {
                var stashItems = ingameState.IngameUi.StashElement.VisibleStash.VisibleInventoryItems;

                var highlightedItems = stashItems
                   .Where(stashItem => stashItem.isHighlighted)
                   .ToList();

                return highlightedItems.ToList();
            }
            catch
            {
                return new List<NormalInventoryItem>();
            }
        }
        private bool IsInventoryFull()
        {
            var inventoryItems = ingameState.ServerData.PlayerInventories[0].Inventory.InventorySlotItems;

            // quick sanity check
            if (inventoryItems.Count < 12)
            {
                return false;
            }

            // track each inventory slot
            bool[,] inventorySlot = new bool[12, 5];

            // iterate through each item in the inventory and mark used slots
            foreach (var inventoryItem in inventoryItems)
            {
                int x = inventoryItem.PosX;
                int y = inventoryItem.PosY;
                int height = inventoryItem.SizeY;
                int width = inventoryItem.SizeX;
                for (int row = x; row < x + width; row++)
                {
                    for (int col = y; col < y + height; col++)
                    {
                        inventorySlot[row, col] = true;
                    }
                }
            }

            // check for any empty slots
            for (int x = 0; x < 12; x++)
            {
                for (int y = 0; y < 5; y++)
                {
                    if (inventorySlot[x, y] == false)
                    {
                        return false;
                    }
                }
            }
            // no empty slots, so inventory is full
            return true;
        }
        
        private TimeSpan KeyDelay => TimeSpan.FromMilliseconds(Settings.DelaySettings.KeyDelay.Value);
        private TimeSpan MouseMoveDelay => TimeSpan.FromMilliseconds(Settings.DelaySettings.MouseMoveDelay.Value);
        private TimeSpan MouseDownDelay => TimeSpan.FromMilliseconds(5 + Settings.DelaySettings.ExtraDelay.Value);
        private TimeSpan MouseUpDelay => TimeSpan.FromMilliseconds(5 + Settings.DelaySettings.ExtraDelay.Value);

        private IEnumerable<bool> MoveItem(SharpDX.Vector2 itemPosition)
        {
            itemPosition += windowOffset;
            Keyboard.KeyDown(Keys.LControlKey);
            foreach (var _ in Wait(KeyDelay, true)) { yield return false; }
            Mouse.moveMouse(itemPosition);
            foreach (var _ in Wait(MouseMoveDelay, true)) { yield return false; }
            Mouse.LeftDown();
            foreach (var _ in Wait(MouseDownDelay, true)) { yield return false; }
            Mouse.LeftUp();
            foreach (var _ in Wait(MouseUpDelay, true)) { yield return false; }
            Keyboard.KeyUp(Keys.LControlKey);
            foreach (var _ in Wait(KeyDelay, false)) { yield return false; }
        }

        private IEnumerable<bool> Wait(TimeSpan period, bool canUseThreadSleep)
        {
            if (canUseThreadSleep && Settings.UseThreadSleep)
            {
                Thread.Sleep(period);
                yield break;
            }

            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < period)
            {
                yield return false;
            }
        }

        public bool isButtonPressed(SharpDX.RectangleF buttonRect)
        {
            if (Control.MouseButtons == MouseButtons.Left)
            {
                if (buttonRect.Contains(Mouse.GetCursorPosition() - windowOffset))
                {
                    return true;
                }
            }

            return false;
        }


        private bool CheckIgnoreCells(InventSlotItem inventItem)
        {
            var inventPosX = inventItem.PosX;
            var inventPosY = inventItem.PosY;

            if (inventPosX < 0 || inventPosX >= 12)
                return true;
            if (inventPosY < 0 || inventPosY >= 5)
                return true;

            return Settings.IgnoredCells[inventPosY, inventPosX] != 0; //No need to check all item size
        }


        private void DrawIgnoredCellsSettings()
        {
            ImGui.BeginChild("##IgnoredCellsMain", new Vector2(ImGui.GetContentRegionAvail().X, 204f), true,
                (ImGuiWindowFlags) 16);
            ImGui.Text("Ignored Inventory Slots (checked = ignored)");

            Vector2 contentRegionAvail = ImGui.GetContentRegionAvail();
            ImGui.BeginChild("##IgnoredCellsCels", new Vector2(contentRegionAvail.X, contentRegionAvail.Y), true,
                (ImGuiWindowFlags) 16);

            int num = 1;
            for (int index1 = 0; index1 < 5; ++index1)
            {
                for (int index2 = 0; index2 < 12; ++index2)
                {
                    bool boolean = Convert.ToBoolean(Settings.IgnoredCells[index1, index2]);
                    if (ImGui.Checkbox(string.Format("##{0}IgnoredCells", (object) num), ref boolean))
                        Settings.IgnoredCells[index1, index2] ^= 1;
                    if ((num - 1) % 12 < 11)
                        ImGui.SameLine();
                    ++num;
                }
            }

            ImGui.EndChild();
            ImGui.EndChild();
        }
    }
}