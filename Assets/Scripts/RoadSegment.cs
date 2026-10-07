using UnityEngine;

namespace TornadoChaser
{
    // A single piece of road. The pivot sits at the start of the piece, and the
    // geometry runs from z = 0 to z = Length.
    public class RoadSegment : MonoBehaviour
    {
        [SerializeField] float length = 40f;

        public float Length => length;
        public int Index { get; private set; }

        public void Place(int index)
        {
            Index = index;
            transform.localPosition = new Vector3(0f, 0f, index * length);
        }
    }
}
