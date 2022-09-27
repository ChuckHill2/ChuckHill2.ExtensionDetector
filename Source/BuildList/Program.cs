using System;
using Chuckhill2.Utilities;

namespace BuildList
{
    class Program
    {
        private static readonly bool WinExplorerStart = Console.CursorLeft == 0 && Console.CursorTop == 0;

        static void Main(string[] args)
        {
            FileEx.Log += s => Console.WriteLine(s);
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // If there is a command-line argument, it is assumed to be an existing filename. The properties will be printed for just this single file.
            if (args.Length> 0)
            {
                if (!FileEx.Exists(args[0]))
                {
                    Console.WriteLine($"File \"{args[0]}\" not found.");
                    Environment.Exit(1);
                }
                BuildList.GetFileContent(args[0]);
                Pause();
                Environment.Exit(1);
            }

            Console.WriteLine("Debugging tool to generate a list of all known file types and content");
            Console.WriteLine("properties from the C: drive. The results \"AllFilesList.txt\", is located in");
            Console.WriteLine("the same folder as this executable. Import this file into Excel for analysis");
            Console.WriteLine("to verify that all the file types are properly handled by virtue of the file");
            Console.WriteLine("content.  Note: This will be faster if you empty your recycle bin first.");
            Console.WriteLine();
            Console.Write("This will take some time. Do you want to continue? [Yes] ");
            var key = Console.ReadKey(true);
            Console.WriteLine();
            if (key.KeyChar == 'N' || key.KeyChar == 'n') Environment.Exit(1);
            Console.WriteLine();

            BuildList.FindAllFiles();  //all the work occurs here....

            Console.WriteLine("List building complete.");
            Pause();
        }

        private static void Pause()
        {
            if (WinExplorerStart)
            {
                Console.WriteLine();
                Console.Write("Press any key to exit.");
                Console.ReadKey(true);
            }
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Console.WriteLine("Unhandled Exception: " + e.ExceptionObject.ToString());
            Pause();
        }
    }
}
