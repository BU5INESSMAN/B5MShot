#define WIN32_LEAN_AND_MEAN
#define _WIN32_WINNT 0x0A00
#include <windows.h>
#include <shobjidl.h>
#include <shellapi.h>
#include <atomic>
#include <cwctype>
#include <new>
#include <string>

namespace
{
    // {B541C9AE-75E1-4A38-87AC-091A6CEB7A55}
    constexpr CLSID CommandClsid =
        {0xb541c9ae, 0x75e1, 0x4a38, {0x87, 0xac, 0x09, 0x1a, 0x6c, 0xeb, 0x7a, 0x55}};

    // {5D0B6C1E-386A-4A9D-B76D-A35043BF1C28}
    constexpr GUID CanonicalCommandGuid =
        {0x5d0b6c1e, 0x386a, 0x4a9d, {0xb7, 0x6d, 0xa3, 0x50, 0x43, 0xbf, 0x1c, 0x28}};

    HMODULE g_module = nullptr;
    std::atomic<long> g_objectCount{0};
    std::atomic<long> g_serverLocks{0};

    HRESULT DuplicateString(const std::wstring& value, PWSTR* output)
    {
        if (output == nullptr)
        {
            return E_POINTER;
        }

        *output = nullptr;
        const auto bytes = (value.size() + 1) * sizeof(wchar_t);
        auto* copy = static_cast<PWSTR>(CoTaskMemAlloc(bytes));
        if (copy == nullptr)
        {
            return E_OUTOFMEMORY;
        }

        CopyMemory(copy, value.c_str(), bytes);
        *output = copy;
        return S_OK;
    }

    bool HasSupportedExtension(const std::wstring& path)
    {
        const auto separator = path.find_last_of(L'.');
        if (separator == std::wstring::npos)
        {
            return false;
        }

        auto extension = path.substr(separator);
        for (auto& character : extension)
        {
            character = static_cast<wchar_t>(std::towlower(character));
        }

        return extension == L".png" || extension == L".jpg" || extension == L".jpeg" ||
               extension == L".bmp" || extension == L".gif" || extension == L".tif" ||
               extension == L".tiff";
    }

    HRESULT GetSelectedImagePath(IShellItemArray* items, std::wstring& path)
    {
        if (items == nullptr)
        {
            return E_INVALIDARG;
        }

        DWORD count = 0;
        if (FAILED(items->GetCount(&count)) || count != 1)
        {
            return E_INVALIDARG;
        }

        IShellItem* item = nullptr;
        auto result = items->GetItemAt(0, &item);
        if (FAILED(result))
        {
            return result;
        }

        PWSTR rawPath = nullptr;
        result = item->GetDisplayName(SIGDN_FILESYSPATH, &rawPath);
        item->Release();
        if (FAILED(result) || rawPath == nullptr)
        {
            CoTaskMemFree(rawPath);
            return FAILED(result) ? result : E_FAIL;
        }

        path.assign(rawPath);
        CoTaskMemFree(rawPath);
        return HasSupportedExtension(path) ? S_OK : E_INVALIDARG;
    }

    std::wstring GetModulePath()
    {
        std::wstring path(32768, L'\0');
        const auto length = GetModuleFileNameW(g_module, path.data(), static_cast<DWORD>(path.size()));
        if (length == 0 || length >= path.size())
        {
            return {};
        }

        path.resize(length);
        return path;
    }

    class ExplorerCommand final : public IExplorerCommand
    {
    public:
        ExplorerCommand() { ++g_objectCount; }
        ~ExplorerCommand() { --g_objectCount; }

        IFACEMETHODIMP QueryInterface(REFIID iid, void** object) override
        {
            if (object == nullptr)
            {
                return E_POINTER;
            }

            *object = nullptr;
            if (iid == IID_IUnknown || iid == __uuidof(IExplorerCommand))
            {
                *object = static_cast<IExplorerCommand*>(this);
                AddRef();
                return S_OK;
            }

            return E_NOINTERFACE;
        }

        IFACEMETHODIMP_(ULONG) AddRef() override { return ++_references; }

        IFACEMETHODIMP_(ULONG) Release() override
        {
            const auto remaining = --_references;
            if (remaining == 0)
            {
                delete this;
            }
            return remaining;
        }

        IFACEMETHODIMP GetTitle(IShellItemArray*, PWSTR* title) override
        {
            return DuplicateString(L"Редактировать в B5MShot", title);
        }

        IFACEMETHODIMP GetIcon(IShellItemArray*, PWSTR* icon) override
        {
            const auto modulePath = GetModulePath();
            return modulePath.empty() ? E_FAIL : DuplicateString(modulePath + L",-101", icon);
        }

        IFACEMETHODIMP GetToolTip(IShellItemArray*, PWSTR* tooltip) override
        {
            if (tooltip == nullptr)
            {
                return E_POINTER;
            }
            *tooltip = nullptr;
            return E_NOTIMPL;
        }

        IFACEMETHODIMP GetCanonicalName(GUID* canonicalName) override
        {
            if (canonicalName == nullptr)
            {
                return E_POINTER;
            }
            *canonicalName = CanonicalCommandGuid;
            return S_OK;
        }

        IFACEMETHODIMP GetState(IShellItemArray* items, BOOL, EXPCMDSTATE* state) override
        {
            if (state == nullptr)
            {
                return E_POINTER;
            }

            std::wstring path;
            *state = SUCCEEDED(GetSelectedImagePath(items, path)) ? ECS_ENABLED : ECS_HIDDEN;
            return S_OK;
        }

        IFACEMETHODIMP Invoke(IShellItemArray* items, IBindCtx*) override
        {
            std::wstring imagePath;
            auto result = GetSelectedImagePath(items, imagePath);
            if (FAILED(result))
            {
                return result;
            }

            auto modulePath = GetModulePath();
            const auto slash = modulePath.find_last_of(L"\\/");
            if (slash == std::wstring::npos)
            {
                return E_FAIL;
            }

            const auto directory = modulePath.substr(0, slash);
            const auto executable = directory + L"\\B5MShot.exe";
            const auto arguments = L"--upload \"" + imagePath + L"\"";
            const auto launchResult = reinterpret_cast<INT_PTR>(ShellExecuteW(
                nullptr,
                L"open",
                executable.c_str(),
                arguments.c_str(),
                directory.c_str(),
                SW_SHOWNORMAL));

            return launchResult > 32 ? S_OK : HRESULT_FROM_WIN32(static_cast<DWORD>(launchResult));
        }

        IFACEMETHODIMP GetFlags(EXPCMDFLAGS* flags) override
        {
            if (flags == nullptr)
            {
                return E_POINTER;
            }
            *flags = ECF_DEFAULT;
            return S_OK;
        }

        IFACEMETHODIMP EnumSubCommands(IEnumExplorerCommand** commands) override
        {
            if (commands == nullptr)
            {
                return E_POINTER;
            }
            *commands = nullptr;
            return E_NOTIMPL;
        }

    private:
        std::atomic<ULONG> _references{1};
    };

    class CommandClassFactory final : public IClassFactory
    {
    public:
        CommandClassFactory() { ++g_objectCount; }
        ~CommandClassFactory() { --g_objectCount; }

        IFACEMETHODIMP QueryInterface(REFIID iid, void** object) override
        {
            if (object == nullptr)
            {
                return E_POINTER;
            }

            *object = nullptr;
            if (iid == IID_IUnknown || iid == IID_IClassFactory)
            {
                *object = static_cast<IClassFactory*>(this);
                AddRef();
                return S_OK;
            }
            return E_NOINTERFACE;
        }

        IFACEMETHODIMP_(ULONG) AddRef() override { return ++_references; }

        IFACEMETHODIMP_(ULONG) Release() override
        {
            const auto remaining = --_references;
            if (remaining == 0)
            {
                delete this;
            }
            return remaining;
        }

        IFACEMETHODIMP CreateInstance(IUnknown* outer, REFIID iid, void** object) override
        {
            if (outer != nullptr)
            {
                return CLASS_E_NOAGGREGATION;
            }

            auto* command = new (std::nothrow) ExplorerCommand();
            if (command == nullptr)
            {
                return E_OUTOFMEMORY;
            }

            const auto result = command->QueryInterface(iid, object);
            command->Release();
            return result;
        }

        IFACEMETHODIMP LockServer(BOOL lock) override
        {
            lock ? ++g_serverLocks : --g_serverLocks;
            return S_OK;
        }

    private:
        std::atomic<ULONG> _references{1};
    };
}

extern "C" BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        g_module = instance;
        DisableThreadLibraryCalls(instance);
    }
    return TRUE;
}

extern "C" __declspec(dllexport) HRESULT WINAPI DllGetClassObject(REFCLSID clsid, REFIID iid, void** object)
{
    if (clsid != CommandClsid)
    {
        return CLASS_E_CLASSNOTAVAILABLE;
    }

    auto* factory = new (std::nothrow) CommandClassFactory();
    if (factory == nullptr)
    {
        return E_OUTOFMEMORY;
    }

    const auto result = factory->QueryInterface(iid, object);
    factory->Release();
    return result;
}

extern "C" __declspec(dllexport) HRESULT WINAPI DllCanUnloadNow()
{
    return g_objectCount == 0 && g_serverLocks == 0 ? S_OK : S_FALSE;
}
