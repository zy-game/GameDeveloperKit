using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.Editor
{
    internal sealed class SingleSelectDropdownField : VisualElement
    {
        private readonly DropdownField _dropdown;
        private List<string> _options = new();

        public SingleSelectDropdownField(string label)
        {
            _dropdown = new DropdownField(label, new List<string> { "None" }, 0);
            _dropdown.AddToClassList("resource-field");
            _dropdown.RegisterValueChangedCallback(evt =>
            {
                ValueChanged?.Invoke(string.Equals(evt.newValue, "None", StringComparison.Ordinal) ? string.Empty : evt.newValue);
            });
            Add(_dropdown);
        }

        public event Action<string> ValueChanged;

        public void SetOptions(IEnumerable<string> options)
        {
            _options = options?
                .Where(static item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static item => item, StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();
            RefreshChoices(string.Empty);
        }

        public void SetValue(string value)
        {
            RefreshChoices(value);
        }

        private void RefreshChoices(string value)
        {
            var choices = new List<string> { "None" };
            choices.AddRange(_options);

            var normalizedValue = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
            var selectedIndex = string.IsNullOrWhiteSpace(normalizedValue)
                ? 0
                : Mathf.Max(0, choices.FindIndex(item => string.Equals(item, normalizedValue, StringComparison.OrdinalIgnoreCase)));

            _dropdown.choices = choices;
            _dropdown.index = selectedIndex;
        }
    }
}
