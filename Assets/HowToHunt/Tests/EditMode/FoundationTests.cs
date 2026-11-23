using System;
using System.Collections.Generic;
using System.IO;
using HowToHunt.Foundation;
using NUnit.Framework;

namespace HowToHunt.Tests
{
    public class FoundationTests
    {
        private struct Ping { public int Value; }

        [TearDown]
        public void TearDown() => EventBus.ClearAll();

        [Test]
        public void EventBus_DeliversToAllSubscribers_AndUnsubscribes()
        {
            int sum = 0;
            EventHandlerIn<Ping> a = (in Ping p) => sum += p.Value;
            EventHandlerIn<Ping> b = (in Ping p) => sum += p.Value * 10;
            EventBus.Subscribe(a);
            EventBus.Subscribe(b);
            EventBus.Publish(new Ping { Value = 2 });
            Assert.AreEqual(22, sum);

            EventBus.Unsubscribe(a);
            EventBus.Publish(new Ping { Value = 1 });
            Assert.AreEqual(32, sum);
            Assert.AreEqual(1, EventBus.SubscriberCount<Ping>());
        }

        [Test]
        public void EventBus_PublishDoesNotAllocate()
        {
            int sum = 0;
            EventBus.Subscribe((in Ping p) => sum += p.Value);
            EventBus.Publish(new Ping { Value = 1 }); // warm up (static ctor, JIT)
            AllocationAssert.None(() =>
            {
                for (int i = 0; i < 1000; i++) EventBus.Publish(new Ping { Value = 1 });
            }, "EventBus.Publish allocated");
            Assert.AreEqual(1001, sum);
        }

        [Test]
        public void Console_SetsCvars_RespectsCheatsAndClamping()
        {
            var con = new ConsoleSystem();
            var gravity = con.Register(new FloatCvar("sv_gravity", 20f, "g", CvarFlags.Cheat, 0f, 100f));
            var fov = con.Register(new FloatCvar("fov", 90f, "fov", CvarFlags.Archive, 60f, 120f));

            con.Execute("sv_gravity 5");
            Assert.AreEqual(20f, gravity.Value, "cheat cvar changed without sv_cheats");

            con.Execute("sv_cheats 1; sv_gravity 5");
            Assert.AreEqual(5f, gravity.Value);

            con.Execute("fov 500");
            Assert.AreEqual(120f, fov.Value, "clamped to max");

            con.Execute("reset fov");
            Assert.AreEqual(90f, fov.Value);
        }

        [Test]
        public void Console_TokenizesQuotesAndComments()
        {
            var tokens = new List<string>();
            ConsoleSystem.Tokenize("say \"hello world\" now // comment", tokens);
            CollectionAssert.AreEqual(new[] { "say", "hello world", "now" }, tokens);
        }

        [Test]
        public void Console_CommandsAndArchiveRoundTrip()
        {
            var con = new ConsoleSystem();
            var sens = con.Register(new FloatCvar("sensitivity", 2f, "mouse", CvarFlags.Archive));
            string echoed = null;
            con.Output += (_, line) => echoed = line;
            con.Execute("sensitivity 1.25; echo hi there");
            Assert.AreEqual("hi there", echoed);

            var writer = new StringWriter();
            con.WriteArchive(writer);

            var con2 = new ConsoleSystem();
            var sens2 = con2.Register(new FloatCvar("sensitivity", 2f, "mouse", CvarFlags.Archive));
            con2.Execute(writer.ToString());
            Assert.AreEqual(1.25f, sens2.Value);
            Assert.AreEqual(sens.Value, sens2.Value);
        }

        [Test]
        public void Console_CommentsMayContainSemicolons()
        {
            var con = new ConsoleSystem();
            var fov = con.Register(new FloatCvar("fov", 90f, "fov"));
            var warnings = new List<string>();
            con.Output += (level, line) => { if (level != ConsoleLevel.Info) warnings.Add(line); };
            con.Execute("// generated; edit freely\nfov 100 // trailing; comment\n");
            Assert.AreEqual(100f, fov.Value);
            CollectionAssert.IsEmpty(warnings);
        }

        [Test]
        public void Console_ReplicatedCvarsAreHostControlled()
        {
            var con = new ConsoleSystem();
            var accel = con.Register(new FloatCvar("sv_accelerate", 5.5f, "a", CvarFlags.Replicated));
            con.IsRemoteClient = true;
            con.Execute("sv_accelerate 10");
            Assert.AreEqual(5.5f, accel.Value);
            Assert.IsTrue(con.TrySet("sv_accelerate", "10", fromHost: true));
            Assert.AreEqual(10f, accel.Value);
        }

        [Test]
        public void ContentId_IsStableAndCaseInsensitive()
        {
            Assert.AreEqual(ContentId.FromKey("prey.butterfly"), ContentId.FromKey("Prey.Butterfly"));
            Assert.AreNotEqual(ContentId.FromKey("prey.butterfly"), ContentId.FromKey("prey.rabbit"));
            // Pinned value: changing the hash breaks saves and network compatibility.
            Assert.AreEqual(ContentId.FromKey("prey.butterfly").Value, StableHash.Fnv1a32("prey.butterfly"));
            Assert.IsTrue(ContentId.FromKey("x").IsValid);
        }
    }
}
