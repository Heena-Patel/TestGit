Imports MySql.Data
Imports MySql.Data.MySqlClient

Public Class FTPSetting

    Private Shared strHost As String
    Private Shared strUsername As String
    Private Shared strPassword As String
    Private Shared strXMLFilePath As String
    Private Shared strFileUsername As String
    Private Shared strFilePassword As String
    Private Shared blnIsCredentials As Boolean
    Private Shared strUsername1 As String

    Public Shared Sub RefreshFTPSettings()
        Dim drData As DataTableReader = Database.FetchDataReader("select * from ftpsetting Where isactive=1")
        If drData.Read Then
            strHost = drData.Item("host")
            strUsername = drData.Item("username")
            strUsername1 = drData.Item("username1")
            strPassword = drData.Item("passcode")
            strXMLFilePath = drData.Item("xmlfilepath")
            blnIsCredentials = drData.Item("iscredentials")
            strFileUsername = drData.Item("fileusername")
            strFilePassword = drData.Item("filepassword")
        End If
        drData.Close()
    End Sub

    Public Shared ReadOnly Property Host() As String
        Get
            Host = strHost
        End Get
    End Property

    Public Shared ReadOnly Property Username() As String
        Get
            Username = strUsername
        End Get
    End Property

    Public Shared ReadOnly Property Username1() As String
        Get
            Username1 = strUsername1
        End Get
    End Property

    Public Shared ReadOnly Property Password() As String
        Get
            Password = strPassword
        End Get
    End Property

    Public Shared ReadOnly Property XMLFilePath() As String
        Get
            XMLFilePath = strXMLFilePath
        End Get
    End Property

    Public Shared ReadOnly Property IsCredentials() As Boolean
        Get
            IsCredentials = blnIsCredentials
        End Get
    End Property

    Public Shared ReadOnly Property FileUsername() As String
        Get
            FileUsername = strFileUsername
        End Get
    End Property

    Public Shared ReadOnly Property FilePassword() As String
        Get
            FilePassword = strFilePassword
        End Get
    End Property
End Class
