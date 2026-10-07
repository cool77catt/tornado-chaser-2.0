using UnityEngine;

namespace TornadoChaser
{
    // Temporary prototype HUD showing chase state. Replace with real UI.
    public class DebugHud : MonoBehaviour
    {
        [SerializeField] ChaseTracker tracker;
        [SerializeField] CarController car;
        [SerializeField] TornadoController tornado;

        GUIStyle style;

        void OnGUI()
        {
            style ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, wordWrap = true };
            style.fontSize = Screen.height / 40;
            float margin = Screen.width * 0.04f;
            var area = new Rect(margin, margin, Screen.width - margin * 2f, Screen.height * 0.5f);

            string text = $"{tracker.Zone}  |  gap {tracker.Gap:F0} m\n" +
                          $"car {car.Speed:F0} m/s  |  tornado {tornado.Speed:F0} m/s";
            if (tracker.HasEnded)
                text += tracker.Zone == ChaseZone.SuckedUp ? "\n\nSUCKED UP!\nTap to retry" : "\n\nLOST IT!\nTap to retry";
            else if (tracker.Zone == ChaseZone.Lost)
                text += $"\n\nLosing it... {tracker.LostTimeRemaining:F1}";

            GUI.Label(area, text, style);
        }
    }
}
