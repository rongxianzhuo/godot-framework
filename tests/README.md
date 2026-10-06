# Framework Tests (xUnit)

This directory holds the framework's unit tests. Tests run via standard
`dotnet test` — **no Godot engine runtime required** for the v0.3 / v0.4
test suite, because the test surface (`ServiceRegistry`, type-key
registration contract, `GameService` structural shape, `EventDispatcher`
pub/sub logic, `EventBus` lifecycle IL shape) is either pure C# or
verifiable via reflection only.

## Layout

```
tests/
├── README.md                                # this file
├── ServiceFrameworkTests/                   # Phase 0 (v0.3)
│   ├── ServiceFrameworkTests.csproj         # Godot.NET.Sdk/4.7.2 + net9.0
│   ├── ServiceRegistryTests.cs              # 14 POCO tests (13 active + 1 skipped)
│   ├── DerivedTypeRegistrationTests.cs      # 6 tests (the "Pitfall #1" principle)
│   └── GameServiceContractTests.cs          # 4 structural / reflection tests
└── EventBusTests/                           # Phase 1 (v0.4)
    ├── EventBusTests.csproj                 # Godot.NET.Sdk/4.7.2 + net9.0
    ├── EventBusTests.cs                     # 12 EventDispatcher POCO tests
    ├── EventBusContractTests.cs             # 6 EventBus structural / reflection tests
    └── GameServiceLifecycleTests.cs         # 9 EventBus lifecycle IL-shape tests
```

**Total: 51 tests** (50 passed + 1 skipped) across both projects under
`dotnet test`. The 1 skipped test exercises the `GD.PrintErr` warning
path which requires a Godot engine runtime (see "Out of scope" below).

## Test fixtures

### `ServiceRegistryTests` (14 tests, 1 skipped)

Covers the POCO surface of `ServiceRegistry`:

- `Register<T>(T)` stores under `typeof(T)` and `Resolve<T>` finds it
- `Resolve<T>` on a missing key throws `InvalidOperationException` with the type name
- `TryGet<T>` on a missing key returns `null`
- `TryGet<T>` on a registered key returns the instance
- `Unregister<T>` on an existing key removes it (returns `true`)
- `Unregister<T>` on a missing key returns `false`
- Re-registering the **same instance** is a no-op (no replacement, no warning, `Count` stays at 1)
- Multiple distinct types coexist; `Count` equals the count
- `null` arguments throw `ArgumentNullException` (3 cases: generic instance,
  explicit type key, explicit instance)
- `Count` starts at 0
- `TryGet<TBase>` when stored under a different (unrelated) type returns `null`
- `[SKIP]` Re-registering with a **different instance** replaces + logs via
  `GD.PrintErr` (requires Godot engine — see "Out of scope")

### `DerivedTypeRegistrationTests` (6 tests)

Verifies the "register under derived type" pattern that `GameService._Ready`
uses internally (see README §"Avoiding Common Pitfalls" §1). Uses plain POCO
classes — no `Node` inheritance — so the principle is exercised in
isolation from Godot lifecycle:

- Register under `typeof(Derived)` → `Resolve<Derived>` works
- Register under `typeof(Derived)` → `TryGet<Base>` returns `null` (intentional)
- Naive `Register<Base>(derived)` → `Resolve<Base>` works (the wrong pattern)
- Naive `Register<Base>(derived)` → `Resolve<Derived>` fails (the bug the
  README warns about)
- Three-level inheritance chain: only the exact type matches
- Sibling types share a base, but each is independently resolvable under its
  own type

### `GameServiceContractTests` (4 tests)

Reflection-based structural assertions on `GameService` — no instantiation,
so no Godot engine runtime needed:

- `GameService` is `abstract`
- `GameService` inherits `Godot.Node`
- `GameService._Ready` is a concrete override (so subclasses inherit the
  auto-registration behavior)
- `GameService._ExitTree` is a concrete override (so cleanup runs)

### `EventBusTests` (12 tests)

Covers the POCO surface of `EventDispatcher` — the implementation behind
`EventBus`. Pure POCO tests; the dispatcher takes an `Action<string>`
error-logger constructor seam so tests can pass a silent or capturing
logger (avoiding `GD.PrintErr`'s native dependency under plain
`dotnet test`):

- `Subscribe_Publish_ReceivesEvent`
- `Subscribe_MultipleSubscribers_AllReceive`
- `Unsubscribe_NoLongerReceives`
- `Publish_NoSubscribers_DoesNotThrow`
- `Publish_WithNoMatchingEventType_DoesNotThrow`
- `SubscriberThrows_OtherSubscribersStillReceive` (error isolation)
- `SubscriberThrows_LoggerReceivesFormattedMessage` (verifies injected logger)
- `DisposeToken_AutoUnsubscribes`
- `DisposeToken_MultipleDispose_IsIdempotent`
- `DifferentEventTypes_AreIndependentSubscribers`
- `EventTypeCount_ReflectsSubscriptions`
- `SubscriberCount_ReflectsAllSubscribers`

### `EventBusContractTests` (6 tests)

Reflection-based structural assertions on `EventBus` (Node wrapper):

- `EventBus` is `sealed`
- `EventBus` inherits `GameService` → autoload-eligible
- `EventBus.Instance` static property has a `private` setter
- `EventBus._Ready` / `_ExitTree` are concrete overrides
- `EventBus.Publish` / `Subscribe` arity matches `EventDispatcher`'s
- `EventDispatcher` is public + sealed

### `GameServiceLifecycleTests` (9 tests)

IL-shape reflection tests for `EventBus`'s lifecycle behavior. Scans the
IL bytecode of `_Ready` / `_ExitTree` to verify (without instantiating):

- `_Ready` / `_ExitTree` are `public void` no-args overrides
- Both are true `override`s (not `new`) — `GetBaseDefinition` walks to
  `Godot.Node`, not to the declaring type itself
- `EventBus._dispatcher` field is `private readonly EventDispatcher`
  (composition check)
- `_Ready` calls `GameService._Ready()` (so ServiceRegistry registration runs)
- `_Ready` calls `set_Instance` (auto-property setter for `Instance = this`)
- `_ExitTree` calls `GameService._ExitTree()` (so ServiceRegistry cleanup runs)
- `_ExitTree` calls `get_Instance` (for the `if (Instance == this)` check)
- `_ExitTree` calls `set_Instance` (for `Instance = null!`)

The IL scanner handles both single-byte opcodes and the `0xFE` two-byte
prefix (e.g. `ceq`) so it walks the full method body correctly.

## Out of scope (future work)

Full `GameService._Ready()` / `_ExitTree()` **lifecycle** tests — i.e.,
actually instantiating a concrete `GameService` subclass, invoking its
`_Ready()`, and asserting that `Game.Instance.Services` received the
registration — are deferred. They require a Godot engine runtime
(`Node` constructor calls into native bindings), and `dotnet test` does
not provide one.

To unblock this in v0.4+:

- **Option A**: Add a `godot --headless` test runner (custom Godot test
  scene that bootstraps `Game.Instance` and invokes test methods
  reflectively).
- **Option B**: Refactor `GameService._Ready` to delegate registration to a
  `protected static` method that takes `(ServiceRegistry, Node)` so unit
  tests can invoke it directly without instantiating `Node`.

Tracked as a follow-up in `CHANGELOG.md` v0.3 [WIP] / v0.4-alpha.

## Running

```bash
cd addons/godot-framework/tests/ServiceFrameworkTests
dotnet test
```

```bash
cd addons/godot-framework/tests/EventBusTests
dotnet test
```

Expected output:

```
# ServiceFrameworkTests
Passed!  - Failed: 0, Passed: 23, Skipped: 1, Total: 24

# EventBusTests
Passed!  - Failed: 0, Passed: 27, Skipped: 0, Total: 27
```

The 1 skipped test (`ServiceRegistryTests.Register_DifferentInstance_ReplacesAndWarns`)
triggers `ServiceRegistry.Register`'s `GD.PrintErr` warning, which
native-aborts the test host under plain `dotnet test` because no Godot
engine is running. Re-running it under `godot --headless` would work —
see "Out of scope" above.

The `EventDispatcherTests.SubscriberThrows_*` tests work under `dotnet
test` because the error-logger is injected via constructor (defaulting to
`GD.PrintErr` for production). Tests pass `_ => { }` (silent) or a
capturing logger to avoid the native dependency.
