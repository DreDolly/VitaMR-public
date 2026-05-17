using VitaMR.Models;

namespace VitaMR.Services;

public interface ISettingsService
{
    AppSettings Load();

    void Save(AppSettings settings);
}
