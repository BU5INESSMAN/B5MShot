#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <wincrypt.h>
#include <shellapi.h>
#include <string>
#include <vector>

namespace
{
    constexpr int PackageResourceId = 201;
    constexpr int CertificateResourceId = 202;

    bool WriteEmbeddedResource(int resourceId, const wchar_t* resourceType, const std::wstring& destination)
    {
        const auto resource = FindResourceW(nullptr, MAKEINTRESOURCEW(resourceId), resourceType);
        if (resource == nullptr)
        {
            return false;
        }

        const auto loaded = LoadResource(nullptr, resource);
        const auto* data = LockResource(loaded);
        const auto size = SizeofResource(nullptr, resource);
        if (loaded == nullptr || data == nullptr || size == 0)
        {
            return false;
        }

        const auto file = CreateFileW(destination.c_str(), GENERIC_WRITE, 0, nullptr, CREATE_ALWAYS, FILE_ATTRIBUTE_TEMPORARY, nullptr);
        if (file == INVALID_HANDLE_VALUE)
        {
            return false;
        }

        DWORD written = 0;
        const auto succeeded = WriteFile(file, data, size, &written, nullptr) && written == size;
        CloseHandle(file);
        return succeeded;
    }

    bool ReadFileBytes(const std::wstring& path, std::vector<BYTE>& bytes)
    {
        const auto file = CreateFileW(path.c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
        if (file == INVALID_HANDLE_VALUE)
        {
            return false;
        }

        LARGE_INTEGER size{};
        if (!GetFileSizeEx(file, &size) || size.QuadPart <= 0 || size.QuadPart > 1024 * 1024)
        {
            CloseHandle(file);
            return false;
        }

        bytes.resize(static_cast<size_t>(size.QuadPart));
        DWORD read = 0;
        const auto succeeded = ReadFile(file, bytes.data(), static_cast<DWORD>(bytes.size()), &read, nullptr) && read == bytes.size();
        CloseHandle(file);
        return succeeded;
    }

    bool CertificateExists(HCERTSTORE store, PCCERT_CONTEXT certificate)
    {
        BYTE hash[64]{};
        DWORD hashSize = sizeof(hash);
        if (!CertGetCertificateContextProperty(certificate, CERT_HASH_PROP_ID, hash, &hashSize))
        {
            return false;
        }

        CRYPT_HASH_BLOB hashBlob{hashSize, hash};
        const auto existing = CertFindCertificateInStore(
            store,
            X509_ASN_ENCODING | PKCS_7_ASN_ENCODING,
            0,
            CERT_FIND_SHA1_HASH,
            &hashBlob,
            nullptr);

        if (existing != nullptr)
        {
            CertFreeCertificateContext(existing);
            return true;
        }
        return false;
    }

    bool RemoveCertificate(HCERTSTORE store, PCCERT_CONTEXT certificate)
    {
        BYTE hash[64]{};
        DWORD hashSize = sizeof(hash);
        if (!CertGetCertificateContextProperty(certificate, CERT_HASH_PROP_ID, hash, &hashSize))
        {
            return false;
        }

        CRYPT_HASH_BLOB hashBlob{hashSize, hash};
        const auto existing = CertFindCertificateInStore(
            store,
            X509_ASN_ENCODING | PKCS_7_ASN_ENCODING,
            0,
            CERT_FIND_SHA1_HASH,
            &hashBlob,
            nullptr);

        return existing != nullptr && CertDeleteCertificateFromStore(existing);
    }

    std::wstring EncodePowerShellCommand(const std::wstring& command)
    {
        DWORD outputLength = 0;
        const auto* bytes = reinterpret_cast<const BYTE*>(command.data());
        const auto byteLength = static_cast<DWORD>(command.size() * sizeof(wchar_t));
        CryptBinaryToStringW(bytes, byteLength, CRYPT_STRING_BASE64 | CRYPT_STRING_NOCRLF, nullptr, &outputLength);

        std::wstring encoded(outputLength, L'\0');
        if (!CryptBinaryToStringW(bytes, byteLength, CRYPT_STRING_BASE64 | CRYPT_STRING_NOCRLF, encoded.data(), &outputLength))
        {
            return {};
        }

        if (!encoded.empty() && encoded.back() == L'\0')
        {
            encoded.pop_back();
        }
        return encoded;
    }

    DWORD RunPowerShell(const std::wstring& script)
    {
        wchar_t systemDirectory[MAX_PATH]{};
        if (GetSystemDirectoryW(systemDirectory, MAX_PATH) == 0)
        {
            return ERROR_PATH_NOT_FOUND;
        }

        const auto powershell = std::wstring(systemDirectory) + L"\\WindowsPowerShell\\v1.0\\powershell.exe";
        const auto encoded = EncodePowerShellCommand(script);
        auto commandLine = L"\"" + powershell + L"\" -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + encoded;
        std::vector<wchar_t> mutableCommand(commandLine.begin(), commandLine.end());
        mutableCommand.push_back(L'\0');

        STARTUPINFOW startup{sizeof(startup)};
        PROCESS_INFORMATION process{};
        if (!CreateProcessW(
                powershell.c_str(),
                mutableCommand.data(),
                nullptr,
                nullptr,
                FALSE,
                CREATE_NO_WINDOW,
                nullptr,
                nullptr,
                &startup,
                &process))
        {
            return GetLastError();
        }

        WaitForSingleObject(process.hProcess, INFINITE);
        DWORD exitCode = ERROR_INSTALL_FAILURE;
        GetExitCodeProcess(process.hProcess, &exitCode);
        CloseHandle(process.hThread);
        CloseHandle(process.hProcess);
        return exitCode;
    }

    std::wstring QuotePowerShellLiteral(const std::wstring& value)
    {
        std::wstring escaped;
        escaped.reserve(value.size() + 8);
        for (const auto character : value)
        {
            escaped += character == L'\'' ? L"''" : std::wstring(1, character);
        }
        return L"'" + escaped + L"'";
    }
}

int WINAPI wWinMain(HINSTANCE, HINSTANCE, PWSTR, int)
{
    const auto confirmation = MessageBoxW(
        nullptr,
        L"B5MShot будет установлен для текущего компьютера.\n\n"
        L"Windows запросил права администратора, чтобы добавить тестовый сертификат B5MShot и современную команду Проводника.\n\n"
        L"Продолжить установку?",
        L"Установка B5MShot 0.6.0",
        MB_ICONINFORMATION | MB_OKCANCEL | MB_DEFBUTTON1);
    if (confirmation != IDOK)
    {
        return 0;
    }

    wchar_t temporaryRoot[MAX_PATH]{};
    if (GetTempPathW(MAX_PATH, temporaryRoot) == 0)
    {
        MessageBoxW(nullptr, L"Не удалось открыть временную папку Windows.", L"B5MShot", MB_ICONERROR);
        return 1;
    }

    const auto temporaryDirectory = std::wstring(temporaryRoot) + L"B5MShot-Setup-" + std::to_wstring(GetCurrentProcessId());
    if (!CreateDirectoryW(temporaryDirectory.c_str(), nullptr) && GetLastError() != ERROR_ALREADY_EXISTS)
    {
        MessageBoxW(nullptr, L"Не удалось подготовить файлы установки.", L"B5MShot", MB_ICONERROR);
        return 1;
    }

    const auto packagePath = temporaryDirectory + L"\\B5MShot.msix";
    const auto certificatePath = temporaryDirectory + L"\\B5MShot.cer";
    if (!WriteEmbeddedResource(PackageResourceId, RT_RCDATA, packagePath) ||
        !WriteEmbeddedResource(CertificateResourceId, RT_RCDATA, certificatePath))
    {
        DeleteFileW(packagePath.c_str());
        DeleteFileW(certificatePath.c_str());
        RemoveDirectoryW(temporaryDirectory.c_str());
        MessageBoxW(nullptr, L"Файлы установки повреждены. Скачайте установщик ещё раз.", L"B5MShot", MB_ICONERROR);
        return 1;
    }

    std::vector<BYTE> certificateBytes;
    auto* certificate = static_cast<PCCERT_CONTEXT>(nullptr);
    auto store = static_cast<HCERTSTORE>(nullptr);
    bool certificateAdded = false;

    if (ReadFileBytes(certificatePath, certificateBytes))
    {
        certificate = CertCreateCertificateContext(
            X509_ASN_ENCODING | PKCS_7_ASN_ENCODING,
            certificateBytes.data(),
            static_cast<DWORD>(certificateBytes.size()));
    }

    if (certificate != nullptr)
    {
        store = CertOpenStore(
            CERT_STORE_PROV_SYSTEM_W,
            0,
            0,
            CERT_SYSTEM_STORE_LOCAL_MACHINE,
            L"TrustedPeople");
    }

    if (store == nullptr || certificate == nullptr)
    {
        if (certificate != nullptr) CertFreeCertificateContext(certificate);
        if (store != nullptr) CertCloseStore(store, 0);
        DeleteFileW(packagePath.c_str());
        DeleteFileW(certificatePath.c_str());
        RemoveDirectoryW(temporaryDirectory.c_str());
        MessageBoxW(nullptr, L"Не удалось открыть хранилище сертификатов Windows.", L"B5MShot", MB_ICONERROR);
        return 1;
    }

    if (!CertificateExists(store, certificate))
    {
        if (!CertAddCertificateContextToStore(store, certificate, CERT_STORE_ADD_REPLACE_EXISTING, nullptr))
        {
            CertFreeCertificateContext(certificate);
            CertCloseStore(store, 0);
            DeleteFileW(packagePath.c_str());
            DeleteFileW(certificatePath.c_str());
            RemoveDirectoryW(temporaryDirectory.c_str());
            MessageBoxW(nullptr, L"Windows не разрешил добавить тестовый сертификат B5MShot.", L"B5MShot", MB_ICONERROR);
            return 1;
        }
        certificateAdded = true;
    }

    const auto quotedPackage = QuotePowerShellLiteral(packagePath);
    const auto script =
        L"$ErrorActionPreference='Stop'; "
        L"$previous=@(Get-Process -Name 'B5MShot' -ErrorAction SilentlyContinue | ForEach-Object {$_.Path} | Where-Object {$_}); "
        L"Get-Process -Name 'B5MShot' -ErrorAction SilentlyContinue | Stop-Process -Force; "
        L"try { Add-AppxPackage -Path " + quotedPackage + L" -ForceApplicationShutdown -ForceUpdateFromAnyVersion } "
        L"catch { $previous | ForEach-Object {if(Test-Path -LiteralPath $_){Start-Process -FilePath $_}}; throw }; "
        L"Remove-Item -LiteralPath 'Registry::HKEY_CURRENT_USER\\Software\\Classes\\SystemFileAssociations\\image\\shell\\B5MShot.Upload' -Recurse -Force -ErrorAction SilentlyContinue; "
        L"$package=Get-AppxPackage -Name 'BU5INESSMAN.B5MShot' | Sort-Object Version -Descending | Select-Object -First 1; "
        L"if($null -eq $package){throw 'Package was not registered'}; "
        L"Start-Process -FilePath (Join-Path $package.InstallLocation 'B5MShot.exe');";

    const auto installationResult = RunPowerShell(script);
    if (installationResult != 0 && certificateAdded)
    {
        RemoveCertificate(store, certificate);
    }

    CertFreeCertificateContext(certificate);
    CertCloseStore(store, 0);
    DeleteFileW(packagePath.c_str());
    DeleteFileW(certificatePath.c_str());
    RemoveDirectoryW(temporaryDirectory.c_str());

    if (installationResult != 0)
    {
        MessageBoxW(
            nullptr,
            (L"Установка не завершена. Код Windows: " + std::to_wstring(installationResult) +
             L".\n\nИзменения сертификата отменены.").c_str(),
            L"B5MShot",
            MB_ICONERROR);
        return static_cast<int>(installationResult);
    }

    MessageBoxW(
        nullptr,
        L"B5MShot 0.6.0 установлен.\n\nКоманда «Редактировать в B5MShot» появится в основном контекстном меню Windows 11. Если Проводник был открыт во время установки, обновите окно или откройте его заново.",
        L"B5MShot",
        MB_ICONINFORMATION);
    return 0;
}
