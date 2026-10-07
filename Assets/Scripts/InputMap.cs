using UnityEngine;

namespace KidoTraining
{
    // 火影忍者終極風暴式操作（只有基本動作；鬼道由語音詠唱觸發，麥克風持續監聽）
    //   鍵盤：WASD 移動｜J 攻擊｜K / Space 跳躍｜L 靈壓｜I 防禦
    //   手把：左搖桿移動｜B 攻擊｜A 跳躍｜Y 靈壓｜RB／LB 防禦
    //   組合：L→K 瞬步衝刺｜長按 L 集中靈壓
    public static class InputMap
    {
        static bool Down(KeyCode a, KeyCode b = KeyCode.None, KeyCode c = KeyCode.None) =>
            Input.GetKeyDown(a) || b != KeyCode.None && Input.GetKeyDown(b) || c != KeyCode.None && Input.GetKeyDown(c);
        static bool Held(KeyCode a, KeyCode b = KeyCode.None, KeyCode c = KeyCode.None) =>
            Input.GetKey(a) || b != KeyCode.None && Input.GetKey(b) || c != KeyCode.None && Input.GetKey(c);

        public static bool Attack => Down(KeyCode.J, KeyCode.JoystickButton1);
        public static bool Jump => Down(KeyCode.K, KeyCode.Space, KeyCode.JoystickButton0);
        public static bool ChargeDown => Down(KeyCode.L, KeyCode.JoystickButton3);
        public static bool ChargeHeld => Held(KeyCode.L, KeyCode.JoystickButton3);
        public static bool GuardHeld => Held(KeyCode.I, KeyCode.JoystickButton5, KeyCode.JoystickButton4);
        public static bool Pause => Down(KeyCode.Escape, KeyCode.JoystickButton7);

        // 搖桿／方向鍵（x: 右, y: 前）
        public static Vector2 Stick()
        {
            float x = 0, y = 0;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) y += 1;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) y -= 1;
            if (x == 0 && y == 0)
            {
                // 預設 InputManager 的 Horizontal/Vertical 軸包含手把左搖桿
                try { x = Input.GetAxisRaw("Horizontal"); y = Input.GetAxisRaw("Vertical"); } catch { }
                if (Mathf.Abs(x) < 0.25f) x = 0;
                if (Mathf.Abs(y) < 0.25f) y = 0;
            }
            var v = new Vector2(x, y);
            return v.sqrMagnitude > 1 ? v.normalized : v;
        }

        // 讀取玩家意圖（移動方向依鏡頭換算為世界座標）
        public static Intent Read(Transform cam)
        {
            var st = Stick();
            var fwd = cam.forward; fwd.y = 0; fwd.Normalize();
            var right = cam.right; right.y = 0; right.Normalize();
            return new Intent
            {
                Move = right * st.x + fwd * st.y,
                Attack = Attack,
                Jump = Jump,
                ChargePressed = ChargeDown,
                ChargeHeld = ChargeHeld,
                GuardHeld = GuardHeld,
            };
        }
    }
}
