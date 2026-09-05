using System;
using System.Collections.Generic;
using System.Linq;

namespace HiProtobuf.Lib
{
    internal sealed class LocalizationRegistry
    {
        private const int FirstTextKey = 100000;

        private readonly Dictionary<string, string> _valueToKey = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _keyToValue = new Dictionary<string, string>();
        private int _nextKey = FirstTextKey;

        public int Count => _keyToValue.Count;

        public IEnumerable<KeyValuePair<string, string>> Entries =>
            _keyToValue.OrderBy(pair => ParseKeyForSort(pair.Key)).ThenBy(pair => pair.Key, StringComparer.Ordinal);

        public void Load(string key, string value)
        {
            _keyToValue[key] = value;
            if (!string.IsNullOrEmpty(value))
            {
                _valueToKey[value] = key;
            }

            if (int.TryParse(key, out var numericKey) && numericKey >= _nextKey)
            {
                _nextKey = numericKey + 1;
            }
        }

        public LocalizationReconcileResult Reconcile(IEnumerable<string> currentValues)
        {
            var values = DistinctValuesInOrder(currentValues);
            var retainedValueToKey = new Dictionary<string, string>();
            var retainedKeys = new HashSet<string>();

            foreach (var value in values)
            {
                if (_valueToKey.TryGetValue(value, out var key) &&
                    _keyToValue.TryGetValue(key, out var existingValue) &&
                    existingValue == value &&
                    retainedKeys.Add(key))
                {
                    retainedValueToKey[value] = key;
                }
            }

            var reusableKeys = _keyToValue.Keys
                .Where(key => !retainedKeys.Contains(key))
                .OrderBy(ParseKeyForSort)
                .ThenBy(key => key, StringComparer.Ordinal)
                .ToList();
            var previousValues = reusableKeys.ToDictionary(key => key, key => _keyToValue[key]);

            foreach (var key in reusableKeys)
            {
                _keyToValue[key] = string.Empty;
            }

            int reusableIndex = 0;
            int reusedCount = 0;
            int allocatedCount = 0;
            foreach (var value in values)
            {
                if (retainedValueToKey.ContainsKey(value))
                {
                    continue;
                }

                string key;
                if (reusableIndex < reusableKeys.Count)
                {
                    key = reusableKeys[reusableIndex++];
                    reusedCount++;
                }
                else
                {
                    key = CreateKey();
                    allocatedCount++;
                }

                _keyToValue[key] = value;
                retainedValueToKey[value] = key;
            }

            _valueToKey.Clear();
            foreach (var pair in retainedValueToKey)
            {
                _valueToKey[pair.Key] = pair.Value;
            }

            var changes = new List<LocalizationSlotChange>();
            foreach (var key in reusableKeys)
            {
                var oldValue = previousValues[key];
                var newValue = _keyToValue[key];
                if (oldValue != newValue)
                {
                    changes.Add(new LocalizationSlotChange(key, oldValue, newValue));
                }
            }

            return new LocalizationReconcileResult(reusedCount, allocatedCount, reusableKeys.Count - reusedCount, changes);
        }

        public string GetKey(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            if (_valueToKey.TryGetValue(value, out var existingKey))
            {
                return existingKey;
            }

            var reusableKey = _keyToValue
                .Where(pair => string.IsNullOrEmpty(pair.Value))
                .Select(pair => pair.Key)
                .OrderBy(ParseKeyForSort)
                .ThenBy(candidateKey => candidateKey, StringComparer.Ordinal)
                .FirstOrDefault();
            var key = reusableKey ?? CreateKey();
            _keyToValue[key] = value;
            _valueToKey[value] = key;
            return key;
        }

        private string CreateKey()
        {
            string key;
            do
            {
                key = (_nextKey++).ToString();
            }
            while (_keyToValue.ContainsKey(key));

            return key;
        }

        private static List<string> DistinctValuesInOrder(IEnumerable<string> values)
        {
            var result = new List<string>();
            var seen = new HashSet<string>();
            foreach (var value in values)
            {
                if (!string.IsNullOrEmpty(value) && seen.Add(value))
                {
                    result.Add(value);
                }
            }
            return result;
        }

        private static int ParseKeyForSort(string key)
        {
            return int.TryParse(key, out var numericKey) ? numericKey : int.MaxValue;
        }
    }

    internal sealed class LocalizationReconcileResult
    {
        public LocalizationReconcileResult(int reusedCount, int allocatedCount, int emptyCount, IReadOnlyList<LocalizationSlotChange> changes)
        {
            ReusedCount = reusedCount;
            AllocatedCount = allocatedCount;
            EmptyCount = emptyCount;
            Changes = changes;
        }

        public int ReusedCount { get; }
        public int AllocatedCount { get; }
        public int EmptyCount { get; }
        public IReadOnlyList<LocalizationSlotChange> Changes { get; }
    }

    internal sealed class LocalizationSlotChange
    {
        public LocalizationSlotChange(string key, string oldValue, string newValue)
        {
            Key = key;
            OldValue = oldValue;
            NewValue = newValue;
        }

        public string Key { get; }
        public string OldValue { get; }
        public string NewValue { get; }
    }
}
