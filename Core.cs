using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Craftie.Utilities;
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
        public Core()
        {
            PluginName = "Craftie";
        }

        public override void Render()
        {
            if (Settings.HotkeyEnabled.Value && !Keyboard.IsKeyToggled(Settings.Hotkey.Value))
            {
                return;
            }

            if (Settings.HotkeyEnabled.Value)
            {
                LogMessage($"Craftie: Hotkey currently toggled, press {Settings.Hotkey.Value.ToString()} to disable.", 5);
            }

            var stashPanel = GameController.Game.IngameState.ServerData.StashPanel;
            if (!stashPanel.IsVisible)
            {
                return;
            }

            var craftingItem = CraftingItemFromCurrencyStash();

            // If one of the tuple values is null, then they both are, but for completions sake we check both.
            if (craftingItem.RealStats == null || craftingItem.RealPosition == null)
            {
                return;
            }

            Craftable(craftingItem);

            #region debug

            /*Graphics.DrawFrame(craftingItem.RealPosition.GetClientRect(), 2, Color.Blue);

            var baseItemType = GameController.Files.BaseItemTypes.Translate(craftingItem.RealStats.Path);
            LogMessage($"BaseName: {baseItemType.BaseName}. \n" +
                       $"ClassName: {baseItemType.ClassName}. \n", 5);*/

            #endregion
        }

        /// <summary>
        /// Given the number of wanted sockets, links and/or socket colors, is it possible to craft them onto the item?
        /// If it is then do it.
        /// </summary>
        /// <param name="craftingItem"></param>
        /// <returns></returns>
        private bool Craftable((Entity realStats, Element realPosition) craftingItem)
        {
            var item = craftingItem.realStats;
            var rec = craftingItem.realPosition.GetClientRect();

            // if the item is corrupted we can't craft upon it.
            if (IsCorrupted(item))
            {
                return false;
            }

            // If we want to socket the item.
            if (Settings.SocketItem.Value)
            {
                var sockets = item.GetComponent<Sockets>();

                if (sockets.NumberOfSockets < Settings.SocketsWanted.Value)
                {
                    if (CanItemHaveNumberOfSockets(Settings.SocketsWanted.Value, item) &&
                        DoWeHaveCurrency("Jeweller's Orb"))
                    {
                        CraftIt("Jeweller's Orb", rec);
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            // If we want to link the item.
            if (Settings.LinkItem.Value)
            {
                var sockets = item.GetComponent<Sockets>();

                if (sockets.LargestLinkSize < Settings.LinksWanted.Value)
                {
                    if (Settings.LinksWanted.Value <= sockets.NumberOfSockets &&
                        DoWeHaveCurrency("Orb of Fusing"))
                    {
                        CraftIt("Orb of Fusing", rec);
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            // If we want to recolor the item using Chromatic Orbs
            if (Settings.ChromaticItem.Value)
            {
                // if requirements are not met
                LogMessage("Coloring is not supporoted.", Constants.WHILE_DELAY);
                return false;
            }


            return true;
        }

        /// <summary>
        /// Takes the desired crafting currency and applies it to the item being crafted upon.
        /// </summary>
        /// <param name="craftingCurrencyBaseName"></param>
        /// <param name="rec"></param>
        /// <returns>Returns false if there's unexpected behaviour, otherwise true.</returns>
        private bool CraftIt(string craftingCurrencyBaseName, RectangleF rec)
        {
            var craftinCurrency = GetCraftingCurrency(craftingCurrencyBaseName);
            var windowOffset = GameController.Window.GetWindowRectangle().TopLeft;
            var latency = GameController.Game.IngameState.CurLatency;
            if (craftinCurrency == null)
            {
                return false;
            }

            var currencyPos = RandomizedCenterPoint(craftinCurrency.GetClientRect());

            Mouse.SetCursorPosAndRightClick(currencyPos, windowOffset,
                Settings.ExtraDelay.Value);
            Thread.Sleep(Constants.INPUT_DELAY);

            var pos = RandomizedCenterPoint(rec);

            Mouse.SetCursorPosAndLeftClick(pos, windowOffset, Settings.ExtraDelay.Value);
            Thread.Sleep((int) latency * 3);
            return true;
        }

        /// <summary>
        /// Do we have the requested currency in our currency stash tab?
        /// </summary>
        /// <param name="baseName"></param>
        /// <returns></returns>
        private bool DoWeHaveCurrency(string baseName)
        {
            var inventoryItems = GetVisibleInventoryItems();
            if (inventoryItems == null || inventoryItems.Count == 0)
            {
                return false;
            }

            return inventoryItems.Any(normalInventoryItem => GameController.Files.BaseItemTypes
                .Translate(normalInventoryItem.Item.Path).BaseName.Equals(baseName));
        }

        /// <summary>
        /// We are using a Tuple, since the craftingItem it self is falsely positioned, the real position of the craftingItem is it's parent.
        /// As shown here: https://i.imgur.com/HZO1Cre.png, where the blue triangle is the .RealPosition.GetClientRec(), and the red is the CraftingItem.
        /// </summary>
        /// <returns>Tuple with CraftingItem and it's RealPosition, the RealStats entity should be used for checking Mods, and the RealPosition for Mouse movement.</returns>
        private (Entity RealStats, Element RealPosition) CraftingItemFromCurrencyStash()
        {
            try
            {
                var parent = GameController.Game.IngameState.IngameUi.OpenLeftPanel
                    .Children[2]
                    .Children[0]
                    .Children[1]
                    .Children[1]
                    .Children[Settings.CurrencyTab.Value]
                    .Children[0]
                    .Children[28];

                var item = parent.Children[0].AsObject<NormalInventoryItem>().Item;

                return (item, parent);
            }
            catch
            {
                return (null, null);
            }
        }


        /// <summary>
        /// Gets visible inventory items.
        /// </summary>
        /// <returns>a list contains the inventory items or null if we can't get it.</returns>
        private List<NormalInventoryItem> GetVisibleInventoryItems()
        {
            try
            {
                return GameController.Game.IngameState.ServerData.StashPanel.VisibleStash.VisibleInventoryItems;
            }
            catch
            {
                return null;
            }
        }


        private bool IsCorrupted(IEntity item)
        {
            return item.GetComponent<Base>().isCorrupted;
        }

        /// <summary>
        /// Finds out if an item can have the wanted amount of sockets.
        /// </summary>
        /// <param name="amount">the desired amount of sockets</param>
        /// <param name="item">the item to check</param>
        /// <returns>true if it's possible, otherwise false.</returns>
        private bool CanItemHaveNumberOfSockets(int amount, IEntity item)
        {
            var mods = item.GetComponent<Mods>();
            var iLvl = mods.ItemLevel;
            var className = GameController.Files.BaseItemTypes.Translate(item.Path).ClassName;
            // The first limiting factor is item level.

            var numberOfSocketsItemLevel = new Dictionary<int, int>
            {
                {1, 1},
                {2, 1},
                {3, 2},
                {4, 25},
                {5, 35},
                {6, 50}
            };

            if (iLvl < numberOfSocketsItemLevel[amount])
            {
                LogMessage(
                    $"Your item needs to be item level {numberOfSocketsItemLevel[amount]} or above for it to have {amount} of sockets.",
                    Constants.WHILE_DELAY);
                return false;
            }
            // Then the item type (ring, 2h, 1h, boots, ...)
            if (className.Contains("One Hand") || className.Equals("Shield") || className.Equals("Dagger") ||
                className.Equals("Wand"))
            {
                if (amount > 3)
                {
                    LogMessage("You can only have 3 sockets, on this type of item.", Constants.WHILE_DELAY);
                }
                return amount <= 3;
            }

            if (className.Equals("Boots") || className.Equals("Gloves") || className.Equals("Helmet"))
            {
                if (amount > 4)
                {
                    LogMessage("You can only have 4 sockets, on this type of item.", Constants.WHILE_DELAY);
                }
                return amount <= 4;
            }

            if (className.Contains("Two Hand") || className.Equals("Bow"))
            {
                if (amount > 6)
                {
                    LogMessage("You can only have 6 sockets, on this type of item.", Constants.WHILE_DELAY);
                }
                return amount <= 6;
            }

            // The remainder can't be socketed.=
            LogMessage($"{className} can't have sockets!", 5);
            return false;
        }

        private NormalInventoryItem GetCraftingCurrency(string baseName)
        {
            try
            {
                return GameController.Game.IngameState.ServerData.StashPanel.VisibleStash.VisibleInventoryItems.First(
                    normalInventoryItem => GameController.Files.BaseItemTypes.Translate(normalInventoryItem.Item.Path)
                        .BaseName.Equals(baseName));
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Takes a rectangle, and randomizes the offsets from center in both planes (X,Y), then returns that point as a Vector2.
        /// </summary>
        /// <param name="rec">the rectangle you want a randomized center point from.</param>
        /// <returns>Randomized center point of the given rectangle</returns>
        private Vector2 RandomizedCenterPoint(RectangleF rec)
        {
            var randomized = rec.Center;
            var xOffsetMin = (int) (-1 * rec.Width / 2) + 2;
            var xOffsetMax = (int) (rec.Width / 2) - 2;
            var yOffsetMin = (int)(-1 * rec.Height / 2) + 2;
            var yOffsetMax = (int)(rec.Height / 2) - 2;
            var random = new Random();

            randomized.X += random.Next(xOffsetMin, xOffsetMax);
            randomized.Y += random.Next(yOffsetMin, yOffsetMax);

            return randomized;
        }
    }
}