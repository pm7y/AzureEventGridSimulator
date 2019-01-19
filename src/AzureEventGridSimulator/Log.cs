using System;

namespace AzureEventGridSimulator
{
    public class Log
    {
        private static readonly object Lock = new object();

        public static void Error(Exception ex)
        {
            lock (Lock)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff}: {ex}");
                Console.ResetColor();
            }
        }

        public static void Error(string msg)
        {
            lock (Lock)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff}: {msg}");
                Console.ResetColor();
            }
        }

        public static void Info(string msg)
        {
            lock (Lock)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff}: {msg}");
                Console.ResetColor();
            }
        }

        public static void Debug(string msg)
        {
            lock (Lock)
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff}: {msg}");
                Console.ResetColor();
            }
        }

        public static void Warn(string msg)
        {
            lock (Lock)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff}: {msg}");
                Console.ResetColor();
            }
        }
    }
}
