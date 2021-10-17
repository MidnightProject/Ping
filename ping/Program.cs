using System;
using System.Net.NetworkInformation;
using System.Media;
using System.Threading;
using System.Collections.Generic;
using CommandLine;
using System.Linq;
using System.Runtime.InteropServices;

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
        private static string[] arguments = null;

        private static string nameOrAddress = null;
        private static int counter = 0;
        private static int timeout = 0;
        private static int repetitionTime = 0;


        private static decimal sentCounter = 0;
        private static decimal receivedCounter = 0;
        private static decimal lostCounter = 0;

        private static decimal minTime = 0;
        private static decimal maxTime = 0;
        private static decimal avgTime = 0;

        private static SystemSound errorSound = SystemSounds.Hand;


        static void Main(string[] args)
        {

            Console.WriteLine("Copyright (c) 2021 MidnightProject" + "\n" + "\n");

            do
            {
                sentCounter = 0;
                receivedCounter = 0;
                lostCounter = 0;

                minTime = 0;
                maxTime = 0;
                avgTime = 0;

                counter = 0;

                do
                {
                    line = Console.ReadLine();

                    if (!string.IsNullOrEmpty(line) && !string.IsNullOrWhiteSpace(line))
                    {
                        List<string> list = line.Split(' ').ToList();

                        for (int i = 0; i < list.Count; i++)
                        {
                            if (list[i] == "-t")
                            {
                                list.Remove("-t");

                                counter = -1;
                            }
                        }

                        arguments = list.ToArray();

                        CommandLine.Parser.Default.ParseArguments<Options>(arguments).WithParsed<Options>(o =>
                        {
                            if (!string.IsNullOrEmpty(o.NameOrAddress))
                            {
                                nameOrAddress = o.NameOrAddress;

                                if (counter == 0)
                                {
                                    if (o.Counter > 0)
                                    {
                                        counter = o.Counter;
                                    }

                                    timeout = o.Timeout;
                                    repetitionTime = o.RepetitionTime;
                                }

                                ready = true;
                            }
                        })
                        .WithNotParsed<Options>(e =>
                        {
                            ready = false;
                        });
                    }
                    else
                    {
                        ready = false;
                    }

                } while (!ready);
                

                Console.Clear();

                repetitionsTimer = new Timer(TimerCallback, null, 0, repetitionTime);

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

                repetitionsTimer.Change(Timeout.Infinite, Timeout.Infinite);
                repetitionsTimer = null;

                while (!ready)
                {
                    ;
                }

                Console.WriteLine("\n");
                Console.WriteLine("Packets: Sent = " + sentCounter + "; Received = " + receivedCounter + "; Lost = " + lostCounter + " (" + Decimal.Round(lostCounter / sentCounter * 100) + "% loss)");
                if (receivedCounter != 0)
                {
                    Console.WriteLine("Times [ms]: Minimum = " + minTime + "; Maximum = " + maxTime + "; Average = " + Decimal.Round(avgTime / sentCounter));
                }
                Console.WriteLine("\n");

            } while (true);
        }

        private static void TimerCallback(Object o)
        {
            repetitionsTimer.Change(Timeout.Infinite, Timeout.Infinite);

            if (counter == 0)
            {
                ThreadPool.QueueUserWorkItem((q) =>
                {
                    var hWnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                    PostMessage(hWnd, WM_KEYDOWN, VK_RETURN, 0);
                });

                return;
            }

            if (counter > 0)
            {
                counter--;
            }

            PingHost(nameOrAddress, timeout);
        }

        private static void PingHost(string nameOrAddress, int timeout)
        {
            if (repetitionsTimer == null)
            {
                return;
            }

            ready = false;

            sentCounter++;

            Ping ping = null;
            PingReply reply = null;

            try
            {
                ping = new Ping();
                reply = ping.Send(nameOrAddress, timeout);

                if (reply.Status == IPStatus.Success)
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

                    Console.WriteLine("Ping to " + nameOrAddress + " [" + reply.Address.ToString() + "]: " + reply.Status.ToString() + "; " + "time = " + reply.RoundtripTime.ToString() + " ms");
                }
                else
                {
                    Console.WriteLine("Ping to " + nameOrAddress + ": " + reply.Status.ToString());

                    errorSound.Play();
                }
            }
            catch (PingException e)
            {
                Console.WriteLine(e.InnerException.Message);

                errorSound.Play();
            }
            finally
            {
                if (ping != null)
                {
                    ping.Dispose();
                }
            }

            lostCounter = sentCounter - receivedCounter;

            if (repetitionsTimer != null)
            {
                repetitionsTimer.Change(repetitionTime, repetitionTime);
            }

            ready = true;
        }
    }

    public class Options
    {
        [Value(0, HelpText = "Name or IP address.")]
        public string NameOrAddress { get; set; }

        [Option('n', "counter", Default = 4, HelpText = "Number of echo requests to send. Minimum value is 1.")]
        public int Counter { get; set; }

        [Option('t', HelpText = "Ping the specified host until stopped. To stop and see statistics - type Enter.")]
        public bool t { get; set; }

        [Option('w', "timeout", Default = 1000, HelpText = "Timeout in milliseconds to wait for each reply.")]
        public int Timeout { get; set; }

        [Option('r', "repetition", Default = 1000, HelpText = "Repetition time in milliseconds to wait for between request to send.")]
        public int RepetitionTime { get; set; }
    }
}