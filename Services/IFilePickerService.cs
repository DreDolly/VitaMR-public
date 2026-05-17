namespace VitaMR.Services;

public interface IFilePickerService
{
    IReadOnlyList<string> PickFiles();
}
