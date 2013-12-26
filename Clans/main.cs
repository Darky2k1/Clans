using System;
using Terraria;
using TShockAPI;
using System.IO;
using TerrariaApi.Server;
using System.Reflection;
using System.Collections.Generic;
using System.Timers;
using System.Linq;

namespace Clans
{
    [ApiVersion(1, 14)]
    public class Clans : TerrariaPlugin
    {
        System.Net.WebClient wc = new System.Net.WebClient();
        Timer t = new Timer(2000);
        string[] Invites = new string[255];
        bool UpdateAvailable = false;

        public static ClanManager CM = new ClanManager();


        public override Version Version
        {
            get { return Assembly.GetExecutingAssembly().GetName().Version; }
        }
        public override string Author
        {
            get { return "Ancientgods"; }
        }
        public override string Name
        {
            get { return "Clans"; }
        }

        public override string Description
        {
            get { return "Gives people the ability to create clans!"; }
        }

        public override void Initialize()
        {
            wc.Proxy = null;
            t.Elapsed += OnTimer;
            ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
            ServerApi.Hooks.NetGreetPlayer.Register(this, OnGreetPlayer);
            Commands.ChatCommands.Add(new Command(clan, "clan"));
            Commands.ChatCommands.Add(new Command("clans.admin", clanadmin, "clanadmin"));
            Commands.ChatCommands.Add(new Command(clanchat, "c"));
            for (int i = 0; i < Invites.Length; i++)
            {
                Invites[i] = "";
            }
            if (!Directory.Exists(CM.savepath))
            {
                Directory.CreateDirectory(CM.savepath);
            }
            CM.SetupDb();
            CheckUpdate();
        }

        private void OnTimer(object sender, ElapsedEventArgs args)
        {
            t.Stop();
            foreach (TSPlayer ts in CM.awaitingtp)
            {
                if (ts != null)
                {
                    Clan c = CM.FindClanByMember(ts);
                    if (c.ClanTileX > 0 && c.ClanTileY > 0)
                    {
                        ts.Teleport(c.ClanTileX * 16, c.ClanTileY * 16);
                        ts.SendInfoMessage("You have been teleported to your clan's spawnpoint!");
                    }
                }
            }
            CM.awaitingtp.Clear();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
                ServerApi.Hooks.NetGreetPlayer.Deregister(this, OnGreetPlayer);
                t.Elapsed -= OnTimer;
            }
            base.Dispose(disposing);
        }

        public Clans(Main game)
            : base(game)
        {
            Order = -1;
        }

        private void OnGreetPlayer(GreetPlayerEventArgs args)
        {
            if (UpdateAvailable)
            {
                if (TShock.Players[args.Who].Group.HasPermission("tshock.cfg"))
                {
                    TShock.Players[args.Who].SendMessage(string.Format("[Clans] There is a new update for the Clans plugin available!"), CM.clanchatcolor);
                    TShock.Players[args.Who].SendMessage(string.Format("[Clans] Download at http://tshock.co/xf/index.php?threads/1-14-clans.2649/"), CM.clanchatcolor);
                }
            }
            CM.awaitingtp.Add(TShock.Players[args.Who]);
            t.Start();
        }

        private void OnLeave(LeaveEventArgs args)
        {
            Invites[args.Who] = "";
        }

        #region clanchat
        private void clanchat(CommandArgs args)
        {
            if (!args.Player.IsLoggedIn)
            {
                args.Player.SendErrorMessage("You are not logged in!");
                return;
            }
            ClanMember sender = CM.FindClanMember(args.Player);
            Clan c = CM.FindClanByName(sender.ClanName);
            if (sender.ClanName == "" || c.ClanName == "")
            {
                args.Player.SendErrorMessage("You are not in a clan!");
                return;
            }
            string message = string.Join(" ", args.Parameters);
            if (message.Length < 1)
            {
                args.Player.SendErrorMessage("Invalid syntax! valid syntax: /c <message>");
                return;
            }
            string prefix = c.Ranks[sender.ClanRank - 1];
            foreach (TSPlayer ts in TShock.Players)
            {
                if (ts != null)
                {
                    ClanMember cm = CM.FindClanMember(ts);
                    if (c.ClanName == cm.ClanName)
                    {
                        ts.SendMessage(string.Format("[{0} - {1}] {2}: {3}", sender.ClanName, prefix, args.Player.Name, message), CM.clanchatcolor);
                    }
                }
            }
        }
        #endregion clanchat

        #region clan admin commands
        private void clanadmin(CommandArgs args)
        {
            string clanname = "";
            if (args.Parameters.Count < 1)
            {
                args.Player.SendErrorMessage("Invalid syntax! type /clanadmin help");
                return;
            }
            if (args.Parameters.Count > 1)
            {
                clanname = String.Join(" ", args.Parameters.GetRange(1, args.Parameters.Count - 1));
            }
            ClanMember cm = CM.FindClanMember(args.Player);
            Clan c = CM.FindClanByName(cm.ClanName);
            switch (args.Parameters[0])
            {
                case "forcejoin":
                    args.Player.SendErrorMessage("Invalid syntax! valid syntax: /clanadmin forcejoin <clan>");
                    clanname = String.Join(" ", args.Parameters.GetRange(1, args.Parameters.Count - 1));
                    CM.JoinClan(clanname, args.Player);
                    return;
                case "delete":
                    if (args.Parameters.Count < 2)
                    {
                        args.Player.SendErrorMessage("Invalid syntax! valid syntax: /clanadmin forcejoin <clan>");
                        clanname = String.Join(" ", args.Parameters.GetRange(1, args.Parameters.Count - 1));
                        CM.DeleteClan(clanname);
                        return;
                    }
                    return;
            }
        }
        #endregion clan admin commands

        #region clan commands
        private void clan(CommandArgs args)
        {
            if (!args.Player.IsLoggedIn)
            {
                args.Player.SendErrorMessage("You need to be logged in to do this command!");
                return;
            }
            if (args.Parameters.Count < 1)
            {
                args.Player.SendErrorMessage("Invalid syntax! type /clan help");
                return;
            }
            int pageNumber;
            var foundplr = new List<TSPlayer>();
            var plr = new TSPlayer(0);
            ClanMember cm = CM.FindClanMember(args.Player);
            Clan c = CM.FindClanByName(cm.ClanName);
            string name = string.Empty;
            switch (args.Parameters[0])
            {
                case "create":
                    #region create
                    
                    if(!args.Player.Group.Haspermission("clan.master"))
                    {
                        args.Player.SendErrorMessage("You do not have permission to create clans.");
                        return;
                    }
                    if (args.Parameters.Count < 2)
                    {
                        args.Player.SendErrorMessage("Invalid syntax! valid syntax: /clan create <name for the clan>");
                        return;
                    }
                    name = String.Join(" ", args.Parameters.GetRange(1, args.Parameters.Count - 1));
                    Clan foundclan = CM.FindClanByName(name);
                    if (cm.ClanName != "")
                    {
                        args.Player.SendErrorMessage("You are already in a clan!");
                        return;
                    }
                    if (foundclan.ClanName != "")
                    {
                        args.Player.SendErrorMessage("This clan already exists!");
                        return;
                    }
                    if (CM.CreateClan(name, args.Player))
                    {
                        args.Player.SendInfoMessage("Your clan has been created successfully!");
                        TSPlayer.All.SendMessage(string.Format("[Clans] {0} has created a new clan called \"{1}\".", args.Player.Name, name), CM.clanchatcolor);
                    }
                    #endregion create
                    return;
                case "invite":
                    #region invite
                    if (args.Parameters.Count < 2)
                    {
                        args.Player.SendErrorMessage("Invaild syntax! valid syntax: /clan invite <name>");
                        return;
                    }
                    if (cm.ClanName == "" || c.ClanName == "")
                    {
                        args.Player.SendErrorMessage("You are not in a clan!");
                        return;
                    }
                    if (cm.ClanRank < 2)
                    {
                        args.Player.SendErrorMessage("You do not have permission to invite other members to your clan!");
                        return;
                    }
                    foundplr = TShock.Utils.FindPlayer(args.Parameters[1]);
                    if (foundplr.Count == 0)
                    {
                        args.Player.SendMessage("Invalid player!", Color.Red);
                        return;
                    }
                    else if (foundplr.Count > 1)
                    {
                        args.Player.SendMessage(string.Format("More than one ({0}) player matched!", foundplr.Count), Color.Red);
                        return;
                    }
                    plr = foundplr[0];
                    if (args.Player.Name == plr.Name)
                    {
                        args.Player.SendErrorMessage("You cannot invite yourself!");
                        return;
                    }
                    if (CM.FindClanByMember(plr).ClanName != "")
                    {
                        args.Player.SendErrorMessage("This player is already in a clan!");
                        return;
                    }
                    if (Invites[plr.Index] != "")
                    {
                        args.Player.SendErrorMessage("This player has already been invited by someone else!");
                        return;
                    }
                    Invites[plr.Index] = cm.ClanName;
                    args.Player.SendInfoMessage(string.Format("You have invited {0} to your clan!", plr.Name));
                    plr.SendInfoMessage(string.Format("You have been invited to join clan: {0}", cm.ClanName));
                    plr.SendInfoMessage(string.Format("To accept this invite type /clan acceptinvite"));
                    #endregion invite
                    return;
                case "acceptinvite":
                    #region acceptinvite
                    if (args.Parameters.Count < 1)
                    {
                        args.Player.SendErrorMessage("Invaild syntax! valid syntax: /clan invite <name>");
                        return;
                    }
                    if (cm.ClanName != "" || c.ClanName != "")
                    {
                        args.Player.SendErrorMessage("You are already in a clan!");
                        return;
                    }
                    if (Invites[args.Player.Index] == "")
                    {
                        args.Player.SendErrorMessage("You have no invitation pending!");
                        return;
                    }
                    Clan invc = CM.FindClanByName(Invites[args.Player.Index]);
                    if (invc.ClanName == "")
                    {
                        args.Player.SendErrorMessage("This clan does not exist!");
                        return;
                    }
                    if (invc.Bans.Contains(args.Player.IP))
                    {
                        args.Player.SendErrorMessage("You have been banned from this clan!");
                        return;
                    }
                    if (CM.JoinClan(invc.ClanName, args.Player))
                    {
                        args.Player.SendInfoMessage("You have successfully joined the clan " + name);
                        args.Player.SendInfoMessage("Type /c <message> to talk in your clan");
                        CM.SendClanMessage(invc.ClanName, string.Format("{0} has joined the clan!", args.Player.Name));
                        Invites[args.Player.Index] = "";
                    }
                    #endregion acceptinvite
                    return;
                case "denyinvite":
                    #region denyinvite
                    if (Invites[args.Player.Index] == "")
                    {
                        args.Player.SendErrorMessage("You have no invitation pending!");
                        return;
                    }
                    Invites[args.Player.Index] = "";
                    args.Player.SendInfoMessage("You have denied the invitation!");
                    #endregion denyinvite
                    return;
                case "join":
                    #region join
                    if (args.Parameters.Count < 2)
                    {
                        args.Player.SendErrorMessage("Invalid syntax! valid syntax: /clan join <clan name>");
                        return;
                    }
                    name = String.Join(" ", args.Parameters.GetRange(1, args.Parameters.Count - 1));
                    if (cm.ClanName != "")
                    {
                        args.Player.SendErrorMessage("You are already in a clan!");
                        return;
                    }
                    Clan cbn = CM.FindClanByName(name);
                    if (cbn.ClanName == "")
                    {
                        args.Player.SendErrorMessage("This clan does not exist!");
                        return;
                    }
                    if (cbn.Bans.Contains(args.Player.IP))
                    {
                        args.Player.SendErrorMessage("You have been banned from this clan!");
                        return;
                    }
                    if (cbn.InviteOnly == 1)
                    {
                        args.Player.SendErrorMessage("This clan is in invite-only mode!");
                        return;
                    }
                    if (CM.JoinClan(name, args.Player))
                    {
                        args.Player.SendInfoMessage("You have successfully joined the clan " + name);
                        args.Player.SendInfoMessage("Type /c <message> to talk in your clan");
                        CM.SendClanMessage(name, string.Format("{0} has joined the clan!", args.Player.Name));
                    }
                    #endregion join
                    return;
                case "list":
                    #region list
                    if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
                        return;
                    IEnumerable<string> Clans = from clan in CM.ListClans() select clan.ClanName;
                    PaginationTools.SendPage(args.Player, pageNumber, PaginationTools.BuildLinesFromTerms(Clans),
                            new PaginationTools.Settings
                            {
                                HeaderFormat = "Clans ({0}/{1}):",
                                FooterFormat = "Type /clan listclans {0} for more.",
                            });
                    #endregion list
                    return;
                case "tp":
                    #region tp
                    string clanname = CM.FindClanMember(args.Player).ClanName;
                    if (cm.ClanName == "" || c.ClanName == "")
                    {
                        args.Player.SendErrorMessage("You are not in a clan!");
                        return;
                    }
                    if (c.ClanTileX > 0 && c.ClanTileY > 0)
                    {
                        args.Player.Teleport(c.ClanTileX * 16, c.ClanTileY * 16);
                    }
                    else
                    {
                        args.Player.SendErrorMessage("This clan has no clan spawn!");
                    }
                    #endregion tp
                    return;
                case "setspawn":
                    #region setspawn
                    if (cm.ClanName == "" || c.ClanName == "")
                    {
                        args.Player.SendErrorMessage("You are not in a clan!");
                        return;
                    }
                    if (cm.ClanRank != 5)
                    {
                        args.Player.SendErrorMessage("You do not have permission to set the clan spawn!");
                        return;
                    }
                    if (CM.SetClanSpawn(args.Player))
                    {
                        args.Player.SendInfoMessage("You have succesfully updated the clan spawn!");
                        return;
                    }
                    #endregion setspawn
                    return;
                case "inviteonly":
                    #region invite only
                    if (args.Parameters.Count < 2)
                    {
                        args.Player.SendErrorMessage("Invalid syntax! valid syntax: /clan inviteonly <true/false>");
                        return;
                    }
                    if (cm.ClanName == "" || c.ClanName == "")
                    {
                        args.Player.SendErrorMessage("You are not in a clan!");
                        return;
                    }
                    if (cm.ClanRank != 5)
                    {
                        args.Player.SendErrorMessage("You do not have permission to change invite-only mode!");
                        return;
                    }
                    if (args.Parameters[1] == "true")
                    {
                        CM.SetInviteMode(c, 1);
                        CM.SendClanMessage(cm.ClanName, "Invite-only mode is now enabled!");
                        return;
                    }
                    if (args.Parameters[1] == "false")
                    {
                        CM.SetInviteMode(c, 0);
                        CM.SendClanMessage(cm.ClanName, "Invite-only mode is now disabled!");
                        return;
                    }
                    else if (args.Parameters[1] != "true" && args.Parameters[1] != "false")
                        args.Player.SendErrorMessage("Invalid syntax! valid syntax: /clan inviteonly <true/false>");
                    #endregion invite only
                    return;
                case "leave":
                    #region leave
                    if (c.ClanName == "" && cm.ClanName == "")
                    {
                        args.Player.SendErrorMessage("You are not in a clan!");
                        return;
                    }
                    args.Player.SendInfoMessage("You have left your clan!");
                    CM.LeaveClan(args.Player);
                    CM.SendClanMessage(c.ClanName, string.Format("{0} has left the clan!", args.Player.Name));
                    #endregion leave
                    return;
                case "rank":
                    #region rank
                    if (args.Parameters.Count < 3)
                    {
                        args.Player.SendErrorMessage("Invalid syntax! valid syntax: /clan rank <name> <1-4>");
                        return;
                    }
                    if (cm.ClanName == "" || c.ClanName == "")
                    {
                        args.Player.SendErrorMessage("You are not in a clan!");
                        return;
                    }
                    if (cm.ClanRank != 5)
                    {
                        args.Player.SendErrorMessage("You do not have permission to rank other clan members!");
                        return;
                    }
                    foundplr = TShock.Utils.FindPlayer(args.Parameters[1]);
                    if (foundplr.Count == 0)
                    {
                        args.Player.SendMessage("Invalid player!", Color.Red);
                        return;
                    }
                    else if (foundplr.Count > 1)
                    {
                        args.Player.SendMessage(string.Format("More than one ({0}) player matched!", foundplr.Count), Color.Red);
                        return;
                    }
                    plr = foundplr[0];
                    if (plr == args.Player)
                    {
                        args.Player.SendErrorMessage("You cannot rank yourself!");
                        return;
                    }
                    if (CM.FindClanMember(plr).ClanName != cm.ClanName)
                    {
                        args.Player.SendErrorMessage(plr.Name + " Is not a part of your clan!");
                        return;
                    }
                    int rank;
                    int.TryParse(args.Parameters[2], out rank);
                    if (rank > 4 || rank < 1)
                    {
                        args.Player.SendErrorMessage("Invalid syntax! valid syntax: /clan rank <name> <1-4>");
                        return;
                    }
                    int Rank = 0;
                    switch (rank)
                    {
                        case 2:
                            Rank = 2;
                            break;
                        case 3:
                            Rank = 3;
                            break;
                        case 4:
                            Rank = 4;
                            break;
                    }
                    if (CM.SetRank(plr, (int)Rank))
                    {
                        plr.SendInfoMessage("Your clan rank is now " + c.Ranks[rank - 1]);
                        args.Player.SendInfoMessage(string.Format("You have changed {0}'s rank to " + c.Ranks[rank - 1], plr.Name));
                    }
                    else
                    {
                        args.Player.SendErrorMessage("Failed to update rank!");
                    }
                    #endregion rank
                    return;
                case "editrank":
                    #region edit rank
                    if (args.Parameters.Count < 3)
                    {
                        args.Player.SendErrorMessage("Invalid syntax! valid syntax: /clan editrank  <1-5> <new name>");
                        args.Player.SendErrorMessage("5 = owner, 4 = admin 3 = moderator, 2 = Peon, 1 = Rookie");
                        args.Player.SendErrorMessage("This does not affect permissions, only titles!");
                        return;
                    }
                    if (cm.ClanName == "" || c.ClanName == "")
                    {
                        args.Player.SendErrorMessage("You are not in a clan!");
                        return;
                    }
                    if (cm.ClanRank != 5)
                    {
                        args.Player.SendErrorMessage("You do not have permission to edit ranks names!");
                        return;
                    }
                    string rankname = String.Join(" ", args.Parameters.GetRange(2, args.Parameters.Count - 2));
                    int ranks;
                    int.TryParse(args.Parameters[1], out ranks);
                    if (ranks > 5 || ranks < 1)
                    {
                        args.Player.SendErrorMessage("Invalid syntax! valid syntax: /clan editrank  <1-5> <new name>");
                        args.Player.SendErrorMessage("5 = owner 4 = admin 3 = moderator, 2 = Peon, 1 = Rookie");
                        args.Player.SendErrorMessage("This does not affect permissions, only titles!");
                        return;
                    }

                    CM.EditRank(cm.ClanName, rankname, ranks);
                    CM.SendClanMessage(cm.ClanName, string.Format("Rank {0} has been changed to {1}!", c.Ranks[ranks - 1], rankname));
                    #endregion edit rank
                    return;
                case "listmembers":
                    #region listmembers
                    if (cm.ClanName == "" || c.ClanName == "")
                    {
                        args.Player.SendErrorMessage("You are not in a clan!");
                        return;
                    }
                    if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
                        return;
                    int clancount = CM.CountMembers(cm.ClanName);
                    args.Player.SendMessage(string.Format("[Clans] Your clan has {0} member{1}!", clancount, clancount > 1 ? "s" : ""), CM.clanchatcolor);
                    IEnumerable<string> clanmembernames = from clanmember in CM.FindClanMembersByClan(cm.ClanName) select clanmember.UserName;
                    PaginationTools.SendPage(args.Player, pageNumber, PaginationTools.BuildLinesFromTerms(clanmembernames),
                            new PaginationTools.Settings
                            {
                                HeaderFormat = "ClanMembers of " + cm.ClanName + " ({0}/{1}):",
                                FooterFormat = "Type /clan listmembers {0} for more.",
                            });
                    #endregion listmembers
                    return;
                case "ban":
                    #region ban
                    if (args.Parameters.Count < 2)
                    {
                        args.Player.SendErrorMessage("Invalid syntax! valid syntax: /clan ban <player>");
                        return;
                    }
                    if (cm.ClanName == "" || c.ClanName == "")
                    {
                        args.Player.SendErrorMessage("You are not in a clan!");
                        return;
                    }
                    if (cm.ClanRank < 4)
                    {
                        args.Player.SendErrorMessage("You do not have permission to ban other clan members!");
                        return;
                    }
                    foundplr = TShock.Utils.FindPlayer(args.Parameters[1]);
                    if (foundplr.Count == 0)
                    {
                        args.Player.SendMessage("Invalid player!", Color.Red);
                        return;
                    }
                    else if (foundplr.Count > 1)
                    {
                        args.Player.SendMessage(string.Format("More than one ({0}) player matched!", foundplr.Count), Color.Red);
                        return;
                    }
                    plr = foundplr[0];
                    if (plr == args.Player)
                    {
                        args.Player.SendErrorMessage("You cannot ban yourself!");
                        return;
                    }
                    ClanMember foundmember = CM.FindClanMember(plr);
                    if (foundmember.ClanName != cm.ClanName)
                    {
                        args.Player.SendErrorMessage(plr.Name + " Is not a part of your clan!");
                        return;
                    }
                    if (foundmember.ClanRank > 2)
                    {
                        args.Player.SendErrorMessage("You do not have permission to ban this clan member!");
                        return;
                    }
                    CM.Ban(plr);
                    #endregion ban
                    return;
                case "unban":
                    #region unban
                    if (args.Parameters.Count < 2)
                    {
                        args.Player.SendErrorMessage("Invalid syntax! valid syntax: /clan unban <player>");
                        return;
                    }
                    if (cm.ClanName == "" || c.ClanName == "")
                    {
                        args.Player.SendErrorMessage("You are not in a clan!");
                        return;
                    }
                    if (cm.ClanRank < 4)
                    {
                        args.Player.SendErrorMessage("You do not have permission to unban other clan members!");
                        return;
                    }
                    foundplr = TShock.Utils.FindPlayer(args.Parameters[1]);
                    if (foundplr.Count == 0)
                    {
                        args.Player.SendMessage("Invalid player!", Color.Red);
                        return;
                    }
                    else if (foundplr.Count > 1)
                    {
                        args.Player.SendMessage(string.Format("More than one ({0}) player matched!", foundplr.Count), Color.Red);
                        return;
                    }
                    plr = foundplr[0];
                    if (plr == args.Player)
                    {
                        args.Player.SendErrorMessage("You cannot unban yourself!");
                        return;
                    }
                    if (!CM.UnBan(plr, cm.ClanName))
                    {
                        args.Player.SendErrorMessage("This player isn't banned!");
                    }
                    #endregion unban
                    return;
                case "kick":
                    #region kick
                    if (args.Parameters.Count < 2)
                    {
                        args.Player.SendErrorMessage("Invalid syntax! valid syntax: /clan kick <player>");
                        return;
                    }
                    if (cm.ClanName == "" || c.ClanName == "")
                    {
                        args.Player.SendErrorMessage("You are not in a clan!");
                        return;
                    }
                    if (cm.ClanRank < 3)
                    {
                        args.Player.SendErrorMessage("You do not have permission to kick other clan members!");
                        return;
                    }
                    foundplr = TShock.Utils.FindPlayer(args.Parameters[1]);
                    if (foundplr.Count == 0)
                    {
                        args.Player.SendMessage("Invalid player!", Color.Red);
                        return;
                    }
                    else if (foundplr.Count > 1)
                    {
                        args.Player.SendMessage(string.Format("More than one ({0}) player matched!", foundplr.Count), Color.Red);
                        return;
                    }
                    plr = foundplr[0];
                    if (plr == args.Player)
                    {
                        args.Player.SendErrorMessage("You cannot kick yourself!");
                        return;
                    }
                    ClanMember found = CM.FindClanMember(plr);
                    if (found.ClanName != cm.ClanName)
                    {
                        args.Player.SendErrorMessage(plr.Name + " Is not a part of your clan!");
                        return;
                    }
                    if (found.ClanRank > 3)
                    {
                        args.Player.SendErrorMessage("You do not have permission to kick this clan member!");
                        return;
                    }
                    CM.Kick(plr);
                    #endregion ban
                    return;
                case "help":
                #region help
                default:
                    if (args.Parameters.Count < 2)
                    {
                        args.Player.SendSuccessMessage("Clan Help page (1/3)");
                        args.Player.SendInfoMessage(Helpmessages[0]);
                        args.Player.SendInfoMessage(Helpmessages[1]);
                        args.Player.SendInfoMessage(Helpmessages[2]);
                        args.Player.SendInfoMessage(Helpmessages[3]);
                        args.Player.SendInfoMessage(Helpmessages[4]);
                        args.Player.SendInfoMessage("Type /clan help 2 for more");
                        return;
                    }
                    int.TryParse(args.Parameters[1], out pageNumber);
                    if (pageNumber == 2)
                    {
                        args.Player.SendSuccessMessage("Clan Help page (2/3)");
                        args.Player.SendInfoMessage(Helpmessages[5]);
                        args.Player.SendInfoMessage(Helpmessages[6]);
                        args.Player.SendInfoMessage(Helpmessages[7]);
                        args.Player.SendInfoMessage(Helpmessages[8]);
                        args.Player.SendInfoMessage(Helpmessages[9]);
                        args.Player.SendInfoMessage("Type /clan help 3 for more");
                        return;
                    }
                    else if (pageNumber == 3 || pageNumber != 3)
                    {
                        args.Player.SendSuccessMessage("Clan Help page (3/3)");
                        args.Player.SendInfoMessage(Helpmessages[10]);
                        args.Player.SendInfoMessage(Helpmessages[11]);
                        args.Player.SendInfoMessage(Helpmessages[12]);
                        args.Player.SendInfoMessage(Helpmessages[13]);
                        args.Player.SendInfoMessage(Helpmessages[14]);
                        //args.Player.SendInfoMessage("Type /clan help 4 for more");
                    }
                #endregion help
                    return;
            }
        }
        #endregion clan commmands
        string[] Helpmessages = new string[]
        {
            "/clan create <name> - create a new clan with you as leader.",
            "/clan list - lists all the existing clans.",
            "/clan invite - will invite a player to your clan.",
            "/clan acceptinvite - join a clan you were invited to.",
            "/clan join <name> - join an existing clan.",
            "/clan tp - teleport to the clan's spawnpoint.",
            "/clan setspawn - change the clan's spawnpoint.",
            "/clan leave - leave your clan (if leader every member will leave aswell).",
            "/clan inviteonly <true/false> - set Invite-only mode to true/false.",
            "/clan rank <player> <1-4> - will change the clanrank of a player.",
            "/clan editrank <1-5> <new rank name> - will change the rank prefix",
            "/clan listmembers - will list all the members of your current clan.",
            "/clan ban - will ban a player from your clan by Ip-Address.",
            "/clan unban - will unban a player from your clan (if he was banned).",
            "/clan kick - will kick a player out of your clan."
        };

        public void CheckUpdate()
        {
            int newversion;
            int currentversion;
            int.TryParse(wc.DownloadString("https://raw.github.com/ancientgods/Clans/master/README.md").Split('-')[1].Replace(".", ""), out newversion);
            int.TryParse(Assembly.GetExecutingAssembly().GetName().Version.ToString().Replace(".", ""), out currentversion);
            if (newversion > currentversion)
            {
                UpdateAvailable = true;
            }
        }
    }
}
