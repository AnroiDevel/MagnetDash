using System;
using System.Collections.Generic;

public static class ServiceLocator
{
    private static readonly Dictionary<Type, object> _map = new();
    private static readonly Dictionary<Type, List<Delegate>> _waiters = new();

    /// <summary>
    /// Вызывается при успешной регистрации сервиса указанного типа.
    /// </summary>
    public static event Action<Type, object> Registered;

    /// <summary>
    /// Вызывается при удалении сервиса указанного типа.
    /// </summary>
    public static event Action<Type> Unregistered;

    /// <summary>
    /// Регистрирует экземпляр сервиса типа T.
    /// </summary>
    public static void Register<T>(T instance) where T : class
    {
        if(instance == null)
            throw new ArgumentNullException(nameof(instance), $"ServiceLocator.Register<{typeof(T).Name}>: instance is null. Use Unregister(instance) для удаления.");

        var type = typeof(T);

        _map[type] = instance;

        Registered?.Invoke(type, instance);

        if(_waiters.TryGetValue(type, out var list))
        {
            foreach(var d in list)
                ((Action<T>)d)?.Invoke(instance);

            _waiters.Remove(type);
        }
    }

    /// <summary>
    /// Пытается получить зарегистрированный сервис типа T.
    /// </summary>
    public static bool TryGet<T>(out T instance) where T : class
    {
        if(_map.TryGetValue(typeof(T), out var obj) && obj is T t)
        {
            instance = t;
            return true;
        }

        instance = null;
        return false;
    }

    /// <summary>
    /// Если сервис уже есть — сразу вызывает onAvailable.
    /// Если нет — сохранит колбэк и вызовет его при первой регистрации сервиса.
    /// </summary>
    public static void WhenAvailable<T>(Action<T> onAvailable) where T : class
    {
        if(onAvailable == null)
            return;

        if(TryGet(out T inst))
        {
            onAvailable(inst);
            return;
        }

        var type = typeof(T);
        if(!_waiters.TryGetValue(type, out var list))
            _waiters[type] = list = new List<Delegate>();

        list.Add(onAvailable);
    }

    /// <summary>
    /// Убирает колбэк из очереди ожидания сервиса типа T.
    /// </summary>
    public static void Unsubscribe<T>(Action<T> onAvailable) where T : class
    {
        if(onAvailable == null)
            return;

        var type = typeof(T);
        if(_waiters.TryGetValue(type, out var list))
        {
            list.Remove(onAvailable);
            if(list.Count == 0)
                _waiters.Remove(type);
        }
    }

    /// <summary>
    /// Удаляет сервис типа T, только если в локаторе хранится именно этот экземпляр.
    /// </summary>
    public static void Unregister<T>(T instance) where T : class
    {
        if(instance == null)
            return;

        var type = typeof(T);

        if(_map.TryGetValue(type, out var current) && ReferenceEquals(current, instance))
        {
            _map.Remove(type);
            Unregistered?.Invoke(type);
        }
    }
}
