using Microsoft.CodeAnalysis;
using static Consolo.CommandTree;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Consolo;

internal class ProgramClassFileGenerator(
    string assemblyName,
    Root rootCommand) : CodeFileGenerator
{
    private readonly CodeBuilder _codeBuilder = new CodeBuilder();
    public override string FileName => "Program.g.cs";

    private void WriteCommandDebug(CommandTree command)
    {
        var commandName = command is SubCommand sub ? sub.CommandName : "{root}";
        _codeBuilder.AppendLines($"// {commandName} command{(command.Method.IsSome(out _) ? "*" : "")}");

        foreach (var c in command.SubCommands)
        {
            using (_codeBuilder.Indent())
                WriteCommandDebug(c);
        }
    }

    public override string GetCode()
    {
        _codeBuilder.AppendLines(
            "using System;",
            "using System.Linq;",
            "using System.Collections.Generic;",
            "using System.Collections.Immutable;");

        _codeBuilder.AppendLine();

        _codeBuilder.AppendLine("var consoleColor = Console.ForegroundColor;");
        _codeBuilder.AppendLine("var helpTextColor = ConsoleColor.Green;");

        _codeBuilder.AppendLine();
        _codeBuilder.AppendLine("// Commands marked * have an associated method");
        WriteCommandDebug(rootCommand);
        _codeBuilder.AppendLine();

        _codeBuilder.AppendLine("var isHelp = args.Intersect([\"--help\", \"-h\", \"-?\"]).Any();");
        _codeBuilder.AppendLine();

        WriteCommandGroup(rootCommand, []);

        _codeBuilder.AppendLine();
        WriteHelperMethods();

        return _codeBuilder.ToString();
    }

    private void WriteCommandGroup(CommandTree command, ImmutableArray<string> path)
    {
        if (command is SubCommand s)
        {
            _codeBuilder.AppendLines(
                $"if (args.Length >= {path.Length} && args[{path.Length - 1}] == \"{s.CommandName}\")");
        }

        using (_codeBuilder.IndentBraces())
        {
            foreach (var c in command.SubCommands)
            {
                WriteCommandGroup(c, path.Add(c.CommandName));
            }

            if (command.Method.IsSome(out var method))
            {
                WriteCommandHandlerBody(command, path, method);
            }

            _codeBuilder.AppendLine("");
            WriteHelp(assemblyName, path, command);
            _codeBuilder.AppendLine("return;");
        }
    }

    private void WriteCommandHandlerBody(CommandTree command, ImmutableArray<string> path, CommandMethod method)
    {
        var positionalCount = path.Length + method.MandatoryParameterCount;

        _codeBuilder.AppendLines("if (!isHelp)");

        using (_codeBuilder.IndentBraces())
        {
            _codeBuilder.AppendLine("var processedArgs = new bool[args.Length];");

            for (var pathIndex = 0; pathIndex < path.Length; pathIndex++)
            {
                _codeBuilder.AppendLine($"processedArgs[{pathIndex}] = true;");
            }

            _codeBuilder.AppendLine();

            WriteParameterAssignments(method);

            _codeBuilder.AppendLines($"if (processedArgs.Any(x => !x))");

            using (_codeBuilder.IndentBraces())
            {
                _codeBuilder.AppendLine("Console.WriteLine(\"Unrecognised args: \" + string.Join(\", \", args.Where((x, i) => !processedArgs[i])));");
            }

            _codeBuilder.AppendLines("else");

            using (_codeBuilder.IndentBraces())
            {
                var argString = String.Join(", ", method.Parameters.Select(x => x.SourceName));

                _codeBuilder.AppendLine($"{method.FullyQualifiedName}({argString});");
                _codeBuilder.AppendLine("return;");
            }
        }
    }

    private void WriteParameterAssignments(CommandMethod method)
    {
        foreach (var p in
            method.Parameters.OrderBy(p =>
                p switch
                {
                    CommandParameter.Option { IsFlag: true } => 0,
                    CommandParameter.Option => 1,
                    _ => 2
                }
                ))
        {
            _codeBuilder.AppendLine($"var {p.SourceName} = default({p.FullyQualifiedTypeName});");


            switch (p)
            {
                case CommandParameter.Option { IsFlag: true } flag:
                    {
                        _codeBuilder.AppendLine(
                            $"MatchNextFlag([\"--{flag.Name}\"{(flag.ShortForm.IsSome(out var shortForm) ? $", \"-{shortForm}\"" : "")}], ref {flag.SourceName}, processedArgs);");
                        break;
                    }
                case CommandParameter.Option option:
                    {
                        _codeBuilder.AppendLine(
                            $"if (MatchNextOption([\"--{option.Name}\"{(option.ShortForm.IsSome(out var shortForm) ? $", \"-{shortForm}\"" : "")}], ref {option.SourceName}, processedArgs, s => {ConvertParameter(option.Type, "s")}) == 2)");
                        using (_codeBuilder.IndentBraces())
                        {
                            WriteError($"Missing value for option '--{option.Name}'");
                            _codeBuilder.AppendLine("return;");
                        }
                        break;
                    }
                case var positional:
                    {
                        _codeBuilder.AppendLine(
                    $"if (!MatchNextPositional(ref {positional.SourceName}, processedArgs, s => {ConvertParameter(positional.Type, "s")}))");
                        using (_codeBuilder.IndentBraces())
                        {
                            WriteError($"Missing value for argument '{positional.SourceName}'");
                            _codeBuilder.AppendLine("return;");
                        }
                        break;
                    }
            }

            _codeBuilder.AppendLine();
        }
    }

    private string ConvertParameter(ParameterType type, string expression)
    {
        return type switch
        {
            ParameterType.AsIs => expression,
            ParameterType.Parse p => $"{p.FullyQualifiedTypeName}.{p.ParseMethodName}({expression})",
            ParameterType.Constructor c => $"new {c.FullyQualifiedTypeName}({expression})",
            ParameterType.ExplicitCast c => $"({c.FullyQualifiedTypeName})({expression})",
            ParameterType.Enum e => $"({e.FullyQualifiedTypeName})Enum.Parse(typeof({e.FullyQualifiedTypeName}), {expression}, ignoreCase: true)",
            _ => throw new NotSupportedException("Unsupported parameter type: " + type.GetType().Name)
        };
    }

    private string GetHelpTextInline(CommandParameter parameter)
    {
        return parameter switch
        {
            CommandParameter.Option p when IsFlag(p) && p.ShortForm.IsSome(out var shortForm) => $"[-{shortForm} | --{p.Name}]",
            CommandParameter.Option p when IsFlag(p) => $"[<{p.Name}>]",
            CommandParameter.Option p when p.ShortForm.IsSome(out var shortForm) => $"[-{shortForm} | --{p.Name} <{p.SourceName}>]",
            CommandParameter.Option p => $"[--{p.Name}]",
            var p => $"<{p.Name}>",
        };

        bool IsFlag(CommandParameter p) => p switch
        {
            CommandParameter.Option { Type: ParameterType.Bool } => true,
            _ => false
        };
    }

    private string GetSoloHelpFirstColumn(CommandParameter.Positional parameter)
    {
        return parameter switch
        {
            { Type: ParameterType.Enum e } =>
                $"<{String.Join("|", e.EnumValues)}>",
            var p => $"{p.Name}",
        };
    }

    private string GetSoloHelpFirstColumn(CommandParameter.Option parameter)
    {
        var parameterName = parameter.ShortForm.IsSome(out var shortForm)
            ? $"-{shortForm} | --{parameter.Name}"
            : $"--{parameter.Name}";

        return parameter switch
        {
            { Type: ParameterType.Enum e } =>
                $"{parameterName}={String.Join("|", e.EnumValues)}",
            { Type: ParameterType.Bool } =>
                $"{parameterName}",
            _ =>
                $"{parameterName}=<{parameter.SourceName}>",
        };
    }

    private void WriteError(string errorMessage)
    {
        _codeBuilder.AppendLine($"Console.ForegroundColor = ConsoleColor.Red;");
        _codeBuilder.AppendLine($"Console.Error.WriteLine($\"{errorMessage}\");");
        _codeBuilder.AppendLine($"Console.ForegroundColor = consoleColor;");
    }

    private void WriteHelperMethods()
    {
        _codeBuilder.AppendLine(
            """
            bool MatchNextPositional<T>(ref T value, bool[] processedArgs, Func<string, T> mapper)
            {
                var i = 0;
                foreach (var arg in args)
                {
                    if (!processedArgs[i] && !arg.StartsWith("-"))
                    {
                        value = mapper(arg);
                        processedArgs[i] = true;
                        return true;
                    }

                    i++;
                }

                return false;
            }

            bool MatchNextFlag(ImmutableList<string> optionNames, ref bool value, bool[] processedArgs)
            {
                var i = 0;
                foreach (var arg in args)
                {
                    if (!processedArgs[i] && optionNames.Contains(arg))
                    {
                        value = true;
                        processedArgs[i] = true;
                        return true;
                    }

                    i++;
                }

                return false;
            }

            // Returns 0 if the option is not found
            // Returns 1 if the option is found and the value is found
            // Returns 2 if the option is found but the value is missing
            int MatchNextOption<T>(ImmutableList<string> optionNames, ref T value, bool[] processedArgs, Func<string, T> mapper)
            {
                var i = 0;

                foreach (var arg in args)
                {
                    if (!processedArgs[i] && optionNames.Contains(arg))
                    {
                        if (i + 1 < args.Length && !processedArgs[i + 1] && !args[i + 1].StartsWith("-"))
                        {
                            value = mapper(args[i + 1]);
                            processedArgs[i] = true;
                            processedArgs[i + 1] = true;
                            return 1;
                        }
                        else
                        {
                            return 2;
                        }
                    }

                    i++;
                }

                return 0;
            }
            """);
    }

    private void WriteHelp(
        string assemblyName,
        ImmutableArray<string> path,
        CommandTree command)
    {
        var pathStrings = path.Select(x => $"\"{x}\"");

        if (command.Method.IsSome(out var method))
        {
            var allHelpText = path.Concat(
                method.MandatoryParameters.Select(GetHelpTextInline)
            );

            _codeBuilder.AppendLine(
                $"Console.WriteLine("
            );

            using (_codeBuilder.Indent())
            {
                _codeBuilder.AppendLines(
                    "\"\"\"",
                    $"{assemblyName}"
                );

                if (command is SubCommand s)
                {
                    _codeBuilder.AppendLines(
                        $"",
                        $"{s.CommandName}"
                    );
                }

                _codeBuilder.AppendLines(
                    "\"\"\""
                );
            }

            _codeBuilder.AppendLines(
                $");"
            );

            if (command.Description.HasValue)
            {
                _codeBuilder.AppendLine("Console.ForegroundColor = helpTextColor;");

                _codeBuilder.AppendLines(
                    $"Console.WriteLine(",
                    $"    \"\"\"",
                    $"        {command.Description.Trim()}",
                    "",
                    $"    \"\"\"",
                    ");"
                );

                _codeBuilder.AppendLine("Console.ForegroundColor = consoleColor;");
            }

            _codeBuilder.AppendLines(
                $"Console.WriteLine(",
                "    \"\"\"",
                $"    Usage:",
                $"        {assemblyName} {String.Join(" ", allHelpText)} [options]",
                "    \"\"\"",
                ");");

            if (method.MandatoryParameters.Any())
            {
                _codeBuilder.AppendLines(
                    $"Console.WriteLine(",
                    "    \"\"\"",
                    "",
                    $"    Parameters:",
                    "    \"\"\"",
                    ");"
                );

                var helpNames =
                    method.MandatoryParameters.Select(p => (GetSoloHelpFirstColumn(p), p.Description));

                var longestParameter = helpNames.MaxOrDefault(x => x.Item1.Length);

                foreach (var (helpName, description) in helpNames)
                {
                    _codeBuilder.AppendLines(
                        $"Console.Write(\"    {helpName.PadRight(longestParameter)}  \");",
                        "Console.ForegroundColor = helpTextColor;",
                        $"Console.WriteLine(\"{description}\");"
                    );

                    _codeBuilder.AppendLine("Console.ForegroundColor = consoleColor;");
                }
            }

            if (method.Options.Any())
            {
                _codeBuilder.AppendLines(
                    $"Console.WriteLine(",
                    "    \"\"\"",
                    "",
                    $"    Options:",
                    "    \"\"\"",
                    ");"
                );

                var helpNames = method.Options
                    .Select(p => (GetSoloHelpFirstColumn(p), p.Description))
                    .Append(("-h | --help", "Show help and usage information"));

                var longestParameter = helpNames.MaxOrDefault(x => x.Item1.Length);

                foreach (var (helpName, description) in helpNames)
                {
                    _codeBuilder.AppendLines(
                        $"Console.Write(\"    {helpName.PadRight(longestParameter)}  \");",
                        "Console.ForegroundColor = helpTextColor;",
                        $"Console.WriteLine(\"{description}\");"
                    );

                    _codeBuilder.AppendLine("Console.ForegroundColor = consoleColor;");
                }

                _codeBuilder.AppendLine("Console.WriteLine();");
            }
        }
        else
        {
            WriteSubCommandHelpTextInline(command);
        }
    }

    private void WriteSubCommandHelpTextInline(CommandTree command)
    {
        _codeBuilder.AppendLines(
            $"Console.WriteLine(\"{assemblyName}\");",
            "Console.WriteLine(\"\");",
            "Console.WriteLine(\"Commands:\");"
        );

        var firstColumnLength = command.SubCommands.Select(GetFirstColumn).DefaultIfEmpty("").Max(x => x.Length);

        foreach (var subCommand in command.SubCommands)
        {
            var firstColumn = GetFirstColumn(subCommand).PadRight(firstColumnLength);
            _codeBuilder.AppendLine(
                $"Console.Write(\"    {firstColumn}\");"
            );

            if (subCommand.Description.IsSome(out var description))
            {
                _codeBuilder.AppendLine("Console.ForegroundColor = helpTextColor;");
                _codeBuilder.AppendLine($"Console.WriteLine(\"  {description.Trim()}\");");
                _codeBuilder.AppendLine("Console.ForegroundColor = consoleColor;");
            }
            else
            {
                _codeBuilder.AppendLine("Console.WriteLine();");
            }
        }

        string GetFirstColumn(CommandTree c)
        {
            return c switch
            {
                SubCommand s when s.Method.IsSome(out var m) => $"{s.CommandName} {m.Parameters.Select(GetHelpTextInline).StringJoin(" ")}",
                Root r when r.Method.IsSome(out var m) => $"{m.Parameters.Select(GetHelpTextInline).StringJoin(" ")}",
                SubCommand s => s.CommandName,
                _ => ""
            };
        }
    }
}
