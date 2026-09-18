using System;
using System.Collections.Generic;
using Godot;

namespace GameFramework;

/// <summary>
/// A lightweight, AOT-friendly type-safe service registry.
/// Stores services keyed by their Type, supports both Node-based and POCO services.
/// No reflection — works under iOS/Android AOT constraints.
/// </summary>
public sealed class ServiceRegistry
{
    private readonly Dictionary<Type, object> _services = new();
    private readonly Dictionary<Node, List<Type>> _nodeOwnership = new();

    /// <summary>
    /// Registers a service instance under typeof(T). Replaces any existing registration.
    /// </summary>
    public void Register<T>(T service) where T : class
    {
        if (service == null) throw new ArgumentNullException(nameof(service));
        Register(typeof(T), service);
    }

    /// <summary>
    /// Registers a service under an EXPLICIT type key (not necessarily its static
    /// type). Useful for derived-class registration from a base-class method, where
    /// <c>Register&lt;T&gt;(this)</c> would lock in <c>T</c> as the base type.
    /// </summary>
    public void Register(Type key, object service)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (service == null) throw new ArgumentNullException(nameof(service));
        if (_services.TryGetValue(key, out var existing) && !ReferenceEquals(existing, service))
        {
            GD.PrintErr($"[GameFramework] Service {key.Name} re-registered (previous instance replaced).");
        }
        _services[key] = service;

        // Track Node-based service ownership for cleanup.
        if (service is Node node)
        {
            if (!_nodeOwnership.TryGetValue(node, out var types))
            {
                types = new List<Type>();
                _nodeOwnership[node] = types;
            }
            if (!types.Contains(key)) types.Add(key);
        }
    }

    /// <summary>
    /// Returns the service registered as T, or null if not found.
    /// </summary>
    public T? TryGet<T>() where T : class
    {
        return _services.TryGetValue(typeof(T), out var s) ? (T)s : null;
    }

    /// <summary>
    /// Returns the service registered as T. Throws if not found.
    /// </summary>
    public T Resolve<T>() where T : class
    {
        return TryGet<T>() ?? throw new InvalidOperationException(
            $"[GameFramework] Service {typeof(T).Name} not registered. " +
            "Make sure it's added as an autoload or registered manually before this call.");
    }

    /// <summary>
    /// Removes a service by its registered type.
    /// </summary>
    public bool Unregister<T>() where T : class
    {
        return _services.Remove(typeof(T));
    }

    /// <summary>
    /// Internal: remove all services owned by a given Node. Called from GameService._ExitTree.
    /// </summary>
    internal void UnregisterAllFromNode(Node owner)
    {
        if (!_nodeOwnership.TryGetValue(owner, out var types)) return;
        foreach (var t in types)
        {
            if (_services.TryGetValue(t, out var s) && ReferenceEquals(s, owner))
                _services.Remove(t);
        }
        _nodeOwnership.Remove(owner);
    }

    /// <summary>Number of registered services (for diagnostics).</summary>
    public int Count => _services.Count;
}