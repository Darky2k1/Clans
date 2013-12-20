using System;
using TShockAPI.DB;
using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;
using System.Data;
using System.IO;
using TShockAPI;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Clans
{
    public class ClanManager
    {
        public Color clanchatcolor = new Color(135, 214, 9);
        public List<TSPlayer> awaitingtp = new List<TSPlayer>();
        public IDbConnection database;
        public string savepath = TShock.SavePath;

        public ClanMember FindClanMember(TSPlayer ts)
        {
            ClanMember clanmember = new ClanMember();
            try
            {
                using (var reader = database.QueryReader("SELECT * FROM ClanMembers WHERE UserName = @0", ts.Name))
                {
                    if (reader.Read())
                    {
                        clanmember.ClanName = reader.Get<string>("ClanName");
                        clanmember.UserName = reader.Get<string>("UserName");
                        clanmember.ClanRank = reader.Get<int>("ClanRank");
                    }
                }
                return clanmember;
            }
            catch { return clanmember; }
        }

        public Clan FindClanByMember(TSPlayer ts)
        {
            string name = FindClanMember(ts).ClanName;
            return FindClanByName(name);
        }

        public List<ClanMember> FindClanMembersByClan(string clanname)
        {
            List<ClanMember> temp = new List<ClanMember>();
            try
            {
                using (QueryResult reader = database.QueryReader("SELECT * FROM ClanMembers WHERE ClanName = @0", clanname))
                {

                    while (reader.Read())
                    {
                        ClanMember c = new ClanMember();
                        c.UserName = reader.Get<string>("UserName");
                        c.ClanName = reader.Get<string>("ClanName");
                        c.ClanRank = reader.Get<int>("ClanRank");
                        temp.Add(c);
                    }
                }
                return temp;
            }
            catch { return temp; }
        }

        public Clan FindClanByName(string ClanName)
        {
            Clan clan = new Clan();
            try
            {
                using (var reader = database.QueryReader("SELECT * FROM Clans WHERE ClanName = @0", ClanName))
                {
                    if (reader.Read())
                    {
                        clan.ClanName = reader.Get<string>("ClanName");
                        clan.ClanTokens = reader.Get<int>("ClanTokens");
                        clan.ClanTileX = reader.Get<int>("ClanTileX");
                        clan.ClanTileY = reader.Get<int>("ClanTileY");
                        clan.InviteOnly = reader.Get<int>("InviteOnly");
                        clan.Ranks = JsonConvert.DeserializeObject<List<string>>(reader.Get<string>("Ranks"));
                        clan.Bans = JsonConvert.DeserializeObject<List<string>>(reader.Get<string>("Bans"));
                    }
                }
                return clan;
            }
            catch { return clan; }
        }

        public List<Clan> ListClans()
        {
            List<Clan> temp = new List<Clan>();
            try
            {
                using (QueryResult reader = database.QueryReader("SELECT * FROM Clans"))
                {
                    while (reader.Read())
                    {
                        Clan clan = new Clan();
                        clan.ClanName = reader.Get<string>("ClanName");
                        clan.ClanTokens = reader.Get<int>("ClanTokens");
                        clan.ClanTileX = reader.Get<int>("ClanTileX");
                        clan.ClanTileY = reader.Get<int>("ClanTileY");
                        clan.InviteOnly = reader.Get<int>("InviteOnly");
                        clan.Ranks = JsonConvert.DeserializeObject<List<string>>(reader.Get<string>("Ranks"));
                        clan.Bans = JsonConvert.DeserializeObject<List<string>>(reader.Get<string>("Bans"));
                        temp.Add(clan);
                    }
                }
                return temp;

            }
            catch { return temp; }
        }

        public bool Kick(TSPlayer ts)
        {
            Clan c = FindClanByMember(ts);
            ts.SendMessage(string.Format("[Clans] You have been kicked from clan: {0}", c.ClanName), clanchatcolor);
            database.Query("DELETE FROM ClanMembers WHERE ClanName = @0 AND UserName = @1", c.ClanName, ts.Name);
            SendClanMessage(c.ClanName, string.Format("{0} has been kicked from the clan!", ts.Name));
            return true;
        }

        public bool Ban(TSPlayer ts)
        {
            Clan c = FindClanByMember(ts);
            List<string> bans = c.Bans;
            if (bans.Contains(ts.IP))
            {
                return false;
            }
            else
            {
                bans.Add(ts.IP);
                string bansinjsonformat = JsonConvert.SerializeObject(bans, Formatting.None);
                database.Query("UPDATE Clans SET Bans = @0 WHERE ClanName = @1", bansinjsonformat, c.ClanName);
                database.Query("DELETE FROM ClanMembers WHERE ClanName = @0 AND UserName = @1", c.ClanName, ts.Name);
                ts.SendMessage(string.Format("[Clans] You have been banned from clan: {0}", c.ClanName), clanchatcolor);
                SendClanMessage(c.ClanName, string.Format("{0} has been banned from the clan!", ts.Name));
                return true;
            }
        }

        public bool UnBan(TSPlayer ts,string clan)
        {
            Clan c = FindClanByName(clan);
            List<string> bans = c.Bans;
            if (!bans.Contains(ts.IP))
            {
                return false;
            }
            else
            {
                bans.Remove(ts.IP);
                string bansinjsonformat = JsonConvert.SerializeObject(bans, Formatting.None);
                database.Query("UPDATE Clans SET Bans = @0 WHERE ClanName = @1", bansinjsonformat, c.ClanName);
                database.Query("DELETE FROM ClanMembers WHERE ClanName = @0 AND UserName = @1", c.ClanName, ts.Name);
                ts.SendMessage(string.Format("[Clans] You have been unbanned from clan: {0}", c.ClanName), clanchatcolor);
                SendClanMessage(c.ClanName, string.Format("{0} has been unbanned from the clan!", ts.Name));
                return true;
            }
        }

        public void SetInviteMode(Clan clan,int mode)
        {
            database.Query("UPDATE Clans SET InviteOnly = @0 WHERE ClanName = @1", mode, clan.ClanName);
        }

        public bool SetRank(TSPlayer ts, int rank)
        {
            if (rank > 4 || rank < 1)
            {
                return false;
            }
            ClanMember cm = FindClanMember(ts);
            if (cm.ClanName == "")
            {
                return false;
            }
            try
            {
                database.Query("UPDATE ClanMembers SET ClanRank = @0 WHERE UserName = @1", rank, ts.Name);
                return true;
            }
            catch { return false; }
        }

        public bool SetClanSpawn(TSPlayer ts)
        {
            try
            {
                ClanMember cm = FindClanMember(ts);
                if(cm.ClanRank == 5)
                {
                    database.Query("UPDATE Clans SET ClanTileX=@0, ClanTileY=@1 WHERE ClanName =@2", ts.TileX, ts.TileY, cm.ClanName);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Log.ConsoleError(ex.ToString());
                return false;
            }
        }

        public void EditRank(string clanname, string rank, int id)
        {
            Clan c = FindClanByName(clanname);
            List<string> ranks = c.Ranks;
            ranks[id-1] = rank;
            string json = JsonConvert.SerializeObject(ranks);
            database.Query("UPDATE Clans SET Ranks = @0 WHERE ClanName = @1", json, clanname);
        }

        public bool CreateClan(string name, TSPlayer ts)
        {
            try
            {
                ClanMember cm = FindClanMember(ts);
                if (cm.ClanName != "")
                {
                    return false;
                }
                database.Query("INSERT INTO Clans (ClanName, ClanTokens, ClanTileX, ClanTileY, InviteOnly, Ranks , Bans) VALUES (@0, @1, @2, @3, @4, @5, @6)", name, 0, -1, -1, 0,JsonConvert.SerializeObject(new List<string>(){"Rookie","Peon","Moderator","Admin","Owner"},Formatting.None), "[]");
                database.Query("INSERT INTO ClanMembers (UserName, ClanName, ClanRank) VALUES (@0, @1, @2)", ts.Name, name, 5);
                return true;
            }
            catch
            {
                return false;
            }
        }


        public void DeleteClan(string ClanName)
        {
            database.Query("DELETE FROM Clans WHERE ClanName = @0", ClanName);
            database.Query("DELETE FROM ClanMembers WHERE ClanName = @0", ClanName);
        }

        public int CountMembers(string clanname)
        {
            int i = 0;
            using (QueryResult reader = database.QueryReader("SELECT ClanName FROM ClanMembers WHERE ClanName = @0", clanname))
            {
                while (reader.Read())
                {
                    i++;
                }
            }
            return i;
        }

        public bool JoinClan(string name, TSPlayer ts)
        {
            try
            {
                database.Query("INSERT INTO ClanMembers (UserName, ClanName, ClanRank) VALUES (@0, @1, @2)", ts.Name, name,1);
                return true;
            }
            catch (Exception ex)
            {
                Log.ConsoleError(ex.ToString());
                return false;
            }
        }

        public bool EditTokens(string ClanName, ClanTokens Option, int amount)
        {
            Clan clan = FindClanByName(ClanName);
            int money = clan.ClanTokens;
            if (clan.ClanName == "")
            {
                return false;
            }
            try
            {
                if (Option == ClanTokens.Increase)
                    money += amount;
                if (Option == ClanTokens.Decrease)
                    money -= amount;
                database.Query("UPDATE Clans SET ClanTokens = @0 WHERE ClanName = @1", money, clan.ClanName);
                return true;
            }
            catch{return false;}
            
        }

        public void SendClanMessage(string clanname,string message)
        {
            foreach (TSPlayer ts in TShock.Players)
            {
                if (ts != null)
                {
                    ClanMember c = FindClanMember(ts);
                    if (c.ClanName == clanname)
                    {
                        ts.SendMessage(string.Format("[Clans] {0}",message),clanchatcolor);
                    }
                }
            }
        }

        public bool LeaveClan(TSPlayer ts)
        {
            try
            {
                ClanMember cm = FindClanMember(ts);
                if (cm.ClanName == "")
                {
                    return false;
                }
                database.Query("DELETE FROM ClanMembers WHERE UserName = @0", ts.Name);
                if (cm.ClanRank == 5)
                {
                    SendClanMessage(cm.ClanName, "You have been kicked out of the clan for: clan removed");
                    database.Query("DELETE FROM ClanMembers WHERE ClanName = @0", cm.ClanName);
                    database.Query("DELETE FROM Clans WHERE ClanName = @0", cm.ClanName);
                }
                return true;
            }
            catch (Exception ex)
            {
                Log.ConsoleError(ex.ToString());
                return false;
            }
        }

        public void SetupDb()
        {
            string sql = Path.Combine(savepath, "clans.sqlite");
            database = new SqliteConnection(string.Format("uri=file://{0},Version=3", sql));
            SqlTableCreator SQLcreator = new SqlTableCreator(database, (IQueryBuilder)new SqliteQueryCreator());
            var table = new SqlTable("Clans",
            new SqlColumn("ClanName", MySqlDbType.VarChar) { Primary = true, Unique = true, Length = 30 },
            new SqlColumn("ClanTokens", MySqlDbType.Int32),
            new SqlColumn("ClanTileX",MySqlDbType.Int32),
            new SqlColumn("ClanTileY", MySqlDbType.Int32),
            new SqlColumn("InviteOnly",MySqlDbType.Int32),
            new SqlColumn("Ranks",MySqlDbType.VarChar),
            new SqlColumn("Bans", MySqlDbType.VarChar)
            );
            var table2 = new SqlTable("ClanMembers",
            new SqlColumn("UserName", MySqlDbType.VarChar) { Primary = true, Unique = true, Length = 30 },
            new SqlColumn("ClanName", MySqlDbType.VarChar),
            new SqlColumn("ClanRank", MySqlDbType.Int32)
            );
            SQLcreator.EnsureExists(table);
            SQLcreator.EnsureExists(table2);
        }
    }

    public class Clan
    {
        public string ClanName { get; set; }
        public int ClanTokens { get; set; }
        public int ClanTileX { get; set; }
        public int ClanTileY { get; set; }
        public int InviteOnly{get;set;}
        public List<string> Ranks { get; set; }
        public  List<string> Bans { get; set; }
        public Clan(string clanname, int clantokens, int clantilex, int clantiley,int inviteonly,List<string>ranks,List<string> bans)
        {
            ClanName = clanname;
            ClanTokens = clantokens;
            ClanTileX = clantilex;
            ClanTileY = clantiley;
            InviteOnly = inviteonly;
            ranks = Ranks;
            Bans = bans;
        }
        public Clan()
        {
            ClanName = string.Empty;
            ClanTokens = 0;
            ClanTileX = -1;
            ClanTileY = -1;
            InviteOnly = 0;
            Ranks = new List<string>();
            Bans = new List<string>();
        }
    }

    public class ClanCount
    {
        public string ClanName { get; set; }
        public int Count { get; set; }
        public ClanCount(string name, int count)
        {
            name = ClanName;
            count = Count;
        }
    }

    public class ClanMember
    {
        public string UserName;
        public string ClanName;
        public int ClanRank;
        public ClanMember(string username, string clanname, int clanrank)
        {
            UserName = username;
            ClanName = clanname;
            clanrank = ClanRank;
        }
        public ClanMember()
        {
            UserName = "";
            ClanName = string.Empty;
            ClanRank = 5;
        }
    }
    public enum ClanTokens : int
    {
        Decrease,
        Increase
    }
}
