using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SharpBridge;

public static class Cmds
{
    private static readonly Dictionary<string, Cmd> All = new();
    private static readonly Dictionary<Type, Cmd> AllByType = new();

    public static void Init()
    {
        foreach (var type in typeof(Cmd).Assembly.GetTypes())
        {
            if (!typeof(Cmd).IsAssignableFrom(type) || type.IsAbstract)
                continue;

            var cmd = (Cmd)Activator.CreateInstance(type)!;
            All[cmd.ID.ToLowerInvariant()] = cmd;
            AllByType[type] = cmd;
        }
    }

    public static Cmd? Get(string id) => All.TryGetValue(id, out var cmd) ? cmd : null;

    public static T? Get<T>() where T : Cmd => AllByType.TryGetValue(typeof(T), out var cmd) ? (T)cmd : null;
}

public abstract class Cmd
{
    public virtual string ID => GetType().Name[3..];
    public abstract Type? InputType { get; }
    public abstract Type OutputType { get; }
    public virtual bool LogRun => true;
    public virtual bool Taskable => false;
    public abstract object? Run(object input);

    public static object[] Status(string text, float progress, string shape, bool update)
    {
        Console.Error.WriteLine(text);
        return StatusSilent(text, progress, shape, update);
    }

    public static object[] Status(string text, bool progress, string shape, bool update)
    {
        Console.Error.WriteLine(text);
        return StatusSilent(text, progress, shape, update);
    }

    public static object[] StatusSilent(string text, float progress, string shape, bool update)
    {
        if (update) CmdTask.Update++;
        return new object[] { text, progress, shape, update };
    }


    private static object[] StatusSilent(string text, bool progress, string shape, bool update)
    {
        if (update) CmdTask.Update++;
        return new object[] { text, progress, shape, update };
    }
}

public abstract class AsyncCmd : Cmd
{
    public abstract Task<object?> RunAsync(object input);

    public override object? Run(object input)
    {
        return RunAsync(input).GetAwaiter().GetResult();
    }
}

public abstract class Cmd<TOutput> : Cmd
{
    public override Type? InputType => null;
    public override Type OutputType => typeof(TOutput);

    public override object? Run(object input)
    {
        return Run();
    }

    public abstract TOutput Run();
}

public abstract class Cmd<TIn, TOut> : Cmd
{
    public override Type InputType => typeof(Tuple<TIn>);
    public override Type OutputType => typeof(TOut);

    public override object? Run(object input)
    {
        return Run(((Tuple<TIn>)input).Item1);
    }

    public abstract TOut Run(TIn input);
}

public abstract class Cmd<TIn1, TIn2, TOut> : Cmd
{
    public override Type InputType => typeof(Tuple<TIn1, TIn2>);
    public override Type OutputType => typeof(TOut);

    public override object? Run(object input)
    {
        var (i1, i2) = (Tuple<TIn1, TIn2>)input;
        return Run(i1, i2);
    }

    public abstract TOut Run(TIn1 input1, TIn2 input2);
}

public abstract class Cmd<TIn1, TIn2, TIn3, TOut> : Cmd
{
    public override Type InputType => typeof(Tuple<TIn1, TIn2, TIn3>);
    public override Type OutputType => typeof(TOut);

    public override object? Run(object input)
    {
        var (i1, i2, i3) = (Tuple<TIn1, TIn2, TIn3>)input;
        return Run(i1, i2, i3);
    }

    public abstract TOut Run(TIn1 input1, TIn2 input2, TIn3 input3);
}

public abstract class Cmd<TIn1, TIn2, TIn3, TIn4, TOut> : Cmd
{
    public override Type InputType => typeof(Tuple<TIn1, TIn2, TIn3, TIn4>);
    public override Type OutputType => typeof(TOut);

    public override object? Run(object input)
    {
        var (i1, i2, i3, i4) = (Tuple<TIn1, TIn2, TIn3, TIn4>)input;
        return Run(i1, i2, i3, i4);
    }

    public abstract TOut Run(TIn1 input1, TIn2 input2, TIn3 input3, TIn4 input4);
}

public abstract class Cmd<TIn1, TIn2, TIn3, TIn4, TIn5, TOut> : Cmd
{
    public override Type InputType => typeof(Tuple<TIn1, TIn2, TIn3, TIn4, TIn5>);
    public override Type OutputType => typeof(TOut);

    public override object? Run(object input)
    {
        var (i1, i2, i3, i4, i5) = (Tuple<TIn1, TIn2, TIn3, TIn4, TIn5>)input;
        return Run(i1, i2, i3, i4, i5);
    }

    public abstract TOut Run(TIn1 input1, TIn2 input2, TIn3 input3, TIn4 input4, TIn5 input5);
}

public abstract class Cmd<TIn1, TIn2, TIn3, TIn4, TIn5, TIn6, TOut> : Cmd
{
    public override Type InputType => typeof(Tuple<TIn1, TIn2, TIn3, TIn4, TIn5, TIn6>);
    public override Type OutputType => typeof(TOut);

    public override object? Run(object input)
    {
        var (i1, i2, i3, i4, i5, i6) = (Tuple<TIn1, TIn2, TIn3, TIn4, TIn5, TIn6>)input;
        return Run(i1, i2, i3, i4, i5, i6);
    }

    public abstract TOut Run(TIn1 input1, TIn2 input2, TIn3 input3, TIn4 input4, TIn5 input5, TIn6 input6);
}

public abstract class Cmd<TIn1, TIn2, TIn3, TIn4, TIn5, TIn6, TIn7, TOut> : Cmd
{
    public override Type InputType => typeof(Tuple<TIn1, TIn2, TIn3, TIn4, TIn5, TIn6, TIn7>);
    public override Type OutputType => typeof(TOut);

    public override object? Run(object input)
    {
        var (i1, i2, i3, i4, i5, i6, i7) = (Tuple<TIn1, TIn2, TIn3, TIn4, TIn5, TIn6, TIn7>)input;
        return Run(i1, i2, i3, i4, i5, i6, i7);
    }

    public abstract TOut Run(TIn1 input1, TIn2 input2, TIn3 input3, TIn4 input4, TIn5 input5, TIn6 input6, TIn7 input7);
}

public abstract class Cmd<TIn1, TIn2, TIn3, TIn4, TIn5, TIn6, TIn7, TIn8, TOut> : Cmd
{
    public override Type InputType => typeof(Tuple<TIn1, TIn2, TIn3, TIn4, TIn5, TIn6, TIn7, Tuple<TIn8>>);
    public override Type OutputType => typeof(TOut);

    public override object? Run(object input)
    {
        var (i1, i2, i3, i4, i5, i6, i7, i8) = (Tuple<TIn1, TIn2, TIn3, TIn4, TIn5, TIn6, TIn7, Tuple<TIn8>>)input;
        return Run(i1, i2, i3, i4, i5, i6, i7, i8);
    }

    public abstract TOut Run(TIn1 input1, TIn2 input2, TIn3 input3, TIn4 input4, TIn5 input5, TIn6 input6, TIn7 input7,
        TIn8 input8);
}