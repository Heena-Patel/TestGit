Imports CreateIt

Public Class CreateItService

    Protected Overrides Sub OnStart(ByVal args() As String)
        'Add code here to start your service. This method should set things
        'in motion so your service can do its work.
        Dim objMain As New Main
        objMain.StartThread()

    End Sub

    Protected Overrides Sub OnStop()
        ' Add code here to perform any tear-down necessary to stop your service.
        Dim objMain As New Main
        objMain.StopThread()

    End Sub

End Class
