#include "StereoSwapApo.h"

#include <new>
#include <Shlwapi.h>

#pragma comment(lib, "Shlwapi.lib")

namespace
{
    HMODULE g_hModule = nullptr;
    LONG g_dllRef = 0;

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

// COM registration is performed by install\RegisterApo.ps1 (regsvr32 optional path).
STDAPI DllRegisterServer()
{
    // Stub: full InprocServer32 registration done by InstallHelper for MVP clarity.
    return S_OK;
}

STDAPI DllUnregisterServer()
{
    return S_OK;
}
