#include "StereoSwapApo.h"

#include <algorithm>
#include <new>

#pragma comment(lib, "ole32.lib")

namespace
{
    void WriteAliveMarkerOnce()
    {
        static LONG written = 0;
        if (InterlockedCompareExchange(&written, 1, 0) != 0)
            return;

        // No shell32 — keep APO deps minimal for audiodg.
        CreateDirectoryW(L"C:\\ProgramData\\StereoSwap", nullptr);
        HANDLE h = CreateFileW(L"C:\\ProgramData\\StereoSwap\\apo-alive.log",
            GENERIC_WRITE, FILE_SHARE_READ, nullptr, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
        if (h == INVALID_HANDLE_VALUE)
            return;

        const char msg[] = "StereoSwap APO Initialize OK\r\n";
        DWORD writtenBytes = 0;
        WriteFile(h, msg, sizeof(msg) - 1, &writtenBytes, nullptr);
        CloseHandle(h);
    }
}

StereoSwapApo::StereoSwapApo()
    : m_refCount(1)
{
}

StereoSwapApo::~StereoSwapApo() = default;

STDMETHODIMP_(ULONG) StereoSwapApo::AddRef()
{
    return InterlockedIncrement(&m_refCount);
}

STDMETHODIMP_(ULONG) StereoSwapApo::Release()
{
    const LONG ref = InterlockedDecrement(&m_refCount);
    if (ref == 0)
        delete this;
    return ref;
}

STDMETHODIMP StereoSwapApo::QueryInterface(REFIID riid, void** ppvObject)
{
    if (!ppvObject)
        return E_POINTER;

    *ppvObject = nullptr;

    if (riid == IID_IUnknown || riid == __uuidof(IAudioProcessingObject))
        *ppvObject = static_cast<IAudioProcessingObject*>(this);
    else if (riid == __uuidof(IAudioProcessingObjectRT))
        *ppvObject = static_cast<IAudioProcessingObjectRT*>(this);
    else if (riid == __uuidof(IAudioProcessingObjectConfiguration))
        *ppvObject = static_cast<IAudioProcessingObjectConfiguration*>(this);
    else if (riid == __uuidof(IAudioSystemEffects))
        *ppvObject = static_cast<IAudioSystemEffects*>(this);
    else
        return E_NOINTERFACE;

    AddRef();
    return S_OK;
}

STDMETHODIMP StereoSwapApo::GetLatency(HNSTIME* pTime)
{
    if (!pTime)
        return E_POINTER;
    *pTime = 0;
    return S_OK;
}

STDMETHODIMP StereoSwapApo::GetRegistrationProperties(APO_REG_PROPERTIES** ppRegProps)
{
    if (!ppRegProps)
        return E_POINTER;
    *ppRegProps = nullptr;

    // ANYSIZE_ARRAY already reserves one IID slot.
    auto* props = static_cast<APO_REG_PROPERTIES*>(CoTaskMemAlloc(sizeof(APO_REG_PROPERTIES)));
    if (!props)
        return E_OUTOFMEMORY;

    ZeroMemory(props, sizeof(APO_REG_PROPERTIES));
    props->clsid = CLSID_StereoSwapApo;
    // Match OEM LFX/GFX flags (INPLACE | FRAMESPERSECOND | BITSPERSAMPLE).
    props->Flags = static_cast<APO_FLAG>(0x0d);
    wcscpy_s(props->szFriendlyName, L"StereoSwap APO");
    wcscpy_s(props->szCopyrightInfo, L"Copyright StereoSwap");
    props->u32MajorVersion = 1;
    props->u32MinorVersion = 0;
    props->u32MinInputConnections = 1;
    props->u32MaxInputConnections = 1;
    props->u32MinOutputConnections = 1;
    props->u32MaxOutputConnections = 1;
    props->u32MaxInstances = UINT32_MAX;
    props->u32NumAPOInterfaces = 1;
    // Must match AudioProcessingObjects\APOInterface0 (FD7F2B29).
    // On current SDK that GUID is IAudioProcessingObject (historically IAudioSystemEffects).
    props->iidAPOInterfaceList[0] = __uuidof(IAudioProcessingObject);

    *ppRegProps = props;
    return S_OK;
}

STDMETHODIMP StereoSwapApo::Initialize(UINT32 cbDataSize, BYTE* pbyData)
{
    UNREFERENCED_PARAMETER(cbDataSize);
    UNREFERENCED_PARAMETER(pbyData);
    WriteAliveMarkerOnce();
    return S_OK;
}

STDMETHODIMP StereoSwapApo::LockForProcess(
    UINT32 u32NumInputConnections,
    APO_CONNECTION_DESCRIPTOR** ppInputConnections,
    UINT32 u32NumOutputConnections,
    APO_CONNECTION_DESCRIPTOR** ppOutputConnections)
{
    const HRESULT hr = MinimalApoBase::LockForProcess(
        u32NumInputConnections, ppInputConnections,
        u32NumOutputConnections, ppOutputConnections);
    if (FAILED(hr))
        return hr;

    m_channelCount = 2;
    if (ppInputConnections && ppInputConnections[0] && ppInputConnections[0]->pFormat)
    {
        const WAVEFORMATEX* wfx = ppInputConnections[0]->pFormat->GetAudioFormat();
        if (wfx && wfx->nChannels >= 2)
            m_channelCount = wfx->nChannels;
    }

    return S_OK;
}

STDMETHODIMP_(void) StereoSwapApo::APOProcess(
    UINT32 /*u32NumInputConnections*/,
    APO_CONNECTION_PROPERTY** ppInputConnections,
    UINT32 /*u32NumOutputConnections*/,
    APO_CONNECTION_PROPERTY** ppOutputConnections)
{
    if (!ppInputConnections || !ppOutputConnections)
        return;

    APO_CONNECTION_PROPERTY* inProp = ppInputConnections[0];
    APO_CONNECTION_PROPERTY* outProp = ppOutputConnections[0];
    if (!inProp || !outProp || inProp->pBuffer == 0 || outProp->pBuffer == 0)
        return;

    auto* in = reinterpret_cast<float*>(inProp->pBuffer);
    auto* out = reinterpret_cast<float*>(outProp->pBuffer);
    const UINT32 frames = inProp->u32ValidFrameCount;
    const UINT32 ch = m_channelCount >= 2 ? m_channelCount : 2;

    outProp->u32ValidFrameCount = frames;
    outProp->u32BufferFlags = inProp->u32BufferFlags;

    // Always L↔R while this APO is in the graph (bind = enable).
    if (in == out)
    {
        for (UINT32 i = 0; i < frames; ++i)
        {
            const UINT32 base = i * ch;
            const float tmp = out[base + 0];
            out[base + 0] = out[base + 1];
            out[base + 1] = tmp;
        }
        return;
    }

    for (UINT32 i = 0; i < frames; ++i)
    {
        const UINT32 base = i * ch;
        out[base + 0] = in[base + 1];
        out[base + 1] = in[base + 0];
        for (UINT32 c = 2; c < ch; ++c)
            out[base + c] = in[base + c];
    }
}

HRESULT StereoSwapApo::CreateInstance(IUnknown* pUnkOuter, REFIID riid, void** ppv)
{
    if (!ppv)
        return E_POINTER;
    *ppv = nullptr;

    if (pUnkOuter != nullptr)
        return CLASS_E_NOAGGREGATION;

    auto* apo = new (std::nothrow) StereoSwapApo();
    if (!apo)
        return E_OUTOFMEMORY;

    const HRESULT hr = apo->QueryInterface(riid, ppv);
    apo->Release();
    return hr;
}
