Imports System.IO

Public Interface QueueItem
    Sub StartProcess()
    Event ProcessCompleted(ByVal item As QueueItem)
    Event ProcessNotCompleted(ByVal item As QueueItem, ByVal exception As Exception)

End Interface

Public Class Queue
    Inherits Hashtable

#Region " Variables, Constants, Enumerations "

    Private intTempThreads As Integer = 0
    'Used to Pause and Resume the Queue, see QueuePause and QueueResume procedures
    Private intMaxThreads As Integer = 1
    Private arrActiveKeys As New ArrayList
    Private blnIsFirstCall As Boolean = False
    Public Event OnQueueStart()
    Public Event OnQueuePause()
    Public Event OnQueueError(ByVal item As QueueItem, ByVal exception As Exception)
    Public Event OnQueueResume()
    Public Event OnQueueComplete()

#End Region

#Region " Constructors "

    Public Sub New()

    End Sub

    Public Sub New(ByVal MaxThreads As Integer)
        intMaxThreads = MaxThreads
    End Sub

#End Region

#Region " Properties "

    Public ReadOnly Property IsQueueActive() As Boolean
        Get
            IsQueueActive = (arrActiveKeys.Count > 0)
        End Get
    End Property

    Public Property MaxThreads() As Integer
        Get
            MaxThreads = intMaxThreads
        End Get
        Set(ByVal value As Integer)
            If intMaxThreads = -1 Then
                intTempThreads = value
            Else
                intMaxThreads = value
            End If
            Call NewProcess()
        End Set
    End Property

    Public Shadows ReadOnly Property Item(ByVal Index As Integer) As QueueItem
        Get
            Try
                Dim Pos As Integer = 0
                Dim LocatedItem As QueueItem = Nothing

                For Each qItem As QueueItem In Me.Values
                    If Pos = Index Then
                        LocatedItem = qItem
                        Exit For
                    End If
                    Pos += 1
                Next
                Item = LocatedItem

            Catch ex As Exception

                Item = Nothing

            End Try
        End Get
    End Property

    Public Shadows ReadOnly Property Item(ByVal Key As String) As QueueItem
        Get
            Try
                Item = MyBase.Item(Key)

            Catch ex As Exception
                Item = Nothing

            End Try
        End Get
    End Property

#End Region

#Region " Functions & Procedures "

    Public Shadows Sub Add(ByVal Item As QueueItem)
        If MyBase.ContainsValue(Item) Then Exit Sub

        AddHandler Item.ProcessCompleted, AddressOf OnComplete
        AddHandler Item.ProcessNotCompleted, AddressOf OnNotComplete

        Call MyBase.Add(GetUniqueKey, Item)

        Call NewProcess()

    End Sub

    Private Sub NewProcess()
        If intMaxThreads = -1 Then
            WriteToAppLog("QUEUE Class NewProcess Procedure : Max Thread " & intMaxThreads)
            RaiseEvent OnQueueComplete() : blnIsFirstCall = False
        End If
        If arrActiveKeys.Count >= intMaxThreads Then Exit Sub

        Dim arrKeys(Me.Keys.Count - 1) As String
        Call Me.Keys.CopyTo(arrKeys, 0) ' Purpose for doing this is to avoid exception if while looping through the Keys, one of the object gets deleted.

        For Each Key In arrKeys
            If Not arrActiveKeys.Contains(Key) Then
                Call arrActiveKeys.Add(Key)

                If Not blnIsFirstCall Then
                    RaiseEvent OnQueueStart() : blnIsFirstCall = True
                End If

                If Me.Item(Key) IsNot Nothing Then
                    Call Me.Item(Key).StartProcess()
                Else
                    WriteToAppLog("Queue Error")
                    If arrActiveKeys.Contains(Key) Then
                        WriteToAppLog("Removed Error Key : " & Key)
                        arrActiveKeys.Remove(Key)
                    End If
                End If

                Call NewProcess() : Exit For
            End If
        Next
        If arrActiveKeys.Count = 0 Then
            WriteToAppLog("QUEUE Class NewProcess Procedure : arrActiveKeys " & arrActiveKeys.Count)
            RaiseEvent OnQueueComplete() : blnIsFirstCall = False
        End If

    End Sub

    Private Sub OnComplete(ByVal item As QueueItem)
        Dim Key As String = GetItemKey(item)

        If Not Key = vbNullString Then
            Call arrActiveKeys.Remove(Key)
            Call Me.Remove(Key)
        End If

        Call NewProcess()

    End Sub

    Private Sub OnNotComplete(ByVal item As QueueItem, ByVal exception As Exception)
        Dim Key As String = GetItemKey(item)

        RaiseEvent OnQueueError(item, exception)

        If Not Key = vbNullString Then
            Call arrActiveKeys.Remove(Key)
            Call Me.Remove(Key)
        End If

        Call NewProcess()

    End Sub

    Public Sub RemoveAt(ByVal Index As Integer)
        Dim Pos As Integer = 0

        For Each Key As String In Me.Keys
            If Pos = Index Then
                Call Me.Remove(Key)
                Exit For
            End If
            Pos += 1
        Next

    End Sub

    Private Function GetUniqueKey() As String
        Dim strKey As String

        Do
            strKey = Path.GetRandomFileName
        Loop While Me.ContainsKey(strKey)

        GetUniqueKey = strKey

    End Function

    Private Function GetItemKey(ByVal Item As QueueItem) As String
        Dim arrKeys(Me.Keys.Count - 1) As String
        Call Me.Keys.CopyTo(arrKeys, 0) ' Purpose for doing this is to avoid exception if while looping through the Keys, one of the object gets deleted.

        For Each Key In arrKeys
            If Me.Item(Key) Is Item Then
                GetItemKey = Key
                Exit Function
            End If
        Next
        GetItemKey = vbNullString

    End Function

    Public Sub QueuePause()
        intTempThreads = intMaxThreads
        intMaxThreads = -1
        RaiseEvent OnQueuePause()

    End Sub

    Public Sub QueueResume()
        intMaxThreads = intTempThreads

        RaiseEvent OnQueueResume()

        Call NewProcess()

    End Sub

#End Region

End Class

