using UnityEngine;

namespace KidoTraining
{
    // 終極風暴式鏡頭：追蹤兩人中點，從兩人連線的側面拍攝，距離隨間距縮放
    public class CameraRig
    {
        public readonly Camera Cam;
        Vector3 pos, look;
        float side = -1; // -1：玩家在畫面左側

        public CameraRig(Camera cam)
        {
            Cam = cam;
            pos = new Vector3(12, 5, 0);
            look = Vector3.up;
        }

        public void Update(Fighter a, Fighter b, float dt, float shake)
        {
            var mid = (a.Pos + b.Pos) * 0.5f;
            var axis = b.Pos - a.Pos; axis.y = 0;
            float dist = axis.magnitude;
            axis = dist > 0.01f ? axis / dist : Vector3.forward;
            var perp = new Vector3(-axis.z, 0, axis.x);
            // 選擇離目前鏡頭較近的一側，避免鏡頭翻轉
            var cur = pos - mid; cur.y = 0;
            if (Vector3.Dot(cur, perp * side) < -2f) side = -side;
            float d = Mathf.Clamp(6.5f + dist * 0.75f, 7.5f, 22f);
            // 稍微偏向玩家背後，營造終極風暴的斜側視角
            var want = mid + perp * side * d - axis * (d * 0.28f) + Vector3.up * (2.6f + dist * 0.12f + Mathf.Max(a.Pos.y, b.Pos.y) * 0.5f);
            var wantLook = mid + Vector3.up * (1.1f + Mathf.Max(a.Pos.y, b.Pos.y) * 0.4f);
            float k = 1 - Mathf.Exp(-dt * 5);
            pos = Vector3.Lerp(pos, want, k);
            look = Vector3.Lerp(look, wantLook, 1 - Mathf.Exp(-dt * 8));
            var shakeOff = shake > 0 ? Random.insideUnitSphere * shake * 0.35f : Vector3.zero;
            Cam.transform.position = pos + shakeOff;
            Cam.transform.LookAt(look + shakeOff * 0.5f);
        }

        // 標題畫面：緩慢環繞
        public void Orbit(float time)
        {
            float a = time * 0.08f;
            Cam.transform.position = new Vector3(Mathf.Cos(a) * 16, 5.5f, Mathf.Sin(a) * 16);
            Cam.transform.LookAt(new Vector3(0, 1.5f, 0));
            pos = Cam.transform.position;
        }
    }
}
