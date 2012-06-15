/*
 * Created by SharpDevelop.
 * User: novalis78
 * Date: 08.12.2004
 * Time: 19:08
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Meebey.SmartIrc4net;
using System.Timers;
using PreludeEngine;
using NLog;
using System.IO;
using AMS.Profile;

namespace PreludeIRC
{
    class lircBot
    {
        public static IrcClient irc = null;
        public static IrcClient irc_test = null;
        private static Profile profile = null;
        private static PreLudeInterface pi = null;
        private static bool allowPrivateMessages = true;
        private static bool allowPublicMessages = false;
        private static string TargetUser = "";
        private static bool relayChat = false;
        private static string Spy = "";
        private static bool floodChannel = false;
        private static bool allowAny = true; //if you don't want the bot to be able to be chatted with right away set to false
        //in that case we  have to first send him a private message "/msg preludini" with the command "cmd: any on".
        private static bool proactiveMode = true;
        private static SortedList sl = null;
        private static System.Timers.Timer timer = null;
        private static int idleTime = 0;
        private static string autoSpeakInput = "";
        private static IrcEventArgs latest = null;
        private static List<string> AllChannels = new List<string>();
        private static DateTime lastTimeISaidSomething;
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private static int reconnectCountdown = 0;
        private static string[]  serverlist = new string[] { "irc.dal.net" };
        private static int port = 6667;
        private static string channel = "#sexo";
        private static string nick = "sexGirlFlower";
        private static string real = "PLEIRC";

        public static void Main(string[] args)
        {

            //this method is just for some tests
            //TestConnectionStability();
            //return;
            
            
            int cycle_cnt = 0;
            while (true)
            {
                cycle_cnt++;
                //Console.Clear();
                Console.WriteLine("Cycle: " + cycle_cnt);
                Thread.CurrentThread.Name = "Main";

                #region Settings
                string startupPath = Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
                if (File.Exists(startupPath + "\\settings.ini"))
                    profile = new Ini(startupPath + "\\settings.ini");
                else
                {
                    logger.Trace("Did not find" + startupPath);

                }
                channel = (string)profile.GetValue("Main", "Channel");
                nick = (string)profile.GetValue("Main", "Nick");
                real = (string)profile.GetValue("Main", "Real");
                #endregion

                #region IRC Setup
                irc = new IrcClient();
                irc.SendDelay = 500;
                irc.ActiveChannelSyncing = true;
                irc.OnChannelMessage += new IrcEventHandler(OnChannelMessage);
                irc.OnQueryMessage += new IrcEventHandler(OnQueryMessage);
                irc.OnBan += new BanEventHandler(OnBanMessage);

                irc.OnError += new Meebey.SmartIrc4net.ErrorEventHandler(OnError);
                irc.OnPart += new PartEventHandler(irc_OnPart);
                irc.OnRawMessage += new IrcEventHandler(OnRawMessage);
                if (proactiveMode)
                {
                    timer = new System.Timers.Timer();
                    timer.Elapsed += new System.Timers.ElapsedEventHandler(autoAnswering);
                }
                #endregion
                
                sl = new SortedList();
           

                Console.WriteLine("**********************************************************");
                Console.WriteLine("These are my settings: ");
                Console.WriteLine("Trying to connect to " + serverlist[0] + " on port " + port + " - joining channel: " + channel);
                Console.WriteLine("My nickname is: " + nick + " and my real name is: " + real);
                Console.WriteLine("**********************************************************");
                try
                {
                    irc.AutoRetry = true;
                    irc.AutoReconnect = true;
                    irc.Connect(serverlist, port);
                }
                catch (ConnectionException e)
                {
                    logger.Trace("couldn't connect! Reason: " + e.Message);
                }

                try
                {
                    // here we logon and register our nickname and so on 
                    irc.OnRawMessage += new IrcEventHandler(irc_OnRawMessage);
                    irc.Login(nick, real);

                    //load all channels
                    irc.RfcList("");

                    Dictionary<string, int> chann = new Dictionary<string, int>();

                    // join the channel
                    irc.RfcJoin(channel);
                    irc.OnChannelAction += new ActionEventHandler(irc_OnChannelAction);

                    #region Prelude Setup
                    //initialize interface
                    logger.Trace("Loading Prelude...");
                    pi = new PreLudeInterface();
                    //define path to mind file
                    pi.loadedMind = "mind.mdu";
                    pi.avoidLearnByRepeating = true;
                    pi.initializedAssociater = Mind.MatchingAlgorithm.Dice;
                    //start your engine ...
                    pi.initializeEngine();
                    logger.Trace("Prelude loaded and initialized...");
                    #endregion

                    // spawn a new thread to read the stdin of the console, this we use
                    // for reading IRC commands from the keyboard while the IRC connection
                    // stays in its own thread
                    new Thread(new ThreadStart(ReadCommands)).Start();
                    irc.Listen();
                    // when Listen() returns our IRC session is over, to be sure we call
                    // disconnect manually
                    irc.Disconnect();
                }
                catch (ConnectionException)
                {
                    logger.Trace("Connection exception");
                    pi.stopPreludeEngine();
                }
                catch (Exception e)
                {
                    logger.Trace("Error occurred! Message: " + e.Message);
                    logger.Trace("Exception: " + e.StackTrace);
                    pi.stopPreludeEngine();

                }

                logger.Trace("Going to sleep");
                System.Threading.Thread.Sleep(5 * 60000);
                logger.Trace("===========================================");
            }
        }


        #region IRC TEST METHODS
        /// <summary>
        /// just testing the IRC chat connection stability...
        /// </summary>
        private static void TestConnectionStability()
        {
            while (true)
            {
                try
                {
                    irc_test = new IrcClient();
                    irc_test.AutoRetry = true;
                    irc_test.AutoReconnect = true;
                    irc_test.OnChannelMessage += new IrcEventHandler(irc_test_OnChannelMessage);
                    irc_test.OnRawMessage += new IrcEventHandler(irc_test_OnRawMessage);
                    // here we try to connect to the server and exceptions get handled
                    irc_test.Connect(serverlist, port);
                }
                catch (ConnectionException e)
                {
                    // something went wrong, the reason will be shown
                    logger.Trace("couldn't connect! Reason: " + e.Message);
                    //Exit();
                }

                try
                {
                    // here we logon and register our nickname and so on 
                    irc_test.Login(nick, real);

                    // join the channel
                    irc_test.RfcJoin(channel);


                    // here we tell the IRC API to go into a receive mode, all events
                    // will be triggered by _this_ thread (main thread in this case)
                    // Listen() blocks by default, you can also use ListenOnce() if you
                    // need that does one IRC operation and then returns, so you need then 
                    // an own loop 
                    irc_test.Listen();

                    // when Listen() returns our IRC session is over, to be sure we call
                    // disconnect manually
                    irc_test.Disconnect();
                }
                catch (ConnectionException e)
                {
                    // this exception is handled becaused Disconnect() can throw a not
                    // connected exception
                    logger.Trace("Connection exception: " + e.Message);
                    //Exit();
                }
                catch (Exception e)
                {
                    // this should not happen by just in case we handle it nicely
                    logger.Trace("Error occurred! Message: " + e.Message);
                    logger.Trace("Exception: " + e.StackTrace);
                    //Exit();
                }

                logger.Trace("Going to sleep");
                System.Threading.Thread.Sleep(5*60000);
                logger.Trace("===========================================");
            }
        }

        static void irc_test_OnRawMessage(object sender, IrcEventArgs e)
        {
            logger.Trace(e.Data.Message);
            if (e.Data.Message.Contains("This server was"))
            {
                if(irc_test.IsConnected)
                    irc_test.Disconnect();
            }
        }

        static void irc_test_OnChannelMessage(object sender, IrcEventArgs e)
        {
            logger.Trace(e.Data.Message);
        }
        #endregion

        static void irc_OnChannelAction(object sender, ActionEventArgs e)
        {
            if (e.ActionMessage == "joined")
            {
                Channel c = irc.GetChannel("");
                Hashtable users = c.Users;
                Random n = new Random();
                List<string> l = new List<string>();
                foreach(KeyValuePair<string, string> p in users)
                {
                    l.Add(p.Key);
                }
                string user = l[n.Next(l.Count)];
                if(!sl.ContainsKey(user))
                {
                    irc.RfcPrivmsg(TargetUser, "hi there!");
                    allowPrivateMessages = true;
                    sl.Add(user, "");
                }
            }
        }

        static void irc_OnPart(object sender, PartEventArgs e)
        {
            logger.Trace("Part: " + e.Channel);
        }

        static void irc_OnRawMessage(object sender, IrcEventArgs e)
        {

            if (!String.IsNullOrEmpty(e.Data.Message) && e.Data.Message.Contains("not in any channel anymore"))
            {
                if (irc.IsConnected)
                    irc.Disconnect();
            }
            if (e.Data.ReplyCode == Meebey.SmartIrc4net.ReplyCode.List)
            {
                if(!AllChannels.Contains(e.Data.RawMessage))
                    AllChannels.Add(e.Data.RawMessage);
            }
            if (e.Data.ReplyCode == Meebey.SmartIrc4net.ReplyCode.ListEnd)
            {
                Random r = new Random();
                int index = r.Next(AllChannels.Count);
                string randomString = AllChannels[index];
                int a = randomString.IndexOf("#");
                int en = randomString.IndexOf(" ", a);
                string channel = randomString.Substring(a, en - a);
                irc.RfcJoin(channel);

                //initialize various timers at this point...
                idleTime = 30000;
                timer.Interval = idleTime;
                timer.Start();
                lastTimeISaidSomething = DateTime.Now; 
            }

           
        } 


       

        public static void OnQueryMessage(object sender, IrcEventArgs e)
        {
            delegateMessageToPrelude(e);
        }

        public static void OnChannelMessage(object sender, IrcEventArgs e)
        {
            delegateMessageToPrelude(e);
        }

        public static void OnError(object sender, Meebey.SmartIrc4net.ErrorEventArgs e)
        {
            System.Console.WriteLine("Error: " + e.ErrorMessage);
            pi.stopPreludeEngine();
            //Exit();
        }

        public static void OnBanMessage(object sender, BanEventArgs e)
        {
            System.Console.WriteLine("I was banned by !arrrgh " + e.Data.Nick);
            string a = "That was not nice. no good karma. simply because you dont like me, " +
                "you dont have to ban me!";
            irc.SendMessage(SendType.Message, e.Data.Nick, a);
        }

        public static void OnRawMessage(object sender, IrcEventArgs e)
        {
            System.Console.WriteLine("Received: " + e.Data.Message);
            string m = e.Data.Message;
            if (!String.IsNullOrEmpty(m))
            {
                if (e.Data.Message.ToLower().Contains("with another nickname"))
                {
                    Random r = new Random();
                    irc.RfcNick(irc.Nickname + r.Next(2, 500));
                }
            }
        }

        public static void ReadCommands()
        {
            // here we read the commands from the stdin and send it to the IRC API
            // WARNING, it uses WriteLine() means you need to enter RFC commands
            // like "JOIN #test" and then "PRIVMSG #test :hello to you"
            while (true)
            {
                irc.WriteLine(System.Console.ReadLine());
            }
        }

        public static void Exit()
        {
            // we are done, lets exit...
            try
            {
                pi.stopPreludeEngine();
            }
            catch (System.Exception ex)
            {
                Console.WriteLine("Oh no, exception occured while shutting done prelude...");
            }
            System.Console.WriteLine("Exiting...");
            System.Environment.Exit(0);
        }

        public static void delegateMessageToPrelude(IrcEventArgs e)
        {
            latest = e;
            //collect users
            if (!sl.Contains(e.Data.Nick))
            {
                sl.Add(e.Data.Nick, e.Data.Ident);
                logger.Trace("So far I talked to: " + sl.Count + " people.");
            }
            if (e.Data.Message.StartsWith("cmd:"))
            {
                listenToCommands(e);
            }
            else
            {
                if ((e.Data.Nick == TargetUser) || allowAny)
                {
                    string ind = e.Data.Message;
                    string a = "";
                    if (allowPrivateMessages)
                    {
                        if (proactiveMode)
                        {
                            idleTime = 0;
                            if (timer != null)
                                timer.Stop();
                        }
                        //got something:
                        if (ind.Contains("♥") || ind.ToLower().Contains("== Question") || ind.ToLower().Contains("hint:") || ind.ToLower().Contains("times up") || e.Data.Nick.ToLower().Contains("radio"))
                        {
                            SwitchChannel();
                            return;
                        }

                        string userInput = "User said (private|" + e.Data.Nick + "):\t" + ind;
                        appendUserLog(e.Data.Nick, userInput);
                        logger.Info(userInput);
                        
                        //now wait a bit
                        Random rand = new Random();
                        int rnum = rand.Next(8, 15);
                        System.Threading.Thread.Sleep(1000 * rnum);
                        //now answer
                        a = pi.chatWithPrelude(ind);
                        irc.SendMessage(SendType.Message, e.Data.Nick, a);

                        string preludeOutput = "Prelude responded to (pm | " + e.Data.Nick + "):\t" + a;
                        appendUserLog(e.Data.Nick, preludeOutput);
                        logger.Info(preludeOutput);
                        lastTimeISaidSomething = DateTime.Now;

                        //now make sure we save it..
                        pi.forcedSaveMindFile();
                        if (relayChat)
                        {
                            irc.SendMessage(SendType.Message, Spy, ind);
                            irc.SendMessage(SendType.Message, Spy, a);
                        }
                        if (proactiveMode)
                        {
                            Random random = new Random();
                            idleTime = random.Next(15000, 30000);
                            timer.Interval = idleTime;
                            autoSpeakInput = a;
                            timer.Start();
                        }
                    }
                    else if (allowPublicMessages)
                    {
                        logger.Trace("User (plublic): " + ind);
                        a = pi.chatWithPrelude(ind);
                        logger.Trace("Prelude: " + a);
                        irc.SendMessage(SendType.Message, e.Data.Channel, "@" + TargetUser + ": " + a);

                        if (relayChat)
                        {
                            irc.SendMessage(SendType.Message, Spy, ind);
                            irc.SendMessage(SendType.Message, Spy, a);
                        }
                    }
                }
                else if (floodChannel)
                {
                    if (allowPublicMessages)
                    {
                        string ind = e.Data.Message;
                        string a = "";
                        irc.SendMessage(SendType.Message, e.Data.Channel, "@" + TargetUser + ": " + a);
                    }
                }
            }
        }

        private static void appendUserLog(string p, string message)
        {
            if (!File.Exists(p + ".txt"))
            {
                FileStream aFile = new FileStream(p + ".txt", FileMode.Create, FileAccess.Write);
                StreamWriter sw = new StreamWriter(aFile);
                sw.WriteLine(message);
                sw.Close();
                aFile.Close();
            }
            else
            {
                FileStream aFile = new FileStream(p + ".txt", FileMode.Append, FileAccess.Write);
                StreamWriter sw = new StreamWriter(aFile);
                sw.WriteLine(message);
                sw.Close();
                aFile.Close();
            }
        }


        private static void listenToCommands(IrcEventArgs e)
        {
            if (e.Data.MessageArray.Length > 1)
            {
                switch (e.Data.MessageArray[1])
                {
                    // debug stuff
                    case "list":
                        {
                            string a = "list, gc, set, join, relay, rename, any, flood, part, die, settings, size, met, help";
                            irc.SendMessage(SendType.Message, e.Data.Nick, a);
                            break;
                        }
                    case "gc":
                        {
                            GC.Collect();
                            irc.SendMessage(SendType.Message, e.Data.Nick, "I garbage collected...");
                            break;
                        }
                    case "help":
                        {
                            if (e.Data.MessageArray.Length > 2)
                            {
                                showHelpForCommand(e);
                            }
                            else
                            {
                                string a = "missing parameter. syntax is -> 'cmd: help command'. Try 'cmd: list' to start with...";
                                irc.SendMessage(SendType.Message, e.Data.Nick, a);
                            }
                            break;
                        }
                    case "rename":
                        {
                            if (e.Data.MessageArray.Length > 2)
                            {
                                string a = "not possible with IRC, my master. Try new Login!";
                                irc.SendMessage(SendType.Message, e.Data.Nick, a);
                            }
                            else
                            {
                                string a = "missing parameter. syntax is -> 'cmd: rename newname'";
                                irc.SendMessage(SendType.Message, e.Data.Nick, a);
                            }
                            break;
                        }
                    case "size":
                        {
                            int size = pi.countMindMemory();
                            irc.SendMessage(SendType.Message, e.Data.Nick, size.ToString());
                            break;
                        }
                    case "met":
                        {
                            string a = "I met ";
                            IDictionaryEnumerator ide = sl.GetEnumerator();
                            while (ide.MoveNext())
                                a += ide.Key + ",";
                            irc.SendMessage(SendType.Message, e.Data.Nick, a);
                            break;
                        }
                    // typical commands
                    case "join":
                        {
                            if (e.Data.MessageArray.Length > 2)
                            {
                                irc.RfcJoin(e.Data.MessageArray[2]);
                            }
                            else
                            {
                                string a = "missing parameter. syntax: cmd join channel";
                                irc.SendMessage(SendType.Message, e.Data.Nick, a);
                            }
                            break;
                        }
                    case "delay":
                        {
                            if (e.Data.MessageArray.Length > 2)
                            {
                                irc.SendDelay = Convert.ToInt32(e.Data.MessageArray[2]);
                                string a = "set delay value to " + irc.SendDelay;
                                irc.SendMessage(SendType.Message, e.Data.Nick, a);
                            }
                            else
                            {
                                string a = "missing parameter. syntax: 'cmd: delay milliseconds'";
                                irc.SendMessage(SendType.Message, e.Data.Nick, a);
                            }
                            break;
                        }
                    case "part":
                        {
                            if (e.Data.MessageArray.Length > 2)
                            {
                                irc.RfcPart(e.Data.MessageArray[2]);
                            }
                            else
                            {
                                string a = "missing parameter. syntax: cmd part channel";
                                irc.SendMessage(SendType.Message, e.Data.Nick, a);
                            }
                            break;
                        }
                    case "settings":
                        {
                            showCurrentBotSettings(e);
                            break;
                        }
                    case "set":
                        {
                            setNewBotSettings(e);
                            break;
                        }
                    case "relay":
                        {
                            if (e.Data.MessageArray.Length > 2)
                            {
                                Spy = e.Data.MessageArray[2];
                                irc.SendMessage(SendType.Message, e.Data.Nick, "successfully set spy: " + Spy);
                            }
                            else
                            {
                                string a = "missing parameter. syntax: cmd: relayto user";
                                irc.SendMessage(SendType.Message, e.Data.Nick, a);
                            }
                            break;
                        }
                    case "flood":
                        {
                            if (e.Data.MessageArray.Length > 2)
                            {
                                floodChannel = (e.Data.MessageArray[2] == "on" ? true : false);
                                irc.SendMessage(SendType.Message, e.Data.Nick, (floodChannel == true) ? "flood set to 'on'" : "flood set to 'off'");
                            }
                            else
                            {
                                string a = "missing parameter. syntax: cmd: flood on|off";
                                irc.SendMessage(SendType.Message, e.Data.Nick, a);
                            }
                            break;
                        }
                    case "any":
                        {
                            if (e.Data.MessageArray.Length > 2)
                            {
                                allowAny = (e.Data.MessageArray[2] == "on" ? true : false);
                            }
                            else
                            {
                                string a = "missing parameter. syntax: cmd: any on|off";
                                irc.SendMessage(SendType.Message, e.Data.Nick, a);
                            }
                            break;
                        }
                    case "pm":
                        {
                            if (e.Data.MessageArray.Length > 2)
                            {
                                TargetUser = e.Data.MessageArray[2];
                                irc.RfcPrivmsg(TargetUser, "hi there!");
                                allowPrivateMessages = true;
                            }
                            else
                            {
                                string a = "You did not specify a target user (like 'cmd: pm user') - " +
                                    "so i guess you want to allow pm for anyone. i set it to true. You" +
                                    "being already in pm mode, i start chatting with you now!";
                                allowPrivateMessages = true;
                                allowAny = true;
                                irc.SendMessage(SendType.Message, e.Data.Nick, a);
                                irc.RfcPrivmsg(e.Data.Nick, "hi there, master!");
                            }
                            break;
                        }
                    case "die":
                        Exit();
                        break;
                    default:
                        {
                            string a = "available commands are: list, gc, set, " +
                                "join, relay, rename, any, flood, part, die, settings, size, met, help";
                            irc.SendMessage(SendType.Message, e.Data.Nick, a);
                            break;
                        }
                }
            }
            else
            {
                string a = "missing parameter. syntax: 'cmd: command [param]'. type 'cmd: list' for a list of available commands.";
                irc.SendMessage(SendType.Message, e.Data.Nick, a);

            }

        }
        private static void setNewBotSettings(IrcEventArgs e)
        {
            if (e.Data.MessageArray[2].IndexOf("pm") != -1)
            {
                if (e.Data.MessageArray[3].IndexOf("on") != -1)
                {
                    allowPrivateMessages = true;
                    irc.SendMessage(SendType.Message, e.Data.Nick, "pm set to on");
                }
                else if (e.Data.MessageArray[3].IndexOf("off") != -1)
                {
                    allowPrivateMessages = false;
                    irc.SendMessage(SendType.Message, e.Data.Nick, "pm set to off");
                }
                else
                    irc.SendMessage(SendType.Message, e.Data.Nick, "syntax: set pm on|off");
            }
            else if (e.Data.MessageArray[2].IndexOf("cm") != -1)
            {
                if (e.Data.MessageArray[3].IndexOf("on") != -1)
                {
                    allowPublicMessages = true;
                    irc.SendMessage(SendType.Message, e.Data.Nick, "cm set to on");
                }
                else if (e.Data.MessageArray[3].IndexOf("off") != -1)
                {
                    allowPublicMessages = false;
                    irc.SendMessage(SendType.Message, e.Data.Nick, "cm set to off");
                }
                else
                    irc.SendMessage(SendType.Message, e.Data.Nick, "syntax: set cm on|off");
            }
            else
            {
                irc.SendMessage(SendType.Message, e.Data.Nick, "syntax: set cm|pm on|off");
            }

        }

        private static void showCurrentBotSettings(IrcEventArgs e)
        {
            string a = "PM is " + Convert.ToString(allowPrivateMessages).ToLower();
            a += "| CM is " + Convert.ToString(allowPublicMessages).ToLower();
            irc.SendMessage(SendType.Message, e.Data.Nick, a);
        }

        private static void showHelpForCommand(IrcEventArgs e)
        {
            switch (e.Data.MessageArray[2])
            {
                case "help":
                    irc.SendMessage(SendType.Message, e.Data.Nick, "Type 'cmd: help command' to get a short description of each command"); break;
                case "list":
                    irc.SendMessage(SendType.Message, e.Data.Nick, "Type 'cmd: list' to get a list of all available commands"); break;
            }
        }

        private static void autoAnswering(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                //trigger auto answer to frontend
                if (timer.Enabled != false)
                {
                    string[] incs = irc.GetChannels();
                    if (incs.Length <= 0)
                    {
                        logger.Trace("not in any channel anymore");
                        reconnectCountdown++;
                        if (reconnectCountdown > 5)
                        {
                            //irc.Disconnect();
                            //irc.Reconnect();
                            System.Threading.Thread.Sleep(5000);
                            try
                            {
                                irc.Connect(serverlist, port);
                                reconnectCountdown = 0;
                            }
                            catch (System.Exception ex)
                            {
                                if (ex.Message.ToLower().Contains("already connected to"))
                                {
                                    reconnectCountdown = 0;
                                    irc.Disconnect();
                                }
                                logger.Trace(ex.Message);
                            }
                        }
                        logger.Trace("My Mind Size: " + pi.countMindMemory());
                        try
                        {
                            ; //pi.forcedSaveMindFile();
                        }
                        catch (System.Exception ex)
                        {
                            logger.Trace(ex.Message);
                        }
                        SwitchChannel();
                        
                    }
                    else
                    {
                        foreach (string a in incs)
                        {
                            reconnectCountdown = 0;
                            logger.Trace("I am still active in these channels: " + a);
                            DateTime rightnow = DateTime.Now;
                            TimeSpan diff = rightnow - lastTimeISaidSomething;
                            if (diff.TotalMinutes > 5)
                            {
                                irc.RfcPart(a);
                                System.Threading.Thread.Sleep(30000);
                                logger.Trace("Switching channels...has been too long");
                                SwitchChannel();
                                lastTimeISaidSomething = DateTime.Now; //reset..otherwise we will switch all the time if nobody is talking to us
                            }
                            if (true)
                            {
                                Channel c = irc.GetChannel(a);
                                Hashtable users = c.Users;
                                Random n = new Random();
                                List<string> l = new List<string>();
                                foreach (DictionaryEntry u in users)
                                {
                                    l.Add(u.Key.ToString());
                                }
                                string user = l[n.Next(l.Count)];
                                if (!sl.ContainsKey(user) && user != "sexGirlFlower")
                                {
                                    irc.RfcPrivmsg(user, "hi there!");
                                    logger.Trace("prelude said hi to " + user);
                                    allowPrivateMessages = true;
                                    sl.Add(user, "");
                                }
                            }
                            
                        }
                        //pi.forcedSaveMindFile();
                        
                        logger.Trace("My Mind Size: " + pi.countMindMemory());
                    }
                    //string answer = pi.chatWithPrelude(autoSpeakInput);
                    //logger.Trace("Prelude (auto): " + autoSpeakInput);
                    //irc.SendMessage(SendType.Message, latest.Data.Nick, answer);
                }
            }
            catch (System.Exception ex)
            {
                logger.Trace("error: " + ex.Message);
            }

        }

        private static void SwitchChannel()
        {
            Random r = new Random();
            int index = r.Next(AllChannels.Count);
            string randomString = AllChannels[index];
            int a = randomString.IndexOf("#");
            int en = randomString.IndexOf(" ", a);
            string channel = randomString.Substring(a, en - a);
            logger.Trace("Joining " + channel);
            irc.RfcJoin(channel);
        }
    }
}
