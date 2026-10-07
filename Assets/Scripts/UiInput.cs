using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TornadoChaser
{
    static class UiInput
    {
        static readonly List<RaycastResult> Hits = new();

        // True when a screen position is over UI that receives taps, such as a button,
        // so gameplay input can ignore presses meant for the UI.
        public static bool IsOverUi(Vector2 screenPosition)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return false;
            Hits.Clear();
            eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = screenPosition }, Hits);
            return Hits.Count > 0;
        }
    }
}
