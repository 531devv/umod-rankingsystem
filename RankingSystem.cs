using System;
using System.Collections.Generic;
using System.Text;
using Oxide.Core.Database;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;
using System.Collections;
using Oxide.Core;
using UnityEngine.Scripting;
using System.Linq;
using Oxide.Core.MySql;
using System.Text.RegularExpressions;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("RankingSystem", "531devv (531devv@gmail.com)", "1.6.7")]
    [Description("Ranking system.")]

    class RankingSystem : HurtworldPlugin
    {

        #region ClassRegion

        class RankingData
        {
            public Dictionary<string, RankingPlayer> Players = new Dictionary<string, RankingPlayer>();
            public Dictionary<string, RankingGuild> Guilds = new Dictionary<string, RankingGuild>();

            public RankingData()
            {
            }
        }

        class RankingGuild
        {
            public string Tag;
            public string OwnerID;
            public int Kills;
            public int Level;
            public int Coins;
            public string CoinInterval;
            public Dictionary<string, int> GuildSkills;

            public RankingGuild(string tag, string ownerID)
            {
                Tag = tag;
                OwnerID = ownerID;
                Kills = 0;
                Level = 1;
                Coins = 0;
                CoinInterval = "0";
                GuildSkills = new Dictionary<string, int>();

                GuildSkills.Add("HealthPotion", 0);
                GuildSkills.Add("Bandage", 0);
                GuildSkills.Add("InfamyPotion", 0);
            }
        }

        class RankingPlayer
        {
            public string SteamID;
            public string Name;
            public int Kills;
            public int Deaths;
            public double KDR;
            public string Guild;
            public string GuildInvite;
            public string CommandInterval;
            public string HealthPotionInterval;
            public string InfamyPotionInterval;
            public string BandageInterval;
            public Dictionary<string, string> Victims;
            public Dictionary<int, int> GuildCreateItems;

            public RankingPlayer()
            {

            }

            public RankingPlayer(PlayerSession player)
            {
                SteamID = player.SteamId.ToString();
                Name = player.Name;
                Kills = 0;
                Deaths = 0;
                KDR = 0;
                Guild = "null";
                GuildInvite = "null";
                CommandInterval = "0";
                HealthPotionInterval = "0";
                BandageInterval = "0";
                InfamyPotionInterval = "0";
                Victims = new Dictionary<string, string>();
                GuildCreateItems = new Dictionary<int, int>();

                GuildCreateItems.Add(134, 0); // Colors
                GuildCreateItems.Add(135, 0); // Colors 
                GuildCreateItems.Add(136, 0); // Colors
            }
        }

        #endregion

        RankingData rankingData;
        static readonly DateTime epoch = new DateTime(2017, 1, 13, 17, 44, 0);
        static double CurrentTime() { return DateTime.UtcNow.Subtract(epoch).TotalSeconds; }

        [PluginReference("PvpArena")]
        Plugin PvpArena;

        void Loaded()
        {
            rankingData = Interface.GetMod().DataFileSystem.ReadObject<RankingData>("RankingData");
        }

        void OnPlayerConnected(PlayerSession player)
        {
            RankingPlayer p = new RankingPlayer(player);
            if (!rankingData.Players.ContainsKey(p.SteamID))
            {
                rankingData.Players.Add(p.SteamID, p);
                SaveData();
            }
        }

        void OnPlayerDeath(PlayerSession player, EntityEffectSourceData dataSource)
        {
            if ((string)PvpArena?.Call("isPlayerOnArena", player) != "true")
            {
                RankingPlayer victim = new RankingPlayer(player);
                rankingData.Players[victim.SteamID].Deaths++;
                rankingData.Players[victim.SteamID].KDR = Math.Round((Convert.ToDouble(rankingData.Players[victim.SteamID].Kills) / (Convert.ToDouble(rankingData.Players[victim.SteamID].Deaths))), 3);
                var killer_name = GetNameOfObject(dataSource.EntitySource);
                if (killer_name.Length >= 3)
                {
                    var KillerSession = GetPlayerSessionFromDataSource(dataSource);
                    GiveRewardForKills(KillerSession);
                    RankingPlayer killer = new RankingPlayer(KillerSession);
                    hurt.BroadcastChat("<color=red>[☠]</color><i> " + player.Name + " został wyjaśniony przez " + KillerSession.Name + "</i>");
                    if (!rankingData.Players[killer.SteamID].Victims.ContainsKey(victim.SteamID))
                    {
                        if ((!isGuildFriend(player, KillerSession)) && (!isFriend(GameManager.Instance.GetIdentity(player.Player), GameManager.Instance.GetIdentity(KillerSession.Player))))
                        {
                            hurt.SendChatMessage(KillerSession, "<color=#3359cc>[Ranking]</color>: Zabójstwo zaliczono!");
                            rankingData.Players[killer.SteamID].Kills++;
                            if (rankingData.Players[killer.SteamID].Guild != "null")
                            {
                                rankingData.Guilds[rankingData.Players[killer.SteamID].Guild].Kills++;
                            }
                            var killInterval = CurrentTime() + 300;
                            rankingData.Players[killer.SteamID].Victims.Add(player.SteamId.ToString(), killInterval.ToString());

                            if (isGuildOwner(player))
                            {
                                var coinInterval = rankingData.Guilds[rankingData.Players[victim.SteamID].Guild].CoinInterval;
                                var coinIntervalFinish = Convert.ToDouble(coinInterval) - CurrentTime();
                                if ((int)coinIntervalFinish <= 0)
                                {
                                    hurt.SendChatMessage(KillerSession, "<color=#3359cc>[Gildia]</color>: Udało ci się zdobyć monetę, brawo!");
                                    rankingData.Guilds[rankingData.Players[killer.SteamID].Guild].Coins++;
                                    rankingData.Guilds[rankingData.Players[victim.SteamID].Guild].CoinInterval = (CurrentTime() + 3600).ToString();
                                }
                            }
                        }
                        else
                        {
                            hurt.SendChatMessage(KillerSession, "<color=#3359cc>[Ranking]</color>: Nie zaliczymy ci zabójstwa swojego!");
                        }
                    }
                    else
                    {
                        if ((!isGuildFriend(player, KillerSession)) && (!isFriend(GameManager.Instance.GetIdentity(player.Player), GameManager.Instance.GetIdentity(KillerSession.Player))))
                        {
                            var killInterval = rankingData.Players[killer.SteamID].Victims[victim.SteamID];
                            var killIntervalFinish = Convert.ToDouble(killInterval) - CurrentTime();
                            if (killIntervalFinish <= 0)
                            {
                                hurt.SendChatMessage(KillerSession, "<color=#3359cc>[Ranking]</color>: Zabójstwo zaliczono!");
                                rankingData.Players[killer.SteamID].Kills++;
                                if (rankingData.Players[killer.SteamID].Guild != "null")
                                {
                                    rankingData.Guilds[rankingData.Players[killer.SteamID].Guild].Kills++;
                                }
                                rankingData.Players[killer.SteamID].Victims[victim.SteamID] = (CurrentTime() + 25).ToString();

                                if (isGuildOwner(player))
                                {
                                    var coinInterval = rankingData.Guilds[rankingData.Players[victim.SteamID].Guild].CoinInterval;
                                    var coinIntervalFinish = Convert.ToDouble(coinInterval) - CurrentTime();
                                    if ((int)coinIntervalFinish <= 0)
                                    {
                                        rankingData.Guilds[rankingData.Players[killer.SteamID].Guild].Coins++;
                                        rankingData.Guilds[rankingData.Players[victim.SteamID].Guild].CoinInterval = (CurrentTime() + 3600).ToString();
                                        hurt.SendChatMessage(KillerSession, "<color=#3359cc>[Gildia]</color>: Udało ci się zdobyć monetę, brawo!");
                                    }
                                }
                            }
                            else
                            {
                                hurt.SendChatMessage(KillerSession, "<color=#3359cc>[Ranking]</color>: Zbyt szybko zabiłeś tego samego gracza!");
                            }
                        }
                        else
                        {
                            hurt.SendChatMessage(KillerSession, "<color=#3359cc>[Ranking]</color>: Nie zaliczymy ci zabójstwa swojego!");
                        }

                    }
                }
                SaveData();
            }
        }

        void guildCreateItems(PlayerSession player, int itemID, int amount, string itemName)
        {
            RankingPlayer p = new RankingPlayer(player);
            var item = player.WorldPlayerEntity.GetComponent<PlayerInventory>().FindItem(itemID, amount).StackSize;
            if ((item == amount) && (rankingData.Players[p.SteamID].GuildCreateItems[itemID] < amount) && (rankingData.Players[p.SteamID].Guild == "null"))
            {
                hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Odłożyłeś " + amount + "x " + itemName + " na stworzenie gildii!");
                rankingData.Players[p.SteamID].GuildCreateItems[itemID] = amount;
                SaveData();
            }
            else if (rankingData.Players[p.SteamID].GuildCreateItems[itemID] >= amount)
            {
                hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Masz już " + amount + "x " + itemName + " na gildie!");
                GivePlayerItem(player, itemID, item);
            }
            else if (item < amount)
            {
                hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Masz za mało " + itemName + "! Potrzebne jest " + amount + "x " + itemName + " do oddania!");
                GivePlayerItem(player, itemID, item);
            }
            else if (rankingData.Players[p.SteamID].Guild != "null")
            {
                hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Jesteś w gildii, nie możesz wpłacać przedmiotów na założenie!");
                GivePlayerItem(player, itemID, item);
            }
        }

        void GivePlayerItem(PlayerSession player, int itemID, int amount)
        {
            var ItemMgr = Singleton<GlobalItemManager>.Instance;
            ItemMgr.GiveItem(player.Player, ItemMgr.GetItem(itemID), amount);
        }

        void GiveRewardForGuildLevel(PlayerSession player)
        {
            var guildLevel = rankingData.Guilds[rankingData.Players[player.SteamId.ToString()].Guild].Level;
            switch (guildLevel)
            {
                case 2:
                    GivePlayerItem(player, 134, 150);
                    GivePlayerItem(player, 135, 150);
                    GivePlayerItem(player, 136, 150);
                    break;
                case 3:
                    GivePlayerItem(player, 98, 5);
                    GivePlayerItem(player, 23, 255);
                    break;
                case 4:
                    GivePlayerItem(player, 191, 255);
                    GivePlayerItem(player, 144, 1);
                    rankingData.Guilds[rankingData.Players[player.SteamId.ToString()].Guild].GuildSkills["InfamyPotion"] = 1;
                    SaveData();
					hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Każdy członek uzyskał miksture infamy! Użycie: '6' na klawiaturze!");
					hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Miksture można użyc co 3h!");
                    break;
                case 5:
                    GivePlayerItem(player, 144, 3);
                    break;
            }
        }

        void GiveRewardForKills(PlayerSession player)
        {
            var kills = rankingData.Players[player.SteamId.ToString()].Kills + 1;
            switch (kills)
            {
                case 20:
                    GivePlayerItem(player, 279, 1);
                    GivePlayerItem(player, 280, 36);
                    rankingData.Players[player.SteamId.ToString()].Kills++;
                    SaveData();
                    hurt.SendChatMessage(player, "<color=#3359cc>[Ranking]</color>: Informacja: Osiągnąłeś 20 zabójstw, otrzymujesz pistolet + 36ammo.");
                    break;
                case 40:
                    GivePlayerItem(player, 5, 20);
                    rankingData.Players[player.SteamId.ToString()].Kills++;
                    SaveData();
                    hurt.SendChatMessage(player, "<color=#3359cc>[Ranking]</color>: Informacja: Osiągnąłeś 40 zabójstw, otrzymujesz 20 pieczonego steku.");
                    break;
                case 60:
                    GivePlayerItem(player, 146, 1);
                    rankingData.Players[player.SteamId.ToString()].Kills++;
                    SaveData();
                    hurt.SendChatMessage(player, "<color=#3359cc>[Ranking]</color>: Informacja: Osiągnąłeś 60 zabójstw, otrzymujesz łuk kary.");
                    break;
                case 80:
                    GivePlayerItem(player, 131, 100);
                    rankingData.Players[player.SteamId.ToString()].Kills++;
                    SaveData();
                    hurt.SendChatMessage(player, "<color=#3359cc>[Ranking]</color>: Informacja: Osiągnąłeś 80 zabójstw, otrzymujesz 100 czerwonego.");
                    break;
                case 100:
                    GivePlayerItem(player, 132, 100);
                    GivePlayerItem(player, 231, 1);
                    rankingData.Players[player.SteamId.ToString()].Kills++;
                    SaveData();
                    hurt.SendChatMessage(player, "<color=#3359cc>[Ranking]</color>: Informacja: Osiągnąłeś 100 zabójstw, otrzymujesz 100 zielonego, 1x deto.");
                    break;
                case 120:
                    GivePlayerItem(player, 133, 100);
                    rankingData.Players[player.SteamId.ToString()].Kills++;
                    SaveData();
                    hurt.SendChatMessage(player, "<color=#3359cc>[Ranking]</color>: Informacja: Osiągnąłeś 120 zabójstw, otrzymujesz 100 niebieskiego.");
                    break;
                case 140:
                    GivePlayerItem(player, 220, 1);
                    rankingData.Players[player.SteamId.ToString()].Kills++;
                    SaveData();
                    hurt.SendChatMessage(player, "<color=#3359cc>[Ranking]</color>: Informacja: Osiągnąłeś 140 zabójstw, otrzymujesz sombrero.");
                    break;
                case 160:
                    GivePlayerItem(player, 125, 1);
                    GivePlayerItem(player, 191, 20);
                    rankingData.Players[player.SteamId.ToString()].Kills++;
                    SaveData();
                    hurt.SendChatMessage(player, "<color=#3359cc>[Ranking]</color>: Informacja: Osiągnąłeś 160 zabójstw, otrzymujesz pompę + 20ammo.");
                    break;
                case 180:
                    GivePlayerItem(player, 296, 1);
                    rankingData.Players[player.SteamId.ToString()].Kills++;
                    SaveData();
                    hurt.SendChatMessage(player, "<color=#3359cc>[Ranking]</color>: Informacja: Osiągnąłeś 180 zabójstw, otrzymujesz legendarny silnik do quada.");
                    break;
                case 200:
                    GivePlayerItem(player, 144, 1);
                    rankingData.Players[player.SteamId.ToString()].Kills++;
                    SaveData();
                    hurt.SendChatMessage(player, "<color=#3359cc>[Ranking]</color>: Informacja: Osiągnąłeś 200 zabójstw, otrzymujesz 1x C4.");
                    break;
                case 220:
                    GivePlayerItem(player, 23, 255);
                    rankingData.Players[player.SteamId.ToString()].Kills++;
                    SaveData();
                    hurt.SendChatMessage(player, "<color=#3359cc>[Ranking]</color>: Informacja: Osiągnąłeś 220 zabójstw, otrzymujesz 1x stack tłuszczu.");
                    break;
                case 240:
                    GivePlayerItem(player, 87, 120);
                    rankingData.Players[player.SteamId.ToString()].Kills++;
                    SaveData();
                    hurt.SendChatMessage(player, "<color=#3359cc>[Ranking]</color>: Informacja: Osiągnąłeś 240 zabójstw, otrzymujesz 120 bursztynu.");
                    break;
                case 260:
                    GivePlayerItem(player, 115, 1);
                    GivePlayerItem(player, 125, 2);
                    rankingData.Players[player.SteamId.ToString()].Kills++;
                    SaveData();
                    hurt.SendChatMessage(player, "<color=#3359cc>[Ranking]</color>: Informacja: Osiągnąłeś 260 zabójstw, otrzymujesz wąsy + 2x pompa.");
                    break;
                case 280:
                    GivePlayerItem(player, 125, 1);
                    GivePlayerItem(player, 98, 1);
                    GivePlayerItem(player, 52, 100);
                    rankingData.Players[player.SteamId.ToString()].Kills++;
                    SaveData();
                    hurt.SendChatMessage(player, "<color=#3359cc>[Ranking]</color>: Informacja: Osiągnąłeś 280 zabójstw, otrzymujesz pompę, m4 auto, 100ammo.");
                    break;
                case 300:
                    GivePlayerItem(player, 144, 1);
                    rankingData.Players[player.SteamId.ToString()].Kills++;
                    SaveData();
                    hurt.SendChatMessage(player, "<color=#3359cc>[Ranking]</color>: Informacja: Osiągnąłeś 300 zabójstw, otrzymujesz 1x C4.");
                    break;
                case 320:
                    GivePlayerItem(player, 130, 1);
                    rankingData.Players[player.SteamId.ToString()].Kills++;
                    SaveData();
                    hurt.SendChatMessage(player, "<color=#3359cc>[Ranking]</color>: Informacja: Osiągnąłeś 320 zabójstw, otrzymujesz czerw. kurtke.");
                    break;
                case 340:
                    GivePlayerItem(player, 87, 100);
                    GivePlayerItem(player, 23, 255);
                    rankingData.Players[player.SteamId.ToString()].Kills++;
                    SaveData();
                    hurt.SendChatMessage(player, "<color=#3359cc>[Ranking]</color>: Informacja: Osiągnąłeś 340 zabójstw, otrzymujesz stack tłuszczu + 100 bursztynu.");
                    break;
                case 360:
                    GivePlayerItem(player, 191, 120);
                    GivePlayerItem(player, 155, 50);
                    rankingData.Players[player.SteamId.ToString()].Kills++;
                    SaveData();
                    hurt.SendChatMessage(player, "<color=#3359cc>[Ranking]</color>: Informacja: Osiągnąłeś 360 zabójstw, otrzymujesz 50x ammo do pompy i 50 dynamitu.");
                    break;
                case 380:
                    GivePlayerItem(player, 144, 1);
                    rankingData.Players[player.SteamId.ToString()].Kills++;
                    SaveData();
                    hurt.SendChatMessage(player, "<color=#3359cc>[Ranking]</color>: Informacja: Osiągnąłeś 380 zabójstw, otrzymujesz 1x C4.");
                    break;
                case 400:
                    GivePlayerItem(player, 125, 1);
                    GivePlayerItem(player, 131, 50);
                    GivePlayerItem(player, 132, 50);
                    GivePlayerItem(player, 133, 50);
                    GivePlayerItem(player, 87, 100);
                    GivePlayerItem(player, 23, 255);
                    rankingData.Players[player.SteamId.ToString()].Kills++;
                    SaveData();
                    hurt.SendChatMessage(player, "<color=#3359cc>[Ranking]</color>: Informacja: Osiągnąłeś 400 zabójstw, otrzymujesz pompe, 100 bursztynu, 1x stack tłuszczu i x50 kolorków.");
                    break;
            }
        }

        void OnPlayerInput(PlayerSession player, InputControls input)
        {
            if (rankingData.Players[player.SteamId.ToString()].Guild != "null")
            {
                EntityStats stats = player.WorldPlayerEntity.GetComponent<EntityStats>();

                if (input.Hotbar6)
                {
                    if(isPotionIntervalTimeOut(player, "InfamyPotion"))
                    {
                        if (rankingData.Guilds[rankingData.Players[player.SteamId.ToString()].Guild].GuildSkills["InfamyPotion"] == 1)
                        {
                            AlertManager.Instance?.GenericTextNotificationServer("Użyłeś mikstury Infamy!", player.Player);
                            stats.GetFluidEffect(EEntityFluidEffectType.Infamy).SetValue(0f);
                            setPotionInterval(player, "InfamyPotion");
                        }
                    }
                    else
                    {
                        AlertManager.Instance?.GenericTextNotificationServer("Mikstura infamy się ładuje!", player.Player);
                    }
                }

                if (input.Hotbar7)
                {
                    if (isPotionIntervalTimeOut(player, "HealthPotion"))
                    {
                        if (rankingData.Guilds[rankingData.Players[player.SteamId.ToString()].Guild].GuildSkills["HealthPotion"] == 1)
                        {
                            AlertManager.Instance?.GenericTextNotificationServer("Użyłeś mikstury HP(1lvl)!", player.Player);
                            timer.Repeat(1f, 6, () =>
                            {
                                stats.GetFluidEffect(EEntityFluidEffectType.Health).SetValue(Convert.ToInt32(stats.GetFluidEffect(EEntityFluidEffectType.Health).GetValue()) + 10f);
                            });
                            setPotionInterval(player, "HealthPotion");
                        }
                        else if (rankingData.Guilds[rankingData.Players[player.SteamId.ToString()].Guild].GuildSkills["HealthPotion"] == 2)
                        {
                            AlertManager.Instance?.GenericTextNotificationServer("Użyłeś mikstury HP(2lvl)!", player.Player);
                            timer.Repeat(1f, 5, () =>
                            {
                                stats.GetFluidEffect(EEntityFluidEffectType.Health).SetValue(Convert.ToInt32(stats.GetFluidEffect(EEntityFluidEffectType.Health).GetValue()) + 20f);
                            });
                            setPotionInterval(player, "HealthPotion");
                        }
                    }
                    else
                    {
                        AlertManager.Instance?.GenericTextNotificationServer("Mikstura HP się ładuje!", player.Player);
                    }
                }

                if (input.Hotbar8)
                {
                    if (isPotionIntervalTimeOut(player, "Bandage"))
                    {
                        if (rankingData.Guilds[rankingData.Players[player.SteamId.ToString()].Guild].GuildSkills["Bandage"] == 1)
                        {
                            AlertManager.Instance?.GenericTextNotificationServer("Użyłeś bandaż!", player.Player);
                            stats.RemoveBinaryEffect(EEntityBinaryEffectType.BrokenLeg);
                            setPotionInterval(player, "Bandage");
                        }
                    }
                    else
                    {
                        AlertManager.Instance?.GenericTextNotificationServer("Chwilowo nie posiadasz bandaży!", player.Player);
                    }
                }
            }
        }

        [ChatCommand("gildia")]
        void guildCmd(PlayerSession player, string command, string[] args)
        {
            if (isCommandIntervalTimeOut(player))
            {
                RankingPlayer p = new RankingPlayer(player);
                if (args.Length < 1)
                {
                    hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Spis komend:");
                    hurt.SendChatMessage(player, "/gildia 1");
                    hurt.SendChatMessage(player, "/gildia 2");
                    hurt.SendChatMessage(player, "/gildia 3 (Spis komend założyciela gildii)");
                    hurt.SendChatMessage(player, "/gildia 4 (Spis komend założyciela gildii)");
                }
                else if (args[0].Equals("1"))
                {
                    if (args.Length < 2)
                    {
                        hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Lista komend:");
                        hurt.SendChatMessage(player, "/gildia stworz <nazwa>");
                        hurt.SendChatMessage(player, "/gildia dolacz <nazwa>");
                        hurt.SendChatMessage(player, "/gildia top5");
                        hurt.SendChatMessage(player, "/gildia info <tag>");
                    }
                }
                else if (args[0].Equals("2"))
                {
                    if (args.Length < 2)
                    {
                        hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Lista komend:");
                        hurt.SendChatMessage(player, "/gildia przedmioty <c4/yeti/sasquatch/blue/red/green>");
                        hurt.SendChatMessage(player, "/gildia opusc");
                        hurt.SendChatMessage(player, "/gildia poziom");
                        hurt.SendChatMessage(player, "/gildia rubin");
                    }
                }
                else if (args[0].Equals("3"))
                {
                    if (args.Length < 2)
                    {
                        hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Lista komend:");
                        hurt.SendChatMessage(player, "/gildia zapros");
                        hurt.SendChatMessage(player, "/gildia rozwiaz");
                        hurt.SendChatMessage(player, "/gildia wyrzuc <nickname>");
                        hurt.SendChatMessage(player, "/gildia kowadlo");
                    }
                }
                else if (args[0].Equals("4"))
                {
                    if (args.Length < 2)
                    {
                        hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Lista komend:");
                        hurt.SendChatMessage(player, "/gildia poziom");
                    }
                }
                else if (args[0].Equals("kowadlo"))
                {
                    if (args.Length < 2)
                    {
                        if (rankingData.Players[player.SteamId.ToString()].Guild != "null")
                        {
                            if (isGuildOwner(player))
                            {
                                hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: W tym miejscu możesz wykuć specjalne przedmioty!");
                                hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: /gildia kowadlo luk (łuk do pvp)(5monet)");
                                hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: /gildia kowadlo hp (mikstura hp)(5/10monet)");
                                hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: /gildia kowadlo bandaz (leczy połamane nogi)(3monety)");
                            }
                            else
                            {
                                hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Nie jesteś liderem!");
                            }
                        }
                    }
                    else if (args[1].Equals("luk"))
                    {
                        if (args.Length < 3)
                        {
                            int coins = rankingData.Guilds[rankingData.Players[player.SteamId.ToString()].Guild].Coins;
                            hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Posiadane monety: " + coins);
                            hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Monety potrzebne do wykucia: 5");
                            hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Komenda do wykucia: /gildia kowadlo luk wykuj");
                        }
                        else if (args[2].Equals("wykuj"))
                        {
                            if (args.Length < 4)
                            {
                                if (rankingData.Guilds[rankingData.Players[player.SteamId.ToString()].Guild].Level > 1)
                                {
                                    int coins = rankingData.Guilds[rankingData.Players[player.SteamId.ToString()].Guild].Coins;
                                    if (coins >= 5)
                                    {
                                        GivePlayerItem(player, 92, 1);
                                        rankingData.Guilds[rankingData.Players[player.SteamId.ToString()].Guild].Coins = rankingData.Guilds[rankingData.Players[player.SteamId.ToString()].Guild].Coins - 5;
                                        SaveData();
                                        hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Udało ci się wykuć łuk, jest w twoim eq! (lub na ziemi)");
                                        hurt.BroadcastChat("<color=#3359cc>[Gildia]</color>: [" + rankingData.Guilds[rankingData.Players[player.SteamId.ToString()].Guild].Tag + "] - wykuła łuk do pvp!");
                                    }
                                }
                                else
                                {
                                    hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Twoja gildia musi posiadać minimum 2 poziom!");
                                }
                            }
                        }
                    }
                    else if (args[1].Equals("bandaz"))
                    {
                        if (args.Length < 3)
                        {
                            int coins = rankingData.Guilds[rankingData.Players[player.SteamId.ToString()].Guild].Coins;
                            hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Posiadane monety: " + coins);
                            hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Monety potrzebne do wykucia: 3");
                            hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Komenda do wykucia: /gildia kowadlo bandaz wykuj");
                        }
                        else if (args[2].Equals("wykuj"))
                        {
                            if (args.Length < 4)
                            {
                                int coins = rankingData.Guilds[rankingData.Players[player.SteamId.ToString()].Guild].Coins;
                                if (coins >= 3)
                                {
                                    rankingData.Guilds[rankingData.Players[player.SteamId.ToString()].Guild].GuildSkills["Bandage"] = 1;
                                    rankingData.Guilds[rankingData.Players[player.SteamId.ToString()].Guild].Coins = rankingData.Guilds[rankingData.Players[player.SteamId.ToString()].Guild].Coins - 3;
                                    SaveData();
                                    hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Udało ci się stworzyć bandaże, członkowie gildii mogą ich używać!");
                                    hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Po wciśnieciu na klawiaturze '8' (raz na 5min)");
                                    hurt.BroadcastChat("<color=#3359cc>[Gildia]</color>: [" + rankingData.Guilds[rankingData.Players[player.SteamId.ToString()].Guild].Tag + "] - stworzyła bandaże!");
                                }
                                else
                                {
                                    hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Nie posiadasz wystarczająco monet!");
                                }
                            }
                        }
                    }
                    else if (args[1].Equals("hp"))
                    {
                        if (args.Length < 3)
                        {
                            int coins = rankingData.Guilds[rankingData.Players[player.SteamId.ToString()].Guild].Coins;
                            hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Posiadane monety: " + coins);
                            hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Monety potrzebne do wykucia: 1lvl - 5monet, 2lvl - 10monet");
                            hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: 1lvl regeneruje 60hp, 2lvl renegeruje 100hp");
                            hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Komenda do wykucia: /gildia kowadlo hp wykuj");
                        }
                        else if (args[2].Equals("wykuj"))
                        {
                            if (args.Length < 4)
                            {
                                if (rankingData.Guilds[rankingData.Players[player.SteamId.ToString()].Guild].Level > 1)
                                {
                                    int coins = rankingData.Guilds[rankingData.Players[player.SteamId.ToString()].Guild].Coins;
                                    if (rankingData.Guilds[rankingData.Players[player.SteamId.ToString()].Guild].GuildSkills["HealthPotion"] == 0)
                                    {
                                        if (coins >= 5)
                                        {
                                            rankingData.Guilds[rankingData.Players[player.SteamId.ToString()].Guild].GuildSkills["HealthPotion"] = 1;
                                            rankingData.Guilds[rankingData.Players[player.SteamId.ToString()].Guild].Coins = rankingData.Guilds[rankingData.Players[player.SteamId.ToString()].Guild].Coins - 3;
                                            SaveData();
                                            hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Udało ci się stworzyć mikstury HP, członkowie gildii mogą ich używać!");
                                            hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Po wciśnieciu na klawiaturze '7'! (raz na 7min)");
                                            hurt.BroadcastChat("<color=#3359cc>[Gildia]</color>: [" + rankingData.Guilds[rankingData.Players[player.SteamId.ToString()].Guild].Tag + "] - miksture HP 1lvl!");
                                        }
                                        else
                                        {
                                            hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Nie posiadasz wystarczająco monet!");
                                        }
                                    }
                                    else if (rankingData.Guilds[rankingData.Players[player.SteamId.ToString()].Guild].GuildSkills["HealthPotion"] == 1)
                                    {
                                        if (coins >= 10)
                                        {
                                            rankingData.Guilds[rankingData.Players[player.SteamId.ToString()].Guild].GuildSkills["HealthPotion"] = 2;
                                            rankingData.Guilds[rankingData.Players[player.SteamId.ToString()].Guild].Coins = rankingData.Guilds[rankingData.Players[player.SteamId.ToString()].Guild].Coins - 3;
                                            SaveData();
                                            hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Udało ci się stworzyć mikstury HP, członkowie gildii mogą ich używać!");
                                            hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Po wciśnieciu na klawiaturze '7'! (raz na 7min)");
                                            hurt.BroadcastChat("<color=#3359cc>[Gildia]</color>: [" + rankingData.Guilds[rankingData.Players[player.SteamId.ToString()].Guild].Tag + "] - miksture HP 2lvl!");
                                        }
                                        else
                                        {
                                            hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Nie posiadasz wystarczająco monet!");
                                        }
                                    }
                                }
                                else
                                {
                                    hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Twoja gildia musi posiadać minimum 2 poziom!");
                                }
                            }
                        }
                    }
                }
                else if (args[0].Equals("poziom"))
                {
                    if (args.Length < 2)
                    {
                        hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Lista komend:");
                        hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: /gildia poziom info (informacje nt. poziomu i korzyści)");
                        hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: /gildia poziom ulepsz (ulepszanie poziomu gildii)");
                    }
                    else if (args[1].Equals("info"))
                    {
                        if (args.Length < 3)
                        {
                            if (rankingData.Players[player.SteamId.ToString()].Guild != "null")
                            {
                                hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Poziom gildii: " + rankingData.Guilds[rankingData.Players[player.SteamId.ToString()].Guild].Level);
                                hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Wymagane monety na następny poziom: " + (rankingData.Guilds[rankingData.Players[player.SteamId.ToString()].Guild].Level * 5).ToString());
                                hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Posiadane monety: " + rankingData.Guilds[rankingData.Players[player.SteamId.ToString()].Guild].Coins);
                                hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: /gildia poziom ulepsz (ulepszanie poziomu gildii)");
                                hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: /gildia kowadlo");
                            }
                            else
                            {
                                hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Nie posiadasz gildii!");
                            }
                        }
                    }
                    else if (args[1].Equals("ulepsz"))
                    {
                        if (args.Length < 3)
                        {
                            if (rankingData.Players[player.SteamId.ToString()].Guild != "null")
                            {
                                if (isGuildOwner(player))
                                {
                                    int guildLevel = rankingData.Guilds[rankingData.Players[player.SteamId.ToString()].Guild].Level;
                                    int guildCoins = rankingData.Guilds[rankingData.Players[player.SteamId.ToString()].Guild].Coins;
                                    int guildCoinsToLevelUp = guildLevel * 5;
                                    if (guildCoins >= guildCoinsToLevelUp)
                                    {
                                        rankingData.Guilds[rankingData.Players[player.SteamId.ToString()].Guild].Coins = rankingData.Guilds[rankingData.Players[player.SteamId.ToString()].Guild].Coins - guildCoinsToLevelUp;
                                        rankingData.Guilds[rankingData.Players[player.SteamId.ToString()].Guild].Level++;
                                        SaveData();
                                        hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Udało się! Otrzymujesz nagrodę za poziom!");
                                        hurt.BroadcastChat("<color=#3359cc>[Gildia]</color>: [" + rankingData.Guilds[rankingData.Players[player.SteamId.ToString()].Guild].Tag + "] - awansowała, aktualny level gildii: " + rankingData.Guilds[rankingData.Players[player.SteamId.ToString()].Guild].Level);
                                        GiveRewardForGuildLevel(player);
                                    }
                                    else
                                    {
                                        hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Nie masz wystarczająco monet!");
                                    }
                                }
                                else
                                {
                                    hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Nie jesteś założycielem gildii!");
                                }
                            }
                            else
                            {
                                hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Nie posiadasz gildii!");
                            }
                        }
                    }
                }
                else if (args[0].Equals("wyrzuc"))
                {
                    if (args.Length < 2)
                    {
                        hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Musisz wpisać nick gracza!");
                        hurt.SendChatMessage(player, "/gildia wyrzuc <nickname>");
                    }
                    else
                    {
                        if (isGuildOwner(player))
                        {
                            var targetPlayer = GetPlayer(args[1], player);
                            if (targetPlayer == player)
                            {
                                hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Nie możesz wyrzucić samego siebie!");
                            }
                            else
                            {
                                if (targetPlayer != null)
                                {
                                    if (rankingData.Players[targetPlayer.SteamId.ToString()].Guild != "null")
                                    {
                                        if (rankingData.Players[targetPlayer.SteamId.ToString()].Guild == rankingData.Players[player.SteamId.ToString()].Guild)
                                        {
                                            hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Wyrzuciłeś z gildii gracza: " + targetPlayer.Name);
                                            hurt.SendChatMessage(targetPlayer, "<color=#3359cc>[Gildia]</color>: Zostałeś wyrzucony z gildii przez: " + player.Name);
                                            rankingData.Players[targetPlayer.SteamId.ToString()].Guild = "null";
                                            rankingData.Players[targetPlayer.SteamId.ToString()].GuildInvite = "null";
                                        }
                                        else
                                        {
                                            hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Nie możesz wyrzucić gracza z innej gildii!");
                                        }
                                    }
                                    else
                                    {
                                        hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Gracz nie jest w żadnej gildii!");
                                    }
                                }
                            }
                        }
                        else
                        {
                            hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Nie jesteś założycielem gildii!");
                        }
                    }
                }
                else if (args[0].Equals("top5"))
                {
                    var guilds = rankingData.Guilds.Values.Where(c => c.Tag != "null").OrderByDescending(z => z.Kills).Take(5);
                    if (guilds.Count() > 0)
                    {
                        foreach (var guild in guilds)
                        {
                            hurt.SendChatMessage(player, "<color=yellow>[" + guild.Tag + "]</color>: " + guild.Kills + " pkt. <color=yellow>Lider</color>: " + rankingData.Players[guild.OwnerID].Name);
                        }
                    }
                    else
                    {
                        hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Nikt nie stworzył jeszcze gildii!");
                    }
                }
                else if (args[0].Equals("info"))
                {
                    if (args.Length < 2)
                    {
                        hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Musisz wpisać tag gildii!");
                        hurt.SendChatMessage(player, "/gildia info <tag>");
                    }
                    else
                    {
                        string tagGuild = args[1].ToUpper();
                        if (isGuildExists(tagGuild))
                        {
                            var targetGuild = rankingData.Guilds[tagGuild];
                            int members = 0;
                            foreach (var rankingPlayer in rankingData.Players.Values)
                            {
                                if (rankingPlayer.Guild == args[1].ToUpper())
                                {
                                    members++;
                                }
                            }
                            hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Tag gildyjny: " + targetGuild.Tag);
                            hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Punkty: " + targetGuild.Kills);
                            hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Poziom: " + targetGuild.Level);
                            hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Ilość osób: " + (members.ToString()).ToString());
                            hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Monety: " + targetGuild.Coins);
                            members = 0;
                        }
                        else
                        {
                            hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Nie ma takiej gildii!");
                        }
                    }
                }
                else if (args[0].Equals("rozwiaz"))
                {
                    if (args.Length < 2)
                    {
                        hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Ta komenda niszczy całą gildię!");
                        hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Jeśli jesteś pewny że chcesz zniszczyć gildię wpisz:");
                        hurt.SendChatMessage(player, "/gildia rozwiaz potwierdz");
                    }
                    else if (isGuildOwner(player))
                    {
                        Server.Broadcast("<color=#3359cc>[Gildia]</color>: " + player.Name + "rozwiązał swoją gildię [" + rankingData.Players[player.SteamId.ToString()].Guild + "]");
                        rankingData.Guilds[rankingData.Players[player.SteamId.ToString()].Guild].OwnerID = "null";
                        rankingData.Guilds[rankingData.Players[player.SteamId.ToString()].Guild].Tag = "null";
                        var members = rankingData.Players.Values.Where(b => b.Guild == rankingData.Players[player.SteamId.ToString()].Guild);
                        foreach (var member in members)
                        {
                            member.Guild = "null";
                            member.GuildInvite = "null";
                        }
                        rankingData.Players[player.SteamId.ToString()].Guild = "null";
                        rankingData.Players[player.SteamId.ToString()].GuildInvite = "null";
                        SaveData();
                        hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Pomyślnie rozwiązałeś gildię!");
                    }
                    else
                    {
                        hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Nie jesteś założycielem gildi!");
                    }
                }
                else if (args[0].Equals("opusc"))
                {
                    if ((rankingData.Players[player.SteamId.ToString()].Guild != "null") && (!isGuildOwner(player)))
                    {
                        hurt.SendChatMessage(Player.FindById(rankingData.Guilds[rankingData.Players[player.SteamId.ToString()].Guild].OwnerID), "<color=#3359cc>[Gildia]</color>: " + player.Name + " opuścił twoją gildię.");
                        rankingData.Players[p.SteamID].Guild = "null";
                        rankingData.Players[p.SteamID].GuildInvite = "null";
                        hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Pomyślnie opuściłeś gildię!");
                        SaveData();
                    }
                    else if (isGuildOwner(player))
                    {
                        hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Nie możesz opuścic gildii kiedy jesteś jej założycielem!");
                        hurt.SendChatMessage(player, "Użyj: /gildia rozwiaz");
                    }
                    else if (rankingData.Players[player.SteamId.ToString()].Guild == "null")
                    {
                        hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Nie należysz do żadnej gildii!");
                    }
                }
                else if (args[0].Equals("rubin"))
                {
                    hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Możesz wykuć z rubinu monete dla gildii!");
                    hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Musisz mieć jeden rubin w ekwipunku żeby ta komenda działała!");
                    var item = player.WorldPlayerEntity.GetComponent<PlayerInventory>().FindItem(12, 1).StackSize;
                    if ((item == 1) && (rankingData.Players[player.SteamId.ToString()].Guild != "null"))
                    {
                        rankingData.Guilds[rankingData.Players[player.SteamId.ToString()].Guild].Coins++;
                        SaveData();
                        hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Oddałeś monete dla swojej gildii!");
                    }
                    else if (item < 1)
                    {
                        hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Masz za mało rubinu! Potrzebny jest jeden rubin do wykucia monety!");
                        GivePlayerItem(player, 12, item);
                    }
                    else if (rankingData.Players[player.SteamId.ToString()].Guild == "null")
                    {
                        hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Nie jesteś w żadnej gildii!");
                        GivePlayerItem(player, 12, item);
                    }
                }
                else if (args[0].Equals("dolacz"))
                {
                    hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Na dołączenie do gildii musisz mieć 20 bursztynu w ekwipunku!");
                    var item = player.WorldPlayerEntity.GetComponent<PlayerInventory>().FindItem(87, 20).StackSize;
                    if ((item == 20) && (rankingData.Players[player.SteamId.ToString()].Guild == "null") && (rankingData.Players[player.SteamId.ToString()].GuildInvite != "null"))
                    {
                        rankingData.Players[player.SteamId.ToString()].Guild = rankingData.Players[player.SteamId.ToString()].GuildInvite;
                        rankingData.Players[player.SteamId.ToString()].GuildInvite = "null";
                        SaveData();
                        hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Dołączyłeś do gildii " + rankingData.Players[player.SteamId.ToString()].Guild);
                        hurt.SendChatMessage(Player.FindById(rankingData.Guilds[rankingData.Players[player.SteamId.ToString()].Guild].OwnerID), "<color=#3359cc>[Gildia]</color>: " + player.Name + " dołączył do gildii.");
                    }
                    else if (item < 20)
                    {
                        hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Masz za mało Bursztynu! Potrzebne jest 20 Bursztynu do oddania!");
                        GivePlayerItem(player, 87, item);
                    }
                    else if (rankingData.Players[player.SteamId.ToString()].Guild != "null")
                    {
                        hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Jesteś juz w gildii!");
                        GivePlayerItem(player, 87, item);
                    }
                    else if (rankingData.Players[player.SteamId.ToString()].GuildInvite == "null")
                    {
                        hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Nie otrzymałeś żadnego zaproszenia!");
                        GivePlayerItem(player, 87, item);
                    }
                }
                else if (args[0].Equals("zapros"))
                {
                    if (args.Length < 2)
                    {
                        hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Poprawne użycie: /gildia zapros <nickname>");
                    }
                    else if (isGuildOwner(player))
                    {
                        var targetID = GetPlayer(args[1], player);
                        if (targetID == player)
                        {
                            hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Nie możesz zaprosić samego siebie!");
                        }
                        else
                        {
                            rankingData.Players[targetID.SteamId.ToString()].GuildInvite = rankingData.Players[player.SteamId.ToString()].Guild;
                            SaveData();
                            hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Wysłałeś zaproszenie graczowi " + targetID.Name);
                            hurt.SendChatMessage(targetID, "<color=#3359cc>[Gildia]</color>: Otrzymałeś zaproszenie do gildii od " + player.Name);
                            hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Aby dołączyć: /gildia dolacz");
                        }
                    }
                    else if (!isGuildOwner(player))
                    {
                        hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Nie jesteś założycielem gildii!");
                    }
                }
                else if (args[0].Equals("stworz"))
                {
                    if (args.Length < 2)
                    {
                        hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Poprawne użycie: ");
                        hurt.SendChatMessage(player, "/gildia stworz <nazwa>");
                    }
                    else if ((rankingData.Players[p.SteamID].Guild == "null") && (rankingData.Players[p.SteamID].Kills >= 20) && (args[1].Length >= 2) && (args[1].Length <= 4))
                    {
                        RankingGuild g = new RankingGuild(args[1].ToUpper(), p.SteamID);
                        if (!rankingData.Guilds.ContainsKey(g.Tag))
                        {
                            if (isValid(args[1]))
                            {
                                var Ore1 = rankingData.Players[p.SteamID].GuildCreateItems[134];
                                var Ore2 = rankingData.Players[p.SteamID].GuildCreateItems[135];
                                var Ore3 = rankingData.Players[p.SteamID].GuildCreateItems[136];

                                if ((255 - Ore1 == 0) && (255 - Ore2 == 0) && (255 - Ore3 == 0))
                                {
                                    rankingData.Guilds.Add(args[1].ToUpper(), g);
                                    rankingData.Players[p.SteamID].Guild = g.Tag;
                                    rankingData.Players[p.SteamID].GuildCreateItems[134] = 0;
                                    rankingData.Players[p.SteamID].GuildCreateItems[135] = 0;
                                    rankingData.Players[p.SteamID].GuildCreateItems[136] = 0;
                                    SaveData();
                                    hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Udało ci się założyć gildię: [" + g.Tag + "]");
                                    hurt.BroadcastChat("<color=#3359cc>[Gildia]</color>: Gracz " + player.Name + " założył gildię: [" + g.Tag + "]");
                                }
                                else
                                {
                                    hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Nie posiadasz wszystkich itemów na gildię!");
                                    hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Sprawdź co potrzebujesz pod komendą:");
                                    hurt.SendChatMessage(player, "/gildia przedmioty");
                                }
                            }
                            else
                            {
                                hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Nieprawidłowe znaki w tagu!");
                            }
                        }
                        else
                        {
                            hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Istnieje już taka gildia!");
                        }
                    }
                    else if (rankingData.Players[p.SteamID].Kills < 20)
                    {
                        hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Nie osiągnąłeś 20 zabójstw!");
                    }
                    else if (rankingData.Players[p.SteamID].Guild != "null")
                    {
                        hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Jesteś już w gildii!");
                    }
                    else
                    {
                        hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Tag gildii musi mieć od 2 do 4 znaków!");
                    }
                }
                else if (args[0].Equals("przedmioty"))
                {
                    if (args.Length < 2)
                    {
                        var Ore1 = rankingData.Players[p.SteamID].GuildCreateItems[134];
                        var Ore2 = rankingData.Players[p.SteamID].GuildCreateItems[135];
                        var Ore3 = rankingData.Players[p.SteamID].GuildCreateItems[136];

                        if ((255 - Ore1 == 0) && (255 - Ore2 == 0) && (255 - Ore3 == 0))
                        {
                            hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Możesz już założyć gildię!");
                            hurt.SendChatMessage(player, "/gildia stwórz <tag>");
                        }
                        else
                        {
                            hurt.SendChatMessage(player, "<color=#3359cc>[Gildia]</color>: Żeby założyć gildię potrzebujesz jeszcze:");
                            hurt.SendChatMessage(player, (255 - Ore1) + "x czerwone, " + (255 - Ore2) + "x zielone, " + (255 - Ore3) + "x niebieskie");
                            hurt.SendChatMessage(player, "/gildia przedmioty <red/blue/green>");
                        }
                    }
                    else if (args[1].Equals("red"))
                    {
                        guildCreateItems(player, 134, 255, "Przep. Czerw.");
                    }
                    else if (args[1].Equals("green"))
                    {
                        guildCreateItems(player, 135, 255, "Przep. Ziel.");
                    }
                    else if (args[1].Equals("blue"))
                    {
                        guildCreateItems(player, 136, 255, "Przep. Nieb.");
                    }
                }
                setCommandInterval(player);
            }
            else
            {
                hurt.SendChatMessage(player, "Odczekaj chwile przed ponownym użyciem komendy!");
            }
        }

        /* [ChatCommand("debug")]
        void debugCmd(PlayerSession player, string command, string[] args)
        {
            EntityStats stats = player.WorldPlayerEntity.GetComponent<EntityStats>();
            int hp = Convert.ToInt32(args[0]);
            stats.GetFluidEffect(EEntityFluidEffectType.Health).SetValue(hp);
        } */

        [ChatCommand("ranking")]
        void rankingCmd(PlayerSession player, string command, string[] args)
        {
            if (isCommandIntervalTimeOut(player))
            {
                RankingPlayer p = new RankingPlayer(player);
                hurt.SendChatMessage(player, "<color=#3359cc>[Ranking]</color>:  Zabójstwa: " + rankingData.Players[p.SteamID].Kills);
                hurt.SendChatMessage(player, "<color=#3359cc>[Ranking]</color>:  Śmierci: " + rankingData.Players[p.SteamID].Deaths);
                hurt.SendChatMessage(player, "<color=#3359cc>[Ranking]</color>:  KDR: " + rankingData.Players[p.SteamID].KDR);
                hurt.SendChatMessage(player, "<color=#3359cc>[Ranking]</color>:  Bestie: /top5");
                hurt.SendChatMessage(player, "<color=#3359cc>[Ranking]</color>:  Gildia: " + rankingData.Players[p.SteamID].Guild);
                hurt.SendChatMessage(player, "<color=#3359cc>[Ranking]</color>:  Zapro. gildii: " + rankingData.Players[p.SteamID].GuildInvite);
                setCommandInterval(player);
            }
            else
            {
                hurt.SendChatMessage(player, "Odczekaj chwile przed ponownym użyciem komendy!");
            }
        }

        [ChatCommand("top5")]
        void topCmd(PlayerSession player, string command, string[] args)
        {
            if (isCommandIntervalTimeOut(player))
            {
                var items = rankingData.Players.Values.Where(p => p.Kills >= 20).OrderByDescending(p => p.Kills).Take(5);
                int count = items.Count();
                if (count <= 0)
                {
                    hurt.SendChatMessage(player, "<color=#3359cc>[Ranking]</color>:  Informacja: Żaden gracz nie ma 20stu zabójstw!");
                }
                else
                {
                    foreach (var item in items)
                    {
                        hurt.SendChatMessage(player, "<color=#3359cc>[Ranking]</color>: <color=yellow>" + item.Name + "</color>: K:" + item.Kills + " D:" + item.Deaths + " K/D:" + item.KDR + "");
                    }
                }
                setCommandInterval(player);
            }
            else
            {
                hurt.SendChatMessage(player, "Odczekaj chwile przed ponownym użyciem komendy!");
            }
        }

        [ChatCommand("resetstats")]
        void resetkillsCmd(PlayerSession player, string command, string[] args)
        {
            if (args.Length < 1)
            {
                hurt.SendChatMessage(player, "Poprawne użycie: /resetkills <steamid>");
            }
            else if (!player.IsAdmin)
            {
                hurt.SendChatMessage(player, "Nie jesteś adminem ty kurwo opluta!");
            }
            else
            {
                if (player.IsAdmin)
                {
                    var target = args[0].ToString();
                    rankingData.Players[target].Kills = 0;
                    rankingData.Players[target].KDR = 0;
                    SaveData();
                    hurt.SendChatMessage(player, "Zresetowałeś statystyki gracza: " + rankingData.Players[target].Name);
                }
            }
        }

        #region API

        private string GetPlayerGuildTag(PlayerSession player)
        {
            if (rankingData.Players[player.SteamId.ToString()].Guild != "null")
            {
                return "<color=green>[" + rankingData.Players[player.SteamId.ToString()].Guild + "]</color> ";
            }
            else
            {
                return "";
            }
        }

        private string GetPlayerRank(PlayerSession player)
        {
            if (!player.IsAdmin)
            {
                var kills = rankingData.Players[player.SteamId.ToString()].Kills;
                if (kills < 20)
                {
                    return "<color=orange>[Unranked]</color> ";
                }
                else
                {
                    var rank = rankingData.Players.Values.Where(p => p.Kills >= 20).OrderByDescending(p => p.Kills).ToList();
                    return "<color=orange>[TOP" + (rank.FindIndex(p => p.SteamID == player.SteamId.ToString()) + 1).ToString() + "]</color> ";
                }
            }
            else
            {
                return "";
            }
        }

        #endregion

        #region Helpers

        private static bool isValid(String str)
        {
            return Regex.IsMatch(str, @"^[a-zA-Z]+$");
        }

        bool isGuildExists(string Tag)
        {
            if (rankingData.Guilds.ContainsKey(Tag))
            {
                return true;
            }
            return false;
        }

        bool isCommandIntervalTimeOut(PlayerSession player)
        {
            RankingPlayer p = new RankingPlayer(player);
            string interval = rankingData.Players[p.SteamID].CommandInterval;
            var finish = Convert.ToDouble(interval) - CurrentTime();
            if (finish <= 0)
            {
                return true;
            }
            return false;
        }

        bool isPotionIntervalTimeOut(PlayerSession player, string type)
        {
            RankingPlayer p = new RankingPlayer(player);
            if (type == "HealthPotion")
            {
                string interval = rankingData.Players[p.SteamID].HealthPotionInterval;
                var finish = Convert.ToDouble(interval) - CurrentTime();
                if (finish <= 0)
                {
                    return true;
                }
            }
            else if (type == "Bandage")
            {
                string interval = rankingData.Players[p.SteamID].BandageInterval;
                var finish = Convert.ToDouble(interval) - CurrentTime();
                if (finish <= 0)
                {
                    return true;
                }
            }
            else if (type == "InfamyPotion")
            {
                string interval = rankingData.Players[p.SteamID].InfamyPotionInterval;
                var finish = Convert.ToDouble(interval) - CurrentTime();
                if (finish <= 0)
                {
                    return true;
                }
            }
            return false;
        }


        void setPotionInterval(PlayerSession player, string type)
        {
            if (type == "HealthPotion")
            {
                RankingPlayer p = new RankingPlayer(player);
                rankingData.Players[p.SteamID].HealthPotionInterval = (CurrentTime() + 420).ToString();
            }
            else if (type == "Bandage")
            {
                RankingPlayer p = new RankingPlayer(player);
                rankingData.Players[p.SteamID].BandageInterval = (CurrentTime() + 300).ToString();
            }
            else if (type == "InfamyPotion")
            {
                RankingPlayer p = new RankingPlayer(player);
                rankingData.Players[p.SteamID].InfamyPotionInterval = (CurrentTime() + 10800).ToString();
            }
            SaveData();
        }

        void setCommandInterval(PlayerSession player)
        {
            RankingPlayer p = new RankingPlayer(player);
            rankingData.Players[p.SteamID].CommandInterval = (CurrentTime() + 3).ToString();
            SaveData();
        }

        PlayerSession GetPlayer(string searchedPlayer, PlayerSession player)
        {
            foreach (PlayerSession current in GameManager.Instance.GetSessions().Values)
                if (current != null && current.Name != null && current.IsLoaded && current.Name.ToLower() == searchedPlayer)
                    return current;

            List<PlayerSession> foundPlayers =
                (from current in GameManager.Instance.GetSessions().Values
                 where current != null && current.Name != null && current.IsLoaded && current.Name.ToLower().Contains(searchedPlayer.ToLower())
                 select current).ToList();

            switch (foundPlayers.Count)
            {
                case 0:
                    hurt.SendChatMessage(player, "Nie ma takiego gracza!");
                    break;

                case 1:
                    return foundPlayers[0];

                default:
                    List<string> playerNames = (from current in foundPlayers select current.Name).ToList();
                    string players = ListToString(playerNames, 0, ", ");
                    hurt.SendChatMessage(player, "Znaleziono kilku graczy: \n" + players);
                    break;
            }

            return null;
        }

        public bool isGuildOwner(PlayerSession player)
        {
            if (rankingData.Guilds.ContainsKey(rankingData.Players[player.SteamId.ToString()].Guild))
            {
                if (rankingData.Guilds[rankingData.Players[player.SteamId.ToString()].Guild].OwnerID == player.SteamId.ToString())
                {
                    return true;
                }
            }
            return false;
        }

        public bool isGuildFriend(PlayerSession victim, PlayerSession killer)
        {
            if ((rankingData.Players[victim.SteamId.ToString()].Guild == rankingData.Players[killer.SteamId.ToString()].Guild))
            {
                if ((rankingData.Players[victim.SteamId.ToString()].Guild != "null") && (rankingData.Players[killer.SteamId.ToString()].Guild != "null"))
                {
                    return true;
                }
            }
            return false;
        }

        public void SaveData() => Interface.GetMod().DataFileSystem.WriteObject("RankingData", rankingData);

        public List<OwnershipStakeServer> GetStakesFromPlayer(PlayerSession session)
        {
            var stakes = Resources.FindObjectsOfTypeAll<OwnershipStakeServer>();
            if (stakes != null)
            {
                return
                    stakes.Where(
                        s =>
                            !s.IsDestroying && s.gameObject != null && s.gameObject.activeSelf &&
                            s.AuthorizedPlayers.Contains(session.Identity)).ToList();
            }
            return new List<OwnershipStakeServer>();
        }

        private bool isFriend(PlayerIdentity victim, PlayerIdentity killer)
        {
            List<OwnershipStakeServer> vstakes = GetStakesFromPlayer(victim.ConnectedSession);
            List<OwnershipStakeServer> kstakes = GetStakesFromPlayer(killer.ConnectedSession);

            foreach (OwnershipStakeServer killer_stake in kstakes)
            {
                if (vstakes.Contains(killer_stake))
                    return true;
            }
            return false;
        }

        public string GetNameOfObject(UnityEngine.GameObject obj)
        {
            var ManagerInstance = GameManager.Instance;
            return ManagerInstance.GetDescriptionKey(obj);
        }

        public PlayerSession GetPlayerSessionFromDataSource(EntityEffectSourceData dataSource)
        {
            return GameManager.Instance.GetSession(dataSource.EntitySource.GetComponent<uLinkNetworkView>().owner);
        }

        string ListToString(List<string> list, int first, string seperator) => string.Join(seperator, list.Skip(first).ToArray());

        #endregion
    }
}
