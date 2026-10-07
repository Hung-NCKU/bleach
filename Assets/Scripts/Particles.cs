using UnityEngine;

namespace KidoTraining
{
    // 共用粒子發射器（加算發光／受重力／煙塵），以 Emit 逐顆發射
    public class Particles
    {
        readonly ParticleSystem glow, glowGrav, smoke;

        public Particles(Transform parent)
        {
            glow = Make(parent, "FX_Glow", Mats.Glow(Color.white, 1, Mats.GlowTex), 0, 1.8f);
            glowGrav = Make(parent, "FX_GlowGrav", Mats.Glow(Color.white, 1, Mats.GlowTex), 0.35f, 1.2f);
            smoke = Make(parent, "FX_Smoke", Mats.Alpha(Color.white, 1, Mats.SoftTex), 0.15f, 1.5f);
        }

        static ParticleSystem Make(Transform parent, string name, Material mat, float gravity, float drag)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 4000;
            main.startSpeed = 0;
            main.gravityModifier = gravity;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var em = ps.emission; em.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0.2f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var lim = ps.limitVelocityOverLifetime;
            lim.enabled = true;
            lim.limit = 1000;
            lim.drag = drag;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        public void Spawn(Vector3 pos, Vector3 vel, float life, float size, Color color, bool gravity = false, bool smokey = false, float alpha = 1f)
        {
            var ps = smokey ? smoke : gravity ? glowGrav : glow;
            var ep = new ParticleSystem.EmitParams
            {
                position = pos,
                velocity = vel,
                startLifetime = life,
                startSize = size,
                startColor = new Color(color.r, color.g, color.b, alpha),
                applyShapeToPosition = false,
            };
            ps.Emit(ep, 1);
        }

        public void Burst(Vector3 pos, int n, float speed, float size, Color color, float life = 0.6f, bool gravity = false, bool smokey = false, Vector3 baseVel = default, float alpha = 1f)
        {
            for (int i = 0; i < n; i++)
            {
                var dir = Random.onUnitSphere;
                float sp = speed * Random.Range(0.4f, 1.2f);
                Spawn(pos, dir * sp + baseVel, life * Random.Range(0.6f, 1.2f), size * Random.Range(0.7f, 1.2f), color, gravity, smokey, alpha);
            }
        }

        public void Clear()
        {
            glow.Clear(); glowGrav.Clear(); smoke.Clear();
        }
    }
}
