using Microsoft.Win32;

namespace VitaMR.Services;

public sealed class FilePickerService : IFilePickerService
{
    public IReadOnlyList<string> PickFiles()
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Title = "Attach files to Dolly"
        };

        return dialog.ShowDialog() == true
            ? dialog.FileNames
            : [];
    }
}
