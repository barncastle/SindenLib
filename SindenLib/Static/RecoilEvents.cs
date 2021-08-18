using System;

namespace SindenLib.Static
{
    [Flags]
    public enum RecoilEventFlags : byte
    {
        TriggerRecoil = 1,
        TriggerOffscreenRecoil = 2,
        PumpActionRecoilOnEvent = 4,
        PumpActionRecoilOffEvent = 8
    }
}