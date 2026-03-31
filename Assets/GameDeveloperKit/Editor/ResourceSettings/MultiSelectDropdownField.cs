using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.Editor
{
    internal sealed class MultiSelectDropdownField : VisualElement
    {
        private readonly DropdownField _dropdown;
        private readonly VisualElement _clickOverlay;
        private List<string> _options = new();
        private List<string> _selectedValues = new();

        public MultiSelectDropdownField(string label)
        {
            style.position = Position.Relative;

            _dropdown = new DropdownField(label, new List<string> { "None" }, 0);
            _dropdown.AddToClassList("resource-field");
            _dropdown.pickingMode = PickingMode.Ignore;
            Add(_dropdown);

            _clickOverlay = new VisualElement();
            _clickOverlay.style.position = Position.Absolute;
            _clickOverlay.style.left = 0;
            _clickOverlay.style.right = 0;
            _clickOverlay.style.top = 0;
            _clickOverlay.style.bottom = 0;
            _clickOverlay.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _clickOverlay.RegisterCallback<MouseDownEvent>(OnMouseDown, TrickleDown.TrickleDown);
            Add(_clickOverlay);
        }

        public bool IncludeEverything { get; set; }

        public event Action<IReadOnlyList<string>> ValueChanged;

        public void SetOptions(IEnumerable<string> options)
        {
            _options = options?
                .Where(static item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static item => item, StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();
            RefreshSummary();
        }

        public void SetValue(IEnumerable<string> values)
        {
            _selectedValues = values?
                .Where(static item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static item => item, StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();
            RefreshSummary();
        }

        private void OnMouseDown(MouseDownEvent evt)
        {
            if (evt.button != 0)
            {
                return;
            }

            evt.PreventDefault();
            evt.StopImmediatePropagation();

            var selectedSet = new HashSet<string>(_selectedValues, StringComparer.OrdinalIgnoreCase);
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("None"), _selectedValues.Count == 0, () => ApplySelection(Array.Empty<string>()));

            if (IncludeEverything)
            {
                if (_options.Count > 0)
                {
                    menu.AddItem(
                        new GUIContent("Everything"),
                        selectedSet.Count == _options.Count,
                        () => ApplySelection(_options));
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent("Everything"));
                }
            }

            menu.AddSeparator(string.Empty);

            if (_options.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("No options found"));
                menu.DropDown(_dropdown.worldBound);
                return;
            }

            for (var i = 0; i < _options.Count; i++)
            {
                var captured = _options[i];
                var isOn = selectedSet.Contains(captured);
                menu.AddItem(new GUIContent(captured), isOn, () =>
                {
                    var next = new HashSet<string>(selectedSet, StringComparer.OrdinalIgnoreCase);
                    if (isOn)
                    {
                        next.Remove(captured);
                    }
                    else
                    {
                        next.Add(captured);
                    }

                    ApplySelection(next.OrderBy(static item => item, StringComparer.OrdinalIgnoreCase).ToList());
                });
            }

            menu.DropDown(_dropdown.worldBound);
        }

        private void ApplySelection(IEnumerable<string> values)
        {
            _selectedValues = values?
                .Where(static item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static item => item, StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();
            RefreshSummary();
            ValueChanged?.Invoke(new List<string>(_selectedValues));
        }

        private void RefreshSummary()
        {
            var summary = "None";
            if (_selectedValues.Count > 0)
            {
                summary = IncludeEverything && _options.Count > 0 && _selectedValues.Count == _options.Count
                    ? "Everything"
                    : string.Join(", ", _selectedValues);
            }

            _dropdown.choices = new List<string> { summary };
            _dropdown.index = 0;
        }
    }
}
