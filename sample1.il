// .NET Assembly (.asm / .il)
// Computes and prints Fibonacci numbers up to 10 iterations

.assembly extern mscorlib {}
.assembly FibonacciDemo {}

.method public static void Main() cil managed
{
    .entrypoint
    .maxstack 4
    .locals init ([0] int32 a, [1] int32 b, [2] int32 temp, [3] int32 count)

    // a = 0, b = 1, count = 10
    ldc.i4.0
    stloc.0
    ldc.i4.1
    stloc.1
    ldc.i4 10
    stloc.3

    ldstr "--- Starting Fibonacci Sequence ---"
    call void [mscorlib]System.Console::WriteLine(string)

LOOP:
    ldloc.3
    ldc.i4.0
    ble END

    // Console.WriteLine(a)
    ldstr "Term: "
    call void [mscorlib]System.Console::Write(string)
    ldloc.0
    call void [mscorlib]System.Console::WriteLine(int32)

    // temp = a + b
    ldloc.0
    ldloc.1
    add
    stloc.2

    // a = b
    ldloc.1
    stloc.0

    // b = temp
    ldloc.2
    stloc.1

    // count--
    ldloc.3
    ldc.i4.1
    sub
    stloc.3

    br LOOP

END:
    ldstr "Sequence complete!"
    call void [mscorlib]System.Console::WriteLine(string)
    ret
}