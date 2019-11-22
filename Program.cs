using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Trim
{
    class Program
    {
        static int Main(string[] args) {
            int status = Run(args);
            Console.WriteLine("\nPress any key to exit.");
            Console.ReadKey();
            return status;
        }

        static void ExtractFFmpeg(string outputFilename) {
            var assembly = Assembly.GetExecutingAssembly();

            string resourceName = null;
            foreach (var name in assembly.GetManifestResourceNames()) {
                if (name.IndexOf(".ffmpeg.exe.gz") >= 0) {
                    resourceName = name;
                    break;
                }
            }

            if (string.IsNullOrEmpty(resourceName)) {
                throw new Exception("Could not extract ffmpeg.exe.");
            }

            var stream = assembly.GetManifestResourceStream(resourceName);
            using (var gzip = new GZipStream(stream, CompressionMode.Decompress)) {
                using (var output = File.OpenWrite(outputFilename)) {
                    gzip.CopyTo(output);
                }
            }
        }

        static int Run(string[] args) {
            ShowBanner("V I D E O     T R I M M E R");
            if (args.Length != 1) {
                Console.WriteLine("Error: Drag a video file onto this program to run.");
                return 1;
            }

            string inputFilename = Path.GetFullPath(args[0]);
            if (!File.Exists(inputFilename)) {
                Console.WriteLine($"Error: File does not exist at {inputFilename}.");
                return 1;
            }

            string name = Path.GetFileNameWithoutExtension(inputFilename);
            Console.WriteLine($"Trimming \"{name}\"");

            string extension = Path.GetExtension(inputFilename);
            string outputFilename = Path.ChangeExtension(inputFilename, $"TRIMMED{extension}");

            Console.WriteLine("\nTime format: hh:mm:ss");
            Console.WriteLine("\nExamples:");
            Console.WriteLine("             45 seconds  00:00:45");
            Console.WriteLine("              2 minutes  00:02:00");
            Console.WriteLine("    9 minutes 5 seconds  00:09:05");
            Console.WriteLine("      1 hour 15 minutes  01:15:00");

            int start = ReadSeconds("\nWhat time should the trimmed video start? ");
            string startTimestamp = ToTimestamp(start);

            int end;
            while (true) {
                end = ReadSeconds("\nWhat time should trimmed video end? ");
                if (start < end) {
                    break;
                }

                Console.WriteLine($"End time must be after start time ({startTimestamp}).");
            }

            Console.WriteLine("\nTrimming video.");

            string ffmpegFilename = Path.GetTempFileName();
            try {
                ExtractFFmpeg(ffmpegFilename);
                TrimVideo(ffmpegFilename, inputFilename, outputFilename, startTimestamp, ToTimestamp(end));
            } finally {
                File.Delete(ffmpegFilename);
            }

            return 0;
        }

        static void TrimVideo(string ffmpegFilename, string inputFilename, string outputFilename, string startTimestamp, string endTimestamp) {
            var psi = new ProcessStartInfo {
                FileName = ffmpegFilename,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            // ffmpeg -y -i in.mp4 -ss hh:mm:ss -to hh:mm:ss -codec copy out.mp4
            var args = new[] { "-y", "-i", inputFilename, "-ss", startTimestamp, "-to", endTimestamp, "-codec", "copy", outputFilename };

            foreach (var arg in args) {
                psi.ArgumentList.Add(arg);
            }

            var process = Process.Start(psi);
            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0) {
                Console.WriteLine(stderr);
                Console.WriteLine("Trimming error. See output above.");
            } else {
                Console.WriteLine("Trimming complete.");
            }
        }

        static int ReadSeconds(string prompt) {
            while (true) {
                try {
                    Console.Write(prompt);
                    return ToSeconds(Console.ReadLine());
                } catch (Exception e) {
                    Console.WriteLine($"Error: {e.Message}");
                }
            }
        }

        static int ToSeconds(string timestamp) {
            timestamp = timestamp.Trim();
            var pattern = new Regex(@"^(\d+:)?\d\d?:\d\d?");
            if (!pattern.IsMatch(timestamp)) {
                throw new Exception("Invalid time: Format is hh:mm:ss");
            }

            var parts = timestamp.Trim().Split(':').Reverse().ToArray();

            int hour = 0;
            int minute = 0;
            int second = int.Parse(parts[0]);

            if (parts.Length > 1) {
                minute = int.Parse(parts[1]);
            }

            if (parts.Length > 2) {
                hour = int.Parse(parts[2]);
            }

            if (second >= 60) {
                throw new Exception("Invalid time: Seconds must be below 60.");
            }

            if (minute >= 60) {
                throw new Exception("Invalid time: Minutes must be below 60.");
            }

            return (hour * 60 * 60) + (minute * 60) + second;
        }

        static string ToTimestamp(int seconds) {
            int hour = seconds / (60 * 60);
            seconds -= hour * 60 * 60;

            int minute = seconds / 60;
            seconds -= minute * 60;

            int second = seconds;

            return $"{hour.ToString().PadLeft(2, '0')}:{minute.ToString().PadLeft(2, '0')}:{second.ToString().PadLeft(2, '0')}";
        }

        static void ShowBanner(string title) {
            string border = Repeat('─', Console.WindowWidth - 1);

            int padding = (int)Math.Floor((Console.WindowWidth - title.Length) / 2.0);

            Console.WriteLine();
            Console.WriteLine(border);
            Console.WriteLine();
            Console.WriteLine($"{Repeat(' ', padding)}{title}{Repeat(' ', padding)}");
            Console.WriteLine();
            Console.WriteLine(border);
            Console.WriteLine();
        }

        static string Repeat(char c, int count) => new string(Enumerable.Range(0, count).Select(_ => c).ToArray());
    }
}
