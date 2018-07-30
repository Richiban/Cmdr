# Introduction

This project is a command line argument parser for .NET. Its purpose is to be incredibly easy to use.

Most libraries on the subject are just for parsing the arguments to your executable. Something like

```csharp
var options = SomeLibrary.Parse<MyOptions>();
```

The problem with this is twofold: first, you must then write the branching logic yourself to execute whatever logic you want based on what was set on your options object and second, 
this has no support for _verbs_ at the command line (more on that later).

My goal with Richiban.CommandLine was to create a library that felt--as much as possible--as if you could simple call your C# methods directly from the command line. For example, if
I have a method called `ProcessItems` that takes two arguments: an `int batchSize` and a `bool waitBetweenBatches` then I want to write absolutely as little code as I possibly can
apart from:

```csharp
public class Program
{
    public static void Main(string[] args)
    {
        // ... Some magic parsing that I don't care about!
    }

    public void ProcessItems(int batchSize, bool waitBetweenBatches = false)
    {
        // ... Implementation goes here
    }
}
```

I should then be able to call it like this:

```batchfile
myApp.exe 1000 /waitBetweenBatches
```

Well, I feel that this has been achieved with Richiban.CommandLine; this example would be implemented like this:

```csharp
using Richiban.CommandLine;

public class Program
{
    public static void Main(string[] args) =>
        CommandLine.Execute(args);

    [CommandLine]
    public void ProcessItems(int batchSize, bool waitBetweenBatches = false)
    {
        // ... Implementation goes here
    }
}
```

which can, indeed, be called like this:

```batchfile
myApp.exe 1000 /waitBetweenBatches
```

As you can, once I've installed the Richiban.CommandLine NuGet package I've had to write almost no code to make the method callable from the command line.

Multiple methods can be tagged. All that matters is that the command line arguments have unique names‡.

```csharp
public class Program
{
    public static void Main(string[] args) =>
        CommandLine.Execute(args);

    [CommandLine]
    public void ProcessItems(int batchSize, bool waitBetweenBatches = false)
    {
        // ... Implementation goes here
    }

    [CommandLine]
    public void WriteToLogFile(string contents)
    {
        // ... Implementation goes here
    }
}
```

> ‡For more complicated scenarios (i.e. when you have a lot of methods) see the section on _verbs_ below.

## What are some of the features of Richiban.CommandLine?

### It supports Unix-style, Windows-style and Powershell-style argument passing

Unix-style looks like this: 

```sh
someApp --paramA=valueA --flag1
```

Windows-style looks like this: 

```batchfile
someApp.exe /paramA:valueA /flag1
```

Powershell-style looks like this: 

```powershell
someApp -paramA valueA -flag1
```

These are all supported out of the box†, and can even be mixed and matched (although this is not recommended). For example, this is perfectly acceptable:

```
someApp.exe /paramA:valueA --flag1 -paramB:valueB
```

> †Note: On Unix systems (or, more accurately, systems where `/` is the path separator) the Windows-style parameter name parsing is disabled; this is due to the ambiguity with path
> arguments

### The order of supplied arguments doesn't matter (if names are given)

```sh
myApp --paramA=valueA --paramB=valueB
```
and
```sh
myApp --paramB=valueB --paramA=valueA
```
are considered equal, because the names have been given; i.e. the order is not important because it's unambiguous. Note that if you don't give the names:
```sh
myApp valueB valueA
```
then it still works, but the order is now important.

### Verbs are supported

We're getting into more advanced terratory now. Let's look at an example from the command line reference for Git:

```sh
git remote add origin http://example.com/project.git
```

In the example above, the tokens `remote` and `add` are _verbs_, and `origin` and `http://example.com/project.git` are the arguments. Richiban.CommandLine supports verbs! Since, technically, only the last of these tokens is really a verb we call them _routes_. This example with two route parts and two arguments would look like this:

```csharp
[CommandLine, Route("remote", "add")]
public void AddRemote(string remoteName, Uri remoteUri)
{
    // ... Implementation goes here
}
```

> Note that here we have an argument of type `Uri`. See [Argument types are converted].

### Argument types are converted 

Note that arguments do not have to be strings.

The rule is that arguments must either:
 * Be of type `string`
 * implement `IConvertible` (the conversion is done by `Convert.ChangeType(...)`)
 * have a constructor that takes a string as argument
 * be of an Enum type (then the conversion is done by `Enum.Parse(...)`)

Some example types that work out of the box:
* string
* bool
* int
* Uri
* FileInfo
and many more

### Compatible with dependency injection frameworks

It would be pretty ugly if your methods were nicely reachable from the command line, but they all looked like this:

```csharp
public class SomeClass
{
    [CommandLine, Route("method1")]
    public void Method1(string argument)
    {
        var serviceA = ObjectContainer.Instance.Resolve<ServiceA>();
        var serviceB = ObjectContainer.Instance.Resolve<ServiceB>();
        var serviceC = ObjectContainer.Instance.Resolve<ServiceC>();

        // ... Implementation goes here
    }
}
```

Wouldn't it be much better if proper dependency injection like this was possible instead?

```csharp
public class SomeClass
{
    private readonly ServiceA _serviceA;
    private readonly ServiceB _serviceB;
    private readonly ServiceC _serviceC;

    public SomeClass(ServiceA serviceA, ServiceB serviceB, ServiceC serviceC) =>
        (_serviceA, _serviceB, _serviceC) = (serviceA, serviceB, serviceC);

    [CommandLine, Route("method1")]
    public void Method1(string argument)
    {
        var serviceA = ObjectContainer.Instance.Resolve<ServiceA>();
        var serviceB = ObjectContainer.Instance.Resolve<ServiceB>();
        var serviceC = ObjectContainer.Instance.Resolve<ServiceC>();

        // ... Implementation goes here
    }
}
```

Well, it is easy with Richiban.CommandLine! First, we note that there is an overload of `CommandLine.Execute` that takes a `CommandLineConfiguration` object. Let's look at the
`CommandLineConfiguration` type:

```csharp
    public class CommandLineConfiguration
    {
        public Action<string> HelpOutput { get; set; }

        public Func<Type, object> ObjectFactory { get; set; }

        public Assembly AssemblyToScan { get; set; }

        public static CommandLineConfiguration GetDefault() => ...
    }
```

Of interest is the `ObjectFactory` property. It's a `Func<Type, object>`, which is the most generic possible definition of a factory (it's a function that takes a `Type` as argument and 
returns an `object`). The `ObjectFactory` property has a setter, so we can provide whatever implementation we want. Since it's a `Func` we don't even have to implement an interface, we
can simply configure like this:

```csharp
public static void Main(string[] args)
{
    // Instantiate our favourite DI container. CastleWindsor is only an example; it could be anything.
    var container = new WindsorContainer();

    var config = CommandLineConfiguration.GetDefault();
    config.ObjectFactory = container.Resolve;

    CommandLine.Execute(config, args);
}
```

-------
That's about it for the readme. Please feel free to read the issues in this project to see what's coming further down the road or, if you dream up more features for Richiban.CommandLine, post an issue of your own. I also welcome (expected) PRs so contact me before starting any work.
