﻿using Microsoft.CodeAnalysis;

namespace Richiban.Cmdr;

abstract class CommandParameter
{
    private CommandParameter() { }
    public abstract string Name { get; }
    public abstract string OriginalName { get; }
    public abstract string FullyQualifiedTypeName { get; }
    public abstract Option<string> Description { get; }

    public sealed class Positional(
            string name,
            string originalName,
            ITypeSymbol type,
            Option<string> description) : CommandParameter
    {
        public override string Name { get; } = name;
        public ITypeSymbol Type { get; } = type;
        public override string FullyQualifiedTypeName { get; } = type.GetFullyQualifiedName();
        public override Option<string> Description { get; } = description;
        public override string OriginalName { get; } = originalName;
    }

    public sealed class OptionalPositional(
            string name,
            ITypeSymbol type,
            string defaultValue,
            Option<string> description,
            string originalName) : CommandParameter
    {
        public override string Name { get; } = name;
        public override string FullyQualifiedTypeName { get; } = type.GetFullyQualifiedName();
        public override Option<string> Description { get; } = description;
        public override string OriginalName { get; } = originalName;
        public string DefaultValue { get; } = defaultValue;
    }

    public sealed class Flag(
        string name, 
        Option<string> shortForm,
        Option<string> description,
        string originalName) 
        : CommandParameter
    {
        public override string Name { get; } = name;
        public override string FullyQualifiedTypeName { get; } = "System.Boolean";
        public override Option<string> Description { get; } = description;
        public Option<string> ShortForm { get; } = shortForm;
        public override string OriginalName { get; } = originalName;
    }
}
