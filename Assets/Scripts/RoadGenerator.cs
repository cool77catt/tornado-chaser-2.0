using System.Collections.Generic;
using UnityEngine;

namespace TornadoChaser
{
    // Keeps road segments laid out ahead of the target and moves the ones it has
    // passed to the front of the road.
    public class RoadGenerator : MonoBehaviour
    {
        [SerializeField] RoadSegment segmentPrefab;
        [SerializeField] Transform target;
        [SerializeField] int segmentsAhead = 8;
        [SerializeField] int segmentsBehind = 2;

        readonly Queue<RoadSegment> segments = new();
        int nextIndex;

        void Start()
        {
            nextIndex = CurrentIndex() - segmentsBehind;
            Fill();
        }

        void Update() => Fill();

        void Fill()
        {
            int current = CurrentIndex();
            while (nextIndex <= current + segmentsAhead)
            {
                RoadSegment segment = segments.Count > 0 && segments.Peek().Index < current - segmentsBehind
                    ? segments.Dequeue()
                    : Instantiate(segmentPrefab, transform);
                segment.Place(nextIndex++);
                segments.Enqueue(segment);
            }
        }

        int CurrentIndex() => Mathf.FloorToInt(target.position.z / segmentPrefab.Length);
    }
}
