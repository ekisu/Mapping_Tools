﻿using System.Collections;
using Mapping_Tools_Core.BeatmapHelper.Enums;
using Mapping_Tools_Core.BeatmapHelper.Objects;
using Mapping_Tools_Core.MathUtil;

namespace Mapping_Tools_Core.BeatmapHelper.Decoding.HitObject {
    public static class HitObjectDecodingHelper {
        public static void DecodeSharedProperties(BeatmapHelper.HitObject hitObject, string[] values) {
            if (values.Length <= 4)
                throw new BeatmapParsingException("Hit object is missing values.", JoinLine(values));

            if (InputParsers.TryParseDouble(values[0], out var x) && InputParsers.TryParseDouble(values[1], out var y))
                hitObject.Pos = new Vector2(x, y);
            else throw new BeatmapParsingException("Failed to parse coordinate of hit object.", JoinLine(values));

            if (InputParsers.TryParseDouble(values[2], out var t))
                hitObject.StartTime = t;
            else throw new BeatmapParsingException("Failed to parse time of hit object.", JoinLine(values));

            if (InputParsers.TryParseInt(values[3], out var type)) {
                var b = new BitArray(new[] { type });
                hitObject.NewCombo = b[2];
                hitObject.ComboSkip = MathHelper.GetIntFromBitArray(new BitArray(new[] { b[4], b[5], b[6] }));
            } else throw new BeatmapParsingException("Failed to parse type of hit object.", string.Join(',', values));

            if (InputParsers.TryParseInt(values[4], out var hitsounds)) {
                var b = new BitArray(new[] { hitsounds });
                hitObject.Hitsounds.Normal = b[0];
                hitObject.Hitsounds.Whistle = b[1];
                hitObject.Hitsounds.Finish = b[2];
                hitObject.Hitsounds.Clap = b[3];
            }
            else throw new BeatmapParsingException("Failed to parse hitsound of hit object.", JoinLine(values));
        }

        public static void DecodeExtras(BeatmapHelper.HitObject hitObject, string extras) {
            // Extras has an extra value at the start if it's a hold note
            var split = extras.Split(':');
            var i = 0;
            if (hitObject is HoldNote holdNote) {
                if (InputParsers.TryParseDouble(split[i++], out var et))
                    holdNote.EndTime = et;
                else throw new BeatmapParsingException("Failed to parse end time of hold note.", extras);
            }

            if (InputParsers.TryParseInt(split[i++], out var ss))
                hitObject.Hitsounds.SampleSet = (SampleSet)ss;
            else throw new BeatmapParsingException("Failed to parse sample set of hit object.", extras);

            if (InputParsers.TryParseInt(split[i++], out var ass))
                hitObject.Hitsounds.AdditionSet = (SampleSet)ass;
            else throw new BeatmapParsingException("Failed to parse additional sample set of hit object.", extras);

            if (InputParsers.TryParseInt(split[i++], out var ci))
                hitObject.Hitsounds.CustomIndex = ci;
            else throw new BeatmapParsingException("Failed to parse custom index of hit object.", extras);

            if (InputParsers.TryParseDouble(split[i++], out var vol))
                hitObject.Hitsounds.Volume = vol;
            else throw new BeatmapParsingException("Failed to parse volume of hit object.", extras);

            hitObject.Hitsounds.Filename = split[i];
        }

        public static HitObjectType GetHitObjectType(string[] values) {
            if (!InputParsers.TryParseInt(values[3], out var type))
                throw new BeatmapParsingException("Failed to parse type of hit object.", JoinLine(values));

            var b = new BitArray(new[] { type });
            if (b[0]) {
                return HitObjectType.Circle;
            }
            if (b[1]) {
                return HitObjectType.Slider;
            }
            if (b[3]) {
                return HitObjectType.Spinner;
            }
            if (b[7]) {
                return HitObjectType.HoldNote;
            }

            return HitObjectType.Circle;
        }

        public static string[] SplitLine(string line) {
            return line.Split(',');
        }

        public static string JoinLine(string[] values) {
            return string.Join(',', values);
        }
    }
}