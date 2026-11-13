// CLR MicroVM Emulator - C# Console Edition
// Reads a .asm/.il file, executes the supported CIL instructions,
// and displays the VM state in the console.
//
// Usage:
//   dotnet run -- program.asm
//   dotnet run -- program.asm output.txt
//
// If no input file is supplied, the built-in Fibonacci sample is used.
// The optional output file receives the VM console output (not the debugger UI).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

internal sealed class Instruction
{
    public string Opcode { get; init; } = "";
    public List<string> Args { get; init; } = new();
    public int SourceLine { get; init; }
    public string SourceText { get; init; } = "";
}

internal sealed class LocalValue
{
    public string Name { get; set; } = "";
    public object Value { get; set; } = 0;
}

internal sealed class DotNetVM
{
    public List<Instruction> Instructions { get; private set; } = new();
    public Dictionary<string, int> Labels { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<object> Stack { get; private set; } = new();
    public SortedDictionary<int, LocalValue> Locals { get; private set; } = new();
    public int PC { get; private set; }
    public int Cycles { get; private set; }
    public bool Halted { get; private set; }
    public bool HasError { get; private set; }
    private int entryPointIndex;

    public void Reset()
    {
        Instructions = new List<Instruction>();
        Labels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        Stack = new List<object>();
        Locals = new SortedDictionary<int, LocalValue>();
        PC = 0;
        Cycles = 0;
        Halted = false;
        HasError = false;
        entryPointIndex = 0;
    }

    public void Assemble(string source)
    {
        Reset();
        string[] lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        List<Instruction> parsed = new();

        for (int i = 0; i < lines.Length; i++)
        {
            string rawLine = lines[i];
            string line = StripComment(rawLine).Trim();
            if (line.Length == 0) continue;

            Match labelMatch = Regex.Match(line, @"^([A-Za-z0-9_]+):(.*)$");
            if (labelMatch.Success)
            {
                Labels[labelMatch.Groups[1].Value] = parsed.Count;
                line = labelMatch.Groups[2].Value.Trim();
                if (line.Length == 0) continue;
            }

            if (line.StartsWith(".", StringComparison.Ordinal))
            {
                if (line.StartsWith(".entrypoint", StringComparison.OrdinalIgnoreCase))
                    entryPointIndex = parsed.Count;
                else if (line.StartsWith(".locals", StringComparison.OrdinalIgnoreCase))
                {
                    MatchCollection matches = Regex.Matches(line,
                        @"\[(\d+)\]\s+([A-Za-z0-9_]+)\s+([A-Za-z0-9_]+)");
                    foreach (Match m in matches)
                    {
                        int index = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                        Locals[index] = new LocalValue { Name = m.Groups[3].Value, Value = 0 };
                    }
                }
                continue;
            }

            if (line == "{" || line == "}" ||
                line.StartsWith(".assembly", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith(".method", StringComparison.OrdinalIgnoreCase))
                continue;

            List<string> tokens = Tokenize(line);
            if (tokens.Count > 0)
            {
                parsed.Add(new Instruction
                {
                    Opcode = tokens[0].ToLowerInvariant(),
                    Args = tokens.Skip(1).ToList(),
                    SourceLine = i + 1,
                    SourceText = rawLine.Trim()
                });
            }
        }

        Instructions = parsed;
        PC = entryPointIndex;
    }

    private static string StripComment(string line)
    {
        bool quoted = false;
        for (int i = 0; i < line.Length - 1; i++)
        {
            if (line[i] == '"') quoted = !quoted;
            if (!quoted && line[i] == '/' && line[i + 1] == '/')
                return line.Substring(0, i);
        }
        return line;
    }

    private static List<string> Tokenize(string text)
    {
        List<string> result = new();
        StringBuilder current = new();
        bool quoted = false;
        foreach (char ch in text)
        {
            if (ch == '"')
            {
                quoted = !quoted;
                current.Append(ch);
            }
            else if (char.IsWhiteSpace(ch) && !quoted)
            {
                if (current.Length > 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
            }
            else current.Append(ch);
        }
        if (current.Length > 0) result.Add(current.ToString());
        return result;
    }

    public bool Step(Action<string, bool> output)
    {
        if (Halted || HasError) return false;
        if (PC >= Instructions.Count)
        {
            Halted = true;
            return false;
        }

        Instruction inst = Instructions[PC];
        Cycles++;
        int nextPC = PC + 1;
        string op = inst.Opcode;
        string arg = inst.Args.Count > 0 ? inst.Args[0] : "";

        try
        {
            switch (op)
            {
                case "nop": break;

                case "ldc.i4.m1": Stack.Add(-1); break;
                case "ldc.i4.0": Stack.Add(0); break;
                case "ldc.i4.1": Stack.Add(1); break;
                case "ldc.i4.2": Stack.Add(2); break;
                case "ldc.i4.3": Stack.Add(3); break;
                case "ldc.i4.4": Stack.Add(4); break;
                case "ldc.i4.5": Stack.Add(5); break;
                case "ldc.i4":
                case "ldc.i4.s":
                    Stack.Add(int.TryParse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : 0);
                    break;

                case "ldstr":
                {
                    string value = string.Join(" ", inst.Args);
                    if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
                        value = value.Substring(1, value.Length - 2);
                    Stack.Add(value);
                    break;
                }

                case "ldloc.0": PushLocal(0); break;
                case "ldloc.1": PushLocal(1); break;
                case "ldloc.2": PushLocal(2); break;
                case "ldloc.3": PushLocal(3); break;
                case "ldloc":
                case "ldloc.s": PushLocal(ParseIndex(arg)); break;

                case "stloc.0": StoreLocal(0); break;
                case "stloc.1": StoreLocal(1); break;
                case "stloc.2": StoreLocal(2); break;
                case "stloc.3": StoreLocal(3); break;
                case "stloc":
                case "stloc.s": StoreLocal(ParseIndex(arg)); break;

                case "add": BinaryInt((a, b) => unchecked(a + b)); break;
                case "sub": BinaryInt((a, b) => unchecked(a - b)); break;
                case "mul": BinaryInt((a, b) => unchecked(a * b)); break;
                case "div":
                    BinaryInt((a, b) => b == 0 ? throw new DivideByZeroException("DivideByZeroException") : a / b);
                    break;
                case "rem":
                    BinaryInt((a, b) => b == 0 ? throw new DivideByZeroException("DivideByZeroException") : a % b);
                    break;

                case "br":
                case "br.s": nextPC = ResolveLabel(arg); break;

                case "beq":
                case "beq.s": if (ComparePop() == 0) nextPC = ResolveLabel(arg); break;
                case "bne.un":
                case "bne.un.s": if (ComparePop() != 0) nextPC = ResolveLabel(arg); break;
                case "bgt":
                case "bgt.s": if (ComparePop() > 0) nextPC = ResolveLabel(arg); break;
                case "bge":
                case "bge.s": if (ComparePop() >= 0) nextPC = ResolveLabel(arg); break;
                case "blt":
                case "blt.s": if (ComparePop() < 0) nextPC = ResolveLabel(arg); break;
                case "ble":
                case "ble.s": if (ComparePop() <= 0) nextPC = ResolveLabel(arg); break;

                case "brtrue":
                case "brtrue.s":
                    if (ToInt(Pop()) != 0) nextPC = ResolveLabel(arg);
                    break;
                case "brfalse":
                case "brfalse.s":
                    if (ToInt(Pop()) == 0) nextPC = ResolveLabel(arg);
                    break;

                case "call":
                {
                    string signature = string.Join(" ", inst.Args);
                    if (signature.Contains("System.Console::WriteLine", StringComparison.Ordinal))
                    {
                        object value = Stack.Count > 0 ? Pop() : "";
                        output(FormatValue(value), true);
                    }
                    else if (signature.Contains("System.Console::Write", StringComparison.Ordinal))
                    {
                        object value = Stack.Count > 0 ? Pop() : "";
                        output(FormatValue(value), false);
                    }
                    else output("[VM Call invoked: " + signature + "]", true);
                    break;
                }

                case "ret":
                    Halted = true;
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported IL OpCode: '{op}' (source line {inst.SourceLine}).");
            }

            PC = nextPC;
            return !Halted;
        }
        catch
        {
            HasError = true;
            Halted = true;
            throw;
        }
    }

    private static int ParseIndex(string value)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
            throw new InvalidOperationException("Invalid local variable index: " + value);
        return index;
    }

    private void PushLocal(int index)
    {
        if (!Locals.ContainsKey(index))
            Locals[index] = new LocalValue { Name = "loc_" + index, Value = 0 };
        Stack.Add(Locals[index].Value);
    }

    private void StoreLocal(int index)
    {
        object value = Pop();
        if (!Locals.ContainsKey(index))
            Locals[index] = new LocalValue { Name = "loc_" + index, Value = value };
        else Locals[index].Value = value;
    }

    private object Pop()
    {
        if (Stack.Count == 0)
            throw new InvalidOperationException("StackUnderflowException: Attempted to pop from empty stack.");
        object value = Stack[^1];
        Stack.RemoveAt(Stack.Count - 1);
        return value;
    }

    private void BinaryInt(Func<int, int, int> operation)
    {
        int b = ToInt(Pop());
        int a = ToInt(Pop());
        Stack.Add(operation(a, b));
    }

    // Compares the two values popped from the evaluation stack (a compared with b).
    private int ComparePop()
    {
        object b = Pop();
        object a = Pop();
        if (a is string || b is string)
            return string.CompareOrdinal(FormatValue(a), FormatValue(b));
        return ToInt(a).CompareTo(ToInt(b));
    }

    private static int ToInt(object value)
    {
        if (value is int i) return i;
        if (int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out int result))
            return result;
        throw new InvalidOperationException("Invalid integer value: " + value);
    }

    private int ResolveLabel(string label)
    {
        if (Labels.TryGetValue(label, out int target)) return target;
        throw new InvalidOperationException("Invalid branch target: '" + label + "'");
    }

    private static string FormatValue(object value) =>
        Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
}

internal static class Program
{
    private const int MaxInstructionsPerRun = 50000;

    private const string FibonacciSample = @"// .NET Assembly (.asm / .il)
.assembly extern mscorlib {}
.assembly FibonacciDemo {}
.method public static void Main() cil managed
{
    .entrypoint
    .maxstack 4
    .locals init ([0] int32 a, [1] int32 b, [2] int32 temp, [3] int32 count)
    ldc.i4.0
    stloc.0
    ldc.i4.1
    stloc.1
    ldc.i4 10
    stloc.3
    ldstr ""--- Starting Fibonacci Sequence ---""
    call void [mscorlib]System.Console::WriteLine(string)
LOOP:
    ldloc.3
    ldc.i4.0
    ble END
    ldstr ""Term: ""
    call void [mscorlib]System.Console::Write(string)
    ldloc.0
    call void [mscorlib]System.Console::WriteLine(int32)
    ldloc.0
    ldloc.1
    add
    stloc.2
    ldloc.1
    stloc.0
    ldloc.2
    stloc.1
    ldloc.3
    ldc.i4.1
    sub
    stloc.3
    br LOOP
END:
    ldstr ""Sequence complete!""
    call void [mscorlib]System.Console::WriteLine(string)
    ret
}";

    private static DotNetVM vm = new();
    private static readonly StringBuilder programOutput = new();
    private static string? outputFile;

    private static void Main(string[] args)
    {
        string inputFile = args.Length > 0 ? args[0] : "";
        outputFile = args.Length > 1 ? args[1] : null;

        string source;
        string displayName;
        Console.BackgroundColor = ConsoleColor.White;
        Console.ForegroundColor = ConsoleColor.Black;
        Console.Clear();
        try
        {
            if (string.IsNullOrWhiteSpace(inputFile))
            {
                source = FibonacciSample;
                displayName = "built-in Fibonacci sample";
            }
            else
            {
                if (!File.Exists(inputFile))
                {
                    Console.Error.WriteLine("Input file not found: " + inputFile);
                    Environment.ExitCode = 1;
                    return;
                }
                source = File.ReadAllText(inputFile);
                displayName = inputFile;
            }

            originalSource = source;
            vm.Assemble(source);
            Console.WriteLine("CLR MicroVM Emulator - C# Console Edition");
            Console.WriteLine("Input: " + displayName);
            Console.WriteLine("Instructions assembled: " + vm.Instructions.Count);
            Console.WriteLine();
            Console.WriteLine("Commands: [R]un  [S]tep  [V]iew state  [T]est/reset  [Q]uit");
            Console.WriteLine("Press Enter to run the program immediately.");
            Console.Write("> ");

            string? command = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(command) || command.Trim().Equals("r", StringComparison.OrdinalIgnoreCase))
                Run();
            else
                InteractiveLoop(command.Trim());

            SaveOutput();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[Assembly Error] " + ex.Message);
            Environment.ExitCode = 1;
        }
    }

    private static void InteractiveLoop(string firstCommand)
    {
        string? command = firstCommand;
        while (true)
        {
            if (command == null)
            {
                Console.Write("vm> ");
                command = Console.ReadLine();
            }
            if (command == null) break;

            switch (command.Trim().ToLowerInvariant())
            {
                case "r":
                case "run": Run(); break;
                case "s":
                case "step": Step(); break;
                case "v":
                case "view": DisplayState(); break;
                case "t":
                case "test":
                case "reset":
                    vm.Assemble(ReadCurrentSourceFallback());
                    programOutput.Clear();
                    Console.WriteLine("[VM Reset to initial state]");
                    DisplayState();
                    break;
                case "q":
                case "quit":
                case "exit": return;
                case "": break;
                default: Console.WriteLine("Unknown command. Use R, S, V, T or Q."); break;
            }
            command = null;
        }
    }

    // Keep the original source so the reset command can reassemble it.
    private static string originalSource = "";
    private static string ReadCurrentSourceFallback() => originalSource;

    private static void Run()
    {
        if (vm.Halted || vm.Instructions.Count == 0)
        {
            vm.Assemble(originalSource);
            programOutput.Clear();
        }

        Console.WriteLine("--- Execution Started ---");
        int count = 0;
        try
        {
            while (!vm.Halted && count < MaxInstructionsPerRun)
            {
                vm.Step(WriteProgramOutput);
                count++;
            }
            if (count >= MaxInstructionsPerRun)
                Console.WriteLine("[VM Warning]: Execution paused: cycle limit reached (infinite loop guard).");
            else
                Console.WriteLine($"--- Execution Finished ({count} cycles in this run) ---");
        }
        catch (Exception ex)
        {
            Console.WriteLine("[VM Runtime Error]: " + ex.Message);
        }
        DisplayState();
    }

    private static void Step()
    {
        if ((vm.Halted && !vm.HasError) || vm.Instructions.Count == 0)
        {
            vm.Assemble(originalSource);
            programOutput.Clear();
        }

        try
        {
            bool canContinue = vm.Step(WriteProgramOutput);
            if (!canContinue) Console.WriteLine("[Program Exited]");
        }
        catch (Exception ex)
        {
            Console.WriteLine("[VM Runtime Error]: " + ex.Message);
        }
        DisplayState();
    }

    private static void WriteProgramOutput(string text, bool newLine)
    {
        if (newLine)
        {
            Console.WriteLine(text);
            programOutput.AppendLine(text);
        }
        else
        {
            Console.Write(text);
            programOutput.Append(text);
        }
    }

    private static void DisplayState()
    {
        Console.WriteLine();
        Console.WriteLine("--------------- CLR VM STATE ---------------");
        Console.WriteLine("Status : " + (vm.HasError ? "ERROR" : vm.Halted ? "HALTED" : vm.Cycles > 0 ? "EXECUTING" : "READY"));
        Console.WriteLine("PC     : " + vm.PC);
        Console.WriteLine("Cycles : " + vm.Cycles);

        Console.WriteLine();
        Console.WriteLine("Evaluation Stack (top first):");
        if (vm.Stack.Count == 0) Console.WriteLine("  <empty>");
        else
        {
            for (int i = vm.Stack.Count - 1; i >= 0; i--)
                Console.WriteLine($"  [{i}] {vm.Stack[i]}");
        }

        Console.WriteLine();
        Console.WriteLine("Local Variables:");
        if (vm.Locals.Count == 0) Console.WriteLine("  <none>");
        else
        {
            foreach (var item in vm.Locals)
                Console.WriteLine($"  V_{item.Key} ({item.Value.Name}) = {item.Value.Value}");
        }

        if (vm.PC >= 0 && vm.PC < vm.Instructions.Count)
        {
            Instruction current = vm.Instructions[vm.PC];
            Console.WriteLine();
            Console.WriteLine($"Next instruction (source line {current.SourceLine}): {current.SourceText}");
        }
        Console.WriteLine("--------------------------------------------");
    }

    private static void SaveOutput()
    {
        if (string.IsNullOrWhiteSpace(outputFile)) return;
        File.WriteAllText(outputFile, programOutput.ToString(), Encoding.UTF8);
        Console.WriteLine();
        Console.WriteLine("Program output saved to: " + outputFile);
    }
}
