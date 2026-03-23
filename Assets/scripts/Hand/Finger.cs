using System;
using UnityEngine;

namespace Hand
{
    [Serializable]
    public struct Finger : IEquatable<Finger>
    {
        public Transform[] joints;
        public GameObject landmarkBase;
        public int startLandmark;
        public string name;

        public bool Equals(Finger other)
        {
            return name == other.name;
        }

        public override bool Equals(object obj)
        {
            return obj is Finger other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (name != null ? name.GetHashCode() : 0);
        }
    }
}