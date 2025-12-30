using Unity.Mathematics;
using UnityEngine;

namespace WorldBuilderCoop.Network
{
    public class UserAvatar : MonoBehaviour
    {
        public int UserId { get; set; }
        public Vector3 position { get; set; }
        public quaternion rotation { get; set; }
        public int placeIndex { get; set; }
    }
}