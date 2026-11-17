#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;

namespace HowToHunt.Sim.Movement
{
    /// <summary>
    /// A recorded movement session: map, physics config, start state, one command per tick, end state.
    /// Replaying the commands from the start state must reproduce the end state exactly (determinism check).
    /// Text format, one line per tick, so demos diff well and survive git.
    /// </summary>
    public sealed class MoveDemo
    {
        public const string Extension = ".hthdemo";
        private const string Magic = "hthdemo 1";

        public string Map = string.Empty;
        public float TickDelta = 1f / 64f;
        public MoveConfig Config = new MoveConfig();
        public MoveState Start;
        public MoveState End;
        public readonly List<MoveCmd> Cmds = new List<MoveCmd>();

        /// <summary>Re-simulates every command from <see cref="Start"/>.</summary>
        public MoveState Replay(Collision.ICollisionWorld world)
        {
            MoveState s = Start;
            for (int i = 0; i < Cmds.Count; i++)
            {
                MoveCmd cmd = Cmds[i];
                PlayerMove.Step(ref s, in cmd, Config, world, TickDelta);
            }
            return s;
        }

        public string Write()
        {
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(64 + Cmds.Count * 32);
            sb.Append(Magic).Append('\n');
            sb.Append("map ").Append(Map).Append('\n');
            sb.Append("dt ").Append(TickDelta.ToString("R", inv)).Append('\n');
            foreach (KeyValuePair<string, string> kv in MoveConfigCodec.ToPairs(Config))
                sb.Append("cfg ").Append(kv.Key).Append(' ').Append(kv.Value).Append('\n');
            sb.Append("start ").Append(StateToString(Start)).Append('\n');
            sb.Append("end ").Append(StateToString(End)).Append('\n');
            sb.Append("cmds ").Append(Cmds.Count.ToString(inv)).Append('\n');
            foreach (MoveCmd c in Cmds)
            {
                sb.Append(F(c.Forward)).Append(' ').Append(F(c.Side)).Append(' ')
                  .Append(F(c.Yaw)).Append(' ').Append(F(c.Pitch)).Append(' ')
                  .Append(((int)c.Buttons).ToString(inv));
                if (c.Push > 0f) sb.Append(' ').Append(F(c.Push)); // older demos have no shove column
                sb.Append('\n');
            }
            return sb.ToString();
        }

        public static MoveDemo Read(string text)
        {
            var demo = new MoveDemo();
            var reader = new StringReader(text);
            string? line = reader.ReadLine();
            if (line == null || line.Trim() != Magic) throw new FormatException("Not a How to Hunt movement demo.");
            var cfg = new Dictionary<string, string>();
            int cmdCount = -1;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Length == 0) continue;
                if (cmdCount >= 0)
                {
                    string[] p = line.Split(' ');
                    demo.Cmds.Add(new MoveCmd
                    {
                        Forward = P(p[0]), Side = P(p[1]), Yaw = P(p[2]), Pitch = P(p[3]),
                        Buttons = (MoveButtons)int.Parse(p[4], CultureInfo.InvariantCulture),
                        Push = p.Length > 5 ? P(p[5]) : 0f,
                    });
                    continue;
                }
                int space = line.IndexOf(' ');
                string key = space < 0 ? line : line.Substring(0, space);
                string rest = space < 0 ? string.Empty : line.Substring(space + 1);
                switch (key)
                {
                    case "map": demo.Map = rest; break;
                    case "dt": demo.TickDelta = P(rest); break;
                    case "cfg":
                        int s2 = rest.IndexOf(' ');
                        cfg[rest.Substring(0, s2)] = rest.Substring(s2 + 1);
                        break;
                    case "start": demo.Start = StateFromString(rest); break;
                    case "end": demo.End = StateFromString(rest); break;
                    case "cmds": cmdCount = int.Parse(rest, CultureInfo.InvariantCulture); break;
                }
            }
            MoveConfigCodec.Apply(cfg, demo.Config);
            if (cmdCount != demo.Cmds.Count) throw new FormatException($"Demo truncated: {demo.Cmds.Count}/{cmdCount} commands.");
            return demo;
        }

        private static string StateToString(in MoveState s)
        {
            return string.Join(" ", F(s.Position.X), F(s.Position.Y), F(s.Position.Z), F(s.Velocity.X), F(s.Velocity.Y), F(s.Velocity.Z),
                s.OnGround ? "1" : "0", s.JumpHeld ? "1" : "0", s.Ducked ? "1" : "0", ((int)s.Mode).ToString(CultureInfo.InvariantCulture));
        }

        private static MoveState StateFromString(string text)
        {
            string[] p = text.Split(' ');
            return new MoveState
            {
                Position = new Vector3(P(p[0]), P(p[1]), P(p[2])),
                Velocity = new Vector3(P(p[3]), P(p[4]), P(p[5])),
                OnGround = p[6] == "1",
                JumpHeld = p[7] == "1",
                Ducked = p[8] == "1",
                Mode = (MoveMode)int.Parse(p[9], CultureInfo.InvariantCulture),
            };
        }

        // "R" round-trips floats exactly, which the determinism check needs.
        private static string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);
        private static float P(string s) => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    /// <summary>MoveConfig ⇄ name/value pairs. Names match the movement cvars.</summary>
    public static class MoveConfigCodec
    {
        public static IEnumerable<KeyValuePair<string, string>> ToPairs(MoveConfig c)
        {
            var inv = CultureInfo.InvariantCulture;
            yield return Pair("sv_gravity", c.Gravity);
            yield return Pair("sv_friction", c.Friction);
            yield return Pair("sv_stopspeed", c.StopSpeed);
            yield return Pair("sv_accelerate", c.Accelerate);
            yield return Pair("sv_airaccelerate", c.AirAccelerate);
            yield return Pair("sv_air_max_wishspeed", c.AirSpeedCap);
            yield return Pair("sv_maxspeed", c.MaxSpeed);
            yield return Pair("sv_jump_impulse", c.JumpSpeed);
            yield return Pair("sv_maxvelocity", c.MaxVelocity);
            yield return Pair("sv_standable_normal", c.StandableNormalY);
            yield return Pair("sv_stepsize", c.StepHeight);
            yield return new KeyValuePair<string, string>("sv_autobunnyhopping", c.AutoHop ? "1" : "0");

            KeyValuePair<string, string> Pair(string name, float v) => new KeyValuePair<string, string>(name, v.ToString("R", inv));
        }

        public static void Apply(IReadOnlyDictionary<string, string> values, MoveConfig c)
        {
            float Get(string name, float fallback) =>
                values.TryGetValue(name, out string? s) && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : fallback;
            c.Gravity = Get("sv_gravity", c.Gravity);
            c.Friction = Get("sv_friction", c.Friction);
            c.StopSpeed = Get("sv_stopspeed", c.StopSpeed);
            c.Accelerate = Get("sv_accelerate", c.Accelerate);
            c.AirAccelerate = Get("sv_airaccelerate", c.AirAccelerate);
            c.AirSpeedCap = Get("sv_air_max_wishspeed", c.AirSpeedCap);
            c.MaxSpeed = Get("sv_maxspeed", c.MaxSpeed);
            c.JumpSpeed = Get("sv_jump_impulse", c.JumpSpeed);
            c.MaxVelocity = Get("sv_maxvelocity", c.MaxVelocity);
            c.StandableNormalY = Get("sv_standable_normal", c.StandableNormalY);
            c.StepHeight = Get("sv_stepsize", c.StepHeight);
            if (values.TryGetValue("sv_autobunnyhopping", out string? hop)) c.AutoHop = hop == "1";
        }
    }
}
