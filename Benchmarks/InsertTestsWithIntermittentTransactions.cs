﻿using AttackSurfaceAnalyzer.Utils;
using BenchmarkDotNet.Attributes;
using System.Threading;
using System.Threading.Tasks;

namespace AttackSurfaceAnalyzer.Benchmarks
{
    [MarkdownExporterAttribute.GitHub]
    [JsonExporterAttribute.Full]
    public class InsertTestsWithIntermittentTransactions : AsaDatabaseBenchmark
    {
        public InsertTestsWithIntermittentTransactions()
#nullable restore
        {
            Logger.Setup(true, true);
            Strings.Setup();
        }

        // If not -1, how many records each writer should write before committing. When -1 don't do checkpoint commits.
        [Params(-1)]
        public int FlushCount { get; set; }

        // Journaling mode, options are
        //[Params("OFF","DELETE","WAL","MEMORY")]
        [Params("WAL")]
        public string JournalMode { get; set; }

        // The number of records to insert for the benchmark
        //[Params(25000,50000,100000)]
        [Params(100000, 1000000)]
        public int N { get; set; }

        // The amount of padding to add to the object in bytes Default size is approx 530 bytes serialized
        // Does not include SQL overhead
        [Params(0)]
        public int ObjectPadding { get; set; }

        // The number of Shards/Threads to use for Database operations
        [Params(6)]
        public int Shards { get; set; }

        // The number of records to populate the database with before the benchmark
        //[Params(0,100000,200000,400000,800000,1600000,3200000)]
        [Params(0)]
        public int StartingSize { get; set; }

        // Bag of reusable objects to write to the database.
#nullable disable

        public static void Insert_X_Objects(int X, int ObjectPadding = 0, string runName = "Insert_X_Objects")
        {
            DatabaseManager.BeginTransaction();

            Parallel.For(0, X, i =>
            {
                var obj = GetRandomObject(ObjectPadding);
                DatabaseManager.Write(obj, runName);
                BagOfObjects.Add(obj);
            });

            while (DatabaseManager.HasElements)
            {
                Thread.Sleep(1);
            }

            DatabaseManager.Commit();
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            Setup();
            DatabaseManager.Destroy();
        }

        [GlobalSetup]
        public void GlobalSetup()
        {
            PopulateDatabases();
        }

        [Benchmark]
        public void Insert_N_Objects() => Insert_X_Objects(N, ObjectPadding, "Insert_N_Objects");

        [IterationCleanup]
        public void IterationCleanup()
        {
            DatabaseManager.BeginTransaction();
            DatabaseManager.DeleteRun("Insert_N_Objects");
            DatabaseManager.Commit();
            DatabaseManager.Vacuum();
            DatabaseManager.CloseDatabase();
        }

        [IterationSetup]
        public void IterationSetup()
        {
            Setup();
        }

        public void PopulateDatabases()
        {
            Setup();

            Insert_X_Objects(StartingSize, ObjectPadding, "PopulateDatabase");

            DatabaseManager.CloseDatabase();
        }

        public void Setup()
        {
            DatabaseManager.Setup(filename: $"AsaBenchmark_{Shards}.sqlite", new DBSettings()
            {
                ShardingFactor = Shards,
                FlushCount = FlushCount,
                JournalMode = JournalMode
            });
        }
    }
}