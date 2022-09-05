// See https://aka.ms/new-console-template for more information

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace SharpBridge;

public static class Program
{
    public static readonly string? RootDirectory = Path.GetDirectoryName(Environment.CurrentDirectory);
    public static string? _configDirectory = Environment.GetEnvironmentVariable("SHARP_CONFIG");

    public static readonly Encoding UTF8NoBOM = new UTF8Encoding(false);

    public static readonly Dictionary<string, Message> Cache = new();

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception ?? new Exception("Unknown unhandled exception");
        Console.Error.WriteLine(e.IsTerminating ? "FATAL ERROR" : "UNHANDLED ERROR");
        Console.Error.WriteLine(ex.ToString());
    }

    private static void WriteLoop(Process? parentProc, MessageContext ctx, Stream stream, bool verbose,
        char delimiter = '\0')
    {
        for (Message? msg; !(parentProc?.HasExited ?? false) && (msg = ctx.WaitForNext()) != null;)
        {
            if (msg.Value is IEnumerator enumerator)
            {
                Console.Error.WriteLine($"[sharp] New CmdTask: {msg.UID}");
                CmdTasks.Add(new CmdTask(msg.UID, enumerator));
                msg.Value = msg.UID;
            }

            var data = msg.Value as byte[];
            if (data != null)
            {
                msg.RawSize = data.Length;
                msg.Value = null;
            }

            JsonSerializer.Serialize(stream, msg, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                IncludeFields = true
            });
            stream.WriteByte((byte) delimiter);
            stream.WriteByte((byte) '\n');
            stream.Flush();

            if (data == null) continue;

            stream.Write(data, 0, data.Length);
            stream.Flush();
        }

    }

    public static void ReadLoop(Process? parentProc, MessageContext ctx, StreamReader reader, bool verbose, char delimiter = '\0')
    {
        while (!(parentProc?.HasExited ?? false))
        {
            // Commands from the host process should come in pairs of two objects:

            if (verbose)
                Console.Error.WriteLine("[sharp] Awaiting next command");

            // Unique ID
            var uid = JsonSerializer.Deserialize<string>(reader.ReadTerminatedString(delimiter))!;
            if (verbose)
                Console.Error.WriteLine($"[sharp] Receiving command {uid}");

            // Command ID
            var cid = JsonSerializer.Deserialize<string>(reader.ReadTerminatedString(delimiter))!.ToLowerInvariant();
            switch (cid)
            {
                case "_ack":
                    {
                        reader.ReadTerminatedString(delimiter);
                        if (verbose)
                            Console.Error.WriteLine($"[sharp] Ack'd command {uid}");
                        lock (Cache)
                        {
                            if (Cache.ContainsKey(uid))
                                Cache.Remove(uid);
                            else
                                Console.Error.WriteLine($"[sharp] Ack'd command that was already ack'd {uid}");
                        }
                        continue;
                    }
                case "_stop":
                    // Let's just hope everyone knows how to handle this. Truly, wishful thinking.
                    Environment.Exit(0);
                    continue;
            }

            var msg = new Message(cid, uid);

            var cmd = Cmds.Get(cid);
            if (cmd == null)
            {
                reader.ReadTerminatedString(delimiter);
                Console.Error.WriteLine($"[sharp] Unknown command {cid}");
                msg.Error = "cmd failed running: not found: " + cid;
                ctx.Reply(msg);
                continue;
            }

            if (verbose)
                Console.Error.WriteLine($"[sharp] Parsing args for {cid}");

            // Payload
            var input = JsonSerializer.Deserialize(reader.ReadTerminatedString(delimiter), cmd.InputType!)!;
            object output;
            try
            {
                if (verbose || cmd.LogRun)
                    Console.Error.WriteLine($"[sharp] Executing {cid}");

                if (cmd is AsyncCmd ac)
                    output = Task.Run(() => ac.RunAsync(input));
                else if (cmd.Taskable)
                    output = Task.Run(() => cmd.Run(input));
                else
                    output = cmd.Run(input)!;
            } catch (Exception e)
            {
                Console.Error.WriteLine($"[sharp] Failed running {cid}: {e}");
                msg.Error = "cmd failed running: " + e;
                ctx.Reply(msg);
                continue;
            }

            if (output is Task<object> task)
            {
                task.ContinueWith(t =>
                {
                    if (task.Exception != null)
                    {
                        Exception e = task.Exception;
                        Console.Error.WriteLine($"[sharp] Failed running task {cid}: {e}");
                        msg.Error = "cmd task failed running: " + e;
                        ctx.Reply(e);
                        return;
                    }

                    msg.Value = t.Result;
                    lock (Cache)
                        Cache[uid] = msg;
                    ctx.Reply(msg);
                });
            }
            else
            {
                msg.Value = output;
                lock (Cache)
                    Cache[uid] = msg;
                ctx.Reply(msg);
            }
        }
    }

    [DllImport("kernel32")]
    private static extern bool AllocConsole();

    private static string ReadTerminatedString(this TextReader reader, char delimiter)
    {
        var sb = new StringBuilder();
        char c;
        while ((c = (char)reader.Read()) != delimiter)
        {
            if (c < 0) continue;

            sb.Append(c);
        }

        return sb.ToString();
    }

    public class MessageContext : IDisposable
    {
        private readonly ManualResetEvent _Event = new(false);
        private readonly WaitHandle[] _EventWaitHandles;

        private readonly ConcurrentQueue<Message> _Queue = new();

        private bool _Disposed;

        public MessageContext()
        {
            _EventWaitHandles = new WaitHandle[] { _Event };
        }

        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;

            _Event.Set();
            _Event.Dispose();
        }


        public void Reply(Message msg)
        {
            _Queue.Enqueue(msg);

            try
            {
                _Event.Set();
            }
            catch
            {
                // ignored
            }
        }

        public Message? WaitForNext()
        {
            if (_Queue.TryDequeue(out var msg)) return msg;

            while (!_Disposed)
            {
                try
                {
                    WaitHandle.WaitAny(_EventWaitHandles, 2000);
                }
                catch
                {
                    // ignored
                }

                if (!_Queue.TryDequeue(out msg)) continue;
                if (_Queue.Count != 0) return msg;

                try
                {
                    _Event.Reset();
                }
                catch
                {
                    // ignored
                }

                return msg;
            }

            return null;
        }
    }

    public class Message
    {
        public readonly string UID;
        [NonSerialized] public string CID;
        public string? Error;
        public long? RawSize;
        public object? Value;

        public Message(string cid, string uid)
        {
            CID = cid;
            UID = uid;
        }
    }
}