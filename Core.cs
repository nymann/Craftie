using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Craftie.Utilities;
using PoeHUD.Models.Enums;
using PoeHUD.Models.Interfaces;
using PoeHUD.Plugins;
using PoeHUD.Poe;
using PoeHUD.Poe.Components;
using PoeHUD.Poe.Elements;
using PoeHUD.Poe.EntityComponents;
using SharpDX;

namespace Craftie
{
    public class Core : BaseSettingsPlugin<Settings>
    {
        private Thread _crafingThread;
        private bool _succes;
        private int _animation;
        private bool _crafting;

        private int _socketCraftCounter = 0;
        private int _chromaticCraftCounter = 0;
        private int _linkCraftCounter = 0;
        private int _alterationCounter = 0;

        public Core()
        {
            PluginName = "Craftie";
        }

        public override void Initialise()
        {
            SetupOrClosePlugin();
            Settings.Enable.OnValueChanged += SetupOrClosePlugin;
        }

        public override void OnClose()
        {
            CloseThreads();
        }

        public override void Render()
        {
            var path = $"{PluginDirectory}\\images\\";
            var windowSize = GameController.Window.GetWindowRectangle();
            var pos = new RectangleF(windowSize.Width / 2 - 100, windowSize.Height - 200, 200, 200);

            if (!_crafting)
            {
                return;
            }

            if (!_succes)
            {
                path += "FeelsBadMan.png";
            }
            else
            {
                pos.X += _animation;
                path += "FeelsGoodMan.png";
                _animation++;

                if (_animation > 400)
                {
                    _animation = 0;
                }
            }
            Graphics.DrawPluginImage(path, pos);
        }

        private void CloseThreads()
        {
            if (_crafingThread != null && _crafingThread.IsAlive)
            {
                _crafingThread.IsBackground = true;
            }
        }

        private void SetupOrClosePlugin()
        {
            if (!Settings.Enable.Value)
            {
                _animation = 0;
                _succes = false;
                _crafting = false;

                CloseThreads();
                return;
            }

            Settings.CurrencyTab.Max = (int) GameController.Game.IngameState.ServerData.StashPanel.TotalStashes - 1;
            _crafingThread = new Thread(CraftingThread);
            _crafingThread.Start();
        }

        private void CraftingThread()
        {
            while (!_crafingThread.IsBackground)
            {
                if (Settings.HotkeyEnabled.Value)
                {
                    if (Keyboard.IsKeyToggled(Settings.Hotkey.Value) &&
                        Keyboard.IsKeyPressed(Settings.Hotkey.Value))
                    {
                        _crafting = true;
                    }
                    else if (!Keyboard.IsKeyToggled(Settings.Hotkey.Value) &&
                             Keyboard.IsKeyPressed(Settings.Hotkey.Value))
                    {
                        _crafting = false;
                    }

                    if (!_crafting)
                    {
                        Thread.Sleep(Constants.WHILE_DELAY);
                        continue;
                    }
                }
                var item = GetCraftingItem();
                if (item == null)
                {
                    Thread.Sleep(Constants.WHILE_DELAY);
                    continue;
                }

                var parent = GetCraftingParent();

                if (!IsItemCraftable(item))
                {
                    LogMessage("Item is not craftable.", 1);
                    Thread.Sleep(1000);
                    continue;
                }

                if (Settings.SocketItem.Value)
                {
                    SocketItem(item, parent);
                }

                if (Settings.LinkItem.Value && (DoesItemHaveTheWantedAmountOfSockets() || !Settings.SocketItem.Value))
                {
                    LinkItem(item, parent);
                }

                if (Settings.ChromaticItem.Value && (DoesItemHaveTheWantedAmountOfLinks() || !Settings.LinkItem.Value))
                {
                    ChromaticCraft(item, parent);
                }
            }

            _crafingThread.Interrupt();
        }

        private void SocketItem(IEntity item, Element parent)
        {
            while (item.GetComponent<Sockets>().NumberOfSockets < Settings.SocketsWanted.Value ||
                   !Keyboard.IsKeyDown(Settings.Hotkey.Value))
            {
                Thread.Sleep(Constants.WHILE_DELAY);
            }
        }


        private void LinkItem(IEntity item, Element parent)
        {
            var sockets = item.GetComponent<Sockets>();
            const string craftingMaterialName = "Jeweller's Orb";
            var windowOffset = GameController.Window.GetWindowRectangle().TopLeft;

            // Check to see if the item we want to link doesn't have enough sockets.
            if (sockets.NumberOfSockets < Settings.LinksWanted.Value)
            {
                LogMessage("The number of links wanted, is less than the sockets the item has!\n", 5);
                return;
            }

            // Transfer crafting material to player inventory, if we don't have any in the inventory already.
            var doWeHaveCraftingMaterial = CraftingMaterials(craftingMaterialName);
            if (!doWeHaveCraftingMaterial)
            {
                LogMessage("We don't have materials for this!", 1);
                return;
            }
            // Right-click on the crafting material, and press shift.
            var craftingMaterialElement = GameController.Game.IngameState.IngameUi
                .InventoryPanel[InventoryIndex.PlayerInventory].VisibleInventoryItems
                .First(inventoryItem => craftingMaterialName.Equals(GameController.Files.BaseItemTypes.Translate(inventoryItem.Item.Path)
                    .BaseName));
            if (craftingMaterialElement == null)
            {
                LogMessage("Didn't work out fam.", 1);
                return;
            }

            Mouse.SetCursorPosAndRightClick(craftingMaterialElement.GetClientRect().Center, windowOffset, Settings.ExtraDelay.Value);

            Keyboard.KeyDown(Keys.ShiftKey);
            while (item.GetComponent<Sockets>().LargestLinkSize < Settings.LinksWanted.Value ||
                   !Keyboard.IsKeyDown(Settings.Hotkey.Value))
            {
                // Check if we have crafting materials enough
                var materialsEnough = CraftingMaterials(craftingMaterialName);

                // If we don't, then return a message.
                if (!materialsEnough)
                {
                    LogMessage("We don't have materials enough for this.", 5);
                    break;
                }

                // If we do, begin crafting.
                // TODO: CRAFTING.
                Thread.Sleep(Constants.WHILE_DELAY);
            }
            Keyboard.KeyUp(Keys.ShiftKey);
        }

        /// <summary>
        /// If there are any of said crafting material in the playerinventory, return true.
        /// If there isn't check the stash tab.
        /// If stash tab does contain crafting material, move it to player inventory and return true.
        /// Else return false.
        /// </summary>
        /// <param name="craftingMaterialName">The BaseName of the crafting material</param>
        /// <returns></returns>
        private bool CraftingMaterials(string craftingMaterialName)
        {
            // Check Inventory
            var visibleInventoryItems =
                GameController.Game.IngameState.IngameUi.InventoryPanel[InventoryIndex.PlayerInventory]
                    .VisibleInventoryItems;

            var inventoryPanelContainsItem = visibleInventoryItems.Any(item => GameController.Files.BaseItemTypes
                .Translate(item.Item.Path).BaseName.Equals(craftingMaterialName));

            if (inventoryPanelContainsItem)
            {
                return true;
            }

            // Check StashPanel
            var stashTabItems = GameController.Game.IngameState.ServerData.StashPanel.VisibleStash
                .VisibleInventoryItems;

            var stashTabContainsItem = stashTabItems.Any(item => GameController.Files.BaseItemTypes
                .Translate(item.Item.Path).BaseName.Equals(craftingMaterialName));

            if (!stashTabContainsItem)
            {
                LogMessage($"Can't find {craftingMaterialName}.", 1);
                return false;
            }
            LogMessage("StashTab contains item.", 1);
            var craftingMaterial = stashTabItems.First(item => GameController.Files.BaseItemTypes
                .Translate(item.Item.Path).BaseName.Equals(craftingMaterialName));

            // Move them to inventory.
            MoveItemFromStashTabToInventoryPanel(craftingMaterial);
            return true;
        }

        private void MoveItemFromStashTabToInventoryPanel(Element item)
        {
            var windowOffset = GameController.Window.GetWindowRectangle().TopLeft;

            Keyboard.KeyDown(Keys.ShiftKey);
            Mouse.SetCursorPosAndLeftClick(item.GetClientRect().Center, windowOffset, Settings.ExtraDelay.Value);
            Keyboard.KeyUp(Keys.ShiftKey);
        }

        private void ChromaticCraft(IEntity item, Element parent)
        {
            var latency = (int) GameController.Game.IngameState.CurLatency;
            var sumOfWantedColors = Settings.BlueSocketsWanted.Value +
                                    Settings.GreenSocketsWanted.Value +
                                    Settings.RedSocketsWanted.Value;

            var largestLinkSize = item.GetComponent<Sockets>().LargestLinkSize;

            if (sumOfWantedColors > largestLinkSize)
            {
                LogMessage($"Item only has {largestLinkSize} links, you want {sumOfWantedColors} linked colors.", 5);
                Thread.Sleep(latency);
                return;
            }

            if (DoesItemHaveTheRWantedColors(item))
            {
                Thread.Sleep(latency);
            }
        }


        private bool DoesItemHaveTheWantedAmountOfSockets()
        {
            try
            {
                var item = GetCraftingItem();

                var sockets = item.GetComponent<Sockets>();

                return sockets.NumberOfSockets >= Settings.SocketsWanted.Value;
            }
            catch
            {
                return false;
            }
        }

        private bool DoesItemHaveTheWantedAmountOfLinks()
        {
            try
            {
                var item = GetCraftingItem();

                var sockets = item.GetComponent<Sockets>();

                return sockets.LargestLinkSize >= Settings.LinksWanted.Value;
            }
            catch
            {
                return false;
            }
        }

        private bool DoesItemHaveTheRWantedColors(IEntity item)
        {
            var links = item.GetComponent<Sockets>().Links;
            return links.Any(current =>
            {
                if (current.Length < Settings.LinksWanted.Value)
                {
                    return false;
                }
                // 1 = Red
                var redCounter = 0;

                // 2 = Green
                var greenCounter = 0;

                // 3 = Blue
                var blueCounter = 0;

                foreach (var socketColor in current.ToList())
                {
                    switch (socketColor)
                    {
                        case 1:
                            redCounter++;
                            break;

                        case 2:
                            greenCounter++;
                            break;

                        case 3:
                            blueCounter++;
                            break;

                        default:
                            LogError($"ERROR IN: DoesItemHaveTheRWantedColors, error value: {socketColor}", 5);
                            break;
                    }
                }

                return redCounter >= Settings.RedSocketsWanted.Value &&
                       greenCounter >= Settings.GreenSocketsWanted.Value &&
                       blueCounter >= Settings.BlueSocketsWanted.Value;
            });
        }


        private static bool IsItemCraftable(IEntity item)
        {
            if (item == null)
            {
                LogMessage("Item is null.", 1);
                return false;
            }

            if (!item.GetComponent<Mods>().Identified)
            {
                LogMessage("Item is not identified.", 1);
                return false;
            }

            if (item.GetComponent<Sockets>().NumberOfSockets == 0)
            {
                LogMessage("No sockets!", 1);
                return false;
            }

            if (item.GetComponent<Base>().isCorrupted)
            {
                LogMessage("Corrupted!", 1);
                return false;
            }

            return true;
        }

        private static bool CanItemGetNumberOfSockets(int iLvl, int numberOfSocketsWanted)
        {
            switch (numberOfSocketsWanted)
            {
                case 1:
                    if (iLvl < 2)
                    {
                        LogMessage($"Item level is {iLvl}, it can't get {numberOfSocketsWanted} sockets.", 1);
                        return false;
                    }
                    break;

                case 2:
                    if (iLvl < 1)
                    {
                        LogMessage($"Item level is {iLvl}, it can't get {numberOfSocketsWanted} sockets.", 1);
                        return false;
                    }
                    break;

                case 3:
                    if (iLvl < 2)
                    {
                        LogMessage($"Item level is {iLvl}, it can't get {numberOfSocketsWanted} sockets.", 1);
                        return false;
                    }
                    break;

                case 4:
                    if (iLvl < 25)
                    {
                        LogMessage($"Item level is {iLvl}, it can't get {numberOfSocketsWanted} sockets.", 1);
                        return false;
                    }
                    break;

                case 5:
                    if (iLvl < 35)
                    {
                        LogMessage($"Item level is {iLvl}, it can't get {numberOfSocketsWanted} sockets.", 1);
                        return false;
                    }
                    break;

                case 6:
                    if (iLvl < 50)
                    {
                        LogMessage($"Item level is {iLvl}, it can't get {numberOfSocketsWanted} sockets.", 1);
                        return false;
                    }
                    break;

                default:
                    LogMessage("Number of sockets or links can only be between 1 and 6!\n" +
                               $"You wanted {numberOfSocketsWanted}!", 1);
                    return false;
            }
            return true;
        }


        private Entity GetCraftingItem()
        {
            try
            {
                var stashPanel = GameController.Game.IngameState.ServerData.StashPanel;
                if (!stashPanel.IsVisible)
                {
                    return null;
                }

                var item = GameController.Game.IngameState.IngameUi.OpenLeftPanel
                    .Children[2]
                    .Children[0]
                    .Children[1]
                    .Children[1]
                    .Children[Settings.CurrencyTab.Value]
                    .Children[0]
                    .Children[28]
                    .Children[0].AsObject<NormalInventoryItem>().Item;

                return item;
            }
            catch
            {
                return null;
            }
        }

        private Element GetCraftingParent()
        {
            try
            {
                var stashPanel = GameController.Game.IngameState.ServerData.StashPanel;
                if (!stashPanel.IsVisible)
                {
                    return null;
                }

                var parent = GameController.Game.IngameState.IngameUi.OpenLeftPanel
                    .Children[2]
                    .Children[0]
                    .Children[1]
                    .Children[1]
                    .Children[Settings.CurrencyTab.Value]
                    .Children[0]
                    .Children[28];

                return parent;
            }
            catch
            {
                return null;
            }
        }
    }
}