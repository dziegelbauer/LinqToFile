namespace LinqToFile;

/// <summary>Controls publication of a complete document to a file path.</summary>
public enum ExistingFileBehavior
{
    /// <summary>Reject an existing destination.</summary>
    Throw,
    /// <summary>Replace an existing destination after staged preparation.</summary>
    Overwrite
}
