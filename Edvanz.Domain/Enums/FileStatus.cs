namespace Edvanz.Domain.Enums;

/// <summary>
/// Lifecycle state of a <see cref="Entities.FileObject"/>. Drives the generic garbage-collector:
/// <c>Pending</c> rows older than the grace window (abandoned uploads) and all <c>Detached</c> rows
/// (released by a replace/delete) are reclaimed. Numeric values are part of the persisted contract.
/// </summary>
public enum FileStatus : byte
{
    /// <summary>Uploaded but not yet referenced by any resource. GC-eligible after the grace window.</summary>
    Pending = 1,

    /// <summary>Referenced by a live resource. Never GC'd while in this state.</summary>
    Attached = 2,

    /// <summary>Released by an update or a resource delete. Immediately GC-eligible.</summary>
    Detached = 3,
}
