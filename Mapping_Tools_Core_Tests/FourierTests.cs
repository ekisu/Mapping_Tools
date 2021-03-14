﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mapping_Tools_Core_Tests {
    [TestClass]
    public class FourierTests {
        [TestMethod]
        public void FourierTest() {
            const int expectedBPM = 165;
            var reader = new MediaFoundationReader("Resources\\04 Compute It With Some Devilish Alcoholic Steampunk Engines.mp3");
            var sampleProvider = reader.ToSampleProvider().ToMono();
            var sampleRate = sampleProvider.WaveFormat.SampleRate;

            // Just take one minute
            var allSamples = new float[sampleRate * 30];
            sampleProvider.Skip(TimeSpan.FromSeconds(79));
            int samplesRead = sampleProvider.Read(allSamples, 0, allSamples.Length);

            // Now you have the entire wav audio stored in the list

            var tasks = new List<Task<Tuple<int, double>>>();
            for (int bpm = 60; bpm < 300; bpm++) {
                int bpm2 = bpm;
                tasks.Add(Task.Run(() => {
                    double hz = bpm2 / 60d;
                    double x = 0;
                    double y = 0;
                    int i = 0;
                    foreach (var sample in allSamples) {
                        var rot = 2 * Math.PI * hz / sampleRate * i++;
                        var amp = Math.Abs(sample);
                        x += amp * Math.Cos(rot);
                        y += amp * Math.Sin(rot);
                    }
                    Console.WriteLine($"{bpm2};{Math.Sqrt(x * x + y * y)}");
                    return new Tuple<int, double>(bpm2, Math.Sqrt(x * x + y * y));
                }));
            }

            Task t = Task.WhenAll(tasks);
            t.Wait();

            int bestBPM = 0;
            double bestResonance = 0;
            foreach (var task in tasks) {
                if (task.Result.Item2 > bestResonance) {
                    bestResonance = task.Result.Item2;
                    bestBPM = task.Result.Item1;
                }
            }

            Console.WriteLine($"Found BPM: {bestBPM}");
            Assert.AreEqual(expectedBPM, bestBPM);
        }
    }
}
