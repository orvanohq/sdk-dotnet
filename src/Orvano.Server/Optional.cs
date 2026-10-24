namespace Orvano.Server;

/// <summary>
/// A field of a partial update (spec 0014 JSW-10): left unset (the default), it is not sent and the server keeps the
/// field as it is; set, it is sent. Set a nullable field to <c>null</c> to clear it. A value converts to it, so
/// <c>Nickname = "nick"</c> and <c>Nickname = null</c> both set the field.
/// </summary>
/// <typeparam name="T">The field's type.</typeparam>
#pragma warning disable CA1716 // JSW-10: spec 0014 names this type Optional; it is the C# name the SDK documents.
public readonly struct Optional<T> : IEquatable<Optional<T>>
#pragma warning restore CA1716
{
    /// <summary>A field set to <paramref name="value"/>.</summary>
    /// <param name="value">The value to send.</param>
    public Optional(T value)
    {
        Value = value;
        HasValue = true;
    }

    /// <summary>Whether the field is set, and so sent.</summary>
    public bool HasValue { get; }

    /// <summary>The value set, or <c>default</c> when <see cref="HasValue"/> is false.</summary>
    public T Value { get; }

    /// <summary>Sets the field to <paramref name="value"/>.</summary>
    /// <param name="value">The value to send.</param>
#pragma warning disable CA2225 // the Optional(T) constructor is the named alternative
    public static implicit operator Optional<T>(T value) => new(value);
#pragma warning restore CA2225

    /// <summary>Whether two fields are both unset, or set to equal values.</summary>
    /// <param name="left">One field.</param>
    /// <param name="right">The other field.</param>
    public static bool operator ==(Optional<T> left, Optional<T> right) => left.Equals(right);

    /// <summary>Whether two fields differ.</summary>
    /// <param name="left">One field.</param>
    /// <param name="right">The other field.</param>
    public static bool operator !=(Optional<T> left, Optional<T> right) => !left.Equals(right);

    /// <inheritdoc/>
    public bool Equals(Optional<T> other) =>
        HasValue == other.HasValue && (!HasValue || EqualityComparer<T>.Default.Equals(Value, other.Value));

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Optional<T> other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HasValue ? HashCode.Combine(true, Value) : 0;

    /// <inheritdoc/>
    public override string ToString() => HasValue ? $"{Value}" : "(unset)";
}
