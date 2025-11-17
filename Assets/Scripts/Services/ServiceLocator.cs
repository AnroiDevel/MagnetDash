using System;
using System.Collections.Generic;

public static class ServiceLocator
{
    private static readonly Dictionary<Type, object> _map = new();
    private static readonly Dictionary<Type, List<Delegate>> _waiters = new();

    public static void Register<T>(T instance) where T : class
    {
        var type = typeof(T);
        if(instance == null)
        { _map.Remove(type); return; }
        _map[type] = instance;

        if(_waiters.TryGetValue(type, out var list))
        {
            foreach(var d in list)
                ((Action<T>)d)?.Invoke(instance);
            _waiters.Remove(type);
        }
    }

    public static bool TryGet<T>(out T instance) where T : class
    {
        if(_map.TryGetValue(typeof(T), out var obj) && obj is T t)
        { instance = t; return true; }
        instance = null;
        return false;
    }

    public static void WhenAvailable<T>(Action<T> onAvailable) where T : class
    {
        if(onAvailable == null)
            return;
        if(TryGet(out T inst))
        { onAvailable(inst); return; }
        var type = typeof(T);
        if(!_waiters.TryGetValue(type, out var list))
            _waiters[type] = list = new List<Delegate>();
        list.Add(onAvailable);
    }

    public static void Unsubscribe<T>(Action<T> onAvailable) where T : class
    {
        var type = typeof(T);
        if(_waiters.TryGetValue(type, out var list))
        {
            list.Remove(onAvailable);
            if(list.Count == 0)
                _waiters.Remove(type);
        }
    }

    public static void Unregister<T>(T instance) where T : class
    {
        var type = typeof(T);
        _map.Remove(type);
    }
}
