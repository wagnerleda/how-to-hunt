using System;
using System.IO;
using HowToHunt.Core;
using HowToHunt.Player;
using HowToHunt.Sim.Collision;
using HowToHunt.Sim.Movement;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vec3 = System.Numerics.Vector3;

namespace HowToHunt.Editor
{
    /// <summary>
    /// Movement regression tooling: exports the open scene's collision to Tests/Data/Worlds and generates scripted
    /// demos (bhop, surf, gaps, stairs, crouch-jump) into Tests/Data/Demos. ReplayTests re-simulate every demo and
    /// fail if the end state changes. Regenerate on purpose after an intended movement or map change.
    /// </summary>
    public static class MovementLabTools
    {
        public const string WorldsFolder = "Assets/HowToHunt/Tests/Data/Worlds";
        private const float Dt = 1f / 64f;

        [MenuItem("How to Hunt/Movement/Export Collision World", priority = 30)]
        public static void ExportWorld()
        {
            // Brushes only: terrain (thousands of prisms) stays out of the test data; the lab courses don't touch it.
            var world = new BrushWorld();
            SimWorld.AddBrushes(world);
            string name = SceneManager.GetActiveScene().name;
            string path = Path.Combine(ProjectRoot, WorldsFolder, name + BrushWorldCodec.Extension);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, BrushWorldCodec.Write(world.Brushes));
            AssetDatabase.Refresh();
            Debug.Log($"[MovementLab] Exported {world.BrushCount} brushes to {path}");
        }

        [MenuItem("How to Hunt/Movement/Regenerate Scripted Demos", priority = 31)]
        public static string GenerateScriptedDemos()
        {
            string worldPath = Path.Combine(ProjectRoot, WorldsFolder, "Graybox" + BrushWorldCodec.Extension);
            BrushWorld world = BrushWorldCodec.Read(File.ReadAllText(worldPath));
            var cfg = new MoveConfig();
            var report = new System.Text.StringBuilder();

            report.AppendLine(Save("scripted_stairs", world, cfg, Feet(16f, 0.02f, -7f), 0f, 192, (i, s) => new MoveCmd { Forward = 1f, Yaw = 0f }));

            report.AppendLine(Save("scripted_crouch_ledge", world, cfg, Feet(-16f, 0.02f, -16f), 0f, 128, (i, s) =>
            {
                var c = new MoveCmd { Forward = 1f, Yaw = 0f };
                if (i == 14) c.Buttons |= MoveButtons.Jump;
                else if (i > 14 && i < 70 && !s.OnGround) c.Buttons |= MoveButtons.Duck;
                return c;
            }));

            // Bhop on flat ground along the sprint lane: run, then auto-hop with a strafe autopilot (look along the
            // velocity, strafe left/right alternately every 6 ticks). Shows the speed gain air strafing gives.
            report.AppendLine(Save("scripted_bhop_flat", world, cfg, Feet(-8f, 0.02f, 8f), 270f, 64 * 6, (i, s) => StrafeBot(i, s, 270f, 20)));

            // Same autopilot on the bhop strip (lands wherever it lands: a regression demo, not a perfect run).
            report.AppendLine(Save("scripted_bhop", world, cfg, ToV(GrayboxSceneBuilder.BhopStart), 0f, 64 * 6, (i, s) => StrafeBot(i, s, 0f, 12)));

            // Surf: step off the tower (east) onto the ramp's west face, look down the course and hold strafe into the ramp.
            report.AppendLine(Save("scripted_surf", world, cfg, ToV(GrayboxSceneBuilder.SurfStart), 90f, 64 * 7, (i, s) =>
            {
                if (s.OnGround && s.Position.Y > 10f) return new MoveCmd { Forward = 1f, Yaw = 90f };
                // Aim 2° down the slope: gravity along the view direction becomes forward speed.
                return new MoveCmd { Side = -1f, Yaw = 182f };
            }));

            // Gaps: run to the edge, jump, and keep hopping forward.
            report.AppendLine(Save("scripted_gaps", world, cfg, ToV(GrayboxSceneBuilder.GapsStart), 270f, 64 * 6, (i, s) =>
            {
                bool past = s.Position.X <= -22.6f;
                return new MoveCmd { Forward = 1f, Yaw = 270f, Buttons = past ? MoveButtons.Jump | MoveButtons.AutoHop : MoveButtons.None };
            }));

            // The hard route to the desert (2026-11-24): proof it can be done (a run that reaches the landing).
            report.AppendLine(Save("scripted_desert_surf", world, cfg, ToV(GrayboxSceneBuilder.DesertRouteStart), 180f, 64 * 15, DesertPilot));

            AssetDatabase.Refresh();
            Debug.Log("[MovementLab] Scripted demos regenerated:\n" + report);
            return report.ToString();
        }

        private static string Save(string name, BrushWorld world, MoveConfig cfg, Vec3 feet, float startYaw, int ticks, Func<int, MoveState, MoveCmd> script)
        {
            var demo = new MoveDemo { Map = "Graybox", TickDelta = Dt, Config = cfg.Clone() };
            demo.Start = new MoveState { Position = feet + new Vec3(0f, cfg.HullHalfExtents.Y, 0f) };
            MoveState s = demo.Start;
            float maxSpeed = 0f;
            for (int i = 0; i < ticks; i++)
            {
                MoveCmd cmd = script(i, s);
                demo.Cmds.Add(cmd);
                PlayerMove.Step(ref s, in cmd, demo.Config, world, Dt);
                maxSpeed = Mathf.Max(maxSpeed, PlayerMove.HorizontalSpeed(s.Velocity));
            }
            demo.End = s;
            demo.End.Events = MoveEvents.None;
            demo.End.LandSpeed = 0f;
            string path = Path.Combine(ProjectRoot, DemoRecorder.TestDemosFolder, name + MoveDemo.Extension);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, demo.Write());
            return $"{name}: {ticks} ticks, end {s.Position.X:0.0} {s.Position.Y:0.0} {s.Position.Z:0.0}, max speed {maxSpeed:0.0} m/s, ground={s.OnGround}";
        }

        /// <summary>
        /// Surfs the hard desert route like a good player: on a ramp, look down the course (a little down the slope)
        /// and hold strafe into the face; between ramps, air-strafe toward the face of the next ramp on the side you
        /// come from (look along the velocity, strafe toward the target line).
        /// </summary>
        public static MoveCmd DesertPilot(int tick, MoveState s)
        {
            float z = s.Position.Z, x = s.Position.X;
            if (z > GrayboxSceneBuilder.DesertRouteStart.z - 4.5f) return new MoveCmd { Forward = 1f, Yaw = 172f }; // walk off the ledge, a little east of the ridge
            int n = GrayboxSceneBuilder.HardRampLength.Length, on = n - 1;
            for (int i = 0; i < n; i++)
            {
                GrayboxSceneBuilder.HardRamp(i, out Vector3 start, out Vector3 end);
                if (z > end.z) { on = i; break; }
            }
            GrayboxSceneBuilder.HardRamp(on, out Vector3 rs, out _);
            float ridge = rs.x;
            // Still north of this ramp's ridge start (it tilts back ~2 m): flying the gap toward it.
            bool between = on > 0 && z > rs.z - 2.1f;
            bool west = x < ridge;
            if (!between)
            {
                // On the face: strafe into it (facing -z, Side -1 pushes +x), look 2 degrees down the slope.
                return new MoveCmd { Side = west ? -1f : 1f, Yaw = west ? 182f : 178f };
            }
            // In the air: air-strafe toward the face on our side of the next ridge, a few metres off it.
            float e = ridge + (west ? -3f : 3f) - x;
            if (Math.Abs(e) < 0.8f) return new MoveCmd { Yaw = 180f };
            return new MoveCmd { Side = e > 0f ? -1f : 1f, Yaw = Heading(s, 180f) };
        }

        /// <summary>Air-strafe autopilot: aim along the velocity and alternate strafe sides, auto-hopping.</summary>
        private static MoveCmd StrafeBot(int tick, MoveState s, float startYaw, int runUpTicks)
        {
            if (tick < runUpTicks) return new MoveCmd { Forward = 1f, Yaw = startYaw };
            bool right = ((tick - runUpTicks) / 6) % 2 == 0;
            return new MoveCmd { Side = right ? 1f : -1f, Yaw = Heading(s, startYaw), Buttons = MoveButtons.Jump | MoveButtons.AutoHop };
        }

        /// <summary>Yaw of the horizontal velocity (falls back when nearly stopped).</summary>
        private static float Heading(MoveState s, float fallback)
        {
            if (PlayerMove.HorizontalSpeed(s.Velocity) < 0.5f) return fallback;
            return Mathf.Atan2(s.Velocity.X, s.Velocity.Z) * Mathf.Rad2Deg;
        }

        private static Vec3 Feet(float x, float y, float z) => new Vec3(x, y, z);
        private static Vec3 ToV(Vector3 v) => new Vec3(v.x, v.y + 0.02f, v.z);
        private static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);
    }
}
