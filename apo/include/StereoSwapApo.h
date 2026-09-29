#pragma once

#include "MinimalApoBase.h"

// {B3E8C1A0-7D4F-4E2A-9C1B-5F6A8D0E2B11}
static const GUID CLSID_StereoSwapApo =
{ 0xb3e8c1a0, 0x7d4f, 0x4e2a, { 0x9c, 0x1b, 0x5f, 0x6a, 0x8d, 0x0e, 0x2b, 0x11 } };

/// <summary>
/// When this APO is loaded on an endpoint (FxProperties bind), it always swaps L↔R.
/// Enable/disable is done by binding/unbinding the APO, not by a runtime flag.
/// </summary>
class StereoSwapApo :
    public MinimalApoBase,
    public IAudioSystemEffects
{
public:
    StereoSwapApo();
    ~StereoSwapApo() override;

    STDMETHOD_(ULONG, AddRef)() override;
    STDMETHOD_(ULONG, Release)() override;
    STDMETHOD(QueryInterface)(REFIID riid, void** ppvObject) override;

    STDMETHOD(GetLatency)(HNSTIME* pTime) override;
    STDMETHOD(GetRegistrationProperties)(APO_REG_PROPERTIES** ppRegProps) override;
    STDMETHOD(Initialize)(UINT32 cbDataSize, BYTE* pbyData) override;

    STDMETHOD(LockForProcess)(UINT32 u32NumInputConnections, APO_CONNECTION_DESCRIPTOR** ppInputConnections,
                              UINT32 u32NumOutputConnections, APO_CONNECTION_DESCRIPTOR** ppOutputConnections) override;

    STDMETHOD_(void, APOProcess)(UINT32 u32NumInputConnections, APO_CONNECTION_PROPERTY** ppInputConnections,
                                 UINT32 u32NumOutputConnections, APO_CONNECTION_PROPERTY** ppOutputConnections) override;

    static HRESULT CreateInstance(IUnknown* pUnkOuter, REFIID riid, void** ppv);

private:
    LONG m_refCount;
};
