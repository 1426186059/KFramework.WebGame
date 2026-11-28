using Client.MirControls;
using Client.MirGraphics;
using Client.MirNetwork;
using Client.MirObjects;
using Client.MirScenes.Dialogs;
using Client.MirSounds;
using SlimDX;
using WebGame.Mir2.MonoGame.Client;
using C = ClientPackets;
using Effect = Client.MirObjects.Effect;
using S = ServerPackets;

namespace Client.MirScenes
{
    public sealed partial class GameScene : MirScene
    {
        public MirControl NameInfoLabel(UserItem item, bool inspect = false, bool hideDura = false)
        {
            ushort level = inspect ? InspectDialog.Level : MapObject.User.Level;
            MirClass job = inspect ? InspectDialog.Class : MapObject.User.Class;
            HoverItem = item;
            ItemInfoTable realItem = Functions.GetRealItem(item.Info, level, job, ItemInfoList);

            string GradeString = "";
            switch (HoverItem.Info.Grade)
            {
                case ItemGrade.None:
                    break;
                case ItemGrade.Common:
                    GradeString = LanguageMgr.Get("ItemGradeCommon");
                    break;
                case ItemGrade.Rare:
                    GradeString = LanguageMgr.Get("ItemGradeRare");
                    break;
                case ItemGrade.Legendary:
                    GradeString = LanguageMgr.Get("ItemGradeLegendary");
                    break;
                case ItemGrade.Mythical:
                    GradeString = LanguageMgr.Get("ItemGradeMythical");
                    break;
                case ItemGrade.Heroic:
                    GradeString = LanguageMgr.Get("ItemGradeHeroic");
                    break;
            }
            MirLabel nameLabel = new MirLabel
            {
                AutoSize = true,
                ForeColour = GradeNameColor(HoverItem.Info.Grade),
                Location = new Point(4, 4),
                OutLine = true,
                Parent = ItemLabel,
                Text = HoverItem.Info.Grade != ItemGrade.None ? string.Format("{0}{1}{2}", HoverItem.Info.FriendlyName, "\n", GradeString) : HoverItem.Info.FriendlyName,
            };

            if (HoverItem.RefineAdded > 0)
                nameLabel.Text = "(*)" + nameLabel.Text;

            ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, nameLabel.DisplayRectangle.Right + 4),
                Math.Max(ItemLabel.Size.Height, nameLabel.DisplayRectangle.Bottom));

            string text = "";

            if (HoverItem.Info.Durability > 0 && !hideDura)
            {
                switch (HoverItem.Info.Type)
                {
                    case ItemType.Amulet:
                        if (HoverItem.CurrentDura > 0 || HoverItem.MaxDura > 0)
                            text = LanguageMgr.Get("UsageCurrentMax", HoverItem.CurrentDura, HoverItem.MaxDura);
                        break;
                    case ItemType.Ore:
                        if (HoverItem.CurrentDura > 0)
                            text = LanguageMgr.Get("PurityValue", Math.Floor(HoverItem.CurrentDura / 1000M));
                        break;
                    case ItemType.Meat:
                        if (HoverItem.CurrentDura > 0)
                            text = LanguageMgr.Get("QualityValue", Math.Floor(HoverItem.CurrentDura / 1000M));
                        break;
                    case ItemType.Mount:
                        if (HoverItem.CurrentDura > 0 || HoverItem.MaxDura > 0)
                            text = LanguageMgr.Get("LoyaltyValues", HoverItem.CurrentDura, HoverItem.MaxDura);
                        break;
                    case ItemType.Food:
                        if (HoverItem.CurrentDura > 0)
                            text = LanguageMgr.Get("NutritionValue", HoverItem.CurrentDura);
                        break;
                    case ItemType.Gem:
                    case ItemType.Potion:
                    case ItemType.Transform:
                    case ItemType.SealedHero:
                        break;
                    case ItemType.Pets:
                        if ((HoverItem.Info.Shape == 26 || HoverItem.Info.Shape == 28) && HoverItem.CurrentDura > 0)//WonderDrug, Knapsack
                        {
                            string strTime = Functions.PrintTimeSpanFromSeconds((HoverItem.CurrentDura * 3600), false);
                            text = LanguageMgr.Get("DurationValue", strTime);
                        }
                        break;
                    default:
                        int current = (int)Math.Floor(HoverItem.CurrentDura / 1000M);
                        int maximum = (int)Math.Floor(HoverItem.MaxDura / 1000M);
                        if (current > 0 || maximum > 0)
                            text = string.Format("{0} {1}/{2}", LanguageMgr.Get("Durability"), current, maximum);
                        break;
                }
            }

            string baseText = string.Empty;
            switch (HoverItem.Info.Type)
            {
                case ItemType.Nothing:
                    break;
                case ItemType.Weapon:
                    baseText = LanguageMgr.Get("ItemTypeWeapon");
                    break;
                case ItemType.Armour:
                    baseText = LanguageMgr.Get("ItemTypeArmour");
                    break;
                case ItemType.Helmet:
                    baseText = LanguageMgr.Get("ItemTypeHelmet");
                    break;
                case ItemType.Necklace:
                    baseText = LanguageMgr.Get("ItemTypeNecklace");
                    break;
                case ItemType.Bracelet:
                    baseText = LanguageMgr.Get("ItemTypeBracelet");
                    break;
                case ItemType.Ring:
                    baseText = LanguageMgr.Get("ItemTypeRing");
                    break;
                case ItemType.Amulet:
                    baseText = LanguageMgr.Get("ItemTypeAmulet");
                    break;
                case ItemType.Belt:
                    baseText = LanguageMgr.Get("ItemTypeBelt");
                    break;
                case ItemType.Boots:
                    baseText = LanguageMgr.Get("ItemTypeBoots");
                    break;
                case ItemType.Stone:
                    baseText = LanguageMgr.Get("ItemTypeStone");
                    break;
                case ItemType.Torch:
                    baseText = LanguageMgr.Get("ItemTypeTorch");
                    break;
                case ItemType.Potion:
                    baseText = LanguageMgr.Get("ItemTypePotion");
                    break;
                case ItemType.Ore:
                    baseText = LanguageMgr.Get("ItemTypeOre");
                    break;
                case ItemType.Meat:
                    baseText = LanguageMgr.Get("ItemTypeMeat");
                    break;
                case ItemType.CraftingMaterial:
                    baseText = LanguageMgr.Get("ItemTypeCraftingMaterial");
                    break;
                case ItemType.Scroll:
                    baseText = LanguageMgr.Get("ItemTypeScroll");
                    break;
                case ItemType.Gem:
                    baseText = LanguageMgr.Get("ItemTypeGem");
                    break;
                case ItemType.Mount:
                    baseText = LanguageMgr.Get("ItemTypeMount");
                    break;
                case ItemType.Book:
                    baseText = LanguageMgr.Get("ItemTypeBook");
                    break;
                case ItemType.Script:
                    baseText = LanguageMgr.Get("ItemTypeScript");
                    break;
                case ItemType.Reins:
                    baseText = LanguageMgr.Get("ItemTypeReins");
                    break;
                case ItemType.Bells:
                    baseText = LanguageMgr.Get("ItemTypeBells");
                    break;
                case ItemType.Saddle:
                    baseText = LanguageMgr.Get("ItemTypeSaddle");
                    break;
                case ItemType.Ribbon:
                    baseText = LanguageMgr.Get("ItemTypeRibbon");
                    break;
                case ItemType.Mask:
                    baseText = LanguageMgr.Get("ItemTypeMask");
                    break;
                case ItemType.Food:
                    baseText = LanguageMgr.Get("ItemTypeFood");
                    break;
                case ItemType.Hook:
                    baseText = LanguageMgr.Get("ItemTypeHook");
                    break;
                case ItemType.Float:
                    baseText = LanguageMgr.Get("ItemTypeFloat");
                    break;
                case ItemType.Bait:
                    baseText = LanguageMgr.Get("ItemTypeBait");
                    break;
                case ItemType.Finder:
                    baseText = LanguageMgr.Get("ItemTypeFinder");
                    break;
                case ItemType.Reel:
                    baseText = LanguageMgr.Get("ItemTypeReel");
                    break;
                case ItemType.Fish:
                    baseText = LanguageMgr.Get("ItemTypeFish");
                    break;
                case ItemType.Quest:
                    baseText = LanguageMgr.Get("ItemTypeQuest");
                    break;
                case ItemType.Awakening:
                    baseText = LanguageMgr.Get("ItemTypeAwakening");
                    break;
                case ItemType.Pets:
                    baseText = LanguageMgr.Get("ItemTypePets");
                    break;
                case ItemType.Transform:
                    baseText = LanguageMgr.Get("ItemTypeTransform");
                    break;
                case ItemType.Deco:
                    baseText = LanguageMgr.Get("ItemTypeDeco");
                    break;
                case ItemType.MonsterSpawn:
                    baseText = LanguageMgr.Get("ItemTypeMonsterSpawn");
                    break;
                case ItemType.SealedHero:
                    baseText = LanguageMgr.Get("ItemTypeSealedHero");
                    break;
            }

            if (HoverItem.WeddingRing != -1)
            {
                baseText += LanguageMgr.Get("WeddingRing");
            }

            List<string> tailParts = new List<string>();
            if (HoverItem.Weight > 0)
                tailParts.Add(string.Format("{0} {1}", LanguageMgr.Get("Weight"), HoverItem.Weight));
            if (!string.IsNullOrEmpty(text))
                tailParts.Add(text);

            if (tailParts.Count > 0)
            {
                string tailText = string.Join("  ", tailParts.ToArray());
                baseText = string.IsNullOrEmpty(baseText)
                    ? tailText
                    : string.Format("{0}\n{1}", baseText, tailText);
            }

            MirLabel etcLabel = new MirLabel
            {
                AutoSize = true,
                ForeColour = CommonColor.White,
                Location = new Point(4, nameLabel.DisplayRectangle.Bottom),
                OutLine = true,
                Parent = ItemLabel,
                Text = baseText
            };

            ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, etcLabel.DisplayRectangle.Right + 4),
                Math.Max(ItemLabel.Size.Height, etcLabel.DisplayRectangle.Bottom + 4));

            MirControl outLine = new MirControl
            {
                BackColour = CommonColor.FromArgb(255, 50, 50, 50),
                Border = true,
                BorderColour = CommonColor.Gray,
                NotControl = true,
                Parent = ItemLabel,
                Opacity = 0.4F,
                Location = new Point(0, 0)
            };
            outLine.Size = ItemLabel.Size;

            return outLine;
        }

        private Stats GetTotalAddedStats(UserItem item, ushort level, MirClass job)
        {
            Stats total = new Stats();

            if (item == null) return total;

            total.Add(item.AddedStats);

            if (item.Slots == null || item.Slots.Length == 0) return total;

            for (int i = 0; i < item.Slots.Length; i++)
            {
                UserItem socketItem = item.Slots[i];
                if (socketItem == null) continue;

                ItemInfoTable socketInfo = Functions.GetRealItem(socketItem.Info, level, job, ItemInfoList);

                if (socketItem.CurrentDura == 0 && socketInfo.Durability > 0) continue;

                total.Add(socketInfo.Stats);
                total.Add(socketItem.AddedStats);
            }

            return total;
        }
        public MirControl AttackInfoLabel(UserItem item, bool Inspect = false, bool hideAdded = false)
        {
            ushort level = Inspect ? InspectDialog.Level : MapObject.User.Level;
            MirClass job = Inspect ? InspectDialog.Class : MapObject.User.Class;
            HoverItem = item;
            ItemInfoTable realItem = Functions.GetRealItem(item.Info, level, job, ItemInfoList);
            Stats addedStats = GetTotalAddedStats(item, level, job);

            ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height + 4);

            bool fishingItem = false;

            switch (HoverItem.Info.Type)
            {
                case ItemType.Hook:
                case ItemType.Float:
                case ItemType.Bait:
                case ItemType.Finder:
                case ItemType.Reel:
                    fishingItem = true;
                    break;
                case ItemType.Weapon:
                    if (Globals.FishingRodShapes.Contains(HoverItem.Info.Shape))
                        fishingItem = true;
                    break;
                default:
                    fishingItem = false;
                    break;
            }

            int count = 0;
            int minValue = 0;
            int maxValue = 0;
            int addValue = 0;
            string text = "";

            minValue = realItem.Durability;

            if (minValue > 0 && realItem.Type == ItemType.Gem)
            {
                switch (realItem.Shape)
                {
                    default:
                        text = LanguageMgr.Get("AddsDurability", minValue / 1000);
                        break;
                    case 8:
                        text = LanguageMgr.Get("SealsFor", Functions.PrintTimeSpanFromSeconds(minValue * 60));
                        break;
                }

                count++;
                MirLabel DuraLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = CommonColor.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = text
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, DuraLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, DuraLabel.DisplayRectangle.Bottom));
            }


            minValue = realItem.Stats[Stat.MinDC];
            maxValue = realItem.Stats[Stat.MaxDC];
            addValue = (!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) ? addedStats[Stat.MaxDC] : 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                if (HoverItem.Info.Type != ItemType.Gem)
                    text = LanguageMgr.Get("DC", minValue, maxValue + addValue) + (addValue > 0 ? $" (+{addValue})" : string.Empty);
                else
                    text = LanguageMgr.Get("AddsDC", minValue + maxValue + addValue);
                MirLabel DCLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? CommonColor.Cyan : CommonColor.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = text
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, DCLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, DCLabel.DisplayRectangle.Bottom));
            }



            minValue = realItem.Stats[Stat.MinMC];
            maxValue = realItem.Stats[Stat.MaxMC];
            addValue = (!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) ? addedStats[Stat.MaxMC] : 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                if (HoverItem.Info.Type != ItemType.Gem)
                    text = LanguageMgr.Get("MC", minValue, maxValue + addValue) + (addValue > 0 ? $" (+{addValue})" : string.Empty);
                else
                    text = LanguageMgr.Get("AddsMC", minValue + maxValue + addValue);
                MirLabel MCLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? CommonColor.Cyan : CommonColor.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = text
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, MCLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, MCLabel.DisplayRectangle.Bottom));
            }



            minValue = realItem.Stats[Stat.MinSC];
            maxValue = realItem.Stats[Stat.MaxSC];
            addValue = (!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) ? addedStats[Stat.MaxSC] : 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                if (HoverItem.Info.Type != ItemType.Gem)
                    text = LanguageMgr.Get("SC", minValue, maxValue + addValue) + (addValue > 0 ? $" (+{addValue})" : string.Empty);
                else
                    text = LanguageMgr.Get("AddsSC", minValue + maxValue + addValue);
                MirLabel SCLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? CommonColor.Cyan : CommonColor.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    //Text = string.Format("SC + {0}~{1}", minValue, maxValue + addValue)
                    Text = text
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, SCLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, SCLabel.DisplayRectangle.Bottom));
            }



            minValue = realItem.Stats[Stat.Luck];
            maxValue = 0;
            addValue = (!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) ? addedStats[Stat.Luck] : 0;

            if (minValue != 0 || addValue != 0)
            {
                count++;

                if (realItem.Type == ItemType.Pets && realItem.Shape == 28)
                {
                    text = LanguageMgr.Get("BagWeightPercent", minValue + addValue);
                }
                else if (realItem.Type == ItemType.Potion && realItem.Shape == 4)
                {
                    text = LanguageMgr.Get("ExpPercent", minValue + addValue);
                }
                else if (realItem.Type == ItemType.Potion && realItem.Shape == 5)
                {
                    text = LanguageMgr.Get("DropPercent", minValue + addValue);
                }
                else
                {
                    text = string.Format(minValue + addValue > 0 ? LanguageMgr.Get("Luck"): LanguageMgr.Get("CursePlusValue"),
                        Math.Abs(minValue + addValue));
                }

                MirLabel LUCKLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? CommonColor.Cyan : CommonColor.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = text
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, LUCKLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, LUCKLabel.DisplayRectangle.Bottom));
            }





            minValue = realItem.Stats[Stat.Accuracy];
            maxValue = 0;
            addValue = (!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) ? addedStats[Stat.Accuracy] : 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                if (HoverItem.Info.Type != ItemType.Gem)
                    text = LanguageMgr.Get("Accuracy", minValue + addValue) + (addValue > 0 ? $" (+{addValue})" : string.Empty);
                else
                    text = LanguageMgr.Get("AddsAccuracyValue", minValue + maxValue + addValue);
                MirLabel ACCLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? CommonColor.Cyan : CommonColor.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    //Text = string.Format("Accuracy + {0}", minValue + addValue)
                    Text = text
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, ACCLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, ACCLabel.DisplayRectangle.Bottom));
            }



            minValue = realItem.Stats[Stat.Holy];
            maxValue = 0;
            addValue = 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                MirLabel HOLYLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? CommonColor.Cyan : CommonColor.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    //Text = string.Format("Holy + {0}", minValue + addValue)
                    Text = LanguageMgr.Get("Holy", minValue + addValue) + (addValue > 0 ? $" (+{addValue})" : string.Empty)
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, HOLYLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, HOLYLabel.DisplayRectangle.Bottom));
            }



            minValue = realItem.Stats[Stat.AttackSpeed];
            maxValue = 0;
            addValue = (!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) ? addedStats[Stat.AttackSpeed] : 0;

            if (minValue != 0 || maxValue != 0 || addValue != 0)
            {
                string plus = (addValue + minValue < 0) ? String.Empty : "+";

                count++;
                if (HoverItem.Info.Type != ItemType.Gem)
                {
                    string negative = "+";
                    if (addValue < 0) negative = String.Empty;

                    text = LanguageMgr.Get("AttackSpeedValue", plus, minValue + addValue) + (addValue != 0 ? $" ({negative}{addValue})" : string.Empty);
                    //text = string.Format(addValue != 0 ? "A.Speed: " + plus + "{0} ({2}{1})" : "A.Speed: " + plus + "{0}", minValue + addValue, addValue, negative);
                    //text = string.Format(addValue > 0 ? "A.Speed: + {0} (+{1})" : "A.Speed: + {0}", minValue + addValue, addValue);
                }
                else
                    text = LanguageMgr.Get("AddAttackSpeed", minValue + maxValue + addValue);

                MirLabel ASPEEDLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? CommonColor.Cyan : CommonColor.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    //Text = string.Format("A.Speed + {0}", minValue + addValue)
                    Text = text
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, ASPEEDLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, ASPEEDLabel.DisplayRectangle.Bottom));
            }



            minValue = realItem.Stats[Stat.Freezing];
            maxValue = 0;
            addValue = (!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) ? addedStats[Stat.Freezing] : 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                if (HoverItem.Info.Type != ItemType.Gem)
                {
                    text = LanguageMgr.Get("FreezingPlus", minValue + addValue) + (addValue > 0 ? $" (+{addValue})" : string.Empty);
                }
                else text = LanguageMgr.Get("AddsFreezingPlus", minValue + maxValue + addValue);

                MirLabel FREEZINGLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? CommonColor.Cyan : CommonColor.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    //Text = string.Format("Freezing + {0}", minValue + addValue)
                    Text = text
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, FREEZINGLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, FREEZINGLabel.DisplayRectangle.Bottom));
            }



            minValue = realItem.Stats[Stat.PoisonAttack];
            maxValue = 0;
            addValue = (!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) ? addedStats[Stat.PoisonAttack] : 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                if (HoverItem.Info.Type != ItemType.Gem)
                {
                    text = LanguageMgr.Get("PoisonPlus", minValue + addValue) + (addValue > 0 ? $" (+{addValue})" : string.Empty);
                }
                else
                    text = LanguageMgr.Get("AddsPoisonPlus", minValue + maxValue + addValue);

                MirLabel POISONLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? CommonColor.Cyan : CommonColor.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    //Text = string.Format("Poison + {0}", minValue + addValue)
                    Text = text
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, POISONLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, POISONLabel.DisplayRectangle.Bottom));
            }



            minValue = realItem.Stats[Stat.CriticalRate];
            maxValue = 0;
            addValue = (!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) ? addedStats[Stat.CriticalRate] : 0;

            if ((minValue > 0 || maxValue > 0 || addValue > 0) && (realItem.Type != ItemType.Gem))
            {
                count++;
                MirLabel CRITICALRATELabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? CommonColor.Cyan : CommonColor.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    //Text = string.Format("Critical Chance + {0}", minValue + addValue)
                    //Text = string.Format(addValue > 0 ? "Critical Chance: + {0} (+{1})" : "Critical Chance: + {0}", minValue + addValue, addValue)
                    Text = LanguageMgr.Get("CriticalChancePlus", minValue + addValue) + (addValue > 0 ? $" (+{addValue})" : string.Empty)
                };

                if (fishingItem)
                {
                    CRITICALRATELabel.Text = LanguageMgr.Get("FlexibilityPlus", minValue + addValue) + (addValue > 0 ? $" (+{addValue})" : string.Empty);
                }

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, CRITICALRATELabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, CRITICALRATELabel.DisplayRectangle.Bottom));
            }



            minValue = realItem.Stats[Stat.CriticalDamage];
            maxValue = 0;
            addValue = (!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) ? addedStats[Stat.CriticalDamage] : 0;

            if ((minValue > 0 || maxValue > 0 || addValue > 0) && (realItem.Type != ItemType.Gem))
            {
                count++;
                MirLabel CRITICALDAMAGELabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? CommonColor.Cyan : CommonColor.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    //Text = string.Format("Critical Damage + {0}", minValue + addValue)
                    Text = LanguageMgr.Get("CriticalDamagePlus", minValue + addValue) + (addValue > 0 ? $" (+{addValue})" : string.Empty)
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, CRITICALDAMAGELabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, CRITICALDAMAGELabel.DisplayRectangle.Bottom));
            }



            minValue = realItem.Stats[Stat.Reflect];
            maxValue = 0;
            addValue = 0;

            if ((minValue > 0 || maxValue > 0 || addValue > 0) && (realItem.Type != ItemType.Gem))
            {
                count++;
                MirLabel ReflectLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? CommonColor.Cyan : CommonColor.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = LanguageMgr.Get("ReflectChance", minValue)
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, ReflectLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, ReflectLabel.DisplayRectangle.Bottom));
            }



            minValue = realItem.Stats[Stat.HPDrainRatePercent];
            maxValue = 0;
            addValue = 0;

            if ((minValue > 0 || maxValue > 0 || addValue > 0) && (realItem.Type != ItemType.Gem))
            {
                count++;
                MirLabel HPdrainLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? CommonColor.Cyan : CommonColor.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = LanguageMgr.Get("HPDrainRate", minValue)
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, HPdrainLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, HPdrainLabel.DisplayRectangle.Bottom));
            }



            minValue = realItem.Stats[Stat.ExpRatePercent];
            maxValue = 0;
            addValue = (!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) ? addedStats[Stat.ExpRatePercent] : 0;

            if (minValue != 0 || maxValue != 0 || addValue != 0)
            {
                string plus = (addValue + minValue < 0) ? string.Empty : "+";

                count++;
                string negative = "+";
                if (addValue < 0) negative = String.Empty;
                text = LanguageMgr.Get("ExpRate", plus, minValue + addValue) + (addValue != 0 ? $" ({negative}{addValue}%)" : String.Empty);

                MirLabel expRateLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? CommonColor.Cyan : CommonColor.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = text
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, expRateLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, expRateLabel.DisplayRectangle.Bottom));
            }



            minValue = realItem.Stats[Stat.ItemDropRatePercent];
            maxValue = 0;
            addValue = (!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) ? addedStats[Stat.ItemDropRatePercent] : 0;

            if (minValue != 0 || maxValue != 0 || addValue != 0)
            {
                string plus = (addValue + minValue < 0) ? String.Empty : "+";

                count++;
                string negative = "+";
                if (addValue < 0) negative = String.Empty;
                text = LanguageMgr.Get("DropRate", plus, minValue + addValue) + (addValue != 0 ? $" ({negative}{addValue}%)" : string.Empty);

                MirLabel dropRateLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? CommonColor.Cyan : CommonColor.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = text
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, dropRateLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, dropRateLabel.DisplayRectangle.Bottom));
            }



            minValue = realItem.Stats[Stat.GoldDropRatePercent];
            maxValue = 0;
            addValue = (!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) ? addedStats[Stat.GoldDropRatePercent] : 0;

            if (minValue != 0 || maxValue != 0 || addValue != 0)
            {
                string plus = (addValue + minValue < 0) ? String.Empty : "+";

                count++;
                string negative = "+";
                if (addValue < 0) negative = "";
                text = LanguageMgr.Get("GoldRate", plus, minValue + addValue) + (addValue != 0 ? $" ({negative}{addValue}%)" : String.Empty);

                MirLabel goldRateLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? CommonColor.Cyan : CommonColor.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = text
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, goldRateLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, goldRateLabel.DisplayRectangle.Bottom));
            }



            if (addedStats[Stat.Hero] > 0)
            {
                ClientHeroInformation heroInfo = HeroInfoList.FirstOrDefault(x => x.Index == addedStats[Stat.Hero]);
                if (heroInfo != null)
                {
                    count++;
                    text = heroInfo.ToString();

                    MirLabel heroLabel = new MirLabel
                    {
                        AutoSize = true,
                        ForeColour = CommonColor.White,
                        Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                        OutLine = true,
                        Parent = ItemLabel,
                        Text = text
                    };

                    ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, heroLabel.DisplayRectangle.Right + 4),
                        Math.Max(ItemLabel.Size.Height, heroLabel.DisplayRectangle.Bottom));
                }
            }


            if (count > 0)
            {
                ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height + 4);

                MirControl outLine = new MirControl
                {
                    BackColour = CommonColor.FromArgb(255, 50, 50, 50),
                    Border = true,
                    BorderColour = CommonColor.Gray,
                    NotControl = true,
                    Parent = ItemLabel,
                    Opacity = 0.4F,
                    Location = new Point(0, 0)
                };
                outLine.Size = ItemLabel.Size;

                return outLine;
            }
            else
            {
                ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height - 4);
            }
            return null;
        }
        public MirControl DefenceInfoLabel(UserItem item, bool Inspect = false, bool hideAdded = false)
        {
            ushort level = Inspect ? InspectDialog.Level : MapObject.User.Level;
            MirClass job = Inspect ? InspectDialog.Class : MapObject.User.Class;
            HoverItem = item;
            ItemInfoTable realItem = Functions.GetRealItem(item.Info, level, job, ItemInfoList);
            Stats addedStats = GetTotalAddedStats(item, level, job);

            ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height + 4);

            bool fishingItem = false;

            switch (HoverItem.Info.Type)
            {
                case ItemType.Hook:
                case ItemType.Float:
                case ItemType.Bait:
                case ItemType.Finder:
                case ItemType.Reel:
                    fishingItem = true;
                    break;
                case ItemType.Weapon:
                    if (HoverItem.Info.Shape == 49 || HoverItem.Info.Shape == 50)
                        fishingItem = true;
                    break;
                default:
                    fishingItem = false;
                    break;
            }

            int count = 0;
            int minValue = 0;
            int maxValue = 0;
            int addValue = 0;

            string text = "";

            minValue = realItem.Stats[Stat.MinAC];
            maxValue = realItem.Stats[Stat.MaxAC];
            addValue = (!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) ? addedStats[Stat.MaxAC] : 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                if (HoverItem.Info.Type != ItemType.Gem)
                    text = LanguageMgr.Get("AC", minValue, maxValue + addValue) + (addValue > 0 ? $" (+{addValue})" : string.Empty);
                else
                    text = LanguageMgr.Get("AddsAC", minValue + maxValue + addValue);
                MirLabel ACLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? CommonColor.Cyan : CommonColor.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    //Text = string.Format("AC + {0}~{1}", minValue, maxValue + addValue)
                    Text = text
                };

                if (fishingItem)
                {
                    if (HoverItem.Info.Type == ItemType.Float)
                    {
                        ACLabel.Text = LanguageMgr.Get("NibbleChance", minValue, maxValue + addValue) +
                                       (addValue > 0 ? $" (+{addValue})" : String.Empty);
                    }
                    else if (HoverItem.Info.Type == ItemType.Finder)
                    {
                        ACLabel.Text = LanguageMgr.Get("FinderIncrease", minValue, maxValue + addValue) +
                                       (addValue > 0 ? $" (+{addValue})" : String.Empty);
                    }
                    else
                    {
                        ACLabel.Text = LanguageMgr.Get("SuccessChance", maxValue + addValue) + (addValue > 0 ? $" (+{addValue})" : String.Empty);
                    }
                }

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, ACLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, ACLabel.DisplayRectangle.Bottom));
            }



            minValue = realItem.Stats[Stat.MinMAC];
            maxValue = realItem.Stats[Stat.MaxMAC];
            addValue = (!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) ? addedStats[Stat.MaxMAC] : 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                if (HoverItem.Info.Type != ItemType.Gem)
                    text = LanguageMgr.Get("MAC", minValue, maxValue + addValue) + (addValue > 0 ? $" (+{addValue})" : string.Empty);
                else
                    text = LanguageMgr.Get("AddsMAC", minValue + maxValue + addValue);
                MirLabel MACLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? CommonColor.Cyan : CommonColor.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    //Text = string.Format("MAC + {0}~{1}", minValue, maxValue + addValue)
                    Text = text
                };

                if (fishingItem)
                {
                    MACLabel.Text = LanguageMgr.Get("AutoReelChance", maxValue + addValue);
                }

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, MACLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, MACLabel.DisplayRectangle.Bottom));
            }



            if (HoverItem.Info.Type != ItemType.MonsterSpawn)
            {
                minValue = realItem.Stats[Stat.HP];
                maxValue = 0;
                addValue = (!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) ? addedStats[Stat.HP] : 0;

                if (minValue > 0 || maxValue > 0 || addValue > 0)
                {
                    count++;
                    MirLabel MAXHPLabel = new MirLabel
                    {
                        AutoSize = true,
                        ForeColour = addValue > 0 ? CommonColor.Cyan : CommonColor.White,
                        Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                        OutLine = true,
                        Parent = ItemLabel,
                        //Text = string.Format(realItem.Type == ItemType.Potion ? "HP + {0} Recovery" : "MAXHP + {0}", minValue + addValue)
                        Text = LanguageMgr.Get("MaxHpPlus", minValue + addValue) + (addValue > 0 ? $" (+{addValue})" : String.Empty)
                    };

                    ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, MAXHPLabel.DisplayRectangle.Right + 4),
                        Math.Max(ItemLabel.Size.Height, MAXHPLabel.DisplayRectangle.Bottom));
                }
            }



            minValue = realItem.Stats[Stat.MP];
            maxValue = 0;
            addValue = (!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) ? addedStats[Stat.MP] : 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                MirLabel MAXMPLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? CommonColor.Cyan : CommonColor.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    //Text = string.Format(realItem.Type == ItemType.Potion ? "MP + {0} Recovery" : "MAXMP + {0}", minValue + addValue)
                    Text = LanguageMgr.Get("MaxMpPlus", minValue + addValue) + (addValue > 0 ? $" (+{addValue})" : String.Empty)
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, MAXMPLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, MAXMPLabel.DisplayRectangle.Bottom));
            }



            minValue = realItem.Stats[Stat.HPRatePercent];
            maxValue = 0;
            addValue = 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                MirLabel MAXHPRATELabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? CommonColor.Cyan : CommonColor.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = LanguageMgr.Get("MaxHpPlusPercent", minValue + addValue)
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, MAXHPRATELabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, MAXHPRATELabel.DisplayRectangle.Bottom));
            }



            minValue = realItem.Stats[Stat.MPRatePercent];
            maxValue = 0;
            addValue = 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                MirLabel MAXMPRATELabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? CommonColor.Cyan : CommonColor.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = LanguageMgr.Get("MaxMpPlusPercent", minValue + addValue)
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, MAXMPRATELabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, MAXMPRATELabel.DisplayRectangle.Bottom));
            }



            minValue = realItem.Stats[Stat.MaxACRatePercent];
            maxValue = 0;
            addValue = 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                MirLabel MAXACRATE = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? CommonColor.Cyan : CommonColor.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = LanguageMgr.Get("MaxAcPlusPercent", minValue + addValue)
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, MAXACRATE.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, MAXACRATE.DisplayRectangle.Bottom));
            }



            minValue = realItem.Stats[Stat.MaxMACRatePercent];
            maxValue = 0;
            addValue = 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                MirLabel MAXMACRATELabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? CommonColor.Cyan : CommonColor.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = LanguageMgr.Get("MaxMacPlusPercent", minValue + addValue)
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, MAXMACRATELabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, MAXMACRATELabel.DisplayRectangle.Bottom));
            }



            minValue = realItem.Stats[Stat.HealthRecovery];
            maxValue = 0;
            addValue = (!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) ? addedStats[Stat.HealthRecovery] : 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                MirLabel HEALTH_RECOVERYLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? CommonColor.Cyan : CommonColor.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = LanguageMgr.Get("HealthRecoveryPlus", minValue + addValue) + (addValue > 0 ? $" (+{addValue})" : String.Empty)
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, HEALTH_RECOVERYLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, HEALTH_RECOVERYLabel.DisplayRectangle.Bottom));
            }



            minValue = realItem.Stats[Stat.SpellRecovery];
            maxValue = 0;
            addValue = (!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) ? addedStats[Stat.SpellRecovery] : 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                MirLabel MANA_RECOVERYLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? CommonColor.Cyan : CommonColor.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    //Text = string.Format("ManaRecovery + {0}", minValue + addValue)
                    Text = LanguageMgr.Get("ManaRecoveryPlus", minValue + addValue) + (addValue > 0 ? $" (+{addValue})" : String.Empty)
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, MANA_RECOVERYLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, MANA_RECOVERYLabel.DisplayRectangle.Bottom));
            }



            minValue = realItem.Stats[Stat.PoisonRecovery];
            maxValue = 0;
            addValue = (!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) ? addedStats[Stat.PoisonRecovery] : 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                MirLabel POISON_RECOVERYabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? CommonColor.Cyan : CommonColor.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    //Text = string.Format("Poison Recovery + {0}", minValue + addValue)
                    Text = LanguageMgr.Get("PoisonRecoveryPlus", minValue + addValue) + (addValue > 0 ? $" (+{addValue})" : String.Empty)
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, POISON_RECOVERYabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, POISON_RECOVERYabel.DisplayRectangle.Bottom));
            }



            minValue = realItem.Stats[Stat.Agility];
            maxValue = 0;
            addValue = (!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) ? addedStats[Stat.Agility] : 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                if (HoverItem.Info.Type != ItemType.Gem)
                    text = LanguageMgr.Get("Agility", minValue + addValue) + (addValue > 0 ? $" (+{addValue})" : string.Empty);
                else
                    text = LanguageMgr.Get("AddsAgilityPlus", minValue + maxValue + addValue);

                MirLabel AGILITYLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? CommonColor.Cyan : CommonColor.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = text
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, AGILITYLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, AGILITYLabel.DisplayRectangle.Bottom));
            }



            minValue = realItem.Stats[Stat.Strong];
            maxValue = 0;
            addValue = (!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) ? addedStats[Stat.Strong] : 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                MirLabel STRONGLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? CommonColor.Cyan : CommonColor.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    //Text = string.Format("Strong + {0}", minValue + addValue)
                    Text = LanguageMgr.Get("StrongPlus", minValue + addValue) + (addValue > 0 ? $" (+{addValue})" : String.Empty)
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, STRONGLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, STRONGLabel.DisplayRectangle.Bottom));
            }



            minValue = realItem.Stats[Stat.PoisonResist];
            maxValue = 0;
            addValue = (!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) ? addedStats[Stat.PoisonResist] : 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                if (HoverItem.Info.Type != ItemType.Gem)
                    text = LanguageMgr.Get("PoisonResistPlus", minValue + addValue) + (addValue > 0 ? $" (+{addValue})" : String.Empty);
                else
                    text = LanguageMgr.Get("AddPoisonResistPlus", minValue + maxValue + addValue);
                MirLabel POISON_RESISTLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? CommonColor.Cyan : CommonColor.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = text
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, POISON_RESISTLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, POISON_RESISTLabel.DisplayRectangle.Bottom));
            }



            minValue = realItem.Stats[Stat.MagicResist];
            maxValue = 0;
            addValue = (!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) ? addedStats[Stat.MagicResist] : 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                if (HoverItem.Info.Type != ItemType.Gem)
                    text = LanguageMgr.Get("MagicResistPlus", minValue + addValue) + (addValue > 0 ? $" (+{addValue})" : String.Empty);
                else
                    text = LanguageMgr.Get("AddMagicResistPlus", minValue + maxValue + addValue);
                MirLabel MAGIC_RESISTLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? CommonColor.Cyan : CommonColor.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    //Text = string.Format("Magic Resist + {0}", minValue + addValue)
                    Text = text
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, MAGIC_RESISTLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, MAGIC_RESISTLabel.DisplayRectangle.Bottom));
            }



            minValue = realItem.Stats[Stat.MaxDCRatePercent];
            maxValue = 0;
            addValue = 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                MirLabel MAXDCRATE = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? CommonColor.Cyan : CommonColor.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = LanguageMgr.Get("MaxDcPlusPercent", minValue + addValue)
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, MAXDCRATE.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, MAXDCRATE.DisplayRectangle.Bottom));
            }


            minValue = realItem.Stats[Stat.MaxMCRatePercent];
            maxValue = 0;
            addValue = 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                MirLabel MAXMCRATE = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? CommonColor.Cyan : CommonColor.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = LanguageMgr.Get("MaxMcPlusPercent", minValue + addValue)
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, MAXMCRATE.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, MAXMCRATE.DisplayRectangle.Bottom));
            }


            minValue = realItem.Stats[Stat.MaxSCRatePercent];
            maxValue = 0;
            addValue = 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                MirLabel MAXSCRATE = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? CommonColor.Cyan : CommonColor.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = LanguageMgr.Get("MaxScPlusPercent", minValue + addValue)
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, MAXSCRATE.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, MAXSCRATE.DisplayRectangle.Bottom));
            }


            minValue = realItem.Stats[Stat.DamageReductionPercent];
            maxValue = 0;
            addValue = 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                MirLabel DAMAGEREDUC = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? CommonColor.Cyan : CommonColor.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = LanguageMgr.Get("AllDamageReductionPlusPercent", minValue + addValue)
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, DAMAGEREDUC.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, DAMAGEREDUC.DisplayRectangle.Bottom));
            }
            if (count > 0)
            {
                ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height + 4);

                MirControl outLine = new MirControl
                {
                    BackColour = CommonColor.FromArgb(255, 50, 50, 50),
                    Border = true,
                    BorderColour = CommonColor.Gray,
                    NotControl = true,
                    Parent = ItemLabel,
                    Opacity = 0.4F,
                    Location = new Point(0, 0)
                };
                outLine.Size = ItemLabel.Size;

                return outLine;
            }
            else
            {
                ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height - 4);
            }
            return null;
        }
        public MirControl WeightInfoLabel(UserItem item, bool Inspect = false)
        {
            ushort level = Inspect ? InspectDialog.Level : MapObject.User.Level;
            MirClass job = Inspect ? InspectDialog.Class : MapObject.User.Class;
            HoverItem = item;
            ItemInfoTable realItem = Functions.GetRealItem(item.Info, level, job, ItemInfoList);

            ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height + 4);

            int count = 0;
            int minValue = 0;
            int maxValue = 0;
            int addValue = 0;


            minValue = realItem.Stats[Stat.HandWeight];
            maxValue = 0;
            addValue = 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                MirLabel HANDWEIGHTLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? CommonColor.Cyan : CommonColor.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    //Text = string.Format("Hand Weight + {0}", minValue + addValue)
                    Text = LanguageMgr.Get("HandWeightPlus", minValue + addValue) + (addValue > 0 ? $" (+{addValue})" : String.Empty)
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, HANDWEIGHTLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, HANDWEIGHTLabel.DisplayRectangle.Bottom));
            }



            minValue = realItem.Stats[Stat.WearWeight];
            maxValue = 0;
            addValue = 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                MirLabel WEARWEIGHTLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? CommonColor.Cyan : CommonColor.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    //Text = string.Format("Wear Weight + {0}", minValue + addValue)
                    Text = LanguageMgr.Get("WearWeightPlus", minValue + addValue) + (addValue > 0 ? $" (+{addValue})" : String.Empty)
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, WEARWEIGHTLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, WEARWEIGHTLabel.DisplayRectangle.Bottom));
            }



            minValue = realItem.Stats[Stat.BagWeight];
            maxValue = 0;
            addValue = 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                MirLabel BAGWEIGHTLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? CommonColor.Cyan : CommonColor.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    //Text = string.Format("Bag Weight + {0}", minValue + addValue)
                    Text = LanguageMgr.Get("BagWeightPlus", minValue + addValue) + (addValue > 0 ? $" (+{addValue})" : String.Empty)
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, BAGWEIGHTLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, BAGWEIGHTLabel.DisplayRectangle.Bottom));
            }



            minValue = realItem.CanFastRun == true ? 1 : 0;
            maxValue = 0;
            addValue = 0;

            if (minValue > 0 || maxValue > 0 || addValue > 0)
            {
                count++;
                MirLabel BAGWEIGHTLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? CommonColor.Cyan : CommonColor.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = LanguageMgr.Get("InstantRun")};

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, BAGWEIGHTLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, BAGWEIGHTLabel.DisplayRectangle.Bottom));
            }


            minValue = 0;
            maxValue = 0;
            addValue = 0;

            if (HoverItem.Info.Type == ItemType.Potion && HoverItem.Info.Durability > 0)
            {
                count++;
                MirLabel TNRLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? CommonColor.Cyan : CommonColor.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = LanguageMgr.Get("TimeValue", Functions.PrintTimeSpanFromSeconds(HoverItem.Info.Durability * 60))
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, TNRLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, TNRLabel.DisplayRectangle.Bottom));
            }

            if (HoverItem.Info.Type == ItemType.Transform && HoverItem.Info.Durability > 0)
            {
                count++;
                MirLabel TNRLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = addValue > 0 ? CommonColor.Cyan : CommonColor.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = LanguageMgr.Get("TimeValue", Functions.PrintTimeSpanFromSeconds(HoverItem.Info.Durability, false))
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, TNRLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, TNRLabel.DisplayRectangle.Bottom));
            }


            if (count > 0)
            {
                ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height + 4);

                MirControl outLine = new MirControl
                {
                    BackColour = CommonColor.FromArgb(255, 50, 50, 50),
                    Border = true,
                    BorderColour = CommonColor.Gray,
                    NotControl = true,
                    Parent = ItemLabel,
                    Opacity = 0.4F,
                    Location = new Point(0, 0)
                };
                outLine.Size = ItemLabel.Size;

                return outLine;
            }
            else
            {
                ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height - 4);
            }
            return null;
        }
        public MirControl AwakeInfoLabel(UserItem item, bool Inspect = false)
        {
            ushort level = Inspect ? InspectDialog.Level : MapObject.User.Level;
            MirClass job = Inspect ? InspectDialog.Class : MapObject.User.Class;
            HoverItem = item;
            ItemInfoTable realItem = Functions.GetRealItem(item.Info, level, job, ItemInfoList);

            ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height + 4);

            int count = 0;

            if (HoverItem.Awake.GetAwakeLevel() > 0)
            {
                count++;
                MirLabel AWAKENAMELabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = GradeNameColor(HoverItem.Info.Grade),
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = LanguageMgr.Get("AwakeningWithValue", HoverItem.Awake.Type.ToLocalizedString(), HoverItem.Awake.GetAwakeLevel())
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, AWAKENAMELabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, AWAKENAMELabel.DisplayRectangle.Bottom));
            }


            if (HoverItem.Awake.GetAwakeValue() > 0)
            {
                count++;
                MirLabel AWAKE_TOTAL_VALUELabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = CommonColor.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = string.Format(realItem.Type != ItemType.Armour ? "{0} + {1}~{2}" : "MAX {0} + {1}", HoverItem.Awake.Type.ToLocalizedString(), HoverItem.Awake.GetAwakeValue(), HoverItem.Awake.GetAwakeValue())
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, AWAKE_TOTAL_VALUELabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, AWAKE_TOTAL_VALUELabel.DisplayRectangle.Bottom));
            }


            if (HoverItem.Awake.GetAwakeLevel() > 0)
            {
                count++;
                for (int i = 0; i < HoverItem.Awake.GetAwakeLevel(); i++)
                {
                    MirLabel AWAKE_LEVEL_VALUELabel = new MirLabel
                    {
                        AutoSize = true,
                        ForeColour = CommonColor.White,
                        Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                        OutLine = true,
                        Parent = ItemLabel,
                        Text = string.Format(realItem.Type != ItemType.Armour ? "Level {0} : {1} + {2}~{3}" : "Level {0} : MAX {1} + {2}~{3}", i + 1, HoverItem.Awake.Type.ToLocalizedString(), HoverItem.Awake.GetAwakeLevelValue(i), HoverItem.Awake.GetAwakeLevelValue(i))
                    };

                    ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, AWAKE_LEVEL_VALUELabel.DisplayRectangle.Right + 4),
                        Math.Max(ItemLabel.Size.Height, AWAKE_LEVEL_VALUELabel.DisplayRectangle.Bottom));
                }
            }


            if (count > 0)
            {
                ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height + 4);

                MirControl outLine = new MirControl
                {
                    BackColour = CommonColor.FromArgb(255, 50, 50, 50),
                    Border = true,
                    BorderColour = CommonColor.Gray,
                    NotControl = true,
                    Parent = ItemLabel,
                    Opacity = 0.4F,
                    Location = new Point(0, 0)
                };
                outLine.Size = ItemLabel.Size;

                return outLine;
            }
            else
            {
                ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height - 4);
            }
            return null;
        }
        public MirControl SocketInfoLabel(UserItem item, bool Inspect = false)
        {
            ushort level = Inspect ? InspectDialog.Level : MapObject.User.Level;
            MirClass job = Inspect ? InspectDialog.Class : MapObject.User.Class;
            HoverItem = item;
            ItemInfoTable realItem = Functions.GetRealItem(item.Info, level, job, ItemInfoList);

            ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height + 4);


            int count = 0;


            for (int i = 0; i < item.Slots.Length; i++)
            {
                count++;
                MirLabel SOCKETLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = (count > realItem.Slots && !realItem.IsFishingRod && realItem.Type != ItemType.Mount) ? CommonColor.Cyan : CommonColor.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = LanguageMgr.Get("SocketWithValue",
                        item.Slots[i] == null ? LanguageMgr.Get("Empty"): item.Slots[i].FriendlyName)
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, SOCKETLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, SOCKETLabel.DisplayRectangle.Bottom));
            }


            if (count > 0)
            {

                count++;
                MirLabel SOCKETLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = CommonColor.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = LanguageMgr.Get("OpenSocketsTips")};

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, SOCKETLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, SOCKETLabel.DisplayRectangle.Bottom));


                ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height + 4);

                MirControl outLine = new MirControl
                {
                    BackColour = CommonColor.FromArgb(255, 50, 50, 50),
                    Border = true,
                    BorderColour = CommonColor.Gray,
                    NotControl = true,
                    Parent = ItemLabel,
                    Opacity = 0.4F,
                    Location = new Point(0, 0)
                };
                outLine.Size = ItemLabel.Size;

                return outLine;
            }
            else
            {
                ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height - 4);
            }
            return null;
        }
        public MirControl NeedInfoLabel(UserItem item, bool Inspect = false)
        {
            ushort level = Inspect ? InspectDialog.Level : MapObject.User.Level;
            MirClass job = Inspect ? InspectDialog.Class : MapObject.User.Class;
            HoverItem = item;
            ItemInfoTable realItem = Functions.GetRealItem(item.Info, level, job, ItemInfoList);

            ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height + 4);

            int count = 0;

            if (realItem.RequiredAmount > 0)
            {
                count++;
                string text;
                Color colour = CommonColor.White;
                switch (realItem.RequiredType)
                {
                    case RequiredType.Level:
                        text = LanguageMgr.Get("RequiredLevel", realItem.RequiredAmount);
                        if (MapObject.User.Level < realItem.RequiredAmount)
                            colour = CommonColor.Red;
                        break;
                    case RequiredType.MaxAC:
                        text = LanguageMgr.Get("RequiredAC", realItem.RequiredAmount);
                        if (MapObject.User.Stats[Stat.MaxAC] < realItem.RequiredAmount)
                            colour = CommonColor.Red;
                        break;
                    case RequiredType.MaxMAC:
                        text = LanguageMgr.Get("RequiredMAC", realItem.RequiredAmount);
                        if (MapObject.User.Stats[Stat.MaxMAC] < realItem.RequiredAmount)
                            colour = CommonColor.Red;
                        break;
                    case RequiredType.MaxDC:
                        text = LanguageMgr.Get("RequiredDC", realItem.RequiredAmount);
                        if (MapObject.User.Stats[Stat.MaxDC] < realItem.RequiredAmount)
                            colour = CommonColor.Red;
                        break;
                    case RequiredType.MaxMC:
                        text = LanguageMgr.Get("RequiredMC", realItem.RequiredAmount);
                        if (MapObject.User.Stats[Stat.MaxMC] < realItem.RequiredAmount)
                            colour = CommonColor.Red;
                        break;
                    case RequiredType.MaxSC:
                        text = LanguageMgr.Get("RequiredSC", realItem.RequiredAmount);
                        if (MapObject.User.Stats[Stat.MaxSC] < realItem.RequiredAmount)
                            colour = CommonColor.Red;
                        break;
                    case RequiredType.MaxLevel:
                        text = LanguageMgr.Get("MaximumLevel", realItem.RequiredAmount);
                        if (MapObject.User.Level > realItem.RequiredAmount)
                            colour = CommonColor.Red;
                        break;
                    case RequiredType.MinAC:
                        text = LanguageMgr.Get("RequiredBaseAC", realItem.RequiredAmount);
                        if (MapObject.User.Stats[Stat.MinAC] < realItem.RequiredAmount)
                            colour = CommonColor.Red;
                        break;
                    case RequiredType.MinMAC:
                        text = LanguageMgr.Get("RequiredBaseMAC", realItem.RequiredAmount);
                        if (MapObject.User.Stats[Stat.MinMAC] < realItem.RequiredAmount)
                            colour = CommonColor.Red;
                        break;
                    case RequiredType.MinDC:
                        text = LanguageMgr.Get("RequiredBaseDC", realItem.RequiredAmount);
                        if (MapObject.User.Stats[Stat.MinDC] < realItem.RequiredAmount)
                            colour = CommonColor.Red;
                        break;
                    case RequiredType.MinMC:
                        text = LanguageMgr.Get("RequiredBaseMC", realItem.RequiredAmount);
                        if (MapObject.User.Stats[Stat.MinMC] < realItem.RequiredAmount)
                            colour = CommonColor.Red;
                        break;
                    case RequiredType.MinSC:
                        text = LanguageMgr.Get("RequiredBaseSC", realItem.RequiredAmount);
                        if (MapObject.User.Stats[Stat.MinSC] < realItem.RequiredAmount)
                            colour = CommonColor.Red;
                        break;
                    default:
                        text = LanguageMgr.Get("UnknownTypeRequired");
                        break;
                }

                MirLabel LEVELLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = colour,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = text
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, LEVELLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, LEVELLabel.DisplayRectangle.Bottom));
            }


            if (realItem.RequiredClass != RequiredClass.None)
            {
                count++;
                Color colour = CommonColor.White;

                switch (MapObject.User.Class)
                {
                    case MirClass.Warrior:
                        if (!realItem.RequiredClass.HasFlag(RequiredClass.Warrior))
                            colour = CommonColor.Red;
                        break;
                    case MirClass.Wizard:
                        if (!realItem.RequiredClass.HasFlag(RequiredClass.Wizard))
                            colour = CommonColor.Red;
                        break;
                    case MirClass.Taoist:
                        if (!realItem.RequiredClass.HasFlag(RequiredClass.Taoist))
                            colour = CommonColor.Red;
                        break;
                    case MirClass.Assassin:
                        if (!realItem.RequiredClass.HasFlag(RequiredClass.Assassin))
                            colour = CommonColor.Red;
                        break;
                    case MirClass.Archer:
                        if (!realItem.RequiredClass.HasFlag(RequiredClass.Archer))
                            colour = CommonColor.Red;
                        break;
                }

                MirLabel CLASSLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = colour,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = LanguageMgr.Get("ClassRequired", realItem.RequiredClass.ToLocalizedString())
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, CLASSLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, CLASSLabel.DisplayRectangle.Bottom));
            }


            if (item.Price() > 0)
            {
                count++;
                string text;
                var colour = CommonColor.White;

                text = LanguageMgr.Get("SellingPriceGold", ((long)(item.Price() / 2)).ToString("###,###,##0"));

                var costLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = colour,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = text
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, costLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, costLabel.DisplayRectangle.Bottom));
            }



            if (count > 0)
            {
                ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height + 4);

                MirControl outLine = new MirControl
                {
                    BackColour = CommonColor.FromArgb(255, 50, 50, 50),
                    Border = true,
                    BorderColour = CommonColor.Gray,
                    NotControl = true,
                    Parent = ItemLabel,
                    Opacity = 0.4F,
                    Location = new Point(0, 0)
                };
                outLine.Size = ItemLabel.Size;

                return outLine;
            }
            else
            {
                ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height - 4);
            }
            return null;
        }
        public MirControl BindInfoLabel(UserItem item, bool Inspect = false, bool hideAdded = false)
        {
            ushort level = Inspect ? InspectDialog.Level : MapObject.User.Level;
            MirClass job = Inspect ? InspectDialog.Class : MapObject.User.Class;
            HoverItem = item;
            ItemInfoTable realItem = Functions.GetRealItem(item.Info, level, job, ItemInfoList);

            ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height + 4);

            int count = 0;


            if (HoverItem.Info.Bind != BindMode.None && HoverItem.Info.Bind.HasFlag(BindMode.DontDeathdrop))
            {
                count++;
                MirLabel DONT_DEATH_DROPLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = CommonColor.Yellow,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = LanguageMgr.Get("CantDropOnDeath")};

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, DONT_DEATH_DROPLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, DONT_DEATH_DROPLabel.DisplayRectangle.Bottom));
            }



            if (HoverItem.Info.Bind != BindMode.None && HoverItem.Info.Bind.HasFlag(BindMode.DontDrop))
            {
                count++;
                MirLabel DONT_DROPLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = CommonColor.Yellow,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = LanguageMgr.Get("CantDrop")};

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, DONT_DROPLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, DONT_DROPLabel.DisplayRectangle.Bottom));
            }



            if (HoverItem.Info.Bind != BindMode.None && HoverItem.Info.Bind.HasFlag(BindMode.DontUpgrade))
            {
                count++;
                MirLabel DONT_UPGRADELabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = CommonColor.Yellow,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = LanguageMgr.Get("CantUpgrade")};

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, DONT_UPGRADELabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, DONT_UPGRADELabel.DisplayRectangle.Bottom));
            }



            if (HoverItem.Info.Bind != BindMode.None && HoverItem.Info.Bind.HasFlag(BindMode.DontSell))
            {
                count++;
                MirLabel DONT_SELLLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = CommonColor.Yellow,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = LanguageMgr.Get("CantSell")};

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, DONT_SELLLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, DONT_SELLLabel.DisplayRectangle.Bottom));
            }



            if (HoverItem.Info.Bind != BindMode.None && HoverItem.Info.Bind.HasFlag(BindMode.DontTrade))
            {
                count++;
                MirLabel DONT_TRADELabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = CommonColor.Yellow,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = LanguageMgr.Get("CantTrade")};

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, DONT_TRADELabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, DONT_TRADELabel.DisplayRectangle.Bottom));
            }



            if (HoverItem.Info.Bind != BindMode.None && HoverItem.Info.Bind.HasFlag(BindMode.DontStore))
            {
                count++;
                MirLabel DONT_STORELabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = CommonColor.Yellow,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = LanguageMgr.Get("CantStore")};

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, DONT_STORELabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, DONT_STORELabel.DisplayRectangle.Bottom));
            }



            if (HoverItem.Info.Bind != BindMode.None && HoverItem.Info.Bind.HasFlag(BindMode.DontRepair))
            {
                count++;
                MirLabel DONT_REPAIRLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = CommonColor.Yellow,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = LanguageMgr.Get("CantRepair")};

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, DONT_REPAIRLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, DONT_REPAIRLabel.DisplayRectangle.Bottom));
            }



            if (HoverItem.Info.Bind != BindMode.None && HoverItem.Info.Bind.HasFlag(BindMode.NoSRepair))
            {
                count++;
                MirLabel DONT_REPAIRLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = CommonColor.Yellow,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = LanguageMgr.Get("CantSpecialRepair")};

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, DONT_REPAIRLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, DONT_REPAIRLabel.DisplayRectangle.Bottom));
            }



            if (HoverItem.Info.Bind != BindMode.None && HoverItem.Info.Bind.HasFlag(BindMode.BreakOnDeath))
            {
                count++;
                MirLabel DONT_REPAIRLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = CommonColor.Yellow,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = LanguageMgr.Get("BreaksOnDeath")};

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, DONT_REPAIRLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, DONT_REPAIRLabel.DisplayRectangle.Bottom));
            }



            if (HoverItem.Info.Bind != BindMode.None && HoverItem.Info.Bind.HasFlag(BindMode.DestroyOnDrop))
            {
                count++;
                MirLabel DONT_DODLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = CommonColor.Yellow,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = LanguageMgr.Get("DestroyedWhenDropped")};

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, DONT_DODLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, DONT_DODLabel.DisplayRectangle.Bottom));
            }



            if (HoverItem.Info.Bind != BindMode.None && HoverItem.Info.Bind.HasFlag(BindMode.NoWeddingRing))
            {
                count++;
                MirLabel No_WedLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = CommonColor.Yellow,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = LanguageMgr.Get("CannotBeWeddingRing")};

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, No_WedLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, No_WedLabel.DisplayRectangle.Bottom));
            }



            if (HoverItem.Info.Bind != BindMode.None && HoverItem.Info.Bind.HasFlag(BindMode.NoHero))
            {
                count++;
                MirLabel No_HeroLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = CommonColor.Yellow,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = LanguageMgr.Get("CannotBeUsedByHero")};

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, No_HeroLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, No_HeroLabel.DisplayRectangle.Bottom));
            }



            if ((HoverItem.Info.Bind.HasFlag(BindMode.BindOnEquip)) & HoverItem.SoulBoundId == -1)
            {
                count++;
                MirLabel BOELabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = CommonColor.Yellow,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = LanguageMgr.Get("SoulBindsOnEquip")};

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, BOELabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, BOELabel.DisplayRectangle.Bottom));
            }
            else if (HoverItem.SoulBoundId != -1)
            {
                count++;
                MirLabel BOELabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = CommonColor.Yellow,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = LanguageMgr.Get("SoulboundTo")+ GetUserName((uint)HoverItem.SoulBoundId)
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, BOELabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, BOELabel.DisplayRectangle.Bottom));
            }



            if ((!hideAdded && (!HoverItem.Info.NeedIdentify || HoverItem.Identified)) && HoverItem.Cursed)
            {
                count++;
                MirLabel CURSEDLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = CommonColor.Yellow,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = LanguageMgr.Get("Cursed")};

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, CURSEDLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, CURSEDLabel.DisplayRectangle.Bottom));
            }



            if (HoverItem.Info.Type == ItemType.Gem)
            {
                count++;
                string Text = "";
                if (HoverItem.Info.Unique == SpecialItemMode.None)
                {
                    Text = LanguageMgr.Get("CannotBeUsedOnAnyItem");
                }
                else
                {
                    Text = LanguageMgr.Get("CanBeUsedOn");
                }
                MirLabel GemUseOn = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = CommonColor.Yellow,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = Text
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, GemUseOn.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, GemUseOn.DisplayRectangle.Bottom));
                count++;
                if (HoverItem.Info.Unique.HasFlag(SpecialItemMode.Paralize))
                {
                    MirLabel GemWeapon = new MirLabel
                    {
                        AutoSize = true,
                        ForeColour = CommonColor.White,
                        Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                        OutLine = true,
                        Parent = ItemLabel,
                        Text = LanguageMgr.Get("AfterWeapon")};

                    ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, GemWeapon.DisplayRectangle.Right + 4),
                        Math.Max(ItemLabel.Size.Height, GemWeapon.DisplayRectangle.Bottom));
                }
                count++;
                if (HoverItem.Info.Unique.HasFlag(SpecialItemMode.Teleport))
                {
                    MirLabel GemArmour = new MirLabel
                    {
                        AutoSize = true,
                        ForeColour = CommonColor.White,
                        Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                        OutLine = true,
                        Parent = ItemLabel,
                        Text = LanguageMgr.Get("AfterArmour")};

                    ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, GemArmour.DisplayRectangle.Right + 4),
                        Math.Max(ItemLabel.Size.Height, GemArmour.DisplayRectangle.Bottom));
                }
                count++;
                if (HoverItem.Info.Unique.HasFlag(SpecialItemMode.ClearRing))
                {
                    MirLabel Gemhelmet = new MirLabel
                    {
                        AutoSize = true,
                        ForeColour = CommonColor.White,
                        Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                        OutLine = true,
                        Parent = ItemLabel,
                        Text = LanguageMgr.Get("AfterHelmet")};

                    ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, Gemhelmet.DisplayRectangle.Right + 4),
                        Math.Max(ItemLabel.Size.Height, Gemhelmet.DisplayRectangle.Bottom));
                }
                count++;
                if (HoverItem.Info.Unique.HasFlag(SpecialItemMode.Protection))
                {
                    MirLabel Gemnecklace = new MirLabel
                    {
                        AutoSize = true,
                        ForeColour = CommonColor.White,
                        Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                        OutLine = true,
                        Parent = ItemLabel,
                        Text = LanguageMgr.Get("AfterNecklace")};

                    ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, Gemnecklace.DisplayRectangle.Right + 4),
                        Math.Max(ItemLabel.Size.Height, Gemnecklace.DisplayRectangle.Bottom));
                }
                count++;
                if (HoverItem.Info.Unique.HasFlag(SpecialItemMode.Revival))
                {
                    MirLabel GemBracelet = new MirLabel
                    {
                        AutoSize = true,
                        ForeColour = CommonColor.White,
                        Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                        OutLine = true,
                        Parent = ItemLabel,
                        Text = LanguageMgr.Get("AfterBracelet")};

                    ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, GemBracelet.DisplayRectangle.Right + 4),
                        Math.Max(ItemLabel.Size.Height, GemBracelet.DisplayRectangle.Bottom));
                }
                count++;
                if (HoverItem.Info.Unique.HasFlag(SpecialItemMode.Muscle))
                {
                    MirLabel GemRing = new MirLabel
                    {
                        AutoSize = true,
                        ForeColour = CommonColor.White,
                        Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                        OutLine = true,
                        Parent = ItemLabel,
                        Text = LanguageMgr.Get("AfterRing")};

                    ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, GemRing.DisplayRectangle.Right + 4),
                        Math.Max(ItemLabel.Size.Height, GemRing.DisplayRectangle.Bottom));
                }
                count++;
                if (HoverItem.Info.Unique.HasFlag(SpecialItemMode.Flame))
                {
                    MirLabel Gemamulet = new MirLabel
                    {
                        AutoSize = true,
                        ForeColour = CommonColor.White,
                        Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                        OutLine = true,
                        Parent = ItemLabel,
                        Text = LanguageMgr.Get("AfterAmulet")};

                    ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, Gemamulet.DisplayRectangle.Right + 4),
                        Math.Max(ItemLabel.Size.Height, Gemamulet.DisplayRectangle.Bottom));
                }
                count++;
                if (HoverItem.Info.Unique.HasFlag(SpecialItemMode.Healing))
                {
                    MirLabel Gembelt = new MirLabel
                    {
                        AutoSize = true,
                        ForeColour = CommonColor.White,
                        Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                        OutLine = true,
                        Parent = ItemLabel,
                        Text = LanguageMgr.Get("AfterBelt")};

                    ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, Gembelt.DisplayRectangle.Right + 4),
                        Math.Max(ItemLabel.Size.Height, Gembelt.DisplayRectangle.Bottom));
                }
                count++;
                if (HoverItem.Info.Unique.HasFlag(SpecialItemMode.Probe))
                {
                    MirLabel Gemboots = new MirLabel
                    {
                        AutoSize = true,
                        ForeColour = CommonColor.White,
                        Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                        OutLine = true,
                        Parent = ItemLabel,
                        Text = LanguageMgr.Get("AfterBoots")};

                    ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, Gemboots.DisplayRectangle.Right + 4),
                        Math.Max(ItemLabel.Size.Height, Gemboots.DisplayRectangle.Bottom));
                }
                count++;
                if (HoverItem.Info.Unique.HasFlag(SpecialItemMode.Skill))
                {
                    MirLabel Gemstone = new MirLabel
                    {
                        AutoSize = true,
                        ForeColour = CommonColor.White,
                        Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                        OutLine = true,
                        Parent = ItemLabel,
                        Text = LanguageMgr.Get("Stone")};

                    ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, Gemstone.DisplayRectangle.Right + 4),
                        Math.Max(ItemLabel.Size.Height, Gemstone.DisplayRectangle.Bottom));
                }
                count++;
                if (HoverItem.Info.Unique.HasFlag(SpecialItemMode.NoDuraLoss))
                {
                    MirLabel Gemtorch = new MirLabel
                    {
                        AutoSize = true,
                        ForeColour = CommonColor.White,
                        Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                        OutLine = true,
                        Parent = ItemLabel,
                        Text = LanguageMgr.Get("AfterCandle")};

                    ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, Gemtorch.DisplayRectangle.Right + 4),
                        Math.Max(ItemLabel.Size.Height, Gemtorch.DisplayRectangle.Bottom));
                }
            }



            //if ((HoverItem.Info.CanAwakening != true) && (HoverItem.Info.Type != ItemType.Gem))
            //{
            //    count++;
            //    MirLabel CANTAWAKENINGLabel = new MirLabel
            //    {
            //        AutoSize = true,
            //        ForeColour = CommonColor.Yellow,
            //        Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
            //        OutLine = true,
            //        Parent = ItemLabel,
            //        Text = string.Format("Can't awaken")
            //    };

            //    ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, CANTAWAKENINGLabel.DisplayRectangle.Right + 4),
            //        Math.Max(ItemLabel.Size.Height, CANTAWAKENINGLabel.DisplayRectangle.Bottom));
            //}



            if (HoverItem.ExpireInfo != null)
            {
                double remainingSeconds = (HoverItem.ExpireInfo.ExpiryDate - CMain.Now).TotalSeconds;

                count++;
                MirLabel EXPIRELabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = CommonColor.Yellow,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = remainingSeconds > 0 ? LanguageMgr.Get("ExpiresIn", Functions.PrintTimeSpanFromSeconds(remainingSeconds)) : LanguageMgr.Get("Expired")};

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, EXPIRELabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, EXPIRELabel.DisplayRectangle.Bottom));
            }



            if (HoverItem.SealedInfo != null)
            {
                double remainingSeconds = (HoverItem.SealedInfo.ExpiryDate - CMain.Now).TotalSeconds;

                if (remainingSeconds > 0)
                {
                    count++;
                    MirLabel SEALEDLabel = new MirLabel
                    {
                        AutoSize = true,
                        ForeColour = CommonColor.Red,
                        Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                        OutLine = true,
                        Parent = ItemLabel,
                        Text = remainingSeconds > 0 ? LanguageMgr.Get("SealedFor", Functions.PrintTimeSpanFromSeconds(remainingSeconds)) : string.Empty
                    };

                    ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, SEALEDLabel.DisplayRectangle.Right + 4),
                        Math.Max(ItemLabel.Size.Height, SEALEDLabel.DisplayRectangle.Bottom));
                }
            }


            if (HoverItem.RentalInformation?.RentalLocked == false)
            {
                count++;
                MirLabel OWNERLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = CommonColor.DarkKhaki,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = LanguageMgr.Get("ItemRentedFrom")+ HoverItem.RentalInformation.OwnerName
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, OWNERLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, OWNERLabel.DisplayRectangle.Bottom));

                double remainingTime = (HoverItem.RentalInformation.ExpiryDate - CMain.Now).TotalSeconds;

                count++;
                MirLabel RENTALLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = CommonColor.Khaki,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = remainingTime > 0 ? LanguageMgr.Get("RentalExpiresIn", Functions.PrintTimeSpanFromSeconds(remainingTime)) : "Rental expired"
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, RENTALLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, RENTALLabel.DisplayRectangle.Bottom));
            }
            else if (HoverItem.RentalInformation?.RentalLocked == true && HoverItem.RentalInformation.ExpiryDate > CMain.Now)
            {
                count++;
                var remainingTime = (HoverItem.RentalInformation.ExpiryDate - CMain.Now).TotalSeconds;
                var RentalLockLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = CommonColor.DarkKhaki,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = remainingTime > 0
                        ? LanguageMgr.Get("RentalLockExpiresIn", Functions.PrintTimeSpanFromSeconds(remainingTime))
                        : "Rental lock expired"
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, RentalLockLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, RentalLockLabel.DisplayRectangle.Bottom));
            }

            if (count > 0)
            {
                ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height + 4);

                MirControl outLine = new MirControl
                {
                    BackColour = CommonColor.FromArgb(255, 50, 50, 50),
                    Border = true,
                    BorderColour = CommonColor.Gray,
                    NotControl = true,
                    Parent = ItemLabel,
                    Opacity = 0.4F,
                    Location = new Point(0, 0)
                };
                outLine.Size = ItemLabel.Size;

                return outLine;
            }
            else
            {
                ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height - 4);
            }
            return null;
        }
        public MirControl OverlapInfoLabel(UserItem item, bool Inspect = false)
        {
            ushort level = Inspect ? InspectDialog.Level : MapObject.User.Level;
            MirClass job = Inspect ? InspectDialog.Class : MapObject.User.Class;
            HoverItem = item;
            ItemInfoTable realItem = Functions.GetRealItem(item.Info, level, job, ItemInfoList);

            ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height + 4);

            int count = 0;



            if (realItem.Type == ItemType.Gem)
            {
                string text = "";

                switch (realItem.Shape)
                {
                    case 1:
                        text = LanguageMgr.Get("HoldCtrlPartialRepairWeaponsAccessories");
                        break;
                    case 2:
                        text = LanguageMgr.Get("HoldCtrlPartialRepairArmourDrapery");
                        break;
                    case 3:
                        text = LanguageMgr.Get("HoldCtrlCombineDestroyChance");
                        break;
                    case 4:
                        text = LanguageMgr.Get("HoldCtrlCombineNoDestroy");
                        break;
                    case 5:
                        text = LanguageMgr.Get("HoldCtrlFullRepairWeaponsAccessories");
                        break;
                    case 6:
                        text = LanguageMgr.Get("HoldCtrlFullRepairArmourDrapery");
                        break;
                    case 8:
                        text = LanguageMgr.Get("HoldCtrlSealItem");
                        break;
                }
                count++;
                MirLabel GEMLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = CommonColor.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = text
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, GEMLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, GEMLabel.DisplayRectangle.Bottom));
            }



            if (realItem.StackSize > 1 && realItem.Type != ItemType.Gem)
            {
                count++;
                MirLabel SPLITUPLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = CommonColor.White,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = LanguageMgr.Get("MaxCombine", realItem.StackSize, "\n")
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, SPLITUPLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, SPLITUPLabel.DisplayRectangle.Bottom));
            }


            if (count > 0)
            {
                ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height + 4);

                MirControl outLine = new MirControl
                {
                    BackColour = CommonColor.FromArgb(255, 50, 50, 50),
                    Border = true,
                    BorderColour = CommonColor.Gray,
                    NotControl = true,
                    Parent = ItemLabel,
                    Opacity = 0.4F,
                    Location = new Point(0, 0)
                };
                outLine.Size = ItemLabel.Size;

                return outLine;
            }
            else
            {
                ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height - 4);
            }
            return null;
        }
        public MirControl StoryInfoLabel(UserItem item, bool Inspect = false)
        {
            ushort level = Inspect ? InspectDialog.Level : MapObject.User.Level;
            MirClass job = Inspect ? InspectDialog.Class : MapObject.User.Class;
            HoverItem = item;
            ItemInfoTable realItem = Functions.GetRealItem(item.Info, level, job, ItemInfoList);

            ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height + 4);

            int count = 0;


            if (realItem.Type == ItemType.Scroll && realItem.Shape == 7)//Credit Scroll
            {
                HoverItem.Info.ToolTip = LanguageMgr.Get("AddsCreditsToAccount", HoverItem.Info.Price);
            }

            if (!string.IsNullOrEmpty(HoverItem.Info.ToolTip))
            {
                count++;

                MirLabel IDLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = CommonColor.DarkKhaki,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = LanguageMgr.Get("ItemDescription")};

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, IDLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, IDLabel.DisplayRectangle.Bottom));

                MirLabel TOOLTIPLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = CommonColor.Khaki,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = HoverItem.Info.ToolTip
                };

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, TOOLTIPLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height, TOOLTIPLabel.DisplayRectangle.Bottom));
            }


            if (count > 0)
            {
                ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height + 4);

                MirControl outLine = new MirControl
                {
                    BackColour = CommonColor.FromArgb(255, 50, 50, 50),
                    Border = true,
                    BorderColour = CommonColor.Gray,
                    NotControl = true,
                    Parent = ItemLabel,
                    Opacity = 0.4F,
                    Location = new Point(0, 0)
                };
                outLine.Size = ItemLabel.Size;

                return outLine;
            }
            else
            {
                ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height - 4);
            }
            return null;
        }

        public MirControl GMMadeLabel(UserItem item)
        {
            if (item.GMMade)
            {


                ItemLabel.Size = new Size(ItemLabel.Size.Width, ItemLabel.Size.Height + 4);

                MirLabel GMLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = CommonColor.Orchid,
                    Location = new Point(4, ItemLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = ItemLabel,
                    Text = LanguageMgr.Get("CreatedByGameMaster")};

                ItemLabel.Size = new Size(Math.Max(ItemLabel.Size.Width, GMLabel.DisplayRectangle.Right + 4),
                    Math.Max(ItemLabel.Size.Height + 4, GMLabel.DisplayRectangle.Bottom + 4));

                MirControl outLine = new MirControl
                {
                    BackColour = CommonColor.FromArgb(255, 50, 50, 50),
                    Border = true,
                    BorderColour = CommonColor.Gray,
                    NotControl = true,
                    Parent = ItemLabel,
                    Opacity = 0.4F,
                    Location = new Point(0, 0)
                };
                outLine.Size = ItemLabel.Size;

                return outLine;
            }
            else
            {
                return null;
            }
        }

    }
}
