using System;
using UnityEngine;

namespace TornadoChaser
{
    public enum ChaseZone { Lost, Safe, Close, Danger, SuckedUp }

    // Measures how far the car is behind the tornado, works out which chase
    // zone that puts it in, and ends the run when the car is sucked up or loses
    // the tornado.
    public class ChaseTracker : MonoBehaviour
    {
        [SerializeField] CarController car;
        [SerializeField] TornadoController tornado;

        [Header("Zone limits (meters behind the tornado)")]
        [SerializeField] float suckedUpGap = 12f;
        [SerializeField] float dangerGap = 30f;
        [SerializeField] float closeGap = 55f;
        [SerializeField] float safeGap = 90f;
        [SerializeField, Tooltip("Seconds the car can stay out of range before the tornado is lost.")]
        float lostGracePeriod = 3f;

        public float Gap { get; private set; }
        public ChaseZone Zone { get; private set; } = ChaseZone.Safe;
        public float LostTimeRemaining { get; private set; }
        public bool HasEnded { get; private set; }

        public event Action<ChaseZone> RunEnded;

        void Start() => LostTimeRemaining = lostGracePeriod;

        void LateUpdate()
        {
            if (HasEnded) return;

            Gap = tornado.transform.position.z - car.transform.position.z;
            Zone = Gap < suckedUpGap ? ChaseZone.SuckedUp
                : Gap < dangerGap ? ChaseZone.Danger
                : Gap < closeGap ? ChaseZone.Close
                : Gap < safeGap ? ChaseZone.Safe
                : ChaseZone.Lost;

            if (Zone == ChaseZone.Lost) LostTimeRemaining -= Time.deltaTime;
            else LostTimeRemaining = lostGracePeriod;

            if (Zone == ChaseZone.SuckedUp || LostTimeRemaining <= 0f)
            {
                HasEnded = true;
                RunEnded?.Invoke(Zone);
            }
        }
    }
}
