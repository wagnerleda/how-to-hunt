#nullable enable
using System;
using System.Collections.Generic;
using System.IO;

namespace HowToHunt.Foundation
{
    public enum ConsoleLevel { Info, Warning, Error }

    [Flags]
    public enum CommandFlags
    {
        None = 0,
        /// <summary>Requires sv_cheats 1.</summary>
        Cheat = 1 << 0,
    }

    public delegate void CommandHandler(CommandArgs args);

    /// <summary>Tokenized arguments of a console command. Token 0 is the command name.</summary>
    public readonly struct CommandArgs
    {
        private readonly List<string> _tokens;

        public CommandArgs(List<string> tokens, string raw)
        {
            _tokens = tokens;
            Raw = raw;
        }

        public string Raw { get; }
        /// <summary>Number of arguments, not counting the command name.</summary>
        public int Count => _tokens.Count - 1;
        public string Name => _tokens[0];
        public string this[int index] => _tokens[index + 1];

        /// <summary>Everything after the command name, as typed.</summary>
        public string ArgString
        {
            get
            {
                string trimmed = Raw.TrimStart();
                int space = trimmed.IndexOf(' ');
                return space < 0 ? string.Empty : trimmed.Substring(space + 1).Trim();
            }
        }
    }

    public sealed class ConCommand
    {
        public ConCommand(string name, string description, CommandHandler handler, CommandFlags flags = CommandFlags.None)
        {
            Name = name;
            Description = description;
            Handler = handler;
            Flags = flags;
        }

        public string Name { get; }
        public string Description { get; }
        public CommandHandler Handler { get; }
        public CommandFlags Flags { get; }
    }

    /// <summary>
    /// Source-style developer console: cvars, commands, config files.
    /// One global instance is used by the game; tests create their own.
    /// </summary>
    public sealed class ConsoleSystem
    {
        public static ConsoleSystem Global { get; private set; } = new ConsoleSystem();

        private readonly Dictionary<string, Cvar> _cvars = new Dictionary<string, Cvar>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ConCommand> _commands = new Dictionary<string, ConCommand>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _tokenBuffer = new List<string>(16);
        private int _execDepth;

        public readonly BoolCvar Cheats;

        public ConsoleSystem()
        {
            Cheats = new BoolCvar("sv_cheats", false, "Allow cheat cvars and commands (host-controlled).", CvarFlags.Replicated);
            Register(Cheats);
            RegisterBuiltins();
        }

        /// <summary>Raised for every line printed to the console.</summary>
        public event Action<ConsoleLevel, string>? Output;

        /// <summary>Loads the text of a config file by name (for 'exec'). Set by the host application.</summary>
        public Func<string, string?>? ConfigLoader { get; set; }

        /// <summary>Replaces the global instance (tests, domain reload).</summary>
        public static void ResetGlobal() => Global = new ConsoleSystem();

        public IEnumerable<Cvar> Cvars => _cvars.Values;
        public IEnumerable<ConCommand> Commands => _commands.Values;

        public T Register<T>(T cvar) where T : Cvar
        {
            if (_commands.ContainsKey(cvar.Name)) throw new InvalidOperationException($"'{cvar.Name}' is already a command.");
            if (_cvars.TryGetValue(cvar.Name, out Cvar? existing) && !ReferenceEquals(existing, cvar))
                throw new InvalidOperationException($"Cvar '{cvar.Name}' is registered twice.");
            _cvars[cvar.Name] = cvar;
            return cvar;
        }

        public ConCommand Register(ConCommand command)
        {
            if (_cvars.ContainsKey(command.Name)) throw new InvalidOperationException($"'{command.Name}' is already a cvar.");
            _commands[command.Name] = command;
            return command;
        }

        public ConCommand Register(string name, string description, CommandHandler handler, CommandFlags flags = CommandFlags.None)
            => Register(new ConCommand(name, description, handler, flags));

        public bool TryGetCvar(string name, out Cvar cvar)
        {
            bool found = _cvars.TryGetValue(name, out Cvar? c);
            cvar = c!;
            return found;
        }

        public bool TryGetCommand(string name, out ConCommand command)
        {
            bool found = _commands.TryGetValue(name, out ConCommand? c);
            command = c!;
            return found;
        }

        public void Print(string line) => Output?.Invoke(ConsoleLevel.Info, line);
        public void Warn(string line) => Output?.Invoke(ConsoleLevel.Warning, line);
        public void Error(string line) => Output?.Invoke(ConsoleLevel.Error, line);

        /// <summary>Executes one or more commands separated by ';' or new lines.</summary>
        public void Execute(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            int start = 0;
            bool inQuotes = false;
            for (int i = 0; i <= text.Length; i++)
            {
                bool end = i == text.Length;
                char c = end ? '\0' : text[i];
                if (c == '"') inQuotes = !inQuotes;
                if (end || (!inQuotes && (c == ';' || c == '\n' || c == '\r')))
                {
                    if (i > start) ExecuteSingle(text.Substring(start, i - start));
                    start = i + 1;
                }
            }
        }

        /// <summary>Sets a cvar programmatically, honoring flags. Returns false if refused.</summary>
        public bool TrySet(string name, string value, bool fromHost = false)
        {
            if (!_cvars.TryGetValue(name, out Cvar? cvar)) return false;
            if (!CanWrite(cvar, fromHost, out string reason))
            {
                Warn(reason);
                return false;
            }
            if (!cvar.TrySetFromString(value))
            {
                Warn($"Invalid value '{value}' for {cvar.Name}.");
                return false;
            }
            return true;
        }

        /// <summary>When set, Replicated cvars can only be changed by the host (see TrySet fromHost).</summary>
        public bool IsRemoteClient { get; set; }

        private bool CanWrite(Cvar cvar, bool fromHost, out string reason)
        {
            reason = string.Empty;
            if (fromHost) return true;
            if ((cvar.Flags & CvarFlags.ReadOnly) != 0) { reason = $"{cvar.Name} is read-only."; return false; }
            if ((cvar.Flags & CvarFlags.Cheat) != 0 && !Cheats.Value) { reason = $"{cvar.Name} is cheat-protected (sv_cheats 1)."; return false; }
            if ((cvar.Flags & CvarFlags.Replicated) != 0 && IsRemoteClient) { reason = $"{cvar.Name} is controlled by the host."; return false; }
            return true;
        }

        private void ExecuteSingle(string line)
        {
            Tokenize(line, _tokenBuffer);
            if (_tokenBuffer.Count == 0) return;
            string name = _tokenBuffer[0];

            if (_commands.TryGetValue(name, out ConCommand? command))
            {
                if ((command.Flags & CommandFlags.Cheat) != 0 && !Cheats.Value)
                {
                    Warn($"{command.Name} requires sv_cheats 1.");
                    return;
                }
                // Copy tokens: handlers may execute nested commands that reuse the buffer.
                var args = new CommandArgs(new List<string>(_tokenBuffer), line);
                try
                {
                    command.Handler(args);
                }
                catch (Exception e)
                {
                    Error($"{command.Name}: {e.Message}");
                }
                return;
            }

            if (_cvars.TryGetValue(name, out Cvar? cvar))
            {
                if (_tokenBuffer.Count == 1)
                {
                    Print($"\"{cvar.Name}\" = \"{cvar.ValueString}\" (default \"{cvar.DefaultString}\") - {cvar.Description}");
                    return;
                }
                TrySet(cvar.Name, _tokenBuffer[1]);
                return;
            }

            Warn($"Unknown command \"{name}\".");
        }

        /// <summary>Splits a line into tokens; quotes group words. '//' starts a comment.</summary>
        public static void Tokenize(string line, List<string> tokens)
        {
            tokens.Clear();
            int i = 0;
            while (i < line.Length)
            {
                char c = line[i];
                if (char.IsWhiteSpace(c)) { i++; continue; }
                if (c == '/' && i + 1 < line.Length && line[i + 1] == '/') break;
                if (c == '"')
                {
                    int close = line.IndexOf('"', i + 1);
                    if (close < 0) close = line.Length;
                    tokens.Add(line.Substring(i + 1, close - i - 1));
                    i = close + 1;
                    continue;
                }
                int startTok = i;
                while (i < line.Length && !char.IsWhiteSpace(line[i])) i++;
                tokens.Add(line.Substring(startTok, i - startTok));
            }
        }

        /// <summary>Writes every non-default archived cvar as a config file.</summary>
        public void WriteArchive(TextWriter writer)
        {
            writer.WriteLine("// How to Hunt config. Generated; edit freely, unknown lines are ignored.");
            var sorted = new List<Cvar>(_cvars.Values);
            sorted.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            foreach (Cvar cvar in sorted)
            {
                if ((cvar.Flags & CvarFlags.Archive) == 0) continue;
                writer.WriteLine($"{cvar.Name} \"{cvar.ValueString}\"");
            }
        }

        private void RegisterBuiltins()
        {
            Register("help", "help <name>: describe a cvar or command.", args =>
            {
                if (args.Count == 0) { Print("Usage: help <name>. Try 'find <text>', 'cvarlist', 'cmdlist'."); return; }
                string n = args[0];
                if (_cvars.TryGetValue(n, out Cvar? c)) Print($"{c.Name} = \"{c.ValueString}\" (default \"{c.DefaultString}\") [{c.Flags}] - {c.Description}");
                else if (_commands.TryGetValue(n, out ConCommand? cmd)) Print($"{cmd.Name} - {cmd.Description}");
                else Warn($"No cvar or command named \"{n}\".");
            });
            Register("find", "find <text>: list cvars and commands whose name or description contains text.", args =>
            {
                string q = args.Count > 0 ? args[0] : string.Empty;
                foreach (string line in ListMatching(q)) Print(line);
            });
            Register("cvarlist", "List all cvars.", _ =>
            {
                var names = new List<string>(_cvars.Keys);
                names.Sort(StringComparer.OrdinalIgnoreCase);
                foreach (string n in names) Print($"{n} = \"{_cvars[n].ValueString}\"");
            });
            Register("cmdlist", "List all commands.", _ =>
            {
                var names = new List<string>(_commands.Keys);
                names.Sort(StringComparer.OrdinalIgnoreCase);
                foreach (string n in names) Print($"{n} - {_commands[n].Description}");
            });
            Register("echo", "echo <text>: print text.", args => Print(args.ArgString));
            Register("reset", "reset <cvar>: restore a cvar to its default.", args =>
            {
                if (args.Count == 0) return;
                if (_cvars.TryGetValue(args[0], out Cvar? c) && CanWrite(c, false, out string reason)) c.Reset();
                else Warn($"Cannot reset \"{args[0]}\".");
            });
            Register("toggle", "toggle <cvar> [a b ...]: flip a bool cvar or cycle through values.", args =>
            {
                if (args.Count == 0 || !_cvars.TryGetValue(args[0], out Cvar? c)) { Warn("Usage: toggle <cvar> [values...]"); return; }
                if (args.Count == 1)
                {
                    TrySet(c.Name, c.ValueString == "0" ? "1" : "0");
                    return;
                }
                int next = 1;
                for (int i = 1; i < args.Count; i++)
                    if (args[i] == c.ValueString) { next = i + 1 > args.Count - 1 ? 1 : i + 1; break; }
                TrySet(c.Name, args[next]);
            });
            Register("exec", "exec <file>: run a config file.", args =>
            {
                if (args.Count == 0) { Warn("Usage: exec <file>"); return; }
                if (_execDepth > 8) { Error("exec recursion too deep."); return; }
                string? text = ConfigLoader?.Invoke(args[0]);
                if (text == null) { Warn($"Couldn't exec {args[0]}."); return; }
                _execDepth++;
                try { Execute(text); }
                finally { _execDepth--; }
            });
        }

        public List<string> ListMatching(string query)
        {
            var result = new List<string>();
            foreach (Cvar c in _cvars.Values)
                if (Contains(c.Name, query) || Contains(c.Description, query))
                    result.Add($"{c.Name} = \"{c.ValueString}\" - {c.Description}");
            foreach (ConCommand c in _commands.Values)
                if (Contains(c.Name, query) || Contains(c.Description, query))
                    result.Add($"{c.Name} - {c.Description}");
            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }

        /// <summary>Names starting with prefix, for tab completion.</summary>
        public List<string> Complete(string prefix)
        {
            var result = new List<string>();
            foreach (string n in _cvars.Keys) if (n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) result.Add(n);
            foreach (string n in _commands.Keys) if (n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) result.Add(n);
            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }

        private static bool Contains(string haystack, string needle)
            => needle.Length == 0 || haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
