﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using Mapping_Tools.Classes.BeatmapHelper;
using Mapping_Tools.Classes.MathUtil;

namespace Mapping_Tools.Classes.Tools {
    public class AutoFailDetector {
        private class ProblemArea {
            public int index;
            public HitObject unloadableHitObject;
            public HashSet<HitObject> disruptors;
            public HashSet<int> timesToCheck;

            public int GetStartTime() {
                return (int)unloadableHitObject.Time;
            }

            public int GetEndTime() {
                return (int)unloadableHitObject.EndTime;
            }
        }

        private readonly int approachTime;
        private readonly int window50;
        private readonly int physicsTime;
        private List<HitObject> hitObjects;
        private List<ProblemArea> problemAreas;

        private SortedSet<int> timesToCheckStartIndex;
        private SortedSet<int> timesToCheckEndIndex;

        public List<double> UnloadingObjects;
        public List<double> PotentialUnloadingObjects;
        public List<double> Disruptors;

        public AutoFailDetector(List<HitObject> hitObjects, int approachTime, int window50, int physicsTime) {
            // Sort the hitobjects
            SetHitObjects(hitObjects);

            this.approachTime = approachTime;
            this.window50 = window50;
            this.physicsTime = physicsTime;
        }

        private void SortHitObjects() {
            hitObjects.Sort();
        }

        public void SetHitObjects(List<HitObject> hitObjects2) {
            hitObjects = hitObjects2;
            SortHitObjects();
        }

        public bool DetectAutoFail() {
            // Initialize lists
            UnloadingObjects = new List<double>();
            PotentialUnloadingObjects = new List<double>();
            Disruptors = new List<double>();

            // Get times to check
            timesToCheckStartIndex = new SortedSet<int>(hitObjects.SelectMany(ho => new[] {
                (int)ho.EndTime + approachTime,
                (int)ho.EndTime + approachTime + 1
            }));
            timesToCheckEndIndex = new SortedSet<int>(hitObjects.SelectMany(ho => new[] {
                (int)ho.Time - approachTime,
                (int)ho.Time - approachTime + 1
            }));

            // Find all problematic areas which could cause auto-fail depending on the binary search
            // A problem area consists of one object and the objects which can unload it
            // An object B can unload another object A if it has a later index than A and an end time earlier than A's end time - approach time.
            problemAreas = new List<ProblemArea>();
            for (int i = 0; i < hitObjects.Count; i++) {
                var ho = hitObjects[i];
                var adjEndTime = GetAdjustedEndTime(ho);

                // Ignore all problem areas which are contained by another unloadable object,
                // because fixing the outer problem area will also fix all of the problems inside
                if (problemAreas.Count > 0 && adjEndTime <=
                    GetAdjustedEndTime(problemAreas.Last().unloadableHitObject)) {
                    continue;
                }

                // Check all later objects for any which have an early enough end time
                var disruptors = new HashSet<HitObject>();
                for (int j = i + 1; j < hitObjects.Count; j++) {
                    var ho2 = hitObjects[j];
                    if (ho2.EndTime < adjEndTime - approachTime) {
                        disruptors.Add(ho2);

                        Disruptors.Add(ho2.Time);
                    }
                }

                if (disruptors.Count == 0)
                    continue;

                var timesToCheck = new HashSet<int>(timesToCheckStartIndex.GetViewBetween((int)ho.Time, adjEndTime));

                // A problem area can also be ignored if the times-to-check is a subset of the last times-to-check,
                // because if thats the case that implies this problem is contained in the last.
                if (!(problemAreas.Count > 0 && timesToCheck.IsSubsetOf(problemAreas.Last().timesToCheck))) {
                    problemAreas.Add(new ProblemArea { index = i, unloadableHitObject = ho, disruptors = disruptors, timesToCheck = timesToCheck });

                    PotentialUnloadingObjects.Add(ho.Time);
                }
            }

            int autoFails = 0;
            // Use osu!'s object loading algorithm to find out which objects are actually unloaded
            foreach (var problemArea in problemAreas) {
                foreach (var time in problemArea.timesToCheck) {
                    var minimalLeft = time - approachTime;
                    var minimalRight = time + approachTime;

                    var startIndex = OsuBinarySearch(minimalLeft);
                    var endIndex = hitObjects.FindIndex(startIndex, ho => ho.Time > minimalRight);
                    if (endIndex < 0) {
                        endIndex = hitObjects.Count - 1;
                    }

                    var hitObjectsMinimal = hitObjects.GetRange(startIndex, 1 + endIndex - startIndex);

                    if (!hitObjectsMinimal.Contains(problemArea.unloadableHitObject)) {
                        UnloadingObjects.Add(problemArea.unloadableHitObject.Time);
                        autoFails++;
                        break;
                    }
                }
            }

            return autoFails > 0;
        }

        public int GetEndTime() {
            return (int) hitObjects.Max(ho => ho.EndTime);
        }

        private int GetAdjustedEndTime(HitObject ho) {
            if (ho.IsCircle) {
                return (int)ho.Time + window50 + physicsTime;
            }
            if (ho.IsSlider || ho.IsSpinner) {
                return (int)ho.EndTime + physicsTime;
            }

            return (int)Math.Max(ho.Time + window50 + physicsTime, ho.EndTime + physicsTime);
        }

        public bool AutoFailFixDialogue(bool autoPlaceFix) {
            if (problemAreas.Count == 0)
                return false;

            int[] solution = SolveAutoFailPadding();
            int paddingCount = solution.Sum();
            bool acceptedSolution = false;
            int solutionCount = 0;

            foreach (var sol in SolveAutoFailPaddingEnumerableInfinite(paddingCount)) {
                solution = sol;

                StringBuilder guideBuilder = new StringBuilder();
                AddFixGuide(guideBuilder, sol);
                guideBuilder.AppendLine("\nDo you want to use this solution?");

                var result = MessageBox.Show(guideBuilder.ToString(), $"Solution {++solutionCount}", MessageBoxButton.YesNoCancel);
                if (result == MessageBoxResult.Yes) {
                    acceptedSolution = true;
                    break;
                }
                if (result == MessageBoxResult.Cancel) {
                    break;
                }
            }


            if (autoPlaceFix && acceptedSolution) {
                PlaceFixGuide(solution);
                return true;
            }

            return false;
        }

        private void AddFixGuide(StringBuilder guideBuilder, IReadOnlyList<int> paddingSolution) {
            guideBuilder.AppendLine("Auto-fail fix guide. Place these extra objects to fix auto-fail:\n");
            int lastTime = 0;
            for (int i = 0; i < problemAreas.Count; i++) {
                guideBuilder.AppendLine(i == 0
                    ? $"Extra objects before {problemAreas[i].GetStartTime()}: {paddingSolution[i]}"
                    : $"Extra objects between {lastTime} - {problemAreas[i].GetStartTime()}: {paddingSolution[i]}");
                lastTime = GetAdjustedEndTime(problemAreas[i].unloadableHitObject) - approachTime;
            }
            guideBuilder.AppendLine($"Extra objects after {lastTime}: {paddingSolution.Last()}");
        }

        private void PlaceFixGuide(IReadOnlyList<int> paddingSolution) {
            int lastTime = 0;
            for (int i = 0; i < problemAreas.Count; i++) {
                if (paddingSolution[i] > 0) {
                    var t = GetSafePlacementTime(lastTime, problemAreas[i].GetStartTime());
                    for (int j = 0; j < paddingSolution[i]; j++) {
                        hitObjects.Add(new HitObject { Pos = Vector2.Zero, Time = t, ObjectType = 8, EndTime = t - 1 });
                    }
                }

                lastTime = GetAdjustedEndTime(problemAreas[i].unloadableHitObject) - approachTime;
            }

            if (paddingSolution.Last() > 0) {
                var t = GetSafePlacementTime(lastTime, GetEndTime());
                for (int i = 0; i < paddingSolution.Last(); i++) {
                    hitObjects.Add(new HitObject { Pos = Vector2.Zero, Time = t, ObjectType = 8, EndTime = t - 1 });
                }
            }

            SortHitObjects();
        }

        private int GetSafePlacementTime(int start, int end) {
            var rangeObjects = hitObjects.FindAll(o => o.EndTime >= start && o.Time <= end);

            for (int i = end - 1; i >= start; i--) {
                if (!rangeObjects.Any(ho =>
                    i >= (int)ho.Time &&
                    i <= GetAdjustedEndTime(ho) - approachTime)) {
                    return i;
                }
            }

            throw new Exception($"Can't find a safe place to place objects between {start} and {end}.");
        }

        private int[] SolveAutoFailPadding(int startPaddingCount = 0) {
            int padding = startPaddingCount;
            int[] solution;
            while (!SolveAutoFailPadding(padding++, out solution)) { }

            return solution;
        }

        private bool SolveAutoFailPadding(int paddingCount, out int[] solution) {
            solution = new int[problemAreas.Count + 1];

            int leftPadding = 0;
            for (var i = 0; i < problemAreas.Count; i++) {
                var problemAreaSolution =
                    SolveSingleProblemAreaPadding(problemAreas[i], paddingCount, leftPadding);

                if (problemAreaSolution.Count == 0 || problemAreaSolution.Max() < leftPadding) {
                    return false;
                }

                var lowest = problemAreaSolution.First(o => o >= leftPadding);
                solution[i] = lowest - leftPadding;
                leftPadding = lowest;
            }

            solution[problemAreas.Count] = paddingCount - leftPadding;

            return true;
        }

        private IEnumerable<int[]> SolveAutoFailPaddingEnumerableInfinite(int initialPaddingCount) {
            int paddingCount = initialPaddingCount;
            while (true) {
                foreach (var solution in SolveAutoFailPaddingEnumerable(paddingCount)) {
                    yield return solution;
                }

                paddingCount++;
            }
        }

        private IEnumerable<int[]> SolveAutoFailPaddingEnumerable(int paddingCount) {
            List<int>[] allSolutions = new List<int>[problemAreas.Count];

            int minimalLeft = 0;
            for (var i = 0; i < problemAreas.Count; i++) {
                var problemAreaSolution =
                    SolveSingleProblemAreaPadding(problemAreas[i], paddingCount, minimalLeft);

                if (problemAreaSolution.Count == 0 || problemAreaSolution.Last() < minimalLeft) {
                    yield break;
                }

                allSolutions[i] = problemAreaSolution;
                minimalLeft = problemAreaSolution.First();
            }

            // Remove impossible max padding
            int maximalLeft = paddingCount;
            for (int i = allSolutions.Length - 1; i >= 0; i--) {
                allSolutions[i].RemoveAll(o => o > maximalLeft);
                maximalLeft = allSolutions[i].Last();
            }

            foreach (var leftPadding in EnumerateSolutions(allSolutions)) {
                int[] pads = new int[leftPadding.Length + 1];
                int left = 0;
                for (int i = 0; i < leftPadding.Length; i++) {
                    pads[i] = leftPadding[i] - left;
                    left = leftPadding[i];
                }

                pads[pads.Length - 1] = paddingCount - left;
                yield return pads;
            }
        }

        private IEnumerable<int[]> EnumerateSolutions(IReadOnlyList<List<int>> allSolutions, int depth = 0, int minimum = 0) {
            if (depth == allSolutions.Count - 1) {
                foreach (var i in allSolutions[depth].Where(o => o >= minimum)) {
                    var s = new int[allSolutions.Count];
                    s[depth] = i;
                    yield return s;
                }
                yield break;
            }
            foreach (var i in allSolutions[depth].Where(o => o >= minimum)) {
                foreach (var j in EnumerateSolutions(allSolutions, depth + 1, minimum = i)) {
                    j[depth] = i;
                    yield return j;
                }
            }
        }

        private List<int> SolveSingleProblemAreaPadding(ProblemArea problemArea, int paddingCount, int minimalLeft = 0) {
            var solution = new List<int>(paddingCount - minimalLeft + 1);

            for (int left = minimalLeft; left <= paddingCount; left++) {
                var right = paddingCount - left;

                if (ProblemAreaPaddingWorks(problemArea, left, right)) {
                    solution.Add(left);
                }
            }

            return solution;
        }

        private bool ProblemAreaPaddingWorks(ProblemArea problemArea, int left, int right) {
            return problemArea.timesToCheck.All(t =>
                PaddedOsuBinarySearch(t - approachTime, left, right) <= problemArea.index);
        }

        private int OsuBinarySearch(int time) {
            var n = hitObjects.Count;
            var min = 0;
            var max = n - 1;
            while (min <= max) {
                var mid = min + (max - min) / 2;
                var t = (int)hitObjects[mid].EndTime;

                if (time == t) {
                    return mid;
                }
                if (time > t) {
                    min = mid + 1;
                } else {
                    max = mid - 1;
                }
            }

            return min;
        }

        private int PaddedOsuBinarySearch(int time, int left, int right) {
            var n = hitObjects.Count;
            var min = -left;
            var max = n - 1 + right;
            while (min <= max) {
                var mid = min + (max - min) / 2;
                var t = mid < 0 ? int.MinValue : mid > hitObjects.Count - 1 ? int.MaxValue : (int)hitObjects[mid].EndTime;

                if (time == t) {
                    return mid;
                }
                if (time > t) {
                    min = mid + 1;
                } else {
                    max = mid - 1;
                }
            }

            return min;
        }
    }
}