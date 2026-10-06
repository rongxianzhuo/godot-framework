using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using GameFramework;
using Godot;
using Xunit;

namespace EventBusTests;

/// <summary>
/// Lifecycle / IL-shape reflection tests for <see cref="EventBus"/>. Verifies
/// the GameService-subclass behavior contract WITHOUT instantiating
/// (instantiation would require a Godot engine runtime — deferred to a
/// future <c>godot --headless</c> test runner).
///
/// These tests scan the IL bytecode of <c>_Ready</c> and <c>_ExitTree</c>
/// to confirm the behavioral contract:
///   * <c>base._Ready()</c> is called (so ServiceRegistry registration runs)
///   * <c>base._ExitTree()</c> is called (so cleanup runs)
///   * Static <c>Instance</c> field is assigned in <c>_Ready</c>
///   * Static <c>Instance</c> field is read in <c>_ExitTree</c> (the
///     <c>if (Instance == this)</c> check)
///
/// IL scan is stable for Debug builds (which <c>dotnet test</c> uses by
/// default). If a future Roslyn version emits substantially different IL
/// for this pattern, the failing tests point at the affected opcodes and
/// can be updated.
/// </summary>
public class GameServiceLifecycleTests
{
    [Fact]
    public void EventBus_Ready_IsPublicVoidNoArgsOverride()
    {
        var ready = GetInstanceMethod(typeof(EventBus), "_Ready");
        Assert.NotNull(ready);
        Assert.Equal(typeof(void), ready!.ReturnType);
        Assert.Empty(ready.GetParameters());
        // _Ready must be reachable by Godot's reflection-based dispatcher.
        // Godot looks up `_Ready` via reflection; it must be public or protected.
        Assert.True(ready.IsPublic || ready.IsFamily,
            "_Ready must be public or protected so Godot's reflection-based dispatcher can find it.");
        Assert.False(ready.IsAbstract);
        Assert.False(ready.IsStatic);
    }

    [Fact]
    public void EventBus_ExitTree_IsPublicVoidNoArgsOverride()
    {
        var exit = GetInstanceMethod(typeof(EventBus), "_ExitTree");
        Assert.NotNull(exit);
        Assert.Equal(typeof(void), exit!.ReturnType);
        Assert.Empty(exit.GetParameters());
        Assert.True(exit.IsPublic || exit.IsFamily,
            "_ExitTree must be public or protected so Godot's reflection-based dispatcher can find it.");
        Assert.False(exit.IsAbstract);
        Assert.False(exit.IsStatic);
    }

    [Fact]
    public void EventBus_Ready_IsTrueOverrideOfGameServiceReady()
    {
        // `GetBaseDefinition()` walks to the rootmost virtual in the chain.
        // For EventBus._Ready → GameService._Ready → Node._Ready (Godot virtual),
        // GetBaseDefinition returns the rootmost (Node._Ready). The fact that
        // it's NOT EventBus._Ready itself proves it's a true `override`
        // (a `new` declaration would have GetBaseDefinition == self).
        var ready = GetInstanceMethod(typeof(EventBus), "_Ready");
        Assert.NotNull(ready);
        var baseDef = ready!.GetBaseDefinition();
        Assert.NotNull(baseDef);
        Assert.Equal(typeof(Node), baseDef!.DeclaringType);
        Assert.Equal("_Ready", baseDef.Name);
        // Sanity: EventBus._Ready itself is not the rootmost (it's a derived override).
        Assert.NotEqual(typeof(EventBus), baseDef.DeclaringType);
    }

    [Fact]
    public void EventBus_ExitTree_IsTrueOverrideOfGameServiceExitTree()
    {
        var exit = GetInstanceMethod(typeof(EventBus), "_ExitTree");
        Assert.NotNull(exit);
        var baseDef = exit!.GetBaseDefinition();
        Assert.NotNull(baseDef);
        Assert.Equal(typeof(Node), baseDef!.DeclaringType);
        Assert.Equal("_ExitTree", baseDef.Name);
        Assert.NotEqual(typeof(EventBus), baseDef.DeclaringType);
    }

    [Fact]
    public void EventBus_HasPrivateDispatcherField_OfTypeEventDispatcher()
    {
        // Composition: EventBus holds an EventDispatcher for the actual
        // pub/sub logic. The field must be private (encapsulation).
        var fields = typeof(EventBus)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        var dispatcherField = fields.FirstOrDefault(f => f.FieldType == typeof(EventDispatcher));

        Assert.NotNull(dispatcherField);
        Assert.True(dispatcherField!.IsPrivate,
            "EventBus._dispatcher must be private — encapsulation.");
        Assert.True(dispatcherField.IsInitOnly,
            "EventBus._dispatcher should be readonly — it's initialized once and never reassigned.");
    }

    [Fact]
    public void EventBus_Ready_CallsBaseReady()
    {
        // The override must call base._Ready() so that
        // GameService._Ready() runs the ServiceRegistry registration.
        var ready = GetInstanceMethod(typeof(EventBus), "_Ready");
        var baseReady = GetInstanceMethod(typeof(GameService), "_Ready");
        Assert.NotNull(ready);
        Assert.NotNull(baseReady);

        var matches = FindCallInstructions(ready!, baseReady!);
        Assert.True(matches.Count > 0,
            "EventBus._Ready must call GameService._Ready() (to register with Game.Instance.Services).");
    }

    [Fact]
    public void EventBus_ExitTree_CallsBaseExitTree()
    {
        // The override must call base._ExitTree() so that
        // GameService._ExitTree() runs the ServiceRegistry cleanup.
        var exit = GetInstanceMethod(typeof(EventBus), "_ExitTree");
        var baseExit = GetInstanceMethod(typeof(GameService), "_ExitTree");
        Assert.NotNull(exit);
        Assert.NotNull(baseExit);

        var matches = FindCallInstructions(exit!, baseExit!);
        Assert.True(matches.Count > 0,
            "EventBus._ExitTree must call GameService._ExitTree() (to unregister from Game.Instance.Services).");
    }

    [Fact]
    public void EventBus_Ready_CallsSetInstance()
    {
        // The override must call set_Instance (auto-property setter for
        // `Instance = this`) so consumers can find the EventBus via the
        // static pointer. Note: this is `call set_Instance(EventBus)`, NOT
        // a direct `stsfld <Instance>k__BackingField`, because the static
        // auto-property routes through its setter method.
        var ready = GetInstanceMethod(typeof(EventBus), "_Ready");
        var instanceProp = typeof(EventBus).GetProperty(
            "Instance",
            BindingFlags.Static | BindingFlags.Public);
        Assert.NotNull(instanceProp);
        var setter = instanceProp!.SetMethod;
        Assert.NotNull(setter);

        var matches = FindCallInstructions(ready!, setter!);
        Assert.True(matches.Count > 0,
            "EventBus._Ready must call set_Instance (via `Instance = this`).");
    }

    [Fact]
    public void EventBus_ExitTree_ReadsAndWritesInstance()
    {
        // The override must:
        //   - call get_Instance (auto-property getter for the `if (Instance == this)` check)
        //   - call set_Instance (auto-property setter for `Instance = null!`)
        // Same routing-through-method observation as the _Ready test.
        var exit = GetInstanceMethod(typeof(EventBus), "_ExitTree");
        var instanceProp = typeof(EventBus).GetProperty(
            "Instance",
            BindingFlags.Static | BindingFlags.Public);
        Assert.NotNull(instanceProp);
        var getter = instanceProp!.GetMethod;
        var setter = instanceProp.SetMethod;
        Assert.NotNull(getter);
        Assert.NotNull(setter);

        var reads = FindCallInstructions(exit!, getter!);
        var writes = FindCallInstructions(exit!, setter!);

        Assert.True(reads.Count > 0,
            "EventBus._ExitTree must call get_Instance (via `if (Instance == this)`).");
        Assert.True(writes.Count > 0,
            "EventBus._ExitTree must call set_Instance (via `Instance = null!`).");
    }

    // ====================================================================
    // IL scanning helpers — direct byte-value approach (avoids dependency
    // on System.Reflection.Emit.OpCodes.OneByteOpCodes/TwoByteOpCodes
    // which aren't public API in .NET 9).
    // ====================================================================

    private static MethodInfo? GetInstanceMethod(Type type, string name)
    {
        return type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    }

    /// <summary>
    /// Finds all `call`/`callvirt` instructions in <paramref name="caller"/>
    /// that resolve to <paramref name="target"/>.
    /// </summary>
    private static List<int> FindCallInstructions(MethodInfo caller, MethodInfo target)
    {
        var body = caller.GetMethodBody();
        if (body == null) return new List<int>();
        var il = body.GetILAsByteArray() ?? Array.Empty<byte>();
        var module = caller.Module;
        var targetHandle = target.MethodHandle;

        byte callByte = (byte)OpCodes.Call.Value;       // 0x28
        byte callvirtByte = (byte)OpCodes.Callvirt.Value; // 0x6F

        var result = new List<int>();
        int i = 0;
        while (i < il.Length)
        {
            byte b = il[i];
            if (b == callByte || b == callvirtByte)
            {
                if (i + 5 <= il.Length)
                {
                    int token = BitConverter.ToInt32(il, i + 1);
                    try
                    {
                        var member = module.ResolveMethod(token);
                        if (member is MethodBase mb && mb.MethodHandle == targetHandle)
                        {
                            result.Add(i);
                        }
                    }
                    catch { /* unresolvable token */ }
                }
                i += 5; // 1 byte opcode + 4 byte token
            }
            else
            {
                i += AdvancePast(il, i);
            }
        }
        return result;
    }

    /// <summary>
    /// Finds all `stsfld` instructions in <paramref name="caller"/> that
    /// target <paramref name="target"/>.
    /// </summary>
    private static List<int> FindFieldStoreInstructions(MethodInfo caller, FieldInfo target)
    {
        return FindFieldAccessInstructions(caller, target, isStore: true);
    }

    /// <summary>
    /// Finds all `ldsfld` instructions in <paramref name="caller"/> that
    /// target <paramref name="target"/>.
    /// </summary>
    private static List<int> FindFieldLoadInstructions(MethodInfo caller, FieldInfo target)
    {
        return FindFieldAccessInstructions(caller, target, isStore: false);
    }

    private static List<int> FindFieldAccessInstructions(MethodInfo caller, FieldInfo target, bool isStore)
    {
        var body = caller.GetMethodBody();
        if (body == null) return new List<int>();
        var il = body.GetILAsByteArray() ?? Array.Empty<byte>();
        var module = caller.Module;
        var targetHandle = target.FieldHandle;

        byte stsfldByte = (byte)OpCodes.Stsfld.Value;   // 0x80
        byte ldsfldByte = (byte)OpCodes.Ldsfld.Value;   // 0x7E

        var result = new List<int>();
        int i = 0;
        while (i < il.Length)
        {
            byte b = il[i];
            bool match = (isStore && b == stsfldByte) || (!isStore && b == ldsfldByte);
            if (match)
            {
                if (i + 5 <= il.Length)
                {
                    int token = BitConverter.ToInt32(il, i + 1);
                    try
                    {
                        var member = module.ResolveField(token);
                        if (member is FieldInfo fi && fi.FieldHandle == targetHandle)
                        {
                            result.Add(i);
                        }
                    }
                    catch { /* unresolvable token */ }
                }
                i += 5;
            }
            else
            {
                i += AdvancePast(il, i);
            }
        }
        return result;
    }

    /// <summary>
    /// Advances past a single IL instruction at <paramref name="offset"/>,
    /// returning the number of bytes consumed (opcode + operand). Handles
    /// both single-byte opcodes (0x00-0xFF) and two-byte opcodes (0xFE prefix).
    /// </summary>
    private static int AdvancePast(byte[] il, int offset)
    {
        byte b = il[offset];
        if (b == 0xFE)
        {
            // Two-byte opcode prefix. Read the second byte to determine
            // operand size.
            if (offset + 1 >= il.Length)
            {
                throw new InvalidOperationException(
                    $"Truncated two-byte opcode at offset {offset}.");
            }
            byte secondByte = il[offset + 1];
            int operandSize = TwoByteOpcodeOperandSize(secondByte);
            return 2 + operandSize;
        }
        return 1 + OperandSizeForByte(b);
    }

    /// <summary>
    /// Returns the operand size (in bytes) for a two-byte CIL opcode's
    /// second byte. Covers the commonly-encountered two-byte opcodes:
    /// ceq, cgt, clt, ldftn, ldarg, ldarga, starg, ldloc, ldloca, stloc,
    /// unaligned., initobj, constrained., sizeof, and others (which default
    /// to 0 operand).
    /// </summary>
    private static int TwoByteOpcodeOperandSize(byte secondByte) => secondByte switch
    {
        0x06 => 4, // ldftn (4 byte method token)
        0x09 => 2, // ldarg (2 byte index)
        0x0A => 2, // ldarga (2 byte index)
        0x0B => 2, // starg (2 byte index)
        0x0C => 2, // ldloc (2 byte index)
        0x0D => 2, // ldloca (2 byte index)
        0x0E => 2, // stloc (2 byte index)
        0x12 => 1, // unaligned. (1 byte alignment)
        0x15 => 4, // initobj (4 byte type token)
        0x16 => 4, // constrained. (4 byte type token)
        0x1C => 4, // sizeof (4 byte type token)
        // Default 0: ceq (0x01), cgt (0x02), clt (0x04), localloc (0x0F),
        //   endfilter (0x11), volatile. (0x13), tail. (0x14), cpblk (0x17),
        //   initblk (0x18), rethrow (0x1A), refanytype (0x1D), readonly. (0x1E)
        _ => 0,
    };

    /// <summary>
    /// Returns the operand size in bytes for a single-byte CIL opcode.
    /// Covers the opcodes likely to appear in EventBus._Ready/_ExitTree
    /// (load/store, call/callvirt, branches, constant loads). Unknown
    /// opcodes default to 0 (best-effort).
    /// </summary>
    private static int OperandSizeForByte(byte b) => b switch
    {
        // InlineNone (0 bytes operand): nop, ret, break, ldarg.0-3, ldloc.0-3,
        //   stloc.0-3, ldnull, ldc.i4.{m1,0-8}, dup, pop, throw, ldlen, endfinally
        0x00 => 0, 0x01 => 0, 0x02 => 0, 0x03 => 0, 0x04 => 0, 0x05 => 0,
        0x06 => 0, 0x07 => 0, 0x08 => 0, 0x09 => 0, 0x0A => 0, 0x0B => 0,
        0x0C => 0, 0x0D => 0, 0x14 => 0, 0x15 => 0, 0x16 => 0, 0x17 => 0,
        0x18 => 0, 0x19 => 0, 0x1A => 0, 0x1B => 0, 0x1C => 0, 0x1D => 0,
        0x1E => 0, 0x25 => 0, 0x26 => 0, 0x2A => 0, 0x7A => 0, 0x8E => 0,
        0xDC => 0,

        // ShortInline* (1 byte operand): ldarg.s, ldc.i4.s, br.s, brfalse.s,
        //   brtrue.s, leave.s, brfalse.s family, etc.
        0x0E => 1, 0x0F => 1, 0x10 => 1, 0x11 => 1, 0x12 => 1, 0x13 => 1,
        0x1F => 1, 0x2B => 1, 0x2C => 1, 0x2D => 1, 0x2E => 1, 0x2F => 1,
        0x30 => 1, 0x31 => 1, 0x32 => 1, 0x33 => 1, 0x34 => 1, 0x35 => 1,
        0x36 => 1, 0x37 => 1, 0xDD => 1,

        // InlineI/BrTarget/String/Field/Method/Sig/Tok/Type (4 bytes operand):
        //   ldc.i4, call/callvirt/newobj, ldsfld/stsfld, br family (long), etc.
        0x20 => 4, 0x28 => 4, 0x29 => 4, 0x38 => 4, 0x39 => 4, 0x3A => 4,
        0x3B => 4, 0x3C => 4, 0x3D => 4, 0x3E => 4, 0x3F => 4, 0x40 => 4,
        0x41 => 4, 0x42 => 4, 0x43 => 4, 0x44 => 4, 0x45 => 4, 0x6F => 4,
        0x70 => 4, 0x71 => 4, 0x72 => 4, 0x73 => 4, 0x74 => 4, 0x75 => 4,
        0x79 => 4, 0x7B => 4, 0x7C => 4, 0x7D => 4, 0x7E => 4, 0x7F => 4,
        0x80 => 4, 0x81 => 4, 0x8C => 4, 0x8D => 4, 0x8F => 4, 0xA4 => 4,
        0xA5 => 4, 0xD0 => 4, 0xDE => 4,

        // InlineR8/I8 (8 bytes operand)
        0x21 => 8, 0x22 => 8, 0x23 => 8,

        // Default: best-effort 0 (treat as no-operand). May mis-advance for
        // unknown opcodes, but EventBus lifecycle methods don't use them.
        _ => 0,
    };
}
