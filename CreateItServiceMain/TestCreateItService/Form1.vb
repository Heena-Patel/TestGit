Imports CreateIt

Public Class Form1
    Private Sub btnStart_Click(sender As System.Object, e As System.EventArgs) Handles btnStart.Click
        Dim obj As New Main
        obj.StartThread()
    End Sub

    Private Sub btnStop_Click(sender As System.Object, e As System.EventArgs) Handles btnStop.Click
        Dim obj As New Main
        obj.StopThread()
    End Sub
End Class
