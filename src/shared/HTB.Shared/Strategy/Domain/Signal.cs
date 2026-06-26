namespace HTB.Shared.Strategy.Domain;

/// <summary>
/// The intent a strategy emits on a closed decision bar. It is <em>advisory</em>: the risk layer
/// reconciles it against the actual position and may veto or resize (see the format doc §2.5).
/// Stored as a <see cref="short"/> code so the value is stable and independent of declaration order.
/// </summary>
public enum Signal : short
{
    /// <summary>Do nothing on this bar.</summary>
    Hold = 0,

    /// <summary>Ask to open (or add to) a long position.</summary>
    OpenLong = 1,

    /// <summary>Ask to close a long position.</summary>
    CloseLong = 2,

    /// <summary>Ask to open (or add to) a short position.</summary>
    OpenShort = 3,

    /// <summary>Ask to close a short position.</summary>
    CloseShort = 4,
}
