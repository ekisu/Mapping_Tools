﻿using Mapping_Tools.Classes.SnappingTools.DataStructure.RelevantObject;
using Mapping_Tools.Classes.SystemTools;

namespace Mapping_Tools.Classes.SnappingTools.DataStructure.RelevantObjectGenerators.GeneratorInputSelection {
    public class SelectionPredicate : BindableBase {
        private bool _needSelected;
        private bool _needLocked;
        private bool _needGeneratedByThis;
        private double _minRelevancy;
        public bool NeedSelected { get => _needSelected; set => Set(ref _needSelected, value); }
        public bool NeedLocked { get => _needLocked; set => Set(ref _needLocked, value); }
        public bool NeedGeneratedByThis { get => _needGeneratedByThis; set => Set(ref _needGeneratedByThis, value); }
        public double MinRelevancy { get => _minRelevancy; set => Set(ref _minRelevancy, value); }

        public bool Check(IRelevantObject relevantObject, RelevantObjectsGenerator generator) {
            if (NeedSelected && !relevantObject.IsSelected) return false;
            if (NeedLocked && !relevantObject.IsLocked) return false;
            if (NeedGeneratedByThis && relevantObject.Generator != null && relevantObject.Generator != generator) return false;
            return !(relevantObject.Relevancy < MinRelevancy);
        }
    }
}