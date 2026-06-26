using System;
using System.Collections;
using System.Collections.Generic;

namespace ActuarialTranslationEngine.Core.Models;

/// <summary>
/// A wrapper around a standard dictionary that returns 0m for any missing keys,
/// fulfilling the architectural contract that all dependencies are resolved to 0m implicitly.
/// </summary>
public class SafeDictionary : IDictionary<string, decimal>
{
    private readonly IDictionary<string, decimal> _inner;

    public SafeDictionary(IDictionary<string, decimal> inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public decimal this[string key]
    {
        get => _inner.TryGetValue(key, out var val) ? val : 0m;
        set => _inner[key] = value;
    }

    public ICollection<string> Keys => _inner.Keys;
    public ICollection<decimal> Values => _inner.Values;
    public int Count => _inner.Count;
    public bool IsReadOnly => _inner.IsReadOnly;

    public void Add(string key, decimal value) => _inner.Add(key, value);
    public void Add(KeyValuePair<string, decimal> item) => _inner.Add(item);
    public void Clear() => _inner.Clear();
    public bool Contains(KeyValuePair<string, decimal> item) => _inner.Contains(item);
    public bool ContainsKey(string key) => _inner.ContainsKey(key);
    public void CopyTo(KeyValuePair<string, decimal>[] array, int arrayIndex) => _inner.CopyTo(array, arrayIndex);
    public IEnumerator<KeyValuePair<string, decimal>> GetEnumerator() => _inner.GetEnumerator();
    public bool Remove(string key) => _inner.Remove(key);
    public bool Remove(KeyValuePair<string, decimal> item) => _inner.Remove(item);
    public bool TryGetValue(string key, out decimal value) => _inner.TryGetValue(key, out value);
    IEnumerator IEnumerable.GetEnumerator() => _inner.GetEnumerator();
}
