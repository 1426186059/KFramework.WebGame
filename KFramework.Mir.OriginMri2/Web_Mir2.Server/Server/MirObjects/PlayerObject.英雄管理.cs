// partial 文件：PlayerObject 功能拆分（原 PlayerObject.cs 原样迁入）。
using System.Drawing;
using C = ClientPackets;
using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirNetwork;
using S = ServerPackets;
using System.Text.RegularExpressions;
using Timer = Server.MirEnvir.Timer;

namespace Server.MirObjects
{
    public partial class PlayerObject
    {
        public Server.MirEnvir.Timer GetTimer(string key)
        {
            var timerKey = Name + "-" + key;

            if (Envir.Timers.ContainsKey(timerKey))
            {
                return Envir.Timers[timerKey];
            }

            return null;
        }
        public void SetTimer(string key, int seconds, byte type = 0)
        {
            if (seconds < 0) seconds = 0;

            var timerKey = Name + "-" + key;

            Timer t = new Timer(timerKey, seconds, type);

            Envir.Timers[timerKey] = t;

            Enqueue(new S.SetTimer { Key = t.Key, Seconds = t.Seconds, Type = t.Type });
        }
        public void ExpireTimer(string key)
        {
            var timerKey = Name + "-" + key;

            if (Envir.Timers.ContainsKey(timerKey))
            {
                Envir.Timers.Remove(timerKey);
            }

            Enqueue(new S.ExpireTimer { Key = timerKey });
        }
        public void SetCompass(Point location)
        {
            Enqueue(new S.SetCompass { Location = location });
        }
        public bool HasHero
        {
            get { return CurrentHero != null; }
        }
        public bool HeroSpawned
        {
            get { return Hero != null; }
        }
        public void SummonHero()
        {
            if (CurrentMap.Info.NoHero)
            {
                ReceiveChat(LanguageMgr.Get("CannotSummonHeroOnMap"), ChatType.System);
                return;
            }

            HeroObject hero = CurrentHero.Class switch
            {
                MirClass.Warrior => new WarriorHero(CurrentHero, this),
                MirClass.Wizard => new WizardHero(CurrentHero, this),
                MirClass.Taoist => new TaoistHero(CurrentHero, this),
                MirClass.Assassin => new AssassinHero(CurrentHero, this),
                MirClass.Archer => new ArcherHero(CurrentHero, this),
                _ => new HeroObject(CurrentHero, this)
            };

            hero.ActionTime = Envir.Time + 1000;
            hero.RefreshNameColour();

            if (!hero.Dead)
                SpawnHero(hero);

            Hero = hero;
            Info.HeroSpawned = true;
            Enqueue(new S.UpdateHeroSpawnState { State = hero.Dead ? HeroSpawnState.Dead : HeroSpawnState.Summoned });
        }
        private void SpawnHero(HeroObject hero)
        {
            if (CurrentMap.ValidPoint(Front))
                hero.Spawn(CurrentMap, Front);
            else
                hero.Spawn(CurrentMap, CurrentLocation);

            for (int i = 0; i < hero.Buffs.Count; i++)
            {
                var buff = hero.Buffs[i];
                buff.LastTime = Envir.Time;
                buff.ObjectID = hero.ObjectID;

                hero.AddBuff(buff.Type, null, (int)buff.ExpireTime, buff.Stats, true, true, buff.Values);
            }
        }
        public void DespawnHero()
        {
            if (Hero == null) return;

            Hero.Despawn(true);
            Hero = null;

            Enqueue(new S.UpdateHeroSpawnState { State = HeroSpawnState.Unsummoned });

            Console.WriteLine("[Hero] Hero forcibly despawned and removed.");
        }
        public void ReviveHero()
        {
            if (CurrentMap.Info.NoHero)
            {
                ReceiveChat(LanguageMgr.Get("HeroCannotReviveOnMap"), ChatType.System);
                return;
            }
            if (CurrentHero == null) return;
            if (CurrentHero.HP != 0) return;

            if (Hero != null)
            {
                if (Hero.Node != null)
                {
                    Hero.Revive(Hero.Stats[Stat.HP], true);
                }
                else
                {
                    CurrentHero.HP = Hero.Stats[Stat.HP];
                    Hero.Dead = false;
                    SpawnHero(Hero);
                }
                Enqueue(new S.UpdateHeroSpawnState { State = HeroSpawnState.Summoned });
            }
            else CurrentHero.HP = -1;
        }

        public void SealHero()
        {
            if (CurrentHero == null) return;
            if (FreeSpace(Info.Inventory) == 0) return;
            if (Settings.HeroSealItemName == string.Empty) return;

            if (Settings.HeroMaximumSealCount > 0 && CurrentHero.SealCount >= Settings.HeroMaximumSealCount)
            {
                ReceiveChat(LanguageMgr.Get("HeroCannotBeSealed"), ChatType.System);
                return;
            }

            ItemInfoTable itemInfo = Envir.GetItemInfo(Settings.HeroSealItemName);
            if (itemInfo == null) return;

            if (Hero != null)
            {
                DespawnHero();
                Info.HeroSpawned = false;
                Enqueue(new S.UpdateHeroSpawnState { State = HeroSpawnState.None });
            }

            UserItem item = Envir.CreateFreshItem(itemInfo);
            item.AddedStats[Stat.Hero] = CurrentHero.Index;
            if (CanGainItem(item))
                GainItem(item);

            CurrentHero.SealCount++;
            Info.Heroes[CurrentHeroIndex] = null;
            CurrentHero = null;
        }

        public void DeleteHero()
        {
            if (CurrentHero == null) return;

            if (Hero != null)
            {
                DespawnHero();
                Info.HeroSpawned = false;
                Enqueue(new S.UpdateHeroSpawnState { State = HeroSpawnState.None });
            }

            Info.Heroes[CurrentHeroIndex] = null;
            CurrentHero = null;
            ReceiveChat(LanguageMgr.Get("HeroReleasedFromService"), ChatType.System);
        }

        private bool AddHero(HeroInfo hero)
        {
            int heroCount = Info.Heroes.Count(x => x != null);

            if (heroCount >= Info.MaximumHeroCount)
            {
                ReceiveChat(LanguageMgr.Get("YouCannotSummonMoreHeroes"), ChatType.System);
                return false;
            }

            for (int i = 0; i < Info.Heroes.Length; i++)
            {
                if (Info.Heroes[i] != null) continue;

                Info.Heroes[i] = hero;
                if (!HasHero)
                {
                    CurrentHero = hero;
                    SummonHero();
                }
                else
                {
                    ReceiveChat(LanguageMgr.Get("HeroAddedToStorage"), ChatType.System);
                    Enqueue(new S.NewHeroInfo { Info = hero.ClientInformation, StorageIndex = i - 1 });
                }

                return true;
            }

            return false;
        }

        public void ManageHeroes()
        {
            S.ManageHeroes p = new S.ManageHeroes() { MaximumCount = Info.MaximumHeroCount, CurrentHero = CurrentHero?.ClientInformation };

            if (!Connection.HeroStorageSent)
            {
                p.Heroes = new ClientHeroInformation[Info.Heroes.Length - 1];
                for (int i = 1; i < Info.Heroes.Length; i++)
                    p.Heroes[i - 1] = Info.Heroes[i]?.ClientInformation;
                Connection.HeroStorageSent = true;
            }

            Enqueue(p);
        }

        public void SendNPCGoods(List<UserItem> goods, float rate, PanelType panelType, bool hideAddedStats = false)
        {
            var currencyTypes = new List<byte>();
            var prices = new List<uint>();
            foreach (var item in goods)
            {
                currencyTypes.Add(item.ShopCurrencyType);
                prices.Add(item.ShopPrice > 0 ? item.ShopPrice : item.Price()); // 无 shop 价的回落到 Info.Price
            }

            Enqueue(new S.NPCGoods
            {
                List = goods,
                Rate = rate,
                Type = panelType,
                HideAddedStats = hideAddedStats,
                CurrencyTypes = currencyTypes,
                Prices = prices
            });
        }
    }
}
