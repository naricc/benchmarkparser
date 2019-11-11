using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using Newtonsoft.Json;

namespace BenchmarkParser
{
    class Program
    {

        static void Main(string[] args)
        {
            //args = new[] { @"C:\prj\BenchmarkResults\", @"C:\prj\"};

            Console.WriteLine($"{args[0]} {args[1]}");
            MakeReport(folderWithBdnResults: args[1], outputFolder: args[2]);
            Console.WriteLine("Done!");
        }


        static void MakeReport(string folderWithBdnResults, string outputFolder)
        {
            var groups = LoadBenchmarkGroups(folderWithBdnResults).ToList();

            // Headers for CSV:
            string csvMinimal = "Benchmark," + string.Join(",", groups.Select(i => i.GroupName)) + "\n";

            var first = groups[0];
            foreach (var currentBenchmark in first.Benchmarks)
            {
                double miliseconds = 0;
                if (currentBenchmark.Measurements.Any())
                    miliseconds = TimeSpan.FromMilliseconds(currentBenchmark.Measurements.Last().Nanoseconds / 1_000_000UL).TotalSeconds;

                string name = currentBenchmark.FullName.Replace(",", ";"); // escape ',' since it's used as a separator in CSV
                if (name.Length > 100) // if the benchmark name is too long
                    name = name.Substring(0, 100) + "...";

                string csvMinimalc = $"{name},";

                foreach (var group in groups)
                {
                    var bench = group.Benchmarks.FirstOrDefault(b => b.FullName == currentBenchmark.FullName);

                    // calculate scale
                    if (bench?.Statistics != null && currentBenchmark.Statistics != null)
                    {
                        if (currentBenchmark.Statistics.Mean < 5.0)
                            csvMinimalc += "SMALL,";
                        else
                            csvMinimalc += $"{bench.Statistics.Mean / currentBenchmark.Statistics.Mean:F2},";
                    }
                    else
                    {
                        csvMinimalc += "N/A,";

                        goto CONTINUE;
                    }
                }

                csvMinimal += csvMinimalc + "\n";

                CONTINUE:
                continue;
            }

            var envInfo = $"\n\n{groups[0].EnvironmentInfo.OsVersion}\n{groups[0].EnvironmentInfo.ProcessorName}";
            csvMinimal += envInfo;

            File.WriteAllText(Path.Combine(outputFolder, "Report-minimal.csv"), csvMinimal);
        }

        static IEnumerable<BenchmarkGroup> LoadBenchmarkGroups(string folder)
        {
            foreach (var subFolder in Directory.GetDirectories(folder).OrderBy(f => f.Contains("LLVM")).ThenBy(i => i)) //
            {
                BenchmarkRoot[] benchmarks = ParseBdnJson(subFolder).ToArray();
                if (!benchmarks.Any())
                    continue;

                yield return new BenchmarkGroup 
                    {
                        Benchmarks = benchmarks.SelectMany(i => i.Benchmarks).ToList(), 
                        GroupName = Path.GetFileName(subFolder),
                        EnvironmentInfo = benchmarks[0].HostEnvironmentInfo,
                    };
            }
        }

        static IEnumerable<BenchmarkRoot> ParseBdnJson(string folder)
        {
            foreach (var jsonFile in Directory.GetFiles(folder, "*.json"))
            {
                var json = File.ReadAllText(jsonFile);
                var root = JsonConvert.DeserializeObject<BenchmarkRoot>(json);
                if (root != null)
                    yield return root;
            }
        }
    }
}
