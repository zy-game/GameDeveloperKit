using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameDeveloperKit.UIEditor
{
    internal static class UIDocumentBindingRules
    {
        private static readonly Regex s_BindingNamePattern = new Regex(
            "^[A-Za-z_][A-Za-z0-9_]*$",
            RegexOptions.CultureInvariant);

        public static bool IsBindingNameValid(string bindingName)
        {
            return string.IsNullOrWhiteSpace(bindingName) is false &&
                   s_BindingNamePattern.IsMatch(bindingName);
        }

        public static bool IsSelectableComponent(Component component)
        {
            if (component == null || component is CanvasRenderer)
            {
                return false;
            }

            return component is Transform is false || component is RectTransform;
        }

        public static Component SelectDefaultComponent(string bindingName, GameObject targetObject)
        {
            if (targetObject == null)
            {
                return null;
            }

            var candidates = targetObject
                .GetComponents<Component>()
                .Where(IsSelectableComponent)
                .ToArray();
            var expected = candidates.FirstOrDefault(component => IsExpectedComponent(bindingName, component));
            if (expected != null)
            {
                return expected;
            }

            var text = candidates.FirstOrDefault(UIDocumentLocalizationDrawer.IsLocalizableTextComponent);
            if (text != null)
            {
                return text;
            }

            return candidates.FirstOrDefault(component => component is RectTransform is false) ??
                   candidates.FirstOrDefault();
        }

        public static Component SelectExpectedComponent(string bindingName, GameObject targetObject)
        {
            if (targetObject == null || HasExpectedComponentRule(bindingName) is false)
            {
                return null;
            }

            return targetObject
                .GetComponents<Component>()
                .FirstOrDefault(component => IsSelectableComponent(component) && IsExpectedComponent(bindingName, component));
        }

        public static bool HasExpectedComponentRule(string bindingName)
        {
            return GetExpectedComponentName(bindingName) != null;
        }

        public static bool IsExpectedComponent(string bindingName, Component component)
        {
            if (component == null)
            {
                return false;
            }

            if (StartsWith(bindingName, "b_btn_"))
            {
                return component is Button;
            }

            if (StartsWith(bindingName, "b_text_"))
            {
                return component is TMP_Text || component is Text;
            }

            if (StartsWith(bindingName, "b_image_"))
            {
                return component is Image;
            }

            if (StartsWith(bindingName, "b_toggle_"))
            {
                return component is Toggle;
            }

            if (StartsWith(bindingName, "b_slider_"))
            {
                return component is Slider;
            }

            if (StartsWith(bindingName, "b_scroll_"))
            {
                return component is ScrollRect;
            }

            return false;
        }

        public static bool ContainsExpectedComponent(string bindingName, IEnumerable<Component> components)
        {
            if (HasExpectedComponentRule(bindingName) is false)
            {
                return true;
            }

            return components != null && components.Any(component => IsExpectedComponent(bindingName, component));
        }

        public static string GetExpectedComponentName(string bindingName)
        {
            if (StartsWith(bindingName, "b_btn_"))
            {
                return nameof(Button);
            }

            if (StartsWith(bindingName, "b_text_"))
            {
                return "TMP_Text/Text";
            }

            if (StartsWith(bindingName, "b_image_"))
            {
                return nameof(Image);
            }

            if (StartsWith(bindingName, "b_toggle_"))
            {
                return nameof(Toggle);
            }

            if (StartsWith(bindingName, "b_slider_"))
            {
                return nameof(Slider);
            }

            if (StartsWith(bindingName, "b_scroll_"))
            {
                return nameof(ScrollRect);
            }

            return null;
        }

        private static bool StartsWith(string value, string prefix)
        {
            return string.IsNullOrEmpty(value) is false &&
                   value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
    }
}
