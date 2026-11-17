using System.Collections.Generic;
using HowToHunt.Core;
using HowToHunt.Hunting;
using HowToHunt.Sim.Collision;
using HowToHunt.Sim.Movement;
using NUnit.Framework;
using Vec3 = System.Numerics.Vector3;

namespace HowToHunt.Tests
{
    /// <summary>How to Fish's gun handling: the sprung recoil rig, the shove, and what attachments may change.</summary>
    public sealed class GunHandlingTests
    {
        private static GunHandling Handling(float springPos, float springRot) => new GunHandling
        {
            KickPos = new UnityEngine.Vector3(0.1f, 0.1f, 0.5f), KickRot = new UnityEngine.Vector2(15f, 15f),
            SpringPos = springPos, DamperPos = 0f, SpringRot = springRot, DamperRot = 0f,
            AdsSpring = 2f, AdsKickPos = 1f, AdsKickRot = 1f,
        };

        [TestCase(500f, 500f)]
        [TestCase(2500f, 5000f)]
        [TestCase(50000f, 5000f)] // How to Fish's stiffest: must stay stable at 50 Hz
        public void RecoilRig_KicksBackAndUp_ThenSettles(float springPos, float springRot)
        {
            var rig = new GunRecoilRig();
            GunHandling h = Handling(springPos, springRot);
            rig.Kick(h, 0f, aiming: false, scale: 1f);
            float mostBack = 0f, mostUp = 0f;
            for (int i = 0; i < 15; i++)
            {
                rig.Update(GunRecoilRig.Step, h, aiming: false);
                mostBack = UnityEngine.Mathf.Min(mostBack, rig.Position.z);
                mostUp = UnityEngine.Mathf.Min(mostUp, rig.Euler.x);
            }
            Assert.Less(mostBack, -0.005f, "thrown back");
            Assert.Less(mostUp, -0.5f, "muzzle up (negative pitch)");
            for (int i = 0; i < 100; i++) rig.Update(GunRecoilRig.Step, h, aiming: false);
            Assert.Less(rig.Position.magnitude, 0.002f, "back in the hand after 2 s");
            Assert.Less(rig.Euler.magnitude, 0.2f);
            Assert.IsFalse(float.IsNaN(rig.Position.x));
        }

        private static MoveState Standing(MoveConfig cfg) => new MoveState { Position = new Vec3(0f, cfg.HullHalfExtents.Y, 0f), OnGround = true };

        [Test]
        public void Shove_PushesBackAlongTheView_OnceAnInterval_AndIsCapped()
        {
            var cfg = new MoveConfig();
            var world = new BrushWorld();
            world.Add(Brush.Box(new Vec3(0f, -0.5f, 0f), new Vec3(100f, 0.5f, 100f)));
            MoveState s = Standing(cfg);
            const float dt = 1f / 64f;
            var shove = new MoveCmd { Yaw = 0f, Pitch = 0f, Push = 5f };
            PlayerMove.Step(ref s, in shove, cfg, world, dt);
            Assert.Less(s.Velocity.Z, -3f, "facing +Z, shoved towards -Z");
            float afterFirst = s.Velocity.Z;
            PlayerMove.Step(ref s, in shove, cfg, world, dt);
            Assert.Greater(s.Velocity.Z, afterFirst, "a second shove right away is ignored (friction only)");

            MoveState t = Standing(cfg);
            var huge = new MoveCmd { Push = 100f };
            PlayerMove.Step(ref t, in huge, cfg, world, dt);
            Assert.GreaterOrEqual(t.Velocity.Z, -cfg.MaxPush - 0.01f, "capped by sv_push_max");

            MoveState u = Standing(cfg);
            var down = new MoveCmd { Pitch = 80f, Push = 6f };
            PlayerMove.Step(ref u, in down, cfg, world, dt);
            Assert.IsFalse(u.OnGround, "firing at your feet shoves you off the ground");
            Assert.Greater(u.Velocity.Y, 3f);
        }

        [Test]
        public void Demo_KeepsTheShove()
        {
            var demo = new MoveDemo();
            demo.Cmds.Add(new MoveCmd { Forward = 1f, Push = 4.5f });
            demo.Cmds.Add(new MoveCmd { Forward = 1f });
            MoveDemo back = MoveDemo.Read(demo.Write());
            Assert.AreEqual(4.5f, back.Cmds[0].Push, 1e-3f);
            Assert.AreEqual(0f, back.Cmds[1].Push);
        }

        [Test]
        public void OnlyMuzzleDevices_SoftenTheViewKick()
        {
            ContentRegistry.Reset();
            Loadouts.Reset();
            ToolDefinition rifle = ContentRegistry.Get<ToolDefinition>("tool.heavyrifle");
            GunSetup bare = GunSetup.For(rifle, null);
            GunSetup grip = GunSetup.For(rifle, new List<AttachmentDefinition> { ContentRegistry.Get<AttachmentDefinition>("attachment.foregrip") });
            Assert.AreEqual(bare.RecoilUp, grip.RecoilUp, 1e-4f, "a foregrip doesn't touch the view kick");
            Assert.Less(grip.Handling.KickPos.z, bare.Handling.KickPos.z, "it steadies the gun in the hand");
            Assert.Less(grip.Handling.FreeAim.y, bare.Handling.FreeAim.y, "and it wanders less from the hip");
            GunSetup can = GunSetup.For(rifle, new List<AttachmentDefinition> { ContentRegistry.Get<AttachmentDefinition>("attachment.suppressor") });
            Assert.Less(can.RecoilUp, bare.RecoilUp);
            Assert.Greater(can.RecoilUp, bare.RecoilUp * 0.85f, "but only a little");
            Assert.AreEqual(0f, bare.RecoilRecover, "the view kick stays: you pull it down");
        }
    }
}
