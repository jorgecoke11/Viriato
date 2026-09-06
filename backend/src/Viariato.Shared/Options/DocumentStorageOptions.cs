namespace Viariato.Shared.Options;

public sealed class DocumentStorageOptions
{
    public const string SectionName = "DocumentStorage";

    /// <summary>Directory documents are written under when using the local-disk provider. Relative
    /// paths resolve against the API's working directory.</summary>
    public string LocalPath { get; set; } = "./data/documents";
}
