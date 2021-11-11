﻿using System;

namespace Richiban.Cmdr.Writers
{
    internal class ReplFileGenerator : CodeFileGenerator
    {
        public override string FileName => "Repl.g.cs";

        public override string GetCode() =>
            $@"using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;

namespace Richiban.Cmdr
{{
    internal class Repl
    {{
        private const int IndentationSize = 4;

        private readonly Dictionary<char, (string helpText, Action<Repl> action)>
            _commands = new();

        private readonly int _level;

        private readonly string _promptText;
        private bool _isEnd;

        public Repl(Command command, string promptText, int level = 0)
        {{
            _promptText = promptText;
            _level = level;

            var valueTuples = command.OfType<Command>()
                .ToDictionary(
                    c => c.Name[index: 0],
                    c =>
                    {{
                        return (helpText: c.Name, action: (Action<Repl>)(r => Act(c, r)));
                    }});

            if (command is RootCommand)
            {{
                valueTuples.Add(key: '/', (helpText: ""Quit"", action: r => r.End()));
            }}
            else
            {{
                valueTuples.Add(key: '/', (helpText: ""Go back"", action: r => r.End()));
            }}

            _commands = valueTuples;
        }}

        private string Indentation => new(c: ' ', _level * IndentationSize);

        private void Act(Command command, Repl repl)
        {{
            if (command.OfType<Command>().Any())
            {{
                var subRepl = new Repl(command, command.Name, _level + 1);
                subRepl.EnterLoop();
            }}
            else
            {{
                var arguments = command.OfType<Argument>()
                    .Select(
                        a =>
                        {{
                            Write($""{{a.Name}}: "");

                            return PromptString(inline: false);
                        }});

                var options = command.OfType<Option>()
                    .SelectMany(
                        o =>
                        {{
                            Write($""{{o.Name}} [y/*]: "");

                            var promptChar = PromptChar();

                            Console.WriteLine();

                            switch (promptChar)
                            {{
                                case 'y': return new[] {{ o.Aliases.First() }};
                                case var _: return Array.Empty<string>();
                            }}
                        }})
                    .ToArray();

                command.Invoke(arguments.Concat(options).ToArray());
            }}
        }}

        public void End()
        {{
            _isEnd = true;
        }}

        public void Write(object toWrite) => Console.Write($""{{Indentation}}{{toWrite}}"");

        public void WriteLine(object toWrite) =>
            Console.WriteLine($""{{Indentation}}{{toWrite}}"");

        public void EnterLoop()
        {{
            while (_isEnd == false)
            {{
                WriteLine(""-------------------"");

                if (_promptText != null)
                {{
                    WriteLine(_promptText);
                }}

                foreach (var (triggerKey, (helpText, action)) in _commands)
                {{
                    WriteLine($""[{{triggerKey}}] {{helpText}}"");
                }}

                Console.WriteLine();

                var input = PromptChar(_commands.Keys.ToHashSet());

                Console.WriteLine();

                _commands[input].action(this);
            }}
        }}

        public static void EnterNewLoop(RootCommand rootCommand, string promptText)
        {{
            var repl = new Repl(rootCommand, promptText);

            repl.EnterLoop();
        }}

        public static bool IsCall(string[] args)
        {{
            return args.Length == 1 && (args[0] == ""--interactive"" || args[0] == ""-i"");
        }}

        private string PromptString(bool inline = false)
        {{
            if  (!inline) return Console.ReadLine();

            List<char> result = new();

            ConsoleKeyInfo lastInput;

            do
            {{
                lastInput = Console.ReadKey(intercept: true);

                if (lastInput.KeyChar is >= 'a' and <= 'z' or >= '0' and <= '9')
                {{
                    result.Add(lastInput.KeyChar);
                    Console.Write(lastInput.KeyChar);
                }}
            }} while (lastInput.Key != ConsoleKey.Enter &&
                     lastInput.Key != ConsoleKey.Tab);

            if (inline == false && lastInput.Key == ConsoleKey.Enter)
            {{
                Console.WriteLine();
            }}

            return new string(result.ToArray());
        }}

        private char PromptChar(ISet<char> allowedCharacters = null)
        {{
            allowedCharacters ??= ""abcdefghijklmnopqrstuvwxyz"".ToHashSet();

            Write(""[ ]"");

            Console.SetCursorPosition(Console.CursorLeft - 2, Console.CursorTop);

            char input;

            do
            {{
                input = Console.ReadKey(intercept: true).KeyChar;
            }} while (!allowedCharacters.Contains(input));

            Console.Write(input);

            return input;
        }}
    }}
}}";
    }
}