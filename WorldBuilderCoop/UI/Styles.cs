using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WorldBuilderCoop.UI
{
    public class Styles
    {
        public static void ApplyInputStyle(TextField field)
        {
            var s = field.style;
            s.marginBottom = 10;
            s.color = Color.white;
            s.unityFontStyleAndWeight = FontStyle.Bold;

            var label = field.Q<Label>();
            if (label != null)
            {
                label.style.color = new Color(0.15f, 0.67f, 0.95f, 1f);
                label.style.fontSize = 12;
                label.style.marginBottom = 2;
            }

            var input = field.Q("unity-text-input");
            if (input != null)
            {
                input.style.backgroundColor = new Color(0.15f, 0.15f, 0.18f, 1f);
                input.style.borderLeftColor = input.style.borderRightColor = input.style.borderTopColor = input.style.borderBottomColor = new Color(0.15f, 0.67f, 0.95f, 0.5f);
                input.style.borderLeftWidth = input.style.borderRightWidth = input.style.borderTopWidth = input.style.borderBottomWidth = 1;
                input.style.paddingLeft = 5;
                input.style.height = 30;
                input.style.color = Color.white;
                input.style.fontSize = 14;
            }
        }

        public static void ApplyButtonStyle(Button btn)
        {
            var s = btn.style;

            s.width = 220;
            s.height = 60;
            s.fontSize = 18;
            s.unityFontStyleAndWeight = FontStyle.Bold;
            s.color = Color.white;
            s.marginTop = s.marginBottom = 8;

            s.backgroundColor = new Color(0.1f, 0.1f, 0.12f, 0.95f);
            s.borderLeftColor = s.borderRightColor = s.borderTopColor = s.borderBottomColor = new Color(0.15f, 0.67f, 0.95f, 1f);
            s.borderLeftWidth = s.borderRightWidth = s.borderTopWidth = s.borderBottomWidth = 2;
            s.borderTopLeftRadius = s.borderTopRightRadius = s.borderBottomLeftRadius = s.borderBottomRightRadius = 5;

            s.transitionDuration = new List<TimeValue> { new TimeValue(100, TimeUnit.Millisecond) };
            s.transitionProperty = new List<StylePropertyName> {
                new StylePropertyName("background-color"),
                new StylePropertyName("scale"),
                new StylePropertyName("border-color")
            };

            btn.RegisterCallback<MouseEnterEvent>(evt =>
            {
                s.backgroundColor = new Color(0.15f, 0.67f, 0.95f, 0.4f);
                s.scale = new Scale(new Vector2(1.05f, 1.05f));
                s.borderLeftColor = s.borderRightColor = s.borderTopColor = s.borderBottomColor = Color.white;
            });

            btn.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                s.backgroundColor = new Color(0.1f, 0.1f, 0.12f, 0.95f);
                s.scale = new Scale(new Vector2(1f, 1f));
                s.borderLeftColor = s.borderRightColor = s.borderTopColor = s.borderBottomColor = new Color(0.15f, 0.67f, 0.95f, 1f);
            });

            btn.RegisterCallback<MouseDownEvent>(evt =>
            {
                s.backgroundColor = Color.white;
                s.color = Color.black;
                s.scale = new Scale(new Vector2(0.92f, 0.92f));
            });

            btn.RegisterCallback<MouseUpEvent>(evt =>
            {
                s.backgroundColor = new Color(0.15f, 0.67f, 0.95f, 0.4f);
                s.color = Color.white;
                s.scale = new Scale(new Vector2(1.05f, 1.05f));
            });
        }
    }
}
