﻿using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Consolo;

internal record CommandMethod(
        string FullyQualifiedClassName,
        string MethodName,
        IReadOnlyCollection<CommandParameter> Parameters,
        Option<string> Description)
{
    public string FullyQualifiedName { get; } = $"{FullyQualifiedClassName}.{MethodName}";

    public ImmutableArray<CommandParameter.Positional> MandatoryParameters { get; } =
        Parameters
            .OfType<CommandParameter.Positional>()
            .ToImmutableArray();

    public ImmutableArray<CommandParameter.Option> Options { get; } =
        Parameters
            .OfType<CommandParameter.Option>()
            .ToImmutableArray();

    public int MandatoryParameterCount { get; } =
        Parameters
            .OfType<CommandParameter.Positional>()
            .Count();

    public int OptionCount { get; } =
        Parameters
            .OfType<CommandParameter.Option>()
            .Count();
}