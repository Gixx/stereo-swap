#pragma once

// Minimal stand-in for the Windows APO sample CBaseAudioProcessingObject.
// Full production builds can swap this for the official base from the WDK APO samples.

#include <audioclient.h>
#include <audioenginebaseapo.h>
#include <atomic>

class MinimalApoBase :
    public IAudioProcessingObject,
    public IAudioProcessingObjectRT,
    public IAudioProcessingObjectConfiguration
{
public:
    MinimalApoBase() : m_locked(false) {}
    virtual ~MinimalApoBase() = default;

    STDMETHOD(Reset)() override { return S_OK; }
    STDMETHOD(GetLatency)(HNSTIME* pTime) override
    {
        if (!pTime) return E_POINTER;
        *pTime = 0;
        return S_OK;
    }
    STDMETHOD(GetRegistrationProperties)(APO_REG_PROPERTIES** ppRegProps) override
    {
        if (!ppRegProps) return E_POINTER;
        *ppRegProps = nullptr;
        return E_NOTIMPL;
    }
    STDMETHOD(Initialize)(UINT32, BYTE*) override { return S_OK; }
    STDMETHOD(IsInputFormatSupported)(IAudioMediaType*, IAudioMediaType* pRequested, IAudioMediaType** ppSupported) override
    {
        if (!pRequested || !ppSupported) return E_POINTER;
        *ppSupported = pRequested;
        pRequested->AddRef();
        return S_OK;
    }
    STDMETHOD(IsOutputFormatSupported)(IAudioMediaType*, IAudioMediaType* pRequested, IAudioMediaType** ppSupported) override
    {
        if (!pRequested || !ppSupported) return E_POINTER;
        *ppSupported = pRequested;
        pRequested->AddRef();
        return S_OK;
    }
    STDMETHOD(GetInputChannelCount)(UINT32* pu32ChannelCount) override
    {
        if (!pu32ChannelCount) return E_POINTER;
        *pu32ChannelCount = m_channelCount;
        return S_OK;
    }

    STDMETHOD(LockForProcess)(
        UINT32 u32NumInputConnections, APO_CONNECTION_DESCRIPTOR** ppInputConnections,
        UINT32 u32NumOutputConnections, APO_CONNECTION_DESCRIPTOR** ppOutputConnections) override
    {
        UNREFERENCED_PARAMETER(ppInputConnections);
        UNREFERENCED_PARAMETER(ppOutputConnections);
        if (u32NumInputConnections != 1 || u32NumOutputConnections != 1)
            return E_INVALIDARG;
        m_locked = true;
        return S_OK;
    }

    STDMETHOD(UnlockForProcess)() override
    {
        m_locked = false;
        return S_OK;
    }

    STDMETHOD_(void, APOProcess)(
        UINT32, APO_CONNECTION_PROPERTY**,
        UINT32, APO_CONNECTION_PROPERTY**) override
    {
    }

    STDMETHOD_(UINT32, CalcInputFrames)(UINT32 u32OutputFrameCount) override { return u32OutputFrameCount; }
    STDMETHOD_(UINT32, CalcOutputFrames)(UINT32 u32InputFrameCount) override { return u32InputFrameCount; }

protected:
    bool m_locked;
    UINT32 m_channelCount = 2;
};
