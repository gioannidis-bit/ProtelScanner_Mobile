using System;
using System.Collections.Generic;

namespace ProtelScanner.Mobile
{
    // Κλάση αντικατάστασης για το παλαιό App.Current.Properties
    public static class AppStateManager
    {
        private static readonly Dictionary<string, object> _stateItems = new Dictionary<string, object>();

        public static bool ContainsKey(string key)
        {
            return _stateItems.ContainsKey(key);
        }

        public static T GetValue<T>(string key)
        {
            if (_stateItems.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return default;
        }

        public static void SetValue(string key, object value)
        {
            _stateItems[key] = value;
        }

        public static void RemoveValue(string key)
        {
            if (_stateItems.ContainsKey(key))
            {
                _stateItems.Remove(key);
            }
        }

        public static void Clear()
        {
            _stateItems.Clear();
        }
    }
}