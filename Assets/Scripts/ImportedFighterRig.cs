using UnityEngine;

namespace KidoTraining
{
    // Animation adapter for a downloaded skinned character. Mesh and texture stay authored assets.
    public sealed class ImportedFighterRig : MonoBehaviour
    {
        public Transform[] Joints = new Transform[11];
        public Transform HandR, HandL;
        public string SourceUrl, Author;
        public Quaternion[] RestLocal = new Quaternion[11];
        public Quaternion[] BodyAxes = new Quaternion[11];

        public void Apply(Pose pose, float blend)
        {
            for (int i = 0; i < Joints.Length; i++)
            {
                var r = pose.R[i];
                var offset = Quaternion.Inverse(BodyAxes[i]) * FighterModel.Q(r.x, r.y, r.z) * BodyAxes[i];
                Joints[i].localRotation = Quaternion.Slerp(Joints[i].localRotation, RestLocal[i] * offset, blend);
            }
        }
    }
}
