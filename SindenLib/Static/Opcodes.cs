namespace SindenLib.Static
{
    public enum Opcodes : byte
    {
        // Cursor
        CursorOffset = 40,

        // Toggleable Settings
        EnableSleepMode = 50,
        DisableSleepMode = 51,
        EnableEdgeReload = 52,
        DisableEdgeReload = 53,
        EnableEdgeClickReload = 54,
        DisableEdgeClickReload = 55,

        // Buttons
        AssignButton = 60,

        // DeviceInfo
        RequestFirmware = 101,
        RequestCamera = 102,
        UpdateCamera = 103,
        RequestCalibrationX = 104,
        RequestCalibrationY = 105,
        UpdateCalibrationX = 106,
        UpdateCalibrationY = 107,

        // Authentication
        Handshake = 109,
        Connect = 110,
        RequestColour = 111,
        RequesColour = 113,
        RequestManufactureDate = 115,
        Authenticated = 121,

        // Recoil
        EnableRecoil = 161,
        RecoilPulseValues = 162,
        RecoilStyle = 163,
        RecoilEvents = 164,
        RecoilPositions = 165,
        RecoilStrength = 167,
        RecoilTest = 168,
        RecoilTestRepeatStart = 169,
        RecoilTestRepeatStop = 170,
        PulseStrength = 171,
        CustomPulseStrength = 172,

        EnableCalibration = 180,
    }
}