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

namespace PreludeIRC
{
    class lircBot
    {
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
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public static void Main(string[] args)
        {
            Console.WriteLine("Alternatively start program with parameters: ");
            Console.WriteLine("preludeIrc.exe server port #channelname");
            Console.WriteLine("");
            Thread.CurrentThread.Name = "Main";

            irc.SendDelay = 500;
            irc.ActiveChannelSyncing = true;
            irc.OnChannelMessage += new IrcEventHandler(OnChannelMessage);
            irc.OnQueryMessage += new IrcEventHandler(OnQueryMessage);
            irc.OnBan += new BanEventHandler(OnBanMessage);

            irc.OnError += new ErrorEventHandler(OnError);
            irc.OnRawMessage += new IrcEventHandler(OnRawMessage);
            if (proactiveMode)
            {
                timer = new System.Timers.Timer();
                timer.Elapsed += new System.Timers.ElapsedEventHandler(autoAnswering);
            }
            sl = new SortedList();
            string[] serverlist;
            serverlist = new string[] { "irc.dal.net" };
            int port = 6667;
            string channel = "#germany";
            string nick = "preludini";
            string real = "PLEIRC";
            if (args.Length > 2)
            {
                serverlist[0] = args[0];
                port = Convert.ToInt32(args[1]);
                channel = args[2];
            }
            else
            {
                Console.WriteLine("Missing parameters on program start. Start this program with params 'server' 'port' '#channelname'");
            }
            Console.WriteLine("**********************************************************");
            Console.WriteLine("These are my settings: ");
            Console.WriteLine("Trying to connect to " + serverlist[0] + " on port " +
                              port + " - joining channel: " + channel);
            Console.WriteLine("My nickname is: " + nick + " and my real name is: " + real);
            Console.WriteLine("**********************************************************");
            try
            {
                // here we try to connect to the server and exceptions get handled
                irc.Connect(serverlist, port);
            }
            catch (ConnectionException e)
            {
                // something went wrong, the reason will be shown
                logger.Trace("couldn't connect! Reason: " + e.Message);
                Exit();
            }

            try
            {
                // here we logon and register our nickname and so on 
                irc.OnRawMessage += new IrcEventHandler(irc_OnRawMessage);	
                irc.Login(nick, real);
                
                //load all channels
                
                irc.RfcList("");

                Dictionary<string, int> chann = new  Dictionary<string, int>();
                
                
                
                // join the channel
                irc.RfcJoin(channel);

                // here we send just 3 different types of messages, 3 times for
                // testing the delay and flood protection (messagebuffer work)
           //irc.SendMessage(SendType.Message, channel, "hi @ all");
                //irc.SendMessage(SendType.Action, channel, "thinks this is cool "+i.ToString());
                //irc.SendMessage(SendType.Notice, channel, "SmartIrc4net rocks "+i.ToString());

                //initialize interface
                pi = new PreLudeInterface();
                //define path to mind file
                pi.loadedMind = "mind.mdu";
                pi.avoidLearnByRepeating = true;
                pi.initializedAssociater = Mind.MatchingAlgorithm.Dice;
                //start your engine ...
                pi.initializeEngine();

                // spawn a new thread to read the stdin of the console, this we use
                // for reading IRC commands from the keyboard while the IRC connection
                // stays in its own thread
                new Thread(new ThreadStart(ReadCommands)).Start();

                // here we tell the IRC API to go into a receive mode, all events
                // will be triggered by _this_ thread (main thread in this case)
                // Listen() blocks by default, you can also use ListenOnce() if you
                // need that does one IRC operation and then returns, so you need then 
                // an own loop 
                irc.Listen();

                // when Listen() returns our IRC session is over, to be sure we call
                // disconnect manually
                irc.Disconnect();
            }
            catch (ConnectionException)
            {
                // this exception is handled becaused Disconnect() can throw a not
                // connected exception
                logger.Trace("Connection exception");
                Exit();
            }
            catch (Exception e)
            {
                // this should not happen by just in case we handle it nicely
                logger.Trace("Error occurred! Message: " + e.Message);
                logger.Trace("Exception: " + e.StackTrace);
                Exit();
            }
        }

        static void irc_OnRawMessage(object sender, IrcEventArgs e)
        {
            
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
            }

           
        } 


        public static IrcClient irc = new IrcClient();

        public static void OnQueryMessage(object sender, IrcEventArgs e)
        {
            delegateMessageToPrelude(e);
        }

        public static void OnChannelMessage(object sender, IrcEventArgs e)
        {
            delegateMessageToPrelude(e);
        }

        public static void OnError(object sender, ErrorEventArgs e)
        {
            System.Console.WriteLine("Error: " + e.ErrorMessage);
            pi.stopPreludeEngine();
            Exit();
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
                        logger.Info("User (private|"+e.Data.Nick+"): " + ind);
                        
                        //now wait a bit
                        Random rand = new Random();
                        int rnum = rand.Next(2, 15);
                        //System.Threading.Thread.Sleep(1000 * rnum);
                        
                        //now answer
                        a = pi.chatWithPrelude(ind);
                        irc.SendMessage(SendType.Message, e.Data.Nick, a);
                        logger.Info("User said (private|" + e.Data.Nick + "): " + ind);
                        logger.Info("To which Prelude responded: " + a);

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
                        Random r = new Random();
                        int index = r.Next(AllChannels.Count);
                        string randomString = AllChannels[index];
                        int a = randomString.IndexOf("#");
                        int en = randomString.IndexOf(" ", a);
                        string channel = randomString.Substring(a, en - a);
                        logger.Trace("Joining " + channel);
                        irc.RfcJoin(channel);
                        
                    }
                    else
                    {
                        foreach (string a in incs)
                        {
                            logger.Trace("I am still active in these channels: " + a);
                        }
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
    }
}
