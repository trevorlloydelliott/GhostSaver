using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GhostSaver
{
    class Program
    {
        private static readonly string _backupLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"GhostSaver");
        private static string _saveGameLocation;
        private static object _locker = new object();

        static void Main(string[] args)
        {
            Console.WriteLine($"GhostSaver 1.0");
            Console.WriteLine($"Automatically backing up ghost saves. Press 'R' to restore most recent save. Press 'X' to exit.");

            var builder = new ConfigurationBuilder();
            builder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            var config = builder.Build();

            _saveGameLocation = config.GetValue<string>("SaveGameLocation");

            var cts = new CancellationTokenSource();
            var backgroundTask = new Task(async () => await Worker(cts.Token), cts.Token);
            backgroundTask.Start();

            bool exit = false;
            while (!exit)
            {
                var keyInfo = Console.ReadKey(intercept: true);

                if (keyInfo.Key == ConsoleKey.X)
                    break;

                if (keyInfo.Key == ConsoleKey.R)
                {
                    Console.WriteLine("Restoring most recent saves");
                    
                    lock (_locker)
                    {
                        var backupFolder = Directory.EnumerateDirectories(_backupLocation, "*", SearchOption.TopDirectoryOnly)
                            .OrderBy(x => x)
                            .FirstOrDefault();

                        if (backupFolder == null)
                        {
                            Console.WriteLine("No backup to restore");
                        }
                        else
                        {
                            foreach (var file in Directory.EnumerateFiles(backupFolder, "*", SearchOption.TopDirectoryOnly))
                            {
                                var fileName = Path.GetFileName(file);

                                try
                                {
                                    File.Copy(file, Path.Combine(_saveGameLocation, fileName), overwrite: true);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Failed to restore {fileName}: {ex}");
                                }
                            }

                            Console.WriteLine($"Restored backup {backupFolder}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Invalid key");
                }
            }

            Console.WriteLine("Exiting...");
            cts.Cancel();
        }

        private static async Task Worker(CancellationToken cancellationToken)
        {
            const string timestampFormat = "yyyyMMddhhmm";
            

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    lock (_locker)
                    {
                        var files = Directory.EnumerateFiles(_saveGameLocation, "*", SearchOption.TopDirectoryOnly);
                        var timestamp = DateTime.Now.ToString(timestampFormat);
                        var backupFolder = Path.Combine(_backupLocation, timestamp);

                        if (!Directory.Exists(backupFolder))
                        {

                            Directory.CreateDirectory(backupFolder);

                            foreach (var file in files)
                            {
                                var fileName = Path.GetFileName(file);
                                File.Copy(file, Path.Combine(backupFolder, fileName), overwrite: false);
                            }

                            var oldBackupFolders = Directory.EnumerateDirectories(_backupLocation, "*", SearchOption.TopDirectoryOnly)
                                .OrderByDescending(x => x)
                                .Skip(5)
                                .ToArray();

                            foreach (var oldBackupFolder in oldBackupFolders)
                            {
                                Directory.Delete(oldBackupFolder, recursive: true);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to backup saves: {ex}");
                }

                await Task.Delay(TimeSpan.FromMinutes(1));
            }
        }
    }
}
