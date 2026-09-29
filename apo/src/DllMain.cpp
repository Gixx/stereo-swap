#include "StereoSwapApo.h"

#include <new>
#include <Shlwapi.h>

#pragma comment(lib, "Shlwapi.lib")

namespace
{
    HMODULE g_hModule = nullptr;
    LONG g_dllRef = 0;

    void WriteLoadMarker()
    {
        // Prefer ProgramData (readable by tray); fall back to Windows\Temp.
        const wchar_t* paths[] = {
            L"C:\\Users\\Public\\StereoSwap-apo-load.log",
            L"C:\\ProgramData\\StereoSwap\\apo-load.log",
            L"C:\\Windows\\Temp\\StereoSwap-apo-load.log"
        };
        char buf[128];
        const DWORD pid = GetCurrentProcessId();
        int n = wsprintfA(buf, "StereoSwapApo loaded pid=%u\r\n", pid);
        for (auto path : paths)
        {
            HANDLE h = CreateFileW(path, GENERIC_WRITE, FILE_SHARE_READ, nullptr,
                CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
            if (h == INVALID_HANDLE_VALUE)
                continue;
            DWORD written = 0;
            if (n > 0)
                WriteFile(h, buf, (DWORD)n, &written, nullptr);
            CloseHandle(h);
            break;
        }
    }

    class ClassFactory : public IClassFactory
    {
    public:
        ClassFactory() : m_ref(1) {}

        STDMETHODIMP QueryInterface(REFIID riid, void** ppv) override
        {
            if (!ppv) return E_POINTER;
            *ppv = nullptr;
            if (riid == IID_IUnknown || riid == IID_IClassFactory)
            {
                *ppv = static_cast<IClassFactory*>(this);
                AddRef();
                return S_OK;
            }
            return E_NOINTERFACE;
        }

        STDMETHODIMP_(ULONG) AddRef() override { return InterlockedIncrement(&m_ref); }
        STDMETHODIMP_(ULONG) Release() override
        {
            LONG r = InterlockedDecrement(&m_ref);
            if (r == 0) delete this;
            return r;
        }

        STDMETHODIMP CreateInstance(IUnknown* pUnkOuter, REFIID riid, void** ppv) override
        {
            return StereoSwapApo::CreateInstance(pUnkOuter, riid, ppv);
        }

        STDMETHODIMP LockServer(BOOL fLock) override
        {
            if (fLock) InterlockedIncrement(&g_dllRef);
            else InterlockedDecrement(&g_dllRef);
            return S_OK;
        }

    private:
        LONG m_ref;
    };
}

BOOL APIENTRY DllMain(HMODULE hModule, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        g_hModule = hModule;
        DisableThreadLibraryCalls(hModule);
        WriteLoadMarker();
    }
    return TRUE;
}

STDAPI DllCanUnloadNow()
{
    return g_dllRef == 0 ? S_OK : S_FALSE;
}

STDAPI DllGetClassObject(REFCLSID rclsid, REFIID riid, void** ppv)
{
    if (!ppv) return E_POINTER;
    *ppv = nullptr;

    if (rclsid != CLSID_StereoSwapApo)
        return CLASS_E_CLASSNOTAVAILABLE;

    ClassFactory* factory = new (std::nothrow) ClassFactory();
    if (!factory) return E_OUTOFMEMORY;

    HRESULT hr = factory->QueryInterface(riid, ppv);
    factory->Release();
    return hr;
}

STDAPI DllRegisterServer()
{
    return S_OK;
}

STDAPI DllUnregisterServer()
{
    return S_OK;
}
