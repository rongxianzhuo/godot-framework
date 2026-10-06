using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using GameFramework;
using Godot;
using Xunit;

namespace ScreenManagerTests;

/// <summary>
/// IL-shape reflection tests for <see cref="ScreenManager"/> lifecycle. Verifies
/// the GameService-subclass behavior contract WITHOUT instantiating
/// (instantiation would require a Godot engine runtime — deferred to a future
/// <c>godot --headless</c> test runner).
///
/// These tests scan the IL bytecode of <c>_Ready</c> / <c>_ExitTree</c> to confirm
/// the behavioral contract:
///   * <c>base._Ready()</c> is called (so ServiceRegistry registration runs)
///   * <c>base._ExitTree()</c> is called (so cleanup runs)
///   * Static <c>Instance</c> field is assigned in <c>_Ready</c>
///   * Static <c>Instance</c> field is read in <c>_ExitTree</c> (the
///     <c>if (Instance == this)</c> check + clear)
///
/// Sibling test to <c>GameServiceLifecycleTests</c> for EventBus (same IL
/// scanner implementation, different target method).
/// </summary>
public class ScreenManagerLifecycleTests
{
    [Fact]
    public void ScreenManager_Ready_IsPublicVoidNoArgsOverride()
    {
        var ready = GetInstanceMethod(typeof(ScreenManager), "_Ready");
        Assert.NotNull(ready);
        Assert.Equal(typeof(void), ready!.ReturnType);
        Assert.Empty(ready.GetParameters());
        Assert.True(ready.IsPublic || ready.IsFamily,
            "_Ready must be public or protected so Godot's reflection-based dispatcher can find it.");
        Assert.False(ready.IsAbstract);
        Assert.False(ready.IsStatic);
    }

    [Fact]
    public void ScreenManager_ExitTree_IsPublicVoidNoArgsOverride()
    {
        var exit = GetInstanceMethod(typeof(ScreenManager), "_ExitTree");
        Assert.NotNull(exit);
        Assert.Equal(typeof(void), exit!.ReturnType);
        Assert.Empty(exit.GetParameters());
        Assert.True(exit.IsPublic || exit.IsFamily);
        Assert.False(exit.IsAbstract);
        Assert.False(exit.IsStatic);
    }

    [Fact]
    public void ScreenManager_Ready_IsTrueOverrideOfGameServiceReady()
    {
        // GetBaseDefinition walks to the rootmost virtual in the chain.
        // For ScreenManager._Ready → GameService._Ready → Node._Ready, the
        // rootmost is Node._Ready. We assert that — and that it's NOT
        // ScreenManager._Ready itself (which would indicate a `new` keyword
        // accidental).
        var ready = GetInstanceMethod(typeof(ScreenManager), "_Ready");
        Assert.NotNull(ready);
        var baseDef = ready!.GetBaseDefinition();
        Assert.NotNull(baseDef);
        Assert.Equal(typeof(Node), baseDef!.DeclaringType);
        Assert.Equal("_Ready", baseDef.Name);
        Assert.NotEqual(typeof(ScreenManager), baseDef.DeclaringType);
    }

    [Fact]
    public void ScreenManager_ExitTree_IsTrueOverrideOfGameServiceExitTree()
    {
        var exit = GetInstanceMethod(typeof(ScreenManager), "_ExitTree");
        Assert.NotNull(exit);
        var baseDef = exit!.GetBaseDefinition();
        Assert.NotNull(baseDef);
        Assert.Equal(typeof(Node), baseDef!.DeclaringType);
        Assert.Equal("_ExitTree", baseDef.Name);
        Assert.NotEqual(typeof(ScreenManager), baseDef.DeclaringType);
    }

    [Fact]
    public void ScreenManager_Ready_CallsBaseReady()
    {
        var ready = GetInstanceMethod(typeof(ScreenManager), "_Ready");
        var baseReady = GetInstanceMethod(typeof(GameService), "_Ready");
        Assert.NotNull(ready);
        Assert.NotNull(baseReady);

        var matches = FindCallInstructions(ready!, baseReady!);
        Assert.True(matches.Count > 0,
            "ScreenManager._Ready must call GameService._Ready() (to register with Game.Instance.Services).");
    }

    [Fact]
    public void ScreenManager_ExitTree_CallsBaseExitTree()
    {
        var exit = GetInstanceMethod(typeof(ScreenManager), "_ExitTree");
        var baseExit = GetInstanceMethod(typeof(GameService), "_ExitTree");
        Assert.NotNull(exit);
        Assert.NotNull(baseExit);

        var matches = FindCallInstructions(exit!, baseExit!);
        Assert.True(matches.Count > 0,
            "ScreenManager._ExitTree must call GameService._ExitTree() (to unregister).");
    }

    [Fact]
    public void ScreenManager_Ready_CallsSetInstance()
    {
        var ready = GetInstanceMethod(typeof(ScreenManager), "_Ready");
        var instanceProp = typeof(ScreenManager).GetProperty(
            "Instance",
            BindingFlags.Static | BindingFlags.Public);
        Assert.NotNull(instanceProp);
        var setter = instanceProp!.SetMethod;
        Assert.NotNull(setter);

        var matches = FindCallInstructions(ready!, setter!);
        Assert.True(matches.Count > 0,
            "ScreenManager._Ready must call set_Instance (so consumers can access ScreenManager.Instance).");
    }

    [Fact]
    public void ScreenManager_ExitTree_CallsSetInstance()
    {
        // _ExitTree clears Instance (if Instance == this).
        var exit = GetInstanceMethod(typeof(ScreenManager), "_ExitTree");
        var instanceProp = typeof(ScreenManager).GetProperty(
            "Instance",
            BindingFlags.Static | BindingFlags.Public);
        Assert.NotNull(instanceProp);
        var setter = instanceProp!.SetMethod;
        Assert.NotNull(setter);

        var matches = FindCallInstructions(exit!, setter!);
        Assert.True(matches.Count > 0,
            "ScreenManager._ExitTree must call set_Instance to null (cleanup).");
    }

    // ====================================================================
    // IL scanning helpers — same logic as EventBus/GameServiceLifecycleTests.
    // Future refactor: extract to a shared internal helper if a 3rd test
    // project needs it.
    // ====================================================================

    private static MethodInfo? GetInstanceMethod(Type type, string name)
    {
        return type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    }

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
                i += 5;
            }
            else
            {
                i += AdvancePast(il, i);
            }
        }
        return result;
    }

    private static int AdvancePast(byte[] il, int offset)
    {
        byte b = il[offset];
        if (b == 0xFE)
        {
            // Two-byte opcode prefix.
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

    private static int TwoByteOpcodeOperandSize(byte secondByte) => secondByte switch
    {
        0x06 => 4, 0x09 => 2, 0x0A => 2, 0x0B => 2, 0x0C => 2, 0x0D => 2, 0x0E => 2,
        0x12 => 1, 0x15 => 4, 0x16 => 4, 0x1C => 4,
        _ => 0,
    };

    private static int OperandSizeForByte(byte b) => b switch
    {
        // InlineNone (0 bytes operand)
        0x00 => 0, 0x01 => 0, 0x02 => 0, 0x03 => 0, 0x04 => 0, 0x05 => 0,
        0x06 => 0, 0x07 => 0, 0x08 => 0, 0x09 => 0, 0x0A => 0, 0x0B => 0,
        0x0C => 0, 0x0D => 0, 0x14 => 0, 0x15 => 0, 0x16 => 0, 0x17 => 0,
        0x18 => 0, 0x19 => 0, 0x1A => 0, 0x1B => 0, 0x1C => 0, 0x1D => 0,
        0x1E => 0, 0x25 => 0, 0x26 => 0, 0x2A => 0, 0x7A => 0, 0x8E => 0,
        0xDC => 0,
        // ShortInline* (1 byte operand)
        0x0E => 1, 0x0F => 1, 0x10 => 1, 0x11 => 1, 0x12 => 1, 0x13 => 1,
        0x1F => 1, 0x2B => 1, 0x2C => 1, 0x2D => 1, 0x2E => 1, 0x2F => 1,
        0x30 => 1, 0x31 => 1, 0x32 => 1, 0x33 => 1, 0x34 => 1, 0x35 => 1,
        0x36 => 1, 0x37 => 1, 0xDD => 1,
        // InlineI/BrTarget/String/Field/Method/Sig/Tok/Type (4 bytes operand)
        0x20 => 4, 0x28 => 4, 0x29 => 4, 0x38 => 4, 0x39 => 4, 0x3A => 4,
        0x3B => 4, 0x3C => 4, 0x3D => 4, 0x3E => 4, 0x3F => 4, 0x40 => 4,
        0x41 => 4, 0x42 => 4, 0x43 => 4, 0x44 => 4, 0x45 => 4, 0x6F => 4,
        0x70 => 4, 0x71 => 4, 0x72 => 4, 0x73 => 4, 0x74 => 4, 0x75 => 4,
        0x79 => 4, 0x7B => 4, 0x7C => 4, 0x7D => 4, 0x7E => 4, 0x7F => 4,
        0x80 => 4, 0x81 => 4, 0x8C => 4, 0x8D => 4, 0x8F => 4, 0xA4 => 4,
        0xA5 => 4, 0xD0 => 4, 0xDE => 4,
        // InlineR8/I8 (8 bytes operand)
        0x21 => 8, 0x22 => 8, 0x23 => 8,
        _ => 0,
    };
}