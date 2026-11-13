// .NET Assembly (.asm / .il)
// Demonstrates arithmetic, branching & accumulator logic

.assembly AccumulatorDemo {}

.method public static void Main() cil managed
{
    .entrypoint
    .maxstack 8
    .locals init ([0] int32 sum, [1] int32 i)

    ldc.i4.0
    stloc.0
    ldc.i4.1
    stloc.1

LOOP:
    ldloc.1
    ldc.i4 5
    bgt DONE

    // sum = sum + (i * i)
    ldloc.0
    ldloc.1
    ldloc.1
    mul
    add
    stloc.0

    ldstr "Square added for i="
    call void [mscorlib]System.Console::Write(string)
    ldloc.1
    call void [mscorlib]System.Console::WriteLine(int32)

    // i++
    ldloc.1
    ldc.i4.1
    add
    stloc.1
    br LOOP

DONE:
    ldstr "Sum of squares (1 to 5): "
    call void [mscorlib]System.Console::Write(string)
    ldloc.0
    call void [mscorlib]System.Console::WriteLine(int32)
    ret
}