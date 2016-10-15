Imports System.Configuration
Imports System.IO

Module Common

    Public ConnectionString As String
    Public conData As MySql.Data.MySqlClient.MySqlConnection
    Public logApp As New Log()

    Public Const AWS_ACCESS_KEY As String = "AKIAIGGXOECNSQW4M5MQ"
    Public Const AWS_SECRET_KEY As String = "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"
    Public Const MIN_IMAGE_DOWNLOADED_PERCENTAGE As Integer = 80

    Public IMAGE_LOCAL_PATH As String = Application.StartupPath & "\Images\"
    Private VIDEO_LOCAL_PATH As String = Application.StartupPath & "\Videos\"
    Public Const FPS As Integer = 30
    Public Const QUALITY As Integer = 85
    Public NeoSpeechEmail As String = ""
    Public NeoSpeechAccountId As String = ""
    Public NeoSpeechLoginKey As String = ""
    Public NeoSpeechLoginPwd As String = ""

    Public Sub WriteToErrorLog(ByVal ConsoleLine As String)
        Try

            'logApp.Write(ConsoleLine)
            logApp.Write(ConsoleLine)

        Catch ex As Exception

            WriteToErrorLog("WriteToAppLog Procedures : " & ex.ToString)

        End Try
    End Sub

    Public Sub WriteToAppLog(ByVal ConsoleLine As String)
        Try

            'logApp.Write(ConsoleLine)
            logApp.WriteLogLine(ConsoleLine)

        Catch ex As Exception

            WriteToAppLog("WriteToAppLog Procedures : " & ex.ToString)

        End Try
    End Sub

    Public Sub WriteToXMLLog(ByVal ConsoleLine As String)
        Try

            'logApp.Write(ConsoleLine)
            logApp.Write(ConsoleLine)

        Catch ex As Exception

            WriteToAppLog("WriteToAppLog Procedures : " & ex.ToString)

        End Try
    End Sub

    Public Function ConvertNull(ByVal Value As Object, Optional ByVal NullText As String = "", Optional ByVal ValueFormat As String = vbNullString) As Object
        Try
            If IsDBNull(Value) Then
                ConvertNull = NullText
            Else
                If ValueFormat = vbNullString Then
                    ConvertNull = Value
                Else
                    ConvertNull = Format(Value, ValueFormat)
                End If
            End If
        Catch ex As Exception
            ConvertNull = Nothing
        End Try
    End Function

    Public Function IsNull(ByVal Value As Object) As Boolean
        Try
            If IsDBNull(Value) Then
                IsNull = True
            Else
                IsNull = False
            End If

        Catch ex As Exception
            IsNull = True
        End Try

    End Function

    Public Function GetFileSize(ByVal InputFilePath As String) As String
        Try
            If File.Exists(InputFilePath) Then
                Dim info As New FileInfo(InputFilePath)
                GetFileSize = info.Length.ToString
            Else
                WriteToAppLog("GetFileSize Function : Invalid Flie Path - " & InputFilePath.ToString)
                Throw New Exception("GetFileSize Function : Invalid File Path - " & InputFilePath.ToString)
            End If
        Catch ex As Exception
            GetFileSize = ""
            WriteToErrorLog("GetFileSize Function : " & ex.ToString)
        End Try
    End Function

    Public Function ReadSetting(ByVal Key As String) As String
        ReadSetting = Configurationmanager.AppSettings.Item(Key)
    End Function

End Module
