#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <wincrypt.h>
#include <shellapi.h>
#include <shlobj.h>
#include <string>
#include <vector>
#include "AutoStartMigration.h"
#include "InstallWorkflow.h"

namespace
{
    int SetupMessage(HWND owner, const wchar_t* message, const wchar_t* title, UINT flags)
    {
#ifdef B5MSHOT_CI_TEST
        OutputDebugStringW(message);
        return IDOK; // Only the separate, unpublished CI executable bypasses modal dialogs.
#else
        return MessageBoxW(owner, message, title, flags);
#endif
    }
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

    std::wstring ReadSummary(const std::wstring& path)
    {
        std::vector<BYTE> bytes;
        if (!ReadFileBytes(path, bytes) || bytes.size() < 2 || bytes.size() % 2 != 0) return {};
        const auto* text = reinterpret_cast<const wchar_t*>(bytes.data());
        const auto skip = text[0] == 0xFEFF ? 1 : 0;
        return std::wstring(text + skip, bytes.size() / sizeof(wchar_t) - skip);
    }

    DWORD RunPowerShell(const std::wstring& script, const std::wstring& logPath)
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

        SECURITY_ATTRIBUTES security{sizeof(security), nullptr, TRUE};
        const auto log = CreateFileW(logPath.c_str(), GENERIC_WRITE, FILE_SHARE_READ, &security, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
        if (log == INVALID_HANDLE_VALUE) return GetLastError();
        const auto input = CreateFileW(L"NUL", GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, &security, OPEN_EXISTING, 0, nullptr);
        if (input == INVALID_HANDLE_VALUE) { const auto error = GetLastError(); CloseHandle(log); return error; }

        STARTUPINFOW startup{sizeof(startup)};
        startup.dwFlags = STARTF_USESTDHANDLES;
        startup.hStdInput = input;
        startup.hStdOutput = startup.hStdError = log;
        PROCESS_INFORMATION process{};
        if (!CreateProcessW(
                powershell.c_str(),
                mutableCommand.data(),
                nullptr,
                nullptr,
                TRUE,
                CREATE_NO_WINDOW,
                nullptr,
                nullptr,
                &startup,
                &process))
        {
            const auto error = GetLastError();
            CloseHandle(input); CloseHandle(log);
            return error;
        }
        CloseHandle(input); CloseHandle(log);

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
    wchar_t temporaryRoot[MAX_PATH]{};
    if (GetTempPathW(MAX_PATH, temporaryRoot) == 0)
    {
        SetupMessage(nullptr, L"Не удалось открыть временную папку Windows.", L"B5MShot", MB_ICONERROR);
        return 1;
    }

    const auto temporaryDirectory = std::wstring(temporaryRoot) + L"B5MShot-Setup-" + std::to_wstring(GetCurrentProcessId()) + L"-" + std::to_wstring(GetTickCount64());
    if (!CreateDirectoryW(temporaryDirectory.c_str(), nullptr))
    {
        SetupMessage(nullptr, L"Не удалось подготовить файлы установки.", L"B5MShot", MB_ICONERROR);
        return 1;
    }

    const auto packagePath = temporaryDirectory + L"\\B5MShot.msix";
    const auto certificatePath = temporaryDirectory + L"\\B5MShot.cer";
    const auto handoffPath = temporaryDirectory + L"\\UpdateHandoff.cs";
    wchar_t appData[MAX_PATH]{};
    std::wstring logDirectory = temporaryRoot;
    if (SUCCEEDED(SHGetFolderPathW(nullptr, CSIDL_LOCAL_APPDATA | CSIDL_FLAG_CREATE, nullptr, 0, appData)))
    {
        const auto appDirectory = std::wstring(appData) + L"\\B5MShot";
        CreateDirectoryW(appDirectory.c_str(), nullptr);
        const auto logs = appDirectory + L"\\logs";
        if (CreateDirectoryW(logs.c_str(), nullptr) || GetLastError() == ERROR_ALREADY_EXISTS) logDirectory = logs + L"\\";
    }
    const auto logBase = logDirectory + L"setup-" + std::to_wstring(GetCurrentProcessId()) + L"-" + std::to_wstring(GetTickCount64());
    const auto logPath = logBase + L".log";
    const auto summaryPath = logBase + L".txt";
    const auto installedMarker = logBase + L".state";
    const auto attentionMarker = logBase + L".attention";
    if (!WriteEmbeddedResource(PackageResourceId, RT_RCDATA, packagePath) ||
        !WriteEmbeddedResource(CertificateResourceId, RT_RCDATA, certificatePath) ||
        !WriteEmbeddedResource(203, RT_RCDATA, handoffPath))
    {
        DeleteFileW(handoffPath.c_str());
        DeleteFileW(packagePath.c_str());
        DeleteFileW(certificatePath.c_str());
        RemoveDirectoryW(temporaryDirectory.c_str());
        SetupMessage(nullptr, L"Файлы установки повреждены. Скачайте установщик ещё раз.", L"B5MShot", MB_ICONERROR);
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
        DeleteFileW(handoffPath.c_str());
        if (certificate != nullptr) CertFreeCertificateContext(certificate);
        if (store != nullptr) CertCloseStore(store, 0);
        DeleteFileW(packagePath.c_str());
        DeleteFileW(certificatePath.c_str());
        RemoveDirectoryW(temporaryDirectory.c_str());
        SetupMessage(nullptr, L"Не удалось открыть хранилище сертификатов Windows.", L"B5MShot", MB_ICONERROR);
        return 1;
    }

    if (!CertificateExists(store, certificate))
    {
        if (!CertAddCertificateContextToStore(store, certificate, CERT_STORE_ADD_REPLACE_EXISTING, nullptr))
        {
            DeleteFileW(handoffPath.c_str());
            CertFreeCertificateContext(certificate);
            CertCloseStore(store, 0);
            DeleteFileW(packagePath.c_str());
            DeleteFileW(certificatePath.c_str());
            RemoveDirectoryW(temporaryDirectory.c_str());
            SetupMessage(nullptr, L"Windows не разрешил добавить тестовый сертификат B5MShot.", L"B5MShot", MB_ICONERROR);
            return 1;
        }
        certificateAdded = true;
    }

    const auto script =
        std::wstring(L"$ErrorActionPreference='Stop'; $ProgressPreference='SilentlyContinue'; ") +
        L"Add-Type -Path " + QuotePowerShellLiteral(handoffPath) + L"; " +
        InstallWorkflowScript +
        L"\n$migrate={param($package)\n" + AutoStartMigrationScript + L"\n};\n" +
        L"$result=Invoke-B5MShotInstall -PackagePath " + QuotePowerShellLiteral(packagePath) + L" -ExpectedVersion '0.8.7.0' -Migrate $migrate; " +
        L"[IO.File]::WriteAllText(" + QuotePowerShellLiteral(installedMarker) + L",$result.Installed.ToString(),[Text.Encoding]::Unicode); " +
        L"[IO.File]::WriteAllText(" + QuotePowerShellLiteral(attentionMarker) + L",([bool]$result.NeedsAttention).ToString(),[Text.Encoding]::Unicode); " +
        L"[IO.File]::WriteAllText(" + QuotePowerShellLiteral(summaryPath) + L",$result.Message,[Text.Encoding]::Unicode); " +
        L"exit $result.Code;";

    const auto installationResult = RunPowerShell(script, logPath);
    DeleteFileW(handoffPath.c_str());
    // Never remove trust after successful/deferred deployment, or when its state is unknown.
    if (installationResult != 0 && certificateAdded && ReadSummary(installedMarker) == L"False")
    {
        RemoveCertificate(store, certificate);
    }

    CertFreeCertificateContext(certificate);
    CertCloseStore(store, 0);
    DeleteFileW(packagePath.c_str());
    DeleteFileW(certificatePath.c_str());
    RemoveDirectoryW(temporaryDirectory.c_str());

    auto summary = ReadSummary(summaryPath);
    if (installationResult != 0 && installationResult != 3010)
    {
        SetupMessage(
            nullptr,
            ((summary.empty() ? L"Не удалось выполнить сценарий установки. Код: " + std::to_wstring(installationResult) : summary) +
             L"\n\nПодробный журнал:\n" + logPath).c_str(),
            L"B5MShot",
            MB_ICONERROR);
        return static_cast<int>(installationResult);
    }

    if (installationResult == 0 && ReadSummary(attentionMarker) != L"True") return 0;
    SetupMessage(
        nullptr,
        ((summary.empty() ? L"Установка завершена." : summary) + L"\n\nЖурнал:\n" + logPath).c_str(),
        L"B5MShot",
        MB_ICONINFORMATION);
    return static_cast<int>(installationResult);
}
