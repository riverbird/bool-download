using System;
using System.IO;
using System.Threading.Tasks;
using Xunlei.XlDl;

namespace BoolDownload.Services;

public sealed class XunleiService : IDisposable
{
    public const string AppId = "eGwtNUkwbDEyMDg2AAAAAzYvAAA=";
    public const string ApiKey = "xl_72a211329954bc828e8608b8dd9476dafe68581d";

    private readonly XLDownloadAPI _api = new();
    private bool _initialized;
    private bool _loggedIn;

    public bool Initialize()
    {
        if (_initialized) return true;

        var configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BoolDownload",
            "config");
        Directory.CreateDirectory(configPath);

        var result = _api.Initialize(AppId, "1.0.0", configPath, saveTasks: false);
        _initialized = result == XLConstants.ErrorSuccess;
        return _initialized;
    }

    public async Task<bool> EnsureLoggedInAsync()
    {
        if (_loggedIn) return true;
        if (!Initialize()) return false;

        var token = await _api.GetLoginTokenAsync(ApiKey);
        if (token is null || token.Code != 0) return false;

        var (result, _) = _api.Login(token.Token);
        _loggedIn = result == XLConstants.ErrorSuccess;
        return _loggedIn;
    }

    public (int Result, ulong TaskId) CreateTask(string url, string savePath, string saveName)
    {
        if (!EnsureLoggedInAsync().GetAwaiter().GetResult()) return (XLConstants.TaskStatusFailed, 0);
        return _api.CreateP2spTask(url, savePath, saveName);
    }

    public int StartTask(ulong taskId) => _api.StartTask(taskId);

    public int StopTask(ulong taskId) => _api.StopTask(taskId);

    public int DeleteTask(ulong taskId, bool deleteFile) => _api.DeleteTask(taskId, deleteFile);

    public TaskState? GetTaskState(ulong taskId)
    {
        var (result, state) = _api.GetTaskState(taskId);
        return result == XLConstants.ErrorSuccess ? state : null;
    }

    public void Dispose() => _api.Dispose();
}
