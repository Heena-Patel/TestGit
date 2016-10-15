Imports System.IO
Imports System.Text

Public Class Log

#Region " error Log "
    Private stream As FileStream


    Public Sub OpenLog(ByVal FileName As String)
        Try


            If (Not Directory.Exists(AppDomain.CurrentDomain.BaseDirectory & "Logs")) Then

                Directory.CreateDirectory(AppDomain.CurrentDomain.BaseDirectory & "Logs")
            End If

            stream = New FileStream(AppDomain.CurrentDomain.BaseDirectory & "Logs\" & FileName, FileMode.Append, FileAccess.Write, FileShare.Read, 8, FileOptions.WriteThrough)


        Catch ex As Exception

        End Try
    End Sub

    Public Sub Write(ByVal ConsoleLine As String)
        Try
            Dim bytes() As Byte = Encoding.ASCII.GetBytes(Now.ToString & " - " & ConsoleLine & vbCrLf)

            Call stream.Write(bytes, 0, bytes.Length)

        Catch ex As Exception


        End Try
    End Sub

    Public Sub Close()
        Try
            If Not stream Is Nothing Then
                Call stream.Close()
            End If
            stream = Nothing

        Catch ex As Exception


        End Try
    End Sub

#End Region

#Region " Upload  Logs "

    'Public blnLog As Boolean
    Private txtLog As FileStream

    Private Function GetLogFileName() As String
        Try
            If (Not Directory.Exists(AppDomain.CurrentDomain.BaseDirectory & "Logs")) Then

                Directory.CreateDirectory(AppDomain.CurrentDomain.BaseDirectory & "Logs")
            End If
            GetLogFileName = AppDomain.CurrentDomain.BaseDirectory & "\Logs\PropertyTube_" & Format(Today, "MMddyy").ToString & ".log"

        Catch ex As Exception
            'File.WriteAllText("c:\Exception.txt", "In GetPOPLogFileName" & ex.ToString)
            WriteToErrorLog(ex.ToString)
            GetLogFileName = vbNullString

        End Try
    End Function

    Public Function OpenLogFile() As Boolean
        Try
            '  WriteToErrorLog("Log Open")
            Dim strLogFile As String

            Call CloseLogFile()

            strLogFile = GetLogFileName()

            txtLog = New FileStream(strLogFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, 8, FileOptions.WriteThrough)

            OpenLogFile = True

        Catch ex As Exception
            WriteToErrorLog(ex.ToString)
        End Try
    End Function

    Public Function WriteLogLine(ByVal LogLine As String) As Boolean
        Try
            Dim bytes() As Byte
            Dim strLogFile As String


            strLogFile = GetLogFileName()

            If Not File.Exists(strLogFile) Or txtLog Is Nothing Then
                Call OpenLogFile()
            End If

            bytes = Encoding.ASCII.GetBytes(Format(Now, "hh:mm:ss tt").ToString & vbTab & LogLine & vbCrLf)

            Call txtLog.Write(bytes, 0, bytes.Length)

            WriteLogLine = True

        Catch ex As Exception
            'File.WriteAllText("c:\Exception.txt", "In WriteLogLine" & ex.ToString)
            WriteToErrorLog(ex.ToString)
            WriteLogLine = False

        End Try
    End Function

    Public Function CloseLogFile() As Boolean
        Try
            If Not txtLog Is Nothing Then
                Call txtLog.Close()
            End If
            txtLog = Nothing

            CloseLogFile = True

        Catch ex As Exception

            CloseLogFile = False

        End Try
    End Function

#End Region

End Class
