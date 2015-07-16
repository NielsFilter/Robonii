﻿Public Class CommandManager

    ''' <summary>
    ''' This Method translates our <see cref="OutgoingCommand"/> into a byte array.
    ''' </summary>
    ''' <param name="cmd">The <see cref="OutgoingCommand"/> instance we are converting.</param>
    ''' <returns>A byte array converted from the command sent.</returns>
    ''' <remarks>
    ''' The idea behind this method is to bring our object that we used and manipulated back into a byte array
    ''' which can now be used to send to the device. Working in CLR Types (objects), is much easier and so we
    ''' typically stay in the object world until the command needs to go out.
    ''' </remarks>
    Public Shared Function TranslateToBytes(cmd As BaseCommand) As Byte()
        If cmd Is Nothing Then
            Return Nothing
        End If

        Dim lstBytes As New List(Of Byte)

        '==============
        '*** Header ***
        '==============

        'Start Bytes
        '// The Start Bytes are constant and are set in the BaseCommand class.
        lstBytes.Add(BaseCommand.StartByte1)
        lstBytes.Add(BaseCommand.StartByte2)

        'Packet Type
        '// Out PacketTypes enum holds the possible packet types.
        '// Each enum value has an 'HexCode' Attribute (the little piece above it), which holds the integer hex value
        '// Here we get that Hex value from the enum value and convert it back to a byte.
        Dim packetType As Integer = HexCodeAttribute.GetHexCode(cmd.PacketType)
        lstBytes.Add(Utils.ToByte(packetType))

        'Name
        '// The validation to limit the name is in the BaseCommand class.
        '// Here we simply get the name and cast it from ASCII back to a byte array.
        lstBytes.AddRange(Utils.ASCIIToBytes(cmd.DeviceName))

        'Time Division Param
        lstBytes.Add(Utils.ToByte(cmd.TimeDivision))

        'VTrigger
        lstBytes.Add(Utils.ToByte(cmd.VTrigger))

        'Time Division Param
        lstBytes.Add(Utils.ToByte(cmd.TimeDivision))

        'Data Packet Number
        lstBytes.Add(Utils.ToByte(cmd.PacketNumber))

        'Data Packet Total
        lstBytes.Add(Utils.ToByte(cmd.TotalPackets))

        'Data Stream Length
        '// Take the DataStreamLength property and convert it to Byte array (We need an array as the 2 bytes are allocated for this number)
        '// Once we have the byte array, we call Array.Resize to make sure the Byte array is 2 bytes long. (If integer is small, the second byte will be empty).
        Dim dataStreamBytes As Byte() = Utils.ToBytes(cmd.DataStreamLength)
        Array.Resize(dataStreamBytes, BaseCommand.DATASTREAMBYTE_LENGTH)
        lstBytes.AddRange(dataStreamBytes)

        'Data Stream Position
        '// See explanation as 'Data Stream Length' above.
        Dim dataStreamPositionBytes As Byte() = Utils.ToBytes(cmd.DataStreamPosition)
        Array.Resize(dataStreamPositionBytes, BaseCommand.DATASTREAMBYTE_LENGTH)
        lstBytes.AddRange(dataStreamPositionBytes)

        'CRC Header
        '// Automatically calculated in the BaseCommand class.
        lstBytes.Add(cmd.CRCHeader)


        '============
        '*** Body ***
        '============

        'Data
        '// This is the actual Command Data.
        lstBytes.AddRange(cmd.Data)

        'CRC Data
        '// Automatically calculated in the BaseCommand class.
        lstBytes.Add(cmd.CRCData)

        Return lstBytes.ToArray()
    End Function

    Public Shared Function BuildCommand(bytes As Byte(), ByRef cmd As BaseCommand, ByRef packetNumber As Integer, ByRef startPosition As Integer, ByRef offset As Integer) As Boolean

        Dim isMorePacketsToFollow As Boolean = False
        Dim currentByte = bytes(startPosition)

        Try
            Select Case offset
                Case 0 'Start Byte 1
                    If currentByte <> BaseCommand.StartByte1 Then
                        Return False
                    End If

                    offset += 1

                Case 1 'Start Byte 2
                    If currentByte <> BaseCommand.StartByte2 Then
                        Return False
                    End If

                    offset += 1

                Case 2 'Packet Type
                    If currentByte = &H53 Then
                        cmd = New OscilloscopeCommand()
                    ElseIf currentByte = &H46 Then
                        cmd = New FFTCommand()
                    Else
                        Return False
                    End If

                    '// Since we only instantiate the command here, now we can do the checksum for the start bytes and packet type byte.
                    cmd.XorData = &H0
                    cmd.CalculateCRC({BaseCommand.StartByte1, BaseCommand.StartByte2, currentByte})
                    offset += 1

                Case 3 'Device Name
                    Dim diviceNameBytes = bytes.Skip(startPosition).Take(BaseCommand.DEVICENAME_LENGTH).ToArray()
                    cmd.DeviceName = Utils.ByteToASCII(diviceNameBytes)

                    cmd.CalculateCRC(diviceNameBytes)
                    startPosition += BaseCommand.DEVICENAME_LENGTH - 1
                    offset += BaseCommand.DEVICENAME_LENGTH

                Case 8 'Time Division Param
                    cmd.TimeDivision = currentByte

                    cmd.CalculateCRC(currentByte)
                    offset += 1

                Case 9 'V Trigger
                    cmd.VTrigger = currentByte

                    cmd.CalculateCRC(currentByte)
                    offset += 1

                Case 10 'Gain
                    Dim gain As Integer = currentByte
                    If Not [Enum].IsDefined(GetType(GainTypes), gain) Then
                        Return False
                    End If

                    cmd.Gain = gain
                    cmd.CalculateCRC(currentByte)
                    offset += 1

                Case 11 'Packet Number
                    cmd.PacketNumber = currentByte

                    If cmd.PacketNumber <> packetNumber Then
                        Return False '// We've received a different packet number than expected.
                    End If

                    cmd.CalculateCRC(currentByte)
                    offset += 1

                Case 12 'Total Packets
                    cmd.TotalPackets = currentByte

                    If cmd.PacketNumber < cmd.TotalPackets Then
                        isMorePacketsToFollow = True
                        packetNumber += 1 '// Next time we come past here, we expect the "next" packet.
                    ElseIf cmd.PacketNumber > cmd.TotalPackets Then
                        Return False '// Not possible. Data invalid, chuck it.
                    Else
                        isMorePacketsToFollow = False
                    End If

                    cmd.CalculateCRC(currentByte)
                    offset += 1

                Case 13 'Command
                    Dim commandValue As Integer = currentByte
                    If Not [Enum].IsDefined(GetType(CommandTypes), commandValue) Then
                        Return False
                    End If

                    cmd.Command = currentByte
                    cmd.CalculateCRC(currentByte)
                    offset += 1

                Case 14 'Data Stream Length
                    Dim nextByte = bytes.Skip(startPosition + BaseCommand.DATASTREAMBYTE_LENGTH - 1).First()
                    cmd.DataStreamLength = CType(currentByte, Short) << 8 Or CType(nextByte, Short)

                    cmd.CalculateCRC({currentByte, nextByte}) '// We pass the 2 Data Stream Length bytes as an array
                    offset += BaseCommand.DATASTREAMBYTE_LENGTH
                    startPosition += BaseCommand.DATASTREAMBYTE_LENGTH - 1

                Case 16 'Data Stream Position
                    Dim nextByte = bytes.Skip(startPosition + BaseCommand.DATASTREAMBYTE_LENGTH - 1).First()
                    cmd.DataStreamPosition = CType(currentByte, Short) << 8 Or CType(nextByte, Short)

                    cmd.CalculateCRC({currentByte, nextByte}) '// We pass the 2 Data Stream Position bytes as an array
                    startPosition += BaseCommand.DATASTREAMBYTE_LENGTH - 1
                    offset += BaseCommand.DATASTREAMBYTE_LENGTH

                Case 18 ' CRC Header
                    cmd.CRCHeader = currentByte
                    If cmd.CRCHeader <> cmd.XorData Then
                        'Invalid CRC Header, cannot trust data.
                        Return False
                    End If

                    cmd.CalculateCRC(currentByte)
                    offset += 1

                    '// Data and CRC header
                Case Else
                    Dim crcDataIndex = (19 + cmd.DataStreamLength)

                    If offset > 18 AndAlso offset < crcDataIndex Then
                        '// Data
                        If cmd.Data Is Nothing Then
                            cmd.Data = New List(Of Byte)()
                        End If

                        Dim bytesToTake = cmd.DataStreamLength - cmd.Data.Count

                        Dim dataByteArr = bytes.Skip(startPosition).Take(bytesToTake).ToArray()
                        For Each b In dataByteArr
                            cmd.Data.Add(b)

                            cmd.CalculateCRC(b)
                            offset += 1
                        Next

                        startPosition += dataByteArr.Length - 1

                    ElseIf offset = crcDataIndex Then
                        '// CRC Data
                        cmd.CRCData = currentByte
                        If cmd.CRCData <> cmd.XorData Then
                            'Invalid CRC Data, cannot trust data.
                            Return False
                        End If

                        '// Reset offset
                        offset = 0

                        If cmd.PacketNumber = cmd.TotalPackets Then
                            '// We're not expecting anymore packets. Signal that we're done.
                            cmd.DoneBuildingCommand()
                        End If

                    Else
                        Return False
                    End If
            End Select

            Return True
        Catch
            Return False
        End Try
    End Function

End Class
