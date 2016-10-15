Imports MySql.Data
Imports MySql.Data.MySqlClient

Public Class Database

#Region " Variables, Constants, Enumerations "

    Private Shared WithEvents conData As MySqlConnection

    'Private Const LOCAL_CONNECTION As String = "server=localhost;user id=root;password=root;persist security info=True;database=ptrealestate"
    Private Const REMOTE_CONNECTION As String = "server=localhost;user id=root;password=root;persist security info=True;database=ptrealestate"
    'Private Const REMOTE_CONNECTION As String = "server=localhost;user id=root;password=root;persist security info=True;database=videos"

#End Region

#Region " Database Connection "

    Public Shared ReadOnly Property Connection() As MySqlConnection
        Get
            Connection = conData
        End Get
    End Property

    Public Shared Function ConnectionString() As String
        Try
            ConnectionString = REMOTE_CONNECTION
        Catch ex As Exception
            ConnectionString = ""
        End Try
    End Function

    Public Shared Function OpenConnection() As Boolean
        Try
            conData = New MySqlConnection(ConnectionString)
            Do While True
                Try
                    Call conData.Open()
                    OpenConnection = True
                    Exit Do
                Catch ex As Exception
                    WriteToAppLog("OpenConnection - Connection not started ")
                    WriteToErrorLog("OpenConnection Procedure : " & ex.ToString)
                    'If MessageBox.Show("Unable to communicate with database, Would you like to Retry?" & vbCrLf & vbCrLf & ex.Message, "Property Tube", MessageBoxButtons.RetryCancel, MessageBoxIcon.Question) = DialogResult.Cancel Then Exit Do
                    Exit Do
                End Try
            Loop
        Catch ex As Exception
            OpenConnection = False
        End Try
    End Function

    Public Shared Function CloseConnection() As Boolean
        Try
            If conData IsNot Nothing Then
                If conData.State = ConnectionState.Open Then
                    conData.Close()
                End If
                conData = Nothing
            End If

            CloseConnection = True

        Catch ex As Exception
            WriteToErrorLog("CloseConnection Procedure : " & ex.ToString)
            CloseConnection = False
        End Try
    End Function

#End Region

#Region " Common Functions and Procedures "

    Public Shared Function ExecuteSQL(ByVal SQLStatement As String, Optional ByVal Parameters() As String = Nothing, Optional ByRef Connection As MySqlConnection = Nothing) As Object
        Try
            Dim comSQL As New MySqlCommand(SQLStatement)
            comSQL.CommandTimeout = 0
            If Connection Is Nothing Then
                comSQL.Connection = Database.Connection
            Else
                comSQL.Connection = Connection
            End If

            If Not Parameters Is Nothing Then
                For Each strPara As String In Parameters
                    Call comSQL.Parameters.AddWithValue("para" & comSQL.Parameters.Count, strPara)
                Next
            End If
            ExecuteSQL = comSQL.ExecuteScalar()
        Catch ex As Exception
            WriteToErrorLog("ExecuteSQL Procedure : " & ex.ToString)
            ExecuteSQL = False
        End Try
    End Function

    Public Shared Function FetchDataTable(ByVal SQLStatement As String, Optional ByVal Parameters() As String = Nothing, Optional ByRef Connection As MySqlConnection = Nothing) As DataTable
        Try
            Dim drData As MySqlDataReader
            Dim comSQL As New MySqlCommand(SQLStatement)
            comSQL.CommandTimeout = 0
            Dim tabData As New Data.DataTable()

            If Connection Is Nothing Then
                comSQL.Connection = Database.Connection
            Else
                comSQL.Connection = Connection
            End If

            If Not Parameters Is Nothing Then
                For Each strPara As String In Parameters
                    Call comSQL.Parameters.AddWithValue("para" & comSQL.Parameters.Count, strPara)
                Next
            End If

            drData = comSQL.ExecuteReader

            Call tabData.Load(drData)
            Call drData.Close()

            FetchDataTable = tabData

        Catch ex As Exception
            FetchDataTable = Nothing
            WriteToErrorLog("FetchDataTable Procedure : " & ex.ToString)
        End Try
    End Function

    Public Shared Function FetchDataReader(ByVal SQLStatement As String, Optional ByVal Parameters() As String = Nothing, Optional ByRef Connection As MySqlConnection = Nothing) As DataTableReader
        Try
            Dim drData As MySqlDataReader
            Dim comSQL As New MySqlCommand(SQLStatement)
            comSQL.CommandTimeout = 0
            Dim tabData As New Data.DataTable()

            If Connection Is Nothing Then
                comSQL.Connection = Database.Connection
            Else
                comSQL.Connection = Connection
            End If

            If Not Parameters Is Nothing Then
                For Each strPara As String In Parameters
                    Call comSQL.Parameters.AddWithValue("para" & comSQL.Parameters.Count, strPara)
                Next
            End If

            drData = comSQL.ExecuteReader

            Call tabData.Load(drData)
            Call drData.Close()

            FetchDataReader = New DataTableReader(tabData)

        Catch ex As Exception
            WriteToErrorLog("FetchDataReader Procedure : " & ex.ToString)
            FetchDataReader = Nothing
        End Try
    End Function

#End Region

End Class
