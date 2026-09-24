using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.System.UserProfile;

namespace Chronvyr.Helpers;

/// <summary>
/// 当前 Windows 用户资料（FR-1.2）。
/// 优先取微软账户的显示名与头像；非打包应用拿不到 <c>UserInformation.GetAccountPictureAsync</c>
/// （需要 package identity），因此头像改为直接读取系统缓存的账户图片文件，
/// 仍然取不到时由界面生成姓名首字母的圆形头像。
/// </summary>
public static class UserHelper
{
    private static string? _cachedName;
    private static ImageSource? _cachedPicture;
    private static bool _pictureResolved;

    /// <summary>获取用于问候语的用户名。优先微软账户显示名，失败回退本地账户名。</summary>
    public static async Task<string> GetDisplayNameAsync()
    {
        if (_cachedName is not null)
        {
            return _cachedName;
        }

        // 途径一：Windows.System.User 的 DisplayName 属性。
        // （UserInformation.GetDisplayNameAsync 在非打包应用里只会返回本地账户名。）
        try
        {
            var users = await Windows.System.User.FindAllAsync(
                Windows.System.UserType.LocalUser,
                Windows.System.UserAuthenticationStatus.LocallyAuthenticated);

            foreach (var user in users)
            {
                if (await user.GetPropertyAsync(Windows.System.KnownUserProperties.DisplayName) is string name
                    && !string.IsNullOrWhiteSpace(name))
                {
                    _cachedName = name;
                    Services.Log.Write($"已通过 Windows.System.User 读取账户显示名：{name}");
                    return _cachedName;
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Services.Log.Write($"Windows.System.User 取显示名失败：{ex.Message}");
        }

        // 途径二：UserInformation（打包应用里才有意义）。
        try
        {
            var name = await UserInformation.GetDisplayNameAsync();
            if (!string.IsNullOrWhiteSpace(name))
            {
                _cachedName = name;
                return _cachedName;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Services.Log.Write($"取账户显示名失败，回退本地账户名：{ex.Message}");
        }

        // 途径三：本地账户名。
        _cachedName = Environment.UserName;
        return _cachedName;
    }

    /// <summary>获取账户头像；不可用时返回 null（界面回退为姓名首字母）。</summary>
    public static async Task<ImageSource?> GetAccountPictureAsync()
    {
        if (_pictureResolved)
        {
            return _cachedPicture;
        }

        _pictureResolved = true;

        // 途径一：Windows.System.User（与受限的 UserInformation 不是同一套 API，
        // 在非打包应用里有机会可用）。
        var fromUserApi = await TryGetPictureFromUserApiAsync();
        if (fromUserApi is not null)
        {
            return fromUserApi;
        }

        // 途径二：系统缓存的账户图片文件。
        try
        {
            var path = FindAccountPicturePath();
            if (path is null)
            {
                Services.Log.Write("本机没有缓存的账户头像文件，使用默认用户图标");
                return null;
            }

            var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(path);
            using var stream = await file.OpenReadAsync();

            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(stream);
            _cachedPicture = bitmap;
            Services.Log.Write($"已从缓存文件加载账户头像：{path}");
            return _cachedPicture;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Runtime.InteropServices.COMException)
        {
            Services.Log.Write($"加载账户头像失败，改用默认用户图标：{ex.Message}");
            return null;
        }
    }

    /// <summary>尝试通过 Windows.System.User 取当前登录用户的头像。</summary>
    private static async Task<ImageSource?> TryGetPictureFromUserApiAsync()
    {
        try
        {
            var users = await Windows.System.User.FindAllAsync(
                Windows.System.UserType.LocalUser,
                Windows.System.UserAuthenticationStatus.LocallyAuthenticated);

            foreach (var user in users)
            {
                var reference = await user.GetPictureAsync(Windows.System.UserPictureSize.Size208x208);
                if (reference is null)
                {
                    continue;
                }

                using var stream = await reference.OpenReadAsync();
                var bitmap = new BitmapImage();
                await bitmap.SetSourceAsync(stream);

                _cachedPicture = bitmap;
                Services.Log.Write($"已通过 Windows.System.User 加载账户头像（{user.NonRoamableId}）");
                return _cachedPicture;
            }

            Services.Log.Write("Windows.System.User 未返回头像引用");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Services.Log.Write($"Windows.System.User 取头像失败：{ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// 在系统账户图片缓存目录里找分辨率最高的一张头像。
    /// 微软账户会把头像缓存到 %APPDATA%\Microsoft\Windows\AccountPictures 下。
    /// </summary>
    private static string? FindAccountPicturePath()
    {
        var roots = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "AccountPictures"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "AccountPictures"),
        };

        // 文件名形如 Image240.png / Image96.jpg，按尺寸从大到小取第一张。
        string[] preferredNames = ["Image240", "Image208", "Image192", "Image96", "Image64", "Image48", "Image32"];
        string[] extensions = [".png", ".jpg", ".jpeg", ".bmp"];

        foreach (var root in roots)
        {
            try
            {
                if (!Directory.Exists(root))
                {
                    continue;
                }

                foreach (var name in preferredNames)
                {
                    foreach (var ext in extensions)
                    {
                        var hit = Directory
                            .EnumerateFiles(root, name + ext, SearchOption.AllDirectories)
                            .FirstOrDefault();

                        if (hit is not null)
                        {
                            return hit;
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // 尝试下一个根目录。
            }
        }

        return null;
    }

    /// <summary>姓名首字母（用于回退头像）。</summary>
    public static string GetInitial(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "L";
        }

        var trimmed = name.Trim();
        return trimmed[..1].ToUpperInvariant();
    }
}
