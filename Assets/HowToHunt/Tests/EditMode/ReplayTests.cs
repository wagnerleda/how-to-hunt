using System.Collections.Generic;
using System.IO;
using Vector3 = System.Numerics.Vector3;
using HowToHunt.Sim.Collision;
using HowToHunt.Sim.Movement;
using NUnit.Framework;
using UnityEngine;

namespace HowToHunt.Tests
{
    /// <summary>
    /// Re-simulates every recorded and scripted movement demo against the exported collision world and checks the
    /// end state is reproduced exactly. A failure means movement physics or the map changed: if intended,
    /// regenerate (How to Hunt > Movement > Regenerate Scripted Demos) or re-record; otherwise it's a regression.
    /// </summary>
    public class ReplayTests
    {
        private static string DataRoot => Path.Combine(Path.GetDirectoryName(Application.dataPath), "Assets/HowToHunt/Tests/Data");
        private static readonly Dictionary<string, BrushWorld> Worlds = new Dictionary<string, BrushWorld>();

        public static IEnumerable<string> DemoNames()
        {
            string dir = Path.Combine(DataRoot, "Demos");
            if (!Directory.Exists(dir)) yield break;
            foreach (string f in Directory.GetFiles(dir, "*" + MoveDemo.Extension))
                yield return Path.GetFileNameWithoutExtension(f);
        }

        private static MoveDemo LoadDemo(string name) =>
            MoveDemo.Read(File.ReadAllText(Path.Combine(DataRoot, "Demos", name + MoveDemo.Extension)));

        private static BrushWorld World(string map)
        {
            if (!Worlds.TryGetValue(map, out BrushWorld world))
            {
                string path = Path.Combine(DataRoot, "Worlds", map + BrushWorldCodec.Extension);
                Assert.IsTrue(File.Exists(path), $"Missing collision export for map '{map}' ({path}). Run How to Hunt > Movement > Export Collision World.");
                world = BrushWorldCodec.Read(File.ReadAllText(path));
                Worlds[map] = world;
            }
            return world;
        }

        [Test]
        public void ThereAreDemosToReplay()
        {
            CollectionAssert.IsNotEmpty(DemoNames());
        }

        [TestCaseSource(nameof(DemoNames))]
        public void Demo_ReplaysToTheRecordedEndState(string name)
        {
            MoveDemo demo = LoadDemo(name);
            MoveState end = demo.Replay(World(demo.Map));
            float error = Vector3.Distance(end.Position, demo.End.Position);
            Assert.Less(error, 1e-3f, $"'{name}' ended {error:0.0000} m away from the recording ({demo.Cmds.Count} ticks).");
            Assert.Less(Vector3.Distance(end.Velocity, demo.End.Velocity), 1e-3f, "velocity differs");
            Assert.AreEqual(demo.End.OnGround, end.OnGround, "ground state differs");
            Assert.AreEqual(demo.End.Ducked, end.Ducked, "duck state differs");
        }

        [Test]
        public void Replay_IsDeterministicRunToRun()
        {
            foreach (string name in DemoNames())
            {
                MoveDemo demo = LoadDemo(name);
                BrushWorld world = World(demo.Map);
                MoveState a = demo.Replay(world), b = demo.Replay(world);
                Assert.AreEqual(a.Position, b.Position, name);
                Assert.AreEqual(a.Velocity, b.Velocity, name);
            }
        }

        // Behavior checks on the lab map: the courses must stay doable.

        [Test]
        public void Surf_CarriesThePlayerDownTheWholeRamp()
        {
            MoveDemo demo = LoadDemo("scripted_surf");
            float bestSpeed = 0f, minZ = 0f;
            MoveState s = demo.Start;
            BrushWorld world = World(demo.Map);
            foreach (MoveCmd cmd in demo.Cmds)
            {
                MoveCmd c = cmd;
                PlayerMove.Step(ref s, in c, demo.Config, world, demo.TickDelta);
                bestSpeed = System.MathF.Max(bestSpeed, PlayerMove.HorizontalSpeed(s.Velocity));
                minZ = System.MathF.Min(minZ, s.Position.Z);
            }
            Assert.Less(minZ, -65f, "surf run should reach the end of the 58 m ramp");
            Assert.Greater(bestSpeed, 14f, "surfing should build speed");
        }

        [Test]
        public void DesertSurf_TheHardRouteCanBeDone()
        {
            // The scripted run (MovementLabTools.DesertPilot) rides all five ramps and leaves the last one above the
            // desert landing (y -178 from z -172 south): the route is hard, not impossible.
            MoveDemo demo = LoadDemo("scripted_desert_surf");
            MoveState s = demo.Start;
            BrushWorld world = World(demo.Map);
            float best = 0f;
            bool overLanding = false;
            foreach (MoveCmd cmd in demo.Cmds)
            {
                MoveCmd c = cmd;
                PlayerMove.Step(ref s, in c, demo.Config, world, demo.TickDelta);
                best = System.MathF.Max(best, PlayerMove.HorizontalSpeed(s.Velocity));
                if (s.Position.Z < -172f && s.Position.Y > -178f) overLanding = true;
            }
            Assert.IsTrue(overLanding, "the run should reach the desert landing still above its floor");
            Assert.Greater(best, 30f, "the hard route is fast");
        }

        [Test]
        public void Bhop_StrafeAutopilot_GainsSpeed()
        {
            MoveDemo demo = LoadDemo("scripted_bhop_flat");
            float best = 0f;
            MoveState s = demo.Start;
            BrushWorld world = World(demo.Map);
            foreach (MoveCmd cmd in demo.Cmds)
            {
                MoveCmd c = cmd;
                PlayerMove.Step(ref s, in c, demo.Config, world, demo.TickDelta);
                best = System.MathF.Max(best, PlayerMove.HorizontalSpeed(s.Velocity));
            }
            Assert.Greater(best, demo.Config.MaxSpeed * 1.8f, "bunny hopping with air strafes should far exceed run speed");
        }
    }
}
