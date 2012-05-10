using Microsoft.Kinect;
using Microsoft.Xna.Framework;

namespace KinectSabre
{
    public static class Tools
    {
        public static Vector3 ToVector3(this SkeletonPoint vector)
        {
            return new Vector3(vector.X, vector.Y, vector.Z);
        }
    }
}
