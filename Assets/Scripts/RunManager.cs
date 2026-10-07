using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace TornadoChaser
{
    // Freezes the game when a run ends and restarts it on the next tap or key press.
    public class RunManager : MonoBehaviour
    {
        [SerializeField] ChaseTracker tracker;

        bool waitingForRetry;
        bool inputWasDown;

        void OnEnable() => tracker.RunEnded += OnRunEnded;
        void OnDisable() => tracker.RunEnded -= OnRunEnded;

        // Time scale outlives the scene, so a run that ends frozen must not leave
        // the next one (or the next Play session in the Editor) frozen too.
        void OnDestroy() => Time.timeScale = 1f;

        void OnRunEnded(ChaseZone reason)
        {
            Time.timeScale = 0f;
            waitingForRetry = true;
            // A thumb still down from driving shouldn't count as a retry tap.
            inputWasDown = true;
        }

        void Update()
        {
            if (!waitingForRetry) return;

            var pointer = Pointer.current;
            bool tapped = pointer != null && pointer.press.isPressed && !UiInput.IsOverUi(pointer.position.ReadValue());
            bool inputDown = tapped || (Keyboard.current != null && Keyboard.current.anyKey.isPressed);
            if (inputDown && !inputWasDown)
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
            inputWasDown = inputDown;
        }
    }
}
