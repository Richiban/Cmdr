﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Richiban.Cmdr.Models;

internal class MethodModel
{
    public MethodModel(
        string methodName,
        string? providedName,
        IReadOnlyList<CommandPathItem> groupCommandPath,
        string fullyQualifiedClassName,
        IReadOnlyCollection<ArgumentModel> arguments,
        string? description)
    {
        MethodName = methodName;
        ProvidedName = providedName;
        GroupCommandPath = groupCommandPath;
        Arguments = arguments;
        FullyQualifiedClassName = fullyQualifiedClassName;
        Description = description;
    }

    public string FullyQualifiedClassName { get; }
    public string? Description { get; }
    public string MethodName { get; }
    public string? ProvidedName { get; }
    public IReadOnlyList<CommandPathItem> GroupCommandPath { get; }
    public IReadOnlyCollection<ArgumentModel> Arguments { get; }
}

public record CommandPathItem(string Name, string? Description);