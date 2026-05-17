namespace VitaMR.ViewModels;

public sealed class AttachmentViewModel
{
    public AttachmentViewModel(string path)
    {
        Path = path;
        DisplayName = System.IO.Path.GetFileName(path);
        Extension = System.IO.Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
        Kind = string.IsNullOrWhiteSpace(Extension) ? "FILE" : Extension;
    }

    public string Path { get; }

    public string DisplayName { get; }

    public string Extension { get; }

    public string Kind { get; }
}
