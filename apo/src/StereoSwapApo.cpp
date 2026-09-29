#include "StereoSwapApo.h"

#include <algorithm>
#include <fstream>
#include <shlobj.h>

#pragma comment(lib, "ole32.lib")
#pragma comment(lib, "shell32.lib")

namespace
{
    constexpr wchar_t kConfigRelativePath[] = L"StereoSwap\\config.json";

    bool ConfigMentionsDevice(const std::wstring& json, const std::wstring& deviceId)
    {
        if (deviceId.empty() || json.empty())
            return false;
        return json.find(deviceId) != std::wstring::npos;
    }

    std::wstring ReadConfigUtf8AsWide()
    {
        wchar_t programData[MAX_PATH] = {};
        if (FAILED(SHGetFolderPathW(nullptr, CSIDL_COMMON_APPDATA, nullptr, SHGFP_TYPE_CURRENT, programData)))
            return {};

        std::wstring path = std::wstring(programData) + L"\\" + kConfigRelativePath;
        std::ifstream in(path, std::ios::binary);
        if (!in)
            return {};

        std::string utf8((std::istreambuf_iterator<char>(in)), std::istreambuf_iterator<char>());
        if (utf8.empty())
            return {};

        const int needed = MultiByteToWideChar(CP_UTF8, 0, utf8.data(), static_cast<int>(utf8.size()), nullptr, 0);
        if (needed <= 0)
            return {};

        std::wstring wide(static_cast<size_t>(needed), L'\0');
        MultiByteToWideChar(CP_UTF8, 0, utf8.data(), static_cast<int>(utf8.size()), wide.data(), needed);
        return wide;
    }

    bool ConfigHasAnyEnabledDevice(const std::wstring& json)
    {
        // Very small heuristic for MVP stub when endpoint ID is not yet plumbed.
        const auto listPos = json.find(L"\"enabledDeviceIds\"");
        if (listPos == std::wstring::npos)
            return false;
        const auto bracket = json.find(L'[', listPos);
        const auto close = json.find(L']', bracket);
        if (bracket == std::wstring::npos || close == std::wstring::npos || close <= bracket + 1)
            return false;
        const std::wstring inner = json.substr(bracket + 1, close - bracket - 1);
        return inner.find(L'{') != std::wstring::npos;
    }
}

StereoSwapApo::StereoSwapApo()
    : m_refCount(1)
    , m_swapEnabled(false)
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

    if (riid == IID_IUnknown ||
        riid == __uuidof(IAudioProcessingObject) ||
        riid == __uuidof(IAudioProcessingObjectRT) ||
        riid == __uuidof(IAudioProcessingObjectConfiguration))
    {
        *ppvObject = static_cast<IAudioProcessingObject*>(this);
        AddRef();
        return S_OK;
    }

    if (riid == __uuidof(IAudioSystemEffects))
    {
        *ppvObject = static_cast<IAudioSystemEffects*>(this);
        AddRef();
        return S_OK;
    }

    return E_NOINTERFACE;
}

STDMETHODIMP StereoSwapApo::GetLatency(HNSTIME* pTime)
{
    if (!pTime)
        return E_POINTER;
    *pTime = 0;
    return S_OK;
}

STDMETHODIMP StereoSwapApo::Initialize(UINT32 cbDataSize, BYTE* pbyData)
{
    UNREFERENCED_PARAMETER(cbDataSize);
    UNREFERENCED_PARAMETER(pbyData);

    // TODO(mvp-next): read endpoint ID from APOInitSystemEffects / APOInitSystemEffects2.
    m_deviceId.clear();
    RefreshSwapEnabled();
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
    RefreshSwapEnabled();
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

    if (!m_swapEnabled)
    {
        if (in != out)
            std::copy(in, in + static_cast<size_t>(frames) * ch, out);
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

void StereoSwapApo::RefreshSwapEnabled()
{
    if (!m_deviceId.empty())
    {
        m_swapEnabled = IsSwapEnabledForDevice(m_deviceId);
        return;
    }

    // Bound devices only load this APO; until endpoint ID is plumbed, any enabled id turns swap on.
    m_swapEnabled = ConfigHasAnyEnabledDevice(ReadConfigUtf8AsWide());
}

bool StereoSwapApo::IsSwapEnabledForDevice(const std::wstring& deviceId)
{
    return ConfigMentionsDevice(ReadConfigUtf8AsWide(), deviceId);
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
