// .NET Assembly (.asm / .il)
// String outputs and conditionals

.assembly StringTest {}

.method public static void Main() cil managed
{
    .entrypoint
    .locals init ([0] int32 condition)

    ldstr "Verifying .NET VM Instruction Set..."
    call void [mscorlib]System.Console::WriteLine(string)

    ldc.i4 42
    stloc.0

    ldloc.0
    ldc.i4 42
    beq EQUAL

    ldstr "Mismatch detected!"
    call void [mscorlib]System.Console::WriteLine(string)
    br FINISH

EQUAL:
    ldstr "Success: 42 is equal to 42!"
    call void [mscorlib]System.Console::WriteLine(string)

FINISH:
    ldstr "Virtual Machine test finished."
    call void [mscorlib]System.Console::WriteLine(string)
    ret
}