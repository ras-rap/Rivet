using System;
using System.Collections.Generic;

namespace Rivet.Protocol;

public static class MessageRegistry
{
    private static readonly Dictionary<ushort, Func<DataObject>> _factories = new();
    private static readonly Dictionary<Type, ushort> _ids = new();
    private static readonly object _lock = new();

    public static void Register<T>(ushort id) where T : DataObject, new()
    {
        lock (_lock)
        {
            _ids[typeof(T)] = id;
            _factories[id] = () => new T();
        }
    }

    public static ushort GetMsgId(Type t)
    {
        lock (_lock)
        {
            if (_ids.TryGetValue(t, out var id))
                return id;
            throw new InvalidOperationException($"Message type {t.Name} not registered");
        }
    }

    public static DataObject Create(ushort id)
    {
        lock (_lock)
        {
            if (_factories.TryGetValue(id, out var factory))
                return factory();
            return null!;
        }
    }
}
