﻿Public Enum PacketTypes
    None = 0 '0x00
    Oscilloscope = 83 '0x53 = S in ASCII
    FFT = 70 '0x46 = F in ASCII
End Enum

Public Enum GainTypes
    x1 = 1
    x2 = 2
    x4 = 4
    x8 = 8
    x16 = 16
End Enum

Public Enum CommandTypes
    ChangeName = 3          '0x03
    FFTData = 70            '0x46 
    OscilloscopeData = 83   '0x53
    Calibrate = 84          '0x54
    ChangeParameters = 85   '0x55
    ChangeFunction = 241    '0xF1
End Enum