using System;
using System.Collections.Generic;
using UnityEngine;

public static class ServiceLocator
{
    private static readonly Dictionary<Type, object> _services = new();
    private static readonly Dictionary<Type, List<Delegate>> _waiters = new();

    public static void Register<T>(T service) where T : class
    {
        if(service == null)
        {
            Debug.LogError($"[ServiceLocator] Register<{typeof(T).Name}>: service is null");
            return;
        }

        var type = typeof(T);
        _services[type] = service;

        // доставл€ем всем ожидающим
        if(_waiters.TryGetValue(type, out var list) && list != null)
        {
            for(int i = 0; i < list.Count; i++)
            {
                if(list[i] is Action<T> cb)
                    cb(service);
            }
        }
    }

    public static void Unregister<T>(T service) where T : class
    {
        var type = typeof(T);
        if(_services.TryGetValue(type, out var existing) && ReferenceEquals(existing, service))
            _services.Remove(type);
    }

    public static bool TryGet<T>(out T service) where T : class
    {
        if(_services.TryGetValue(typeof(T), out var obj) && obj is T s)
        {
            service = s;
            return true;
        }

        service = null;
        return false;
    }

    /// <summary>
    ///  –»“»„Ќќ: если сервис уже есть Ч вызываем callback сразу.
    /// »наче подписываемс€.
    /// </summary>
    public static void WhenAvailable<T>(Action<T> onReady) where T : class
    {
        if(onReady == null)
            return;

        if(TryGet<T>(out var service))
        {
            onReady(service);
            return;
        }

        var type = typeof(T);
        if(!_waiters.TryGetValue(type, out var list) || list == null)
        {
            list = new List<Delegate>(4);
            _waiters[type] = list;
        }

        // избегаем дублей
        for(int i = 0; i < list.Count; i++)
        {
            if(Equals(list[i], onReady))
                return;
        }

        list.Add(onReady);
    }

    public static void Unsubscribe<T>(Action<T> onReady) where T : class
    {
        if(onReady == null)
            return;

        var type = typeof(T);
        if(!_waiters.TryGetValue(type, out var list) || list == null)
            return;

        for(int i = list.Count - 1; i >= 0; i--)
        {
            if(Equals(list[i], onReady))
                list.RemoveAt(i);
        }
    }
}
