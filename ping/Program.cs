using System;
using System.Net.NetworkInformation;
using System.Media;
using System.Threading;
using System.Collections.Generic;
using CommandLine;
using System.Linq;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace ping
{
    class Program
    {
        [DllImport("User32.Dll", EntryPoint = "PostMessageA")]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, int wParam, int lParam);

        const int VK_RETURN = 0x0D;
        const int WM_KEYDOWN = 0x100;

        private static Timer repetitionsTimer = null;
        private static ConsoleKeyInfo key;
        private static Boolean ready = false;
        private static string line = null;
        private static string text = null;
        private static StreamWriter outputFile = null;
        private static FileStream fs = null;
        private static DateTime time;

        private static decimal sentCounter = 0;
        private static decimal receivedCounter = 0;
        private static decimal lostCounter = 0;

        private static decimal minTime = 0;
        private static decimal maxTime = 0;
        private static decimal avgTime = 0;

        private static string NameOrAddress;
        private static int Counter;
        private static bool NoStop;
        private static int Timeout;
        private static int RepetitionTime;
        private static bool SaveData;
        private static ConsoleColor SuccessColor = ConsoleColor.White;
        private static ConsoleColor FailedColor = ConsoleColor.White;
        private static bool Mute = false;
        private static string DefaultOutputFileName = null;
        private static string OutputFileName = null;

        private static SystemSound errorSound = SystemSounds.Hand;

        private interface ICommand
        {
            void Execute();
        }

        [Verb ("ping")]
        public class PingCommand : ICommand
        {
            [Value(0, Required = true, HelpText = "Name or IP address.")]
            public string NameOrAddress { get; set; }

            [Option('n', "counter", Default = 4, HelpText = "Number of echo requests to send.")]
            public int Counter { get; set; }
            
            [Option('t', HelpText = "Ping the specified host until stopped. To stop and see statistics - type Enter.")]
            public bool NoStop { get; set; }

            [Option('w', "timeout", Default = 1000, HelpText = "Timeout in milliseconds to wait for each reply.")]
            public int Timeout { get; set; }

            [Option('r', "repetition", Default = 1000, HelpText = "Repetition time in milliseconds to wait for between request to send.")]
            public int RepetitionTime { get; set; }

            [Option('f', "file", HelpText = "Save data to output file.")]
            public bool SaveData { get; set; }

            [Value(1, Default = "ping_'yyyy'.'MM'.'dd'_'HH'.'mm'.'ss'.txt", HelpText = "Output file name.")]
            public string OutputFileName { get; set; }

            public void Execute()
            {
                Program.NameOrAddress = this.NameOrAddress;
                Program.Counter = this.Counter;
                Program.NoStop = this.NoStop;
                Program.Timeout = this.Timeout;
                Program.RepetitionTime = this.RepetitionTime;
                Program.SaveData = this.SaveData;

                if (SaveData)
                {
                    if (Program.DefaultOutputFileName == null)
                    { 
                        Program.OutputFileName = GetOutputFileName(this.OutputFileName);
                    }
                    else
                    {
                        Program.OutputFileName = Program.DefaultOutputFileName;
                    }
                }

                Ping();
            }
        }

        [Verb("sound")]
        public class SoundCommand : ICommand
        {
            [Option("mute", HelpText = "Mute failure sound.")]
            public bool Mute { get; set; }

            public void Execute()
            {
                Program.Mute = this.Mute;
            }
        }

        [Verb("ipconfig")]
        public class IpconfigCommand : ICommand
        {
            [Option('a', "all", HelpText = "Display IP address, subnet mask and default gateway for each adapter bound to TCP/IP")]
            public bool All { get; set; }

            public void Execute()
            {
                Console.Clear();
                
                foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (networkInterface.Supports(NetworkInterfaceComponent.IPv4) == false)
                    {
                        if (this.All)
                        {
                            continue;
                        }
                    }

                    if (networkInterface.OperationalStatus != OperationalStatus.Up)
                    {
                        if (!this.All)
                        {
                            continue;
                        }
                    }

                    if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || networkInterface.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                    {
                        Console.WriteLine(networkInterface.Name);

                        if (this.All)
                        {
                            Console.WriteLine(networkInterface.Description);
                        }
                        
                        //Console.WriteLine(String.Empty.PadLeft(networkInterface.Description.Length, '='));
                        Console.WriteLine();

                        if (this.All)
                        {
                            Console.WriteLine("  Interface type ........ : {0}", networkInterface.NetworkInterfaceType);
                            Console.WriteLine("  Status ................ : {0}", networkInterface.OperationalStatus);

                            string  mac = networkInterface.GetPhysicalAddress().ToString(), 
                                    regex = "(.{2})(.{2})(.{2})(.{2})(.{2})(.{2})", 
                                    replace = "$1:$2:$3:$4:$5:$6";
                            Console.WriteLine("  Physical Address ...... : {0}", Regex.Replace(mac, regex, replace));

                            string versions = String.Empty;
                            if (networkInterface.Supports(NetworkInterfaceComponent.IPv4))
                            {
                                versions = "IPv4";
                            }
                            if (networkInterface.Supports(NetworkInterfaceComponent.IPv6))
                            {
                                if (!string.IsNullOrEmpty(versions))
                                {
                                    versions += " ";
                                }

                                versions += "IPv6";
                            }
                            Console.WriteLine("  IP version ............ : {0}", versions);
                        }

                        IPInterfaceProperties adapterProperties = networkInterface.GetIPProperties();
                        IPv4InterfaceProperties IPv4Properties = adapterProperties.GetIPv4Properties();

                        if (IPv4Properties == null)
                        {
                            Console.WriteLine("No IPv4 information is available for this interface.");
                            Console.WriteLine();
                            continue;
                        }

                        Console.WriteLine("  DHCP Enabled .......... : {0}", IPv4Properties.IsDhcpEnabled);

                        foreach (UnicastIPAddressInformation ip in networkInterface.GetIPProperties().UnicastAddresses)
                        {
                            if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                            {
                                Console.WriteLine("  IPv4 Address .......... : {0}", ip.Address.ToString());
                                Console.WriteLine("  Subnet Mask ........... : {0}", ip.IPv4Mask.ToString());
                            }
                        }

                        foreach (GatewayIPAddressInformation gateway in networkInterface.GetIPProperties().GatewayAddresses)
                        {
                            Console.WriteLine("  Gateway Address ....... : {0}",gateway.Address.ToString());
                        }

                        if (this.All)
                        {
                            foreach (IPAddress dns in adapterProperties.DnsAddresses)
                            {
                                if (dns.AddressFamily == AddressFamily.InterNetwork)
                                {
                                    Console.WriteLine("  DNS Servers ........... : {0}", dns.ToString());
                                }
                            }
                        }
                    }

                    Console.WriteLine();
                }
            }
        }

        [Verb("color", HelpText = "Foreground color")]
        public class ColorCommand : ICommand
        {
            [Option("success", Default = ConsoleColor.White, HelpText = "Values: Black, DarkBlue, DarkGreen, DarkCyan, DarkRed, DarkMagenta, DarkYellow, Gray, DarkGray, Blue, Green, Cyan, Red, Magenta, Yellow, White")]
            public ConsoleColor SuccessColor { get; set; }

            [Option("failed", Default = ConsoleColor.White, HelpText = "Values: Black, DarkBlue, DarkGreen, DarkCyan, DarkRed, DarkMagenta, DarkYellow, Gray, DarkGray, Blue, Green, Cyan, Red, Magenta, Yellow, White")]
            public ConsoleColor FailedColor { get; set; }

            public void Execute()
            {
                Program.SuccessColor = this.SuccessColor;
                Program.FailedColor = this.FailedColor;
            }
        }

        [Verb("file")]
        public class FileCommand : ICommand
        {
            [Value(0, HelpText = "Output file path.")]
            public string OutputFileName { get; set; }

            public void Execute()
            {
                Program.DefaultOutputFileName = GetOutputFileName(this.OutputFileName);
            }
        }

        static void Main(string[] args)
        {

            Console.WriteLine("Copyright (c) 2021 MidnightProject" + "\n" + "\n");

            try
            {
                string[] lines = File.ReadAllLines("default.txt");

                foreach (string line in lines)
                {
                    if (!line.StartsWith("#"))
                    {
                        args = line.Split(' ').ToArray();
                        ready = false;

                        Parser.Default.ParseArguments<PingCommand, SoundCommand, ColorCommand, FileCommand, IpconfigCommand>(args).WithParsed<ICommand>(t => t.Execute()); 
                    }
                }
            }
            catch (Exception e)
            {
               
            }

            do
            {
                line = Console.ReadLine().TrimEnd();
                args = line.Split(' ').ToArray();

                Parser.Default.ParseArguments<PingCommand, SoundCommand, ColorCommand, FileCommand, IpconfigCommand>(args).WithParsed<ICommand>(t => t.Execute());
            } while (true);
        }

        private static void Ping()
        {
            Console.Clear();

            sentCounter = 0;
            receivedCounter = 0;
            lostCounter = 0;

            minTime = 0;
            maxTime = 0;
            avgTime = 0;

            if (SaveData)
            {
                fs = new FileStream(@OutputFileName, FileMode.OpenOrCreate);
                outputFile = new StreamWriter(fs);

                outputFile.WriteLine("Copyright (c) 2021 MidnightProject");
                outputFile.WriteLine(String.Empty);
            }
            
            repetitionsTimer = new Timer(TimerCallback, null, 0, RepetitionTime);

            do
            {
                key = Console.ReadKey();

                if ((key.Modifiers & ConsoleModifiers.Control) != 0 && key.Key == ConsoleKey.X)
                {
                    Environment.Exit(0);
                }

                if (key.Key == ConsoleKey.Enter)
                {
                    break;
                }

            } while (true);

            repetitionsTimer.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
            repetitionsTimer = null;

            while (!ready)
            {
                ;
            }

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("\n");
            Console.WriteLine("Packets: Sent = " + sentCounter + "; Received = " + receivedCounter + "; Lost = " + lostCounter + " (" + Decimal.Round(lostCounter / sentCounter * 100) + "% loss)");
            if (receivedCounter != 0)
            {
                Console.WriteLine("Times [ms]: Minimum = " + minTime + "; Maximum = " + maxTime + "; Average = " + Decimal.Round(avgTime / sentCounter));
            }
            Console.WriteLine("\n");

            if (SaveData)
            {
                outputFile.WriteLine(String.Empty);
                outputFile.WriteLine("Packets: Sent = " + sentCounter + "; Received = " + receivedCounter + "; Lost = " + lostCounter + " (" + Decimal.Round(lostCounter / sentCounter * 100) + "% loss)");
                if (receivedCounter != 0)
                {
                    outputFile.WriteLine("Times [ms]: Minimum = " + minTime + "; Maximum = " + maxTime + "; Average = " + Decimal.Round(avgTime / sentCounter));
                }

                outputFile.Flush();
                outputFile.Close();
                fs.Close();
            } 
        }

        private static string GetOutputFileName(string name)
        {
            name = name.Replace("'yyyy'", DateTime.Now.ToString("yyyy"));
            name = name.Replace("'yy'", DateTime.Now.ToString("yy"));
            name = name.Replace("'MM'", DateTime.Now.ToString("MM"));
            name = name.Replace("'MMMM'", DateTime.Now.ToString("MMMM"));
            name = name.Replace("'dd'", DateTime.Now.ToString("dd"));
            name = name.Replace("'dddd'", DateTime.Now.ToString("dddd"));
            name = name.Replace("'HH'", DateTime.Now.ToString("HH"));
            name = name.Replace("'mm'", DateTime.Now.ToString("mm"));
            name = name.Replace("'ss'", DateTime.Now.ToString("ss"));

            return name;
        }

        private static void TimerCallback(Object o)
        {
            repetitionsTimer.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);

            if (Counter == 0)
            {
                ThreadPool.QueueUserWorkItem((q) =>
                {
                    var hWnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                    PostMessage(hWnd, WM_KEYDOWN, VK_RETURN, 0);
                });

                return;
            }

            if (!NoStop)
            {
                if (Counter > 0)
                {
                    Counter--;
                }
            }

            PingHostAsync(NameOrAddress, Timeout);
        }

        /* ---------------------------------------------------------------------------------------------------------- */
        /* Author: Alexandru Clonțea */
        /* Link: https://stackoverflow.com/questions/49069381/why-ping-timeout-is-not-working-correctly               */
        /* ---------------------------------------------------------------------------------------------------------- */
        private static PingReply PingOrTimeout(string hostname, int timeOut)
        {
            PingReply result = null;
            var cancellationTokenSource = new CancellationTokenSource();
            var timeoutTask = Task.Delay(timeOut, cancellationTokenSource.Token);

            var actionTask = Task.Factory.StartNew(() =>
            {
                result = NormalPing(hostname, timeOut);
            }, cancellationTokenSource.Token);

            Task.WhenAny(actionTask, timeoutTask).ContinueWith(t =>
            {
                cancellationTokenSource.Cancel();
            }).Wait();

            return result;
        }

        /* ---------------------------------------------------------------------------------------------------------- */
        /* Ping.Send Method                                                                                           */
        /* Timeout                                                                                                    */
        /* When specifying very small numbers for timeout,                                                            */
        /* the Ping reply can be received even if timeout milliseconds have elapsed.                                  */
        /* ---------------------------------------------------------------------------------------------------------- */
        private static PingReply NormalPing(string hostname, int timeout)                           
        {
            try
            {
                return new Ping().Send(hostname, timeout);
            }
            catch (Exception e)
            {
                return null;
            }
        }

        private static void PingHostAsync(string nameOrAddress, int timeout)
        {
            if (repetitionsTimer == null)
            {
                return;
            }

            ready = false;

            sentCounter++;

            Ping ping = null;
            PingReply reply = null;

            time = DateTime.Now;

            try
            {
                ping = new Ping();
                reply = PingOrTimeout(nameOrAddress, timeout);

                if (reply == null)
                {
                    Console.ForegroundColor = FailedColor;
                    text = "Ping to " + nameOrAddress + ": TimedOut";

                    ErrorSound();
                }
                else if (reply.Status == IPStatus.Success)
                {
                    receivedCounter++;

                    if (receivedCounter > 1)
                    {
                        if (reply.RoundtripTime < minTime)
                        {
                            minTime = reply.RoundtripTime;
                        }

                        if (reply.RoundtripTime > maxTime)
                        {
                            maxTime = reply.RoundtripTime;
                        }
                    }
                    else if (receivedCounter == 1)
                    {
                        minTime = reply.RoundtripTime;
                        maxTime = reply.RoundtripTime;
                    }

                    avgTime += reply.RoundtripTime;

                    Console.ForegroundColor = SuccessColor;
                    text = "Ping to " + nameOrAddress + " [" + reply.Address.ToString() + "]: " + reply.Status.ToString() + "; " + "time = " + reply.RoundtripTime.ToString() + " ms";
                }
                else
                {
                    Console.ForegroundColor = FailedColor;
                    text = "Ping to " + nameOrAddress + ": " + reply.Status.ToString();

                    ErrorSound();
                }
            }
            catch (PingException e)
            {
                Console.ForegroundColor = FailedColor;
                text = e.InnerException.Message;

                ErrorSound();
            }
            finally
            {
                try
                {
                    ping.Dispose();
                }
                catch
                {

                }

                Console.WriteLine(text);

                if (SaveData)
                {
                    outputFile.WriteLine(time.ToString("yyyy.MM.dd_HH:mm:ss.fff") + "   " + text);
                }
            }

            lostCounter = sentCounter - receivedCounter;

            if (repetitionsTimer != null)
            {
                repetitionsTimer.Change(RepetitionTime, RepetitionTime);
            }

            ready = true;
        }

        private static void ErrorSound()
        {
            if (!Mute)
            {
                errorSound.Play();
            }
        }
    }
}
 
 
 
 
 